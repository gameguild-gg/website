using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

/// <summary>
///     Unit tests for the security log retention policy service: configuration with optimistic
/// revisions, enforcement with category overrides and legal holds, and execution history.
/// </summary>
public sealed class SecurityLogRetentionServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    private readonly DbContextOptions<ApplicationDbContext> _contextOptions;
    private readonly TestApplicationDbContext _context;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IActorContextAccessor> _actors = new();
    private readonly Mock<ISecurityEventLogger> _securityEvents = new();
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _admin = Guid.NewGuid();

    public SecurityLogRetentionServiceTests()
    {
        _contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"SecurityLogRetention_{Guid.NewGuid()}", new InMemoryDatabaseRoot())
            .Options;
        _context = new TestApplicationDbContext(_contextOptions);
        _serviceProvider = new ServiceCollection()
            .AddScoped<IApplicationDbContext>(_ => new TestApplicationDbContext(_contextOptions))
            .BuildServiceProvider();
        SetAdminActor();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _context.Dispose();
    }

    private void SetAdminActor(bool authenticated = true, bool tenant = true, string role = "TenantAdmin") =>
        _actors.SetupGet(accessor => accessor.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = _admin.ToString(),
            TenantId = tenant ? _tenant : null,
            IsAuthenticated = authenticated,
            Roles = new HashSet<string> { role },
            Permissions = new HashSet<string>()
        });

    private SecurityLogRetentionService CreateService() => new(
        _actors.Object,
        new SecurityLogRetentionRepository(new TestApplicationDbContext(_contextOptions)),
        _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
        _securityEvents.Object,
        new FrozenTimeProvider(Now),
        Options.Create(new SecurityEventPipelineOptions { RetentionBatchSize = 2 }),
        NullLogger<SecurityLogRetentionService>.Instance);

    private async Task<SecurityLogRetentionPolicy> SeedPolicyAsync(
        int retentionDays = 400,
        Dictionary<string, int>? overrides = null,
        DateTime? legalHoldUntilUtc = null)
    {
        var policy = SecurityLogRetentionPolicy.Create(
            _tenant, retentionDays,
            overrides is null ? null : System.Text.Json.JsonSerializer.Serialize(overrides),
            legalHoldUntilUtc, _admin, Now);
        _context.Set<SecurityLogRetentionPolicy>().Add(policy);
        await _context.SaveChangesAsync();
        return policy;
    }

    private AuditLog SeedAuditLog(string actionType, AuditCategory category, DateTime createdAtUtc, Guid? tenantId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ActionType = actionType,
            ResourceType = "User",
            TenantId = tenantId ?? _tenant,
            Success = true,
            Category = category,
            RiskLevel = AuditRiskLevel.Low,
            CreatedAt = createdAtUtc
        };

    [Fact]
    public async Task ConfigureAsync_CreatesFirstRevision_AndRecordsConfigurationSecurityEvent()
    {
        var service = CreateService();

        var response = await service.ConfigureAsync(new ConfigureSecurityLogRetentionRequest
        {
            ExpectedRevision = 0,
            RetentionDays = 365,
            CategoryOverrides = new Dictionary<string, int> { [nameof(AuditCategory.Authentication)] = 90 }
        }, CancellationToken.None);

        response.Revision.Should().Be(1);
        response.RetentionDays.Should().Be(365);
        response.CategoryOverrides.Should().ContainKey(nameof(AuditCategory.Authentication)).WhoseValue.Should().Be(90);
        response.LegalHoldActive.Should().BeFalse();
        _securityEvents.Verify(logger => logger.RecordAsync(
            It.Is<CreateAuditLogRequest>(request =>
                request.ActionType == AuditActionTypes.SystemConfigChanged
                && request.TenantId == _tenant
                && request.UserId == _admin),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfigureAsync_RejectsStaleRevisions()
    {
        await SeedPolicyAsync();
        var service = CreateService();

        var act = () => service.ConfigureAsync(new ConfigureSecurityLogRetentionRequest
        {
            ExpectedRevision = 0,
            RetentionDays = 200
        }, CancellationToken.None);

        (await act.Should().ThrowAsync<SecurityLogRetentionValidationException>())
            .Which.Errors.Should().ContainKey("ExpectedRevision");
    }

    [Fact]
    public async Task ConfigureAsync_RejectsOutOfRangeRetentionWindows()
    {
        var service = CreateService();

        var act = () => service.ConfigureAsync(new ConfigureSecurityLogRetentionRequest
        {
            RetentionDays = 10,
            CategoryOverrides = new Dictionary<string, int> { ["NotACategory"] = 50 }
        }, CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<SecurityLogRetentionValidationException>())
            .Which;
        exception.Errors.Should().ContainKey("RetentionDays");
        exception.Errors.Keys.Should().Contain(key => key.StartsWith("CategoryOverrides[", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EnforceForTenantAsync_DryRun_ReportsButKeepsRows()
    {
        await SeedPolicyAsync(retentionDays: 30);
        var expired = SeedAuditLog("Login", AuditCategory.Authentication, Now.AddDays(-40));
        var fresh = SeedAuditLog("Login", AuditCategory.Authentication, Now.AddDays(-10));
        _context.Set<AuditLog>().AddRange(expired, fresh);
        await _context.SaveChangesAsync();
        var service = CreateService();

        var execution = await service.EnforceForTenantAsync(_tenant, triggeredByUserId: _admin, dryRun: true, CancellationToken.None);

        execution.Should().NotBeNull();
        execution!.DryRun.Should().BeTrue();
        execution.EvaluatedCount.Should().Be(1);
        execution.DeletedCount.Should().Be(1);
        execution.LegalHoldActive.Should().BeFalse();
        execution.CutoffUtc.Should().Be(Now.AddDays(-30));
        (await _context.Set<AuditLog>().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task EnforceForTenantAsync_DeletesOnlyExpiredRowsForThatTenant()
    {
        await SeedPolicyAsync(retentionDays: 30);
        var expired = SeedAuditLog("Login", AuditCategory.Authentication, Now.AddDays(-31));
        var fresh = SeedAuditLog("Login", AuditCategory.Authentication, Now.AddDays(-29));
        var otherTenant = SeedAuditLog("Login", AuditCategory.Authentication, Now.AddDays(-60), tenantId: Guid.NewGuid());
        _context.Set<AuditLog>().AddRange(expired, fresh, otherTenant);
        await _context.SaveChangesAsync();
        var service = CreateService();

        var execution = await service.EnforceForTenantAsync(_tenant, triggeredByUserId: null, dryRun: false, CancellationToken.None);

        execution!.DeletedCount.Should().Be(1);
        execution.DryRun.Should().BeFalse();
        var remaining = await _context.Set<AuditLog>().IgnoreQueryFilters().ToListAsync();
        remaining.Select(log => log.Id).Should().BeEquivalentTo([fresh.Id, otherTenant.Id]);
        var history = await _context.Set<SecurityLogRetentionExecution>().AsNoTracking().SingleAsync();
        history.TenantId.Should().Be(_tenant);
        history.TriggeredByUserId.Should().BeNull();
    }

    [Fact]
    public async Task EnforceForTenantAsync_HonorsCategoryOverrides()
    {
        await SeedPolicyAsync(
            retentionDays: 400,
            overrides: new Dictionary<string, int> { [nameof(AuditCategory.Authentication)] = 30 });
        var oldAuth = SeedAuditLog("Login", AuditCategory.Authentication, Now.AddDays(-60));
        var oldGeneral = SeedAuditLog("DataTouched", AuditCategory.General, Now.AddDays(-60));
        _context.Set<AuditLog>().AddRange(oldAuth, oldGeneral);
        await _context.SaveChangesAsync();
        var service = CreateService();

        var execution = await service.EnforceForTenantAsync(_tenant, triggeredByUserId: null, dryRun: false, CancellationToken.None);

        execution!.EvaluatedCount.Should().Be(1);
        var remaining = await _context.Set<AuditLog>().ToListAsync();
        remaining.Select(log => log.Id).Should().BeEquivalentTo([oldGeneral.Id]);
    }

    [Fact]
    public async Task EnforceForTenantAsync_ActiveLegalHold_SuspendsEveryDeletion()
    {
        await SeedPolicyAsync(retentionDays: 30, legalHoldUntilUtc: Now.AddDays(5));
        var expired = SeedAuditLog("Login", AuditCategory.Authentication, Now.AddDays(-400));
        _context.Set<AuditLog>().Add(expired);
        await _context.SaveChangesAsync();
        var service = CreateService();

        var execution = await service.EnforceForTenantAsync(_tenant, triggeredByUserId: null, dryRun: false, CancellationToken.None);

        execution!.LegalHoldActive.Should().BeTrue();
        execution.EvaluatedCount.Should().Be(0);
        execution.DeletedCount.Should().Be(0);
        (await _context.Set<AuditLog>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task EnforceForTenantAsync_WithoutPolicy_ReturnsNull()
    {
        var service = CreateService();

        var execution = await service.EnforceForTenantAsync(_tenant, triggeredByUserId: null, dryRun: false, CancellationToken.None);

        execution.Should().BeNull();
    }

    [Fact]
    public async Task GetExecutionsAsync_RequiresTenantAdministrator()
    {
        var service = CreateService();
        SetAdminActor(authenticated: false);

        var act = () => service.GetExecutionsAsync(0, 10, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetExecutionsAsync_ReturnsHistoryNewestFirst()
    {
        await SeedPolicyAsync();
        var service = CreateService();
        await service.EnforceForTenantAsync(_tenant, _admin, dryRun: true, CancellationToken.None);
        await service.EnforceForTenantAsync(_tenant, null, dryRun: false, CancellationToken.None);

        var history = await service.GetExecutionsAsync(0, 10, CancellationToken.None);

        history.Should().HaveCount(2);
        history.Select(execution => execution.TriggeredByUserId)
            .Should().BeEquivalentTo(new Guid?[] { null, _admin });
    }

    private sealed class FrozenTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
