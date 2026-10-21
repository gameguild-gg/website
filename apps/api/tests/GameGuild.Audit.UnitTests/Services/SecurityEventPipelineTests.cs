using Microsoft.Extensions.Configuration;
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
            new NoopPiiRedactionService(),
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
            new NoopPiiRedactionService(),
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
            new NoopPiiRedactionService(),
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

    private static CreateAuditLogRequest PermissionRequest(Guid? tenant, Guid? actor,
        string action = AuditActionTypes.PermissionGranted, bool success = true, object? metadata = null) =>
        new()
        {
            ActionType = action,
            ResourceType = "Permission",
            UserId = actor,
            TenantId = tenant,
            Success = success,
            Category = AuditCategory.Permission,
            Metadata = metadata ?? new { ChangeCount = 1 }
        };

    [Fact]
    public async Task PermissionMutationBurst_AlertsAtThresholdAndMergesRepeatedHits()
    {
        var logger = CreateLogger(new SecurityEventPipelineOptions
        {
            PermissionChangeAlertThreshold = 3,
            BulkPermissionChangeAlertThreshold = 100
        });
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        await logger.RecordAsync(PermissionRequest(tenant, actor));
        await logger.RecordAsync(PermissionRequest(tenant, actor, AuditActionTypes.PermissionRevoked));
        Assert.Empty(await _assertContext.Set<SecurityAlert>().ToListAsync());

        await logger.RecordAsync(PermissionRequest(tenant, actor, AuditActionTypes.PermissionChanged));
        var alert = await _assertContext.Set<SecurityAlert>().AsNoTracking().SingleAsync();
        Assert.Equal(SecurityAlertRules.PermissionChangeBurst, alert.RuleId);
        Assert.Equal(tenant, alert.TenantId);
        Assert.Equal(actor, alert.SubjectUserId);
        Assert.Equal(1, alert.OccurrenceCount);
        Assert.Contains("3 successful permission mutation events", alert.Description);

        await logger.RecordAsync(PermissionRequest(tenant, actor));
        alert = await _assertContext.Set<SecurityAlert>().AsNoTracking().SingleAsync();
        Assert.Equal(2, alert.OccurrenceCount);
        Assert.Equal(4, await _assertContext.Set<AuditLog>().CountAsync());
    }

    [Fact]
    public async Task PermissionMutationBurst_KeepsActorAndTenantWindowsSeparate()
    {
        var logger = CreateLogger(new SecurityEventPipelineOptions { PermissionChangeAlertThreshold = 2 });
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        await logger.RecordAsync(PermissionRequest(Guid.NewGuid(), actor));
        await logger.RecordAsync(PermissionRequest(null, actor));
        await logger.RecordAsync(PermissionRequest(tenant, Guid.NewGuid()));
        await logger.RecordAsync(PermissionRequest(tenant, actor));
        Assert.Empty(await _assertContext.Set<SecurityAlert>().ToListAsync());

        await logger.RecordAsync(PermissionRequest(tenant, actor));
        var alert = await _assertContext.Set<SecurityAlert>().AsNoTracking().SingleAsync();
        Assert.Equal(tenant, alert.TenantId);
        Assert.Equal(actor, alert.SubjectUserId);
        Assert.Contains("2 successful permission mutation events", alert.Description);
    }

    [Fact]
    public async Task PermissionAlerts_DoNotCountFailedChangesOrReadDecisions()
    {
        var logger = CreateLogger(new SecurityEventPipelineOptions
        {
            PermissionChangeAlertThreshold = 2,
            BulkPermissionChangeAlertThreshold = 2
        });
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        for (var check = 0; check < 10; check++)
        {
            await logger.RecordAsync(PermissionRequest(tenant, actor, metadata: new { Permission = "content:read", Allowed = true }));
        }
        await logger.RecordAsync(PermissionRequest(tenant, actor, success: false, metadata: new { ChangeCount = 100 }));
        await logger.RecordAsync(PermissionRequest(tenant, actor, AuditActionTypes.PermissionDenied, success: false));
        await logger.RecordAsync(PermissionRequest(tenant, actor));
        Assert.Empty(await _assertContext.Set<SecurityAlert>().ToListAsync());
        await logger.RecordAsync(PermissionRequest(tenant, actor));
        var alert = await _assertContext.Set<SecurityAlert>().AsNoTracking().SingleAsync();
        Assert.Equal(SecurityAlertRules.PermissionChangeBurst, alert.RuleId);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(25, true)]
    public async Task BulkPermissionChanges_UseCommittedSnapshotCount(int count, bool raisesAlert)
    {
        var logger = CreateLogger(new SecurityEventPipelineOptions
        {
            PermissionChangeAlertThreshold = 100,
            BulkPermissionChangeAlertThreshold = 2
        });
        await logger.RecordAsync(PermissionRequest(Guid.NewGuid(), Guid.NewGuid(), metadata: new { ChangeCount = count }));
        var alerts = await _assertContext.Set<SecurityAlert>().AsNoTracking().ToListAsync();
        if (raisesAlert)
        {
            var alert = Assert.Single(alerts);
            Assert.Equal(SecurityAlertRules.BulkPermissionChange, alert.RuleId);
            Assert.Contains($"{count} permission records", alert.Description);
            Assert.Equal(SecurityEventKind.Authorization, alert.Kind);
        }
        else
        {
            Assert.Empty(alerts);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BulkPermissionChanges_ReadCaptureAndSpoolReplayMetadata(bool camelCase)
    {
        var logger = CreateLogger(new SecurityEventPipelineOptions
        {
            BulkPermissionChangeAlertThreshold = 3,
            PermissionChangeAlertThreshold = 100
        });
        var metadata = new Dictionary<string, object> { [camelCase ? "changeCount" : "ChangeCount"] = 3 };
        await logger.RecordAsync(PermissionRequest(Guid.NewGuid(), Guid.NewGuid(), metadata: metadata));
        var alert = await _assertContext.Set<SecurityAlert>().AsNoTracking().SingleAsync();
        Assert.Equal(SecurityAlertRules.BulkPermissionChange, alert.RuleId);
        Assert.Contains("3 permission records", alert.Description);
    }

    [Fact]
    public async Task BulkPermissionChanges_ReadLegacySnapshotsAndDeduplicateByTenant()
    {
        var logger = CreateLogger(new SecurityEventPipelineOptions
        {
            BulkPermissionChangeAlertThreshold = 2,
            PermissionChangeAlertThreshold = 100
        });
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var request = PermissionRequest(tenant, actor, metadata: new { Changes = new[] { new { Operation = "Added" }, new { Operation = "Modified" } } });
        await logger.RecordAsync(request);
        await logger.RecordAsync(request);
        var alert = await _assertContext.Set<SecurityAlert>().AsNoTracking().SingleAsync();
        Assert.Equal(SecurityAlertRules.BulkPermissionChange, alert.RuleId);
        Assert.Equal(2, alert.OccurrenceCount);
        Assert.Equal(2, await _assertContext.Set<AuditLog>().CountAsync());
    }

    [Fact]
    public async Task PermissionMutationBurst_ExcludesExpiredAndFutureRows()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var now = SystemClock.UtcNow;
        foreach (var at in new[] { now.AddHours(-1), now.AddHours(1), now.AddMinutes(-1) })
        {
            _assertContext.Set<AuditLog>().Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                ActionType = AuditActionTypes.PermissionGranted,
                ResourceType = "Permission",
                UserId = actor,
                TenantId = tenant,
                Category = AuditCategory.Permission,
                Metadata = "{\"ChangeCount\":1}",
                Success = true,
                CreatedAt = at
            });
        }
        await _assertContext.SaveChangesAsync();
        var logger = CreateLogger(new SecurityEventPipelineOptions
        {
            PermissionChangeAlertThreshold = 2,
            PermissionChangeWindowMinutes = 5
        });
        await logger.RecordAsync(PermissionRequest(tenant, actor));
        var alert = await _assertContext.Set<SecurityAlert>().AsNoTracking().SingleAsync();
        Assert.Contains("2 successful permission mutation events", alert.Description);
    }

    [Fact]
    public async Task BulkPermissionChanges_ReplayDurableSpoolOnceWithoutLosingTheAlert()
    {
        var options = new SecurityEventPipelineOptions
        {
            SpoolDirectoryPath = _spoolDirectory,
            DatabaseWriteAttempts = 1,
            BulkPermissionChangeAlertThreshold = 2
        };
        using var failingProvider = new ServiceCollection()
            .AddScoped<IApplicationDbContext>(_ => new FailingDbContext(_contextOptions))
            .BuildServiceProvider();
        var spool = new SecurityEventFileSpool(options);
        var failingLogger = new SecurityEventLogger(
            failingProvider.GetRequiredService<IServiceScopeFactory>(), _httpContextAccessor.Object, spool,
            new SecurityAlertRuleEvaluator(Options.Create(options), NullLogger<SecurityAlertRuleEvaluator>.Instance),
            Options.Create(options), _fallbackAudit.Object, new NoopPiiRedactionService(),
            NullLogger<SecurityEventLogger>.Instance);
        var result = await failingLogger.RecordAsync(PermissionRequest(Guid.NewGuid(), Guid.NewGuid(),
            metadata: new { ChangeCount = 2 }));
        Assert.Equal(SecurityEventCaptureOutcome.SpooledLocally, result.Outcome);
        Assert.Equal(1, spool.GetStats().PendingCount);
        Assert.Empty(await _assertContext.Set<SecurityAlert>().ToListAsync());

        var healthyLogger = CreateLogger(options, spool: spool);
        Assert.Equal(1, await healthyLogger.ReplaySpooledEventsAsync());
        Assert.Equal(0, await healthyLogger.ReplaySpooledEventsAsync());
        Assert.Equal(0, spool.GetStats().PendingCount);
        var alert = await _assertContext.Set<SecurityAlert>().AsNoTracking().SingleAsync();
        Assert.Equal(SecurityAlertRules.BulkPermissionChange, alert.RuleId);
        Assert.Equal(result.EventId, alert.SourceAuditLogId);
        Assert.Equal(1, alert.OccurrenceCount);
        Assert.Equal(1, await _assertContext.Set<AuditLog>().CountAsync());
    }

    [Fact]
    public async Task PermissionMutationBurst_UsesIpOnlyWhenTheActorIsUnknown()
    {
        var logger = CreateLogger(new SecurityEventPipelineOptions { PermissionChangeAlertThreshold = 2 });
        var tenant = Guid.NewGuid();
        var first = PermissionRequest(tenant, null);
        first.IpAddress = "192.0.2.10";
        await logger.RecordAsync(first);
        var other = PermissionRequest(tenant, null);
        other.IpAddress = "192.0.2.11";
        await logger.RecordAsync(other);
        var knownActor = PermissionRequest(tenant, Guid.NewGuid());
        knownActor.IpAddress = first.IpAddress;
        await logger.RecordAsync(knownActor);
        Assert.Empty(await _assertContext.Set<SecurityAlert>().ToListAsync());

        await logger.RecordAsync(first);
        var alert = await _assertContext.Set<SecurityAlert>().AsNoTracking().SingleAsync();
        Assert.Equal(SecurityAlertRules.PermissionChangeBurst, alert.RuleId);
        Assert.Equal(tenant, alert.TenantId);
        Assert.Null(alert.SubjectUserId);
        Assert.Equal(first.IpAddress, alert.IpAddress);
    }

    [Theory]
    [InlineData(-1, 2, 1, 2)]
    [InlineData(int.MaxValue, 1000, 1440, 100000)]
    public void PermissionAlertOptions_BindAndClampAllThresholds(int input, int burst, int window, int bulk)
    {
        var value = input.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var prefix = SecurityEventPipelineOptions.ConfigurationSection + ":";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [prefix + nameof(SecurityEventPipelineOptions.PermissionChangeAlertThreshold)] = value,
            [prefix + nameof(SecurityEventPipelineOptions.PermissionChangeWindowMinutes)] = value,
            [prefix + nameof(SecurityEventPipelineOptions.BulkPermissionChangeAlertThreshold)] = value
        }).Build();

        var options = SecurityEventPipelineOptionsConfiguration.BindFrom(configuration);

        Assert.Equal(burst, options.PermissionChangeAlertThreshold);
        Assert.Equal(window, options.PermissionChangeWindowMinutes);
        Assert.Equal(bulk, options.BulkPermissionChangeAlertThreshold);
    }

    [Fact]
    public void PermissionAlertOptions_AreAppliedByProductionModuleRegistration()
    {
        var prefix = SecurityEventPipelineOptions.ConfigurationSection + ":";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [prefix + nameof(SecurityEventPipelineOptions.PermissionChangeAlertThreshold)] = "4",
            [prefix + nameof(SecurityEventPipelineOptions.PermissionChangeWindowMinutes)] = "3",
            [prefix + nameof(SecurityEventPipelineOptions.BulkPermissionChangeAlertThreshold)] = "17"
        }).Build();
        using var provider = new ServiceCollection().AddSingleton<IConfiguration>(configuration)
            .AddAuditServices().BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<SecurityEventPipelineOptions>>().Value;

        Assert.Equal(4, options.PermissionChangeAlertThreshold);
        Assert.Equal(3, options.PermissionChangeWindowMinutes);
        Assert.Equal(17, options.BulkPermissionChangeAlertThreshold);
    }

    /// <summary>A context whose saves always fail, simulating a database outage.</summary>
    private sealed class FailingDbContext(DbContextOptions<ApplicationDbContext> options) : TestApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated database outage");
    }
}
