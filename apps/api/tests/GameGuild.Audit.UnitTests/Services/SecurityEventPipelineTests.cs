using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

/// <summary>
///     Unit tests for the durable security event pipeline: taxonomy-driven persistence,
/// spool fallback on database outage, replay of spooled events, and alert rules.
/// </summary>
public sealed class SecurityEventPipelineTests : IDisposable
{
    private readonly DbContextOptions<ApplicationDbContext> _contextOptions;
    private readonly TestApplicationDbContext _assertContext;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IAuditService> _fallbackAudit = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly string _spoolDirectory =
        Path.Combine(Path.GetTempPath(), "gg-security-event-pipeline-tests", Guid.NewGuid().ToString("N"));

    public SecurityEventPipelineTests()
    {
        _contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"SecurityEventPipeline_{Guid.NewGuid()}", new InMemoryDatabaseRoot())
            .Options;
        _assertContext = new TestApplicationDbContext(_contextOptions);
        _serviceProvider = new ServiceCollection()
            .AddScoped<IApplicationDbContext>(_ => new TestApplicationDbContext(_contextOptions))
            .BuildServiceProvider();
        _httpContextAccessor.SetupGet(accessor => accessor.HttpContext).Returns((HttpContext?)null);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _assertContext.Dispose();
        if (Directory.Exists(_spoolDirectory))
        {
            Directory.Delete(_spoolDirectory, recursive: true);
        }
    }

    private SecurityEventLogger CreateLogger(
        SecurityEventPipelineOptions? options = null,
        ISecurityAlertRuleEvaluator? alertEvaluator = null,
        ISecurityEventSpool? spool = null)
    {
        options ??= new SecurityEventPipelineOptions
        {
            SpoolDirectoryPath = _spoolDirectory,
            DatabaseWriteAttempts = 2,
            RetryDelayMilliseconds = 0,
        };
        return new SecurityEventLogger(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _httpContextAccessor.Object,
            spool ?? new SecurityEventFileSpool(options),
            alertEvaluator ?? new SecurityAlertRuleEvaluator(Options.Create(options), NullLogger<SecurityAlertRuleEvaluator>.Instance),
            Options.Create(options),
            _fallbackAudit.Object,
            NullLogger<SecurityEventLogger>.Instance);
    }

    private static CreateAuditLogRequest SecurityRequest(
        string actionType = AuditActionTypes.LoginFailed,
        AuditCategory category = AuditCategory.Authentication,
        bool success = false,
        AuditRiskLevel riskLevel = AuditRiskLevel.Low,
        Guid? tenantId = null,
        Guid? userId = null,
        string? ipAddress = null) =>
        new()
        {
            ActionType = actionType,
            ResourceType = "User",
            ResourceId = userId?.ToString(),
            UserId = userId,
            TenantId = tenantId,
            IpAddress = ipAddress,
            Success = success,
            RiskLevel = riskLevel,
            Category = category,
            Description = $"{actionType} occurred."
        };

    [Fact]
    public async Task RecordAsync_SecurityEvent_PersistsWithClassifiedSeverity()
    {
        var logger = CreateLogger();
        var tenantId = Guid.NewGuid();

        var result = await logger.RecordAsync(SecurityRequest(tenantId: tenantId), CancellationToken.None);

        result.Outcome.Should().Be(SecurityEventCaptureOutcome.PersistedToDatabase);
        result.Classification.Kind.Should().Be(SecurityEventKind.Authentication);
        var row = await _assertContext.Set<AuditLog>().AsNoTracking().SingleAsync();
        row.Id.Should().Be(result.EventId);
        row.ActionType.Should().Be(AuditActionTypes.LoginFailed);
        row.TenantId.Should().Be(tenantId);
        row.RiskLevel.Should().Be(AuditRiskLevel.High);
    }

    [Fact]
    public async Task RecordAsync_NonSecurityEvent_DelegatesToStandardAuditPath()
    {
        var logger = CreateLogger();
        var request = SecurityRequest("LowValueAction", AuditCategory.General, success: true);

        var result = await logger.RecordAsync(request, CancellationToken.None);

        result.Outcome.Should().Be(SecurityEventCaptureOutcome.NotSecurityRelevant);
        _fallbackAudit.Verify(service => service.LogAsync(request), Times.Once);
        (await _assertContext.Set<AuditLog>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RecordAsync_TenantIsolationBreach_RaisesCriticalAlerts()
    {
        var options = new SecurityEventPipelineOptions { SpoolDirectoryPath = _spoolDirectory };
        var logger = CreateLogger(options);

        await logger.RecordAsync(
            SecurityRequest(AuditActionTypes.TenantIsolationBypassed, AuditCategory.Security, success: true),
            CancellationToken.None);

        var alerts = await _assertContext.Set<SecurityAlert>().AsNoTracking().ToListAsync();
        alerts.Select(alert => alert.RuleId).Should().BeEquivalentTo(
        [
            SecurityAlertRules.TenantIsolationBreach,
            SecurityAlertRules.CriticalEvent
        ]);
        alerts.Should().OnlyContain(alert => alert.Severity == AuditRiskLevel.Critical && alert.Status == SecurityAlertStatus.Open);
    }

    [Fact]
    public async Task RecordAsync_FailedAuthenticationBurst_RaisesBurstAlertOnlyAtThreshold()
    {
        var options = new SecurityEventPipelineOptions
        {
            SpoolDirectoryPath = _spoolDirectory,
            FailedAuthenticationAlertThreshold = 3,
            FailedAuthenticationWindowMinutes = 15,
        };
        var userId = Guid.NewGuid();
        var logger = CreateLogger(options);

        await logger.RecordAsync(SecurityRequest(userId: userId), CancellationToken.None);
        await logger.RecordAsync(SecurityAction(userId), CancellationToken.None);
        (await _assertContext.Set<SecurityAlert>().CountAsync()).Should().Be(0);

        await logger.RecordAsync(SecurityAction(userId), CancellationToken.None);

        var alert = await _assertContext.Set<SecurityAlert>().AsNoTracking().SingleAsync();
        alert.RuleId.Should().Be(SecurityAlertRules.FailedAuthenticationBurst);
        alert.SubjectUserId.Should().Be(userId);
        alert.OccurrenceCount.Should().Be(1);
        alert.Description.Should().Contain("3 failed authentication attempts");
        return;

        CreateAuditLogRequest SecurityAction(Guid user) => SecurityRequest(userId: user);
    }

    [Fact]
    public async Task RecordAsync_RepeatedRuleHit_MergesIntoOpenAlert()
    {
        var options = new SecurityEventPipelineOptions { SpoolDirectoryPath = _spoolDirectory };
        var logger = CreateLogger(options);
        var tenant = Guid.NewGuid();

        await logger.RecordAsync(
            SecurityRequest(AuditActionTypes.TenantIsolationBypassed, AuditCategory.Security, success: true, tenantId: tenant),
            CancellationToken.None);
        await logger.RecordAsync(
            SecurityRequest(AuditActionTypes.TenantIsolationBypassed, AuditCategory.Security, success: true, tenantId: tenant),
            CancellationToken.None);

        var alerts = await _assertContext.Set<SecurityAlert>().AsNoTracking().ToListAsync();
        alerts.Where(alert => alert.RuleId == SecurityAlertRules.TenantIsolationBreach).Should().ContainSingle();
        var merged = alerts.Single(alert => alert.RuleId == SecurityAlertRules.TenantIsolationBreach);
        merged.OccurrenceCount.Should().Be(2);
        merged.LastSeenAtUtc.Should().BeOnOrAfter(merged.FirstSeenAtUtc);
    }

    [Fact]
    public async Task RecordAsync_DatabaseOutage_SpoolsEventForReplay()
    {
        var options = new SecurityEventPipelineOptions
        {
            SpoolDirectoryPath = _spoolDirectory,
            DatabaseWriteAttempts = 2,
            RetryDelayMilliseconds = 0,
            SpoolingEnabled = true,
        };
        using var failingProvider = new ServiceCollection()
            .AddScoped<IApplicationDbContext>(_ => new FailingDbContext(_contextOptions))
            .BuildServiceProvider();
        var spool = new SecurityEventFileSpool(options);
        var failingLogger = new SecurityEventLogger(
            failingProvider.GetRequiredService<IServiceScopeFactory>(),
            _httpContextAccessor.Object,
            spool,
            new SecurityAlertRuleEvaluator(Options.Create(options), NullLogger<SecurityAlertRuleEvaluator>.Instance),
            Options.Create(options),
            _fallbackAudit.Object,
            NullLogger<SecurityEventLogger>.Instance);

        var result = await failingLogger.RecordAsync(SecurityRequest(), CancellationToken.None);

        result.Outcome.Should().Be(SecurityEventCaptureOutcome.SpooledLocally);
        result.CaptureError.Should().NotBeNullOrWhiteSpace();
        spool.GetStats().PendingCount.Should().Be(1);
        (await _assertContext.Set<AuditLog>().CountAsync()).Should().Be(0);

        var healthyLogger = CreateLogger(options, spool: spool);
        var delivered = await healthyLogger.ReplaySpooledEventsAsync(CancellationToken.None);

        delivered.Should().Be(1);
        spool.GetStats().PendingCount.Should().Be(0);
        var replayed = await _assertContext.Set<AuditLog>().AsNoTracking().SingleAsync();
        replayed.Id.Should().Be(result.EventId);
        replayed.ActionType.Should().Be(AuditActionTypes.LoginFailed);
    }

    [Fact]
    public async Task RecordAsync_DatabaseOutageWithSpoolingDisabled_ReportsCaptureFailure()
    {
        var options = new SecurityEventPipelineOptions
        {
            SpoolDirectoryPath = _spoolDirectory,
            DatabaseWriteAttempts = 1,
            RetryDelayMilliseconds = 0,
            SpoolingEnabled = false,
        };
        using var failingProvider = new ServiceCollection()
            .AddScoped<IApplicationDbContext>(_ => new FailingDbContext(_contextOptions))
            .BuildServiceProvider();
        var logger = new SecurityEventLogger(
            failingProvider.GetRequiredService<IServiceScopeFactory>(),
            _httpContextAccessor.Object,
            new SecurityEventFileSpool(options),
            new SecurityAlertRuleEvaluator(Options.Create(options), NullLogger<SecurityAlertRuleEvaluator>.Instance),
            Options.Create(options),
            _fallbackAudit.Object,
            NullLogger<SecurityEventLogger>.Instance);

        var result = await logger.RecordAsync(SecurityRequest(), CancellationToken.None);

        result.Outcome.Should().Be(SecurityEventCaptureOutcome.SpoolingDisabled);
        result.CaptureError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ReplaySpooledEvents_KeepsAlreadyPersistedEventsIdempotent()
    {
        var options = new SecurityEventPipelineOptions { SpoolDirectoryPath = _spoolDirectory };
        var spool = new SecurityEventFileSpool(options);
        var eventId = Guid.NewGuid();
        var payload = System.Text.Json.JsonSerializer.Serialize(
            SecurityRequest(), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        spool.Append(new SecurityEventSpoolRecord(eventId, SystemClock.UtcNow, payload));

        _assertContext.Set<AuditLog>().Add(AuditLogEntryFactoryForTests(eventId));
        await _assertContext.SaveChangesAsync();

        var logger = CreateLogger(options, spool: spool);
        var delivered = await logger.ReplaySpooledEventsAsync(CancellationToken.None);

        delivered.Should().Be(1);
        spool.GetStats().PendingCount.Should().Be(0);
        (await _assertContext.Set<AuditLog>().CountAsync()).Should().Be(1);
    }

    private AuditLog AuditLogEntryFactoryForTests(Guid eventId) =>
        new()
        {
            Id = eventId,
            ActionType = AuditActionTypes.LoginFailed,
            ResourceType = "User",
            Success = false,
            RiskLevel = AuditRiskLevel.High,
            Category = AuditCategory.Authentication
        };

    /// <summary>A context whose saves always fail, simulating a database outage.</summary>
    private sealed class FailingDbContext(DbContextOptions<ApplicationDbContext> options) : TestApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated database outage");
    }
}
