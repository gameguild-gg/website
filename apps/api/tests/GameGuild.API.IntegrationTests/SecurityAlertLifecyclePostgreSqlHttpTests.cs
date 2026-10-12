using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Compliance.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

/// <summary>
///     HTTP lifecycle coverage for the security alert queue: detection (open) → acknowledgement →
///     resolution, including the audit event emitted on resolution and the authorization denials
///     (anonymous, non-administrator) and transition violations (double resolve) along the way.
/// </summary>
[Collection(ApiPostgreSqlCollection.Name)]
public sealed class SecurityAlertLifecyclePostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private const string Route = "/api/audit/security-events";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient Admin(Guid tenant, Guid user) =>
        fixture.CreateAuthenticatedClient(user, tenant, isSystemAdmin: true);

    private async Task<SecurityAlert> SeedAlertAsync(Guid tenantId, SecurityAlertStatus status = SecurityAlertStatus.Open)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var alert = SecurityAlert.Raise(
            tenantId,
            SecurityAlertRules.FailedAuthenticationBurst,
            SecurityEventKind.Authentication,
            AuditRiskLevel.High,
            "Failed authentication burst",
            "Integration-seeded alert for lifecycle coverage",
            AuditActionTypes.LoginFailed,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "192.0.2.1",
            SystemClock.UtcNow);
        if (status != SecurityAlertStatus.Open)
        {
            var actor = Guid.NewGuid();
            alert.Acknowledge(actor, "seeded acknowledgement", SystemClock.UtcNow);
            if (status == SecurityAlertStatus.Resolved)
            {
                alert.Resolve(actor, "seeded resolution", SystemClock.UtcNow);
            }
        }

        context.Set<SecurityAlert>().Add(alert);
        await context.SaveChangesAsync();
        return alert;
    }

    [Fact]
    public async Task Alert_lifecycle_flows_from_open_through_acknowledgement_to_resolution()
    {
        var tenant = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var alert = await SeedAlertAsync(tenant);
        using var client = Admin(tenant, admin);

        var acknowledged = await client.PostAsJsonAsync($"{Route}/alerts/{alert.Id}/acknowledge", new { Notes = "investigating" });
        Assert.True(acknowledged.StatusCode == HttpStatusCode.OK, await acknowledged.Content.ReadAsStringAsync());
        var acknowledgedBody = (await acknowledged.Content.ReadFromJsonAsync<SecurityAlertLifecycleResponse>(JsonOptions))!;
        Assert.Equal("Acknowledged", acknowledgedBody.Status);

        var resolved = await client.PostAsJsonAsync($"{Route}/alerts/{alert.Id}:resolve", new { Notes = "contained; credentials rotated" });
        Assert.True(resolved.StatusCode == HttpStatusCode.OK, await resolved.Content.ReadAsStringAsync());
        var resolvedBody = (await resolved.Content.ReadFromJsonAsync<SecurityAlertLifecycleResponse>(JsonOptions))!;
        Assert.Equal("Resolved", resolvedBody.Status);
        Assert.Equal(admin, resolvedBody.ResolvedByUserId);
        Assert.NotNull(resolvedBody.ResolvedAtUtc);
        Assert.Equal("contained; credentials rotated", resolvedBody.ResolutionNotes);

        // The resolved alert is queryable in the resolved slice of the queue.
        var queue = await client.GetFromJsonAsync<List<SecurityAlertLifecycleResponse>>($"{Route}/alerts?status=Resolved", JsonOptions);
        Assert.Contains(queue!, item => item.Id == alert.Id);

        // The resolution was audited.
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await context.Set<AuditLog>().AsNoTracking().AnyAsync(log =>
            log.ActionType == AuditActionTypes.SecurityAlertResolved
            && log.TenantId == tenant
            && log.ResourceId == alert.Id.ToString()
            && log.UserId == admin));
    }

    [Fact]
    public async Task Open_alerts_can_be_resolved_directly_without_acknowledgement()
    {
        var tenant = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var alert = await SeedAlertAsync(tenant);
        using var client = Admin(tenant, admin);

        var resolved = await client.PostAsJsonAsync($"{Route}/alerts/{alert.Id}:resolve", new { Notes = (string?)null });

        Assert.True(resolved.StatusCode == HttpStatusCode.OK, await resolved.Content.ReadAsStringAsync());
        var body = (await resolved.Content.ReadFromJsonAsync<SecurityAlertLifecycleResponse>(JsonOptions))!;
        Assert.Equal("Resolved", body.Status);
        Assert.Equal(admin, body.ResolvedByUserId);
    }

    [Fact]
    public async Task Resolving_an_already_resolved_alert_is_rejected_with_a_conflict()
    {
        var tenant = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var alert = await SeedAlertAsync(tenant, SecurityAlertStatus.Resolved);
        using var client = Admin(tenant, admin);

        var response = await client.PostAsJsonAsync($"{Route}/alerts/{alert.Id}:resolve", new { Notes = "double" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Acknowledging_a_resolved_alert_is_rejected_with_a_conflict()
    {
        var tenant = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var alert = await SeedAlertAsync(tenant, SecurityAlertStatus.Resolved);
        using var client = Admin(tenant, admin);

        var response = await client.PostAsJsonAsync($"{Route}/alerts/{alert.Id}/acknowledge", new { Notes = "late" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Alerts_of_other_tenants_are_not_visible_nor_resolvable()
    {
        var tenant = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var foreign = await SeedAlertAsync(Guid.NewGuid());
        using var client = Admin(tenant, admin);

        var response = await client.PostAsJsonAsync($"{Route}/alerts/{foreign.Id}:resolve", new { Notes = "cross-tenant" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Resolution_requires_authentication()
    {
        var tenant = Guid.NewGuid();
        var alert = await SeedAlertAsync(tenant);
        using var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync($"{Route}/alerts/{alert.Id}:resolve", new { Notes = "anonymous" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Resolution_requires_a_tenant_administrator()
    {
        var tenant = Guid.NewGuid();
        var alert = await SeedAlertAsync(tenant);
        using var client = fixture.CreateAuthenticatedClient(Guid.NewGuid(), tenant, isSystemAdmin: false);

        var response = await client.PostAsJsonAsync($"{Route}/alerts/{alert.Id}:resolve", new { Notes = "member" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_alerts_resolve_to_not_found()
    {
        var tenant = Guid.NewGuid();
        var admin = Guid.NewGuid();
        using var client = Admin(tenant, admin);

        var response = await client.PostAsJsonAsync($"{Route}/alerts/{Guid.NewGuid()}:resolve", new { Notes = "missing" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record SecurityAlertLifecycleResponse(
        Guid Id,
        string RuleId,
        string Status,
        int OccurrenceCount,
        Guid? AcknowledgedByUserId,
        Guid? ResolvedByUserId,
        DateTime? ResolvedAtUtc,
        string? ResolutionNotes);
}
