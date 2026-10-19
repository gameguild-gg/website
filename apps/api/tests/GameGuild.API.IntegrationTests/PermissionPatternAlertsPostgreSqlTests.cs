using System.Net;
using System.Net.Http.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class PermissionPatternAlertsPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    private SecurityEventLogger CreateLogger(IServiceProvider services, SecurityEventPipelineOptions options) =>
        new(fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<IHttpContextAccessor>(), new SecurityEventFileSpool(options),
            new SecurityAlertRuleEvaluator(Options.Create(options), NullLogger<SecurityAlertRuleEvaluator>.Instance),
            Options.Create(options), services.GetRequiredService<IAuditService>(),
            services.GetRequiredService<IPiiRedactionService>(), NullLogger<SecurityEventLogger>.Instance);

    private static CreateAuditLogRequest Mutation(Guid tenant, Guid actor) => new()
    {
        ActionType = AuditActionTypes.PermissionChanged,
        Category = AuditCategory.Permission,
        ResourceType = "Permission",
        TenantId = tenant,
        UserId = actor,
        Metadata = new { ChangeCount = 1 },
        Success = true
    };

    [Fact]
    public async Task Concurrent_permission_events_do_not_miss_the_burst_threshold()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var logger = CreateLogger(scope.ServiceProvider, new SecurityEventPipelineOptions
        {
            PermissionChangeAlertThreshold = 3,
            BulkPermissionChangeAlertThreshold = 100,
            DatabaseWriteAttempts = 1,
            SpoolingEnabled = false
        });
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 3).Select(async _ =>
        {
            await start.Task;
            return await logger.RecordAsync(Mutation(tenant, actor));
        }).ToArray();
        start.SetResult();
        var results = await Task.WhenAll(tasks);
        Assert.All(results, result => Assert.Equal(SecurityEventCaptureOutcome.PersistedToDatabase, result.Outcome));

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(3, await context.Set<AuditLog>().CountAsync(log => log.TenantId == tenant && log.UserId == actor));
        var alert = await context.Set<SecurityAlert>().AsNoTracking().SingleAsync(value =>
            value.TenantId == tenant && value.SubjectUserId == actor && value.RuleId == SecurityAlertRules.PermissionChangeBurst);
        Assert.Equal(1, alert.OccurrenceCount);
        Assert.Contains("3 successful permission mutation events", alert.Description);
    }

    [Fact]
    public async Task Committed_bulk_permission_changes_raise_an_alert_visible_only_in_their_tenant()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(25, scope.ServiceProvider.GetRequiredService<IOptions<SecurityEventPipelineOptions>>().Value.BulkPermissionChangeAlertThreshold);
        var tenant = new Tenant
        {
            Name = "Permission alert integration",
            Slug = $"permission-alert-{Guid.NewGuid():N}",
            AdminEmail = "admin@example.test"
        };
        var users = Enumerable.Range(0, 25).Select(_ => User.Create($"permission-alert-{Guid.NewGuid():N}@example.test",  $"Permission alert {Guid.NewGuid():N}")).ToArray();
        context.Add(tenant);
        context.AddRange(users);
        await context.SaveChangesAsync();
        var actor = users[0].Id;
        foreach (var user in users)
        {
            context.Add(new TenantPermission
            {
                UserId = user.Id,
                TenantId = tenant.Id,
                GrantedBy = actor,
                Permissions = ["testing:permission-alert"]
            });
        }
        await context.SaveChangesAsync();

        var alert = await context.Set<SecurityAlert>().AsNoTracking().SingleAsync(value =>
            value.TenantId == tenant.Id && value.SubjectUserId == actor && value.RuleId == SecurityAlertRules.BulkPermissionChange);
        Assert.Contains("25 permission records", alert.Description);
        var source = await context.Set<AuditLog>().AsNoTracking().SingleAsync(log => log.Id == alert.SourceAuditLogId);
        Assert.Equal(AuditActionTypes.PermissionGranted, source.ActionType);
        Assert.Equal(actor, source.UserId);
        using var metadata = System.Text.Json.JsonDocument.Parse(source.Metadata!);
        Assert.Equal(25, metadata.RootElement.GetProperty("ChangeCount").GetInt32());
        Assert.Equal(25, metadata.RootElement.GetProperty("Changes").GetArrayLength());

        using var ownClient = fixture.CreateAuthenticatedClient(actor, tenant.Id, isSystemAdmin: true);
        var response = await ownClient.GetAsync("/api/audit/security-events/alerts?status=Open");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ownQueue = await response.Content.ReadFromJsonAsync<List<AlertRow>>();
        Assert.NotNull(ownQueue);
        Assert.Contains(ownQueue, value => value.Id == alert.Id);
        using var otherClient = fixture.CreateAuthenticatedClient(actor, Guid.NewGuid(), isSystemAdmin: true);
        var otherQueue = await otherClient.GetFromJsonAsync<List<AlertRow>>("/api/audit/security-events/alerts?status=Open");
        Assert.NotNull(otherQueue);
        Assert.DoesNotContain(otherQueue, value => value.Id == alert.Id);
    }

    [Fact]
    public async Task Rolling_back_required_capture_removes_both_permission_event_and_alert()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = CreateLogger(scope.ServiceProvider, new SecurityEventPipelineOptions { BulkPermissionChangeAlertThreshold = 2 });
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync();
            var request = Mutation(tenant, actor);
            request.Metadata = new { ChangeCount = 2 };
            var result = await logger.RecordInCommandAsync(context, request, CancellationToken.None);
            Assert.Equal(SecurityEventCaptureOutcome.PersistedToDatabase, result.Outcome);
            Assert.True(await context.Set<SecurityAlert>().AnyAsync(value => value.TenantId == tenant));
            await transaction.RollbackAsync();
        });
        context.ChangeTracker.Clear();
        Assert.False(await context.Set<AuditLog>().AnyAsync(value => value.TenantId == tenant));
        Assert.False(await context.Set<SecurityAlert>().AnyAsync(value => value.TenantId == tenant));
    }

    private sealed record AlertRow(Guid Id, string RuleId);
}
