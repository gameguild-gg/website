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
///     Unit tests for the read side of the security event pipeline: the published taxonomy,
/// the durable delivery status, and the tenant-scoped security alert queue.
/// </summary>
public sealed class SecurityEventQueryServiceTests : IDisposable
{
    private readonly DbContextOptions<ApplicationDbContext> _contextOptions;
    private readonly TestApplicationDbContext _context;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<IActorContextAccessor> _actors = new();
    private readonly Mock<ISecurityEventSpool> _spool = new();
    private readonly SecurityEventPipelineStatusTracker _statusTracker = new();
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _admin = Guid.NewGuid();

    public SecurityEventQueryServiceTests()
    {
        _contextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"SecurityEventQuery_{Guid.NewGuid()}", new InMemoryDatabaseRoot())
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

    private SecurityEventQueryService CreateService() => new(
        _actors.Object,
        _spool.Object,
        _statusTracker,
        _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new SecurityEventPipelineOptions()),
        NullLogger<SecurityEventQueryService>.Instance);

    [Fact]
    public void GetTaxonomy_PublishesTheCompleteClassification()
    {
        var taxonomy = CreateService().GetTaxonomy();

        taxonomy.TotalEntries.Should().Be(taxonomy.Entries.Count).And.BePositive();
        taxonomy.Entries.Should().Contain(entry =>
            entry.ActionType == AuditActionTypes.LoginFailed && entry.Kind == SecurityEventKind.Authentication);
        taxonomy.Kinds.Should().BeEquivalentTo(Enum.GetNames<SecurityEventKind>());
    }

    [Fact]
    public async Task GetDeliveryStatusAsync_ReportsSpoolDepthAndDrainState()
    {
        var oldest = new DateTime(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc);
        _spool.Setup(spool => spool.GetStats())
            .Returns(new SecurityEventSpoolStats(3, oldest));
        _statusTracker.RecordDrainAttempt(oldest.AddMinutes(1));
        _statusTracker.RecordDrainFailure(oldest.AddMinutes(6), "database unavailable");

        var status = await CreateService().GetDeliveryStatusAsync(CancellationToken.None);

        status.SpooledEventCount.Should().Be(3);
        status.OldestSpooledEventUtc.Should().Be(oldest);
        status.LastDrainError.Should().Be("database unavailable");
        status.SpoolingEnabled.Should().BeTrue();
        status.DatabaseWriteAttemptsBeforeSpool.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAlertsAsync_RequiresAnAuthenticatedTenantAdministrator()
    {
        SetAdminActor(authenticated: false);

        var act = () => CreateService().GetAlertsAsync(new SecurityAlertListRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetAlertsAsync_AppliesSeverityStatusAndSubjectFilters()
    {
        var subject = Guid.NewGuid();
        _context.Set<SecurityAlert>().AddRange(
            SecurityAlert.Raise(_tenant, SecurityAlertRules.FailedAuthenticationBurst, SecurityEventKind.Authentication,
                AuditRiskLevel.High, "Burst", "desc", AuditActionTypes.LoginFailed, Guid.NewGuid(), subject, "192.0.2.1",
                SystemClock.UtcNow),
            SecurityAlert.Raise(_tenant, SecurityAlertRules.CriticalEvent, SecurityEventKind.ThreatDetection,
                AuditRiskLevel.Critical, "Critical", "desc", AuditActionTypes.SecurityViolation, Guid.NewGuid(), null, null,
                SystemClock.UtcNow),
            SecurityAlert.Raise(Guid.NewGuid(), SecurityAlertRules.CriticalEvent, SecurityEventKind.ThreatDetection,
                AuditRiskLevel.Critical, "Other tenant", "desc", AuditActionTypes.SecurityViolation, Guid.NewGuid(), null, null,
                SystemClock.UtcNow));
        await _context.SaveChangesAsync();

        var alerts = await CreateService().GetAlertsAsync(new SecurityAlertListRequest
        {
            MinimumSeverity = AuditRiskLevel.Critical,
            Status = SecurityAlertStatus.Open
        }, CancellationToken.None);

        alerts.Should().ContainSingle().Which.Title.Should().Be("Critical");
    }

    [Fact]
    public async Task GetAlertsAsync_RejectsInvalidPagination()
    {
        var act = () => CreateService().GetAlertsAsync(new SecurityAlertListRequest { Take = 0 }, CancellationToken.None);

        (await act.Should().ThrowAsync<SecurityLogRetentionValidationException>())
            .Which.Errors.Should().ContainKey("Pagination");
    }

    [Fact]
    public async Task AcknowledgeAlertAsync_MarksOpenAlertWithActingAdministrator()
    {
        var alert = SecurityAlert.Raise(_tenant, SecurityAlertRules.FailedAuthenticationBurst,
            SecurityEventKind.Authentication, AuditRiskLevel.High, "Burst", "desc", AuditActionTypes.LoginFailed,
            Guid.NewGuid(), Guid.NewGuid(), "192.0.2.1", SystemClock.UtcNow);
        _context.Set<SecurityAlert>().Add(alert);
        await _context.SaveChangesAsync();

        var acknowledged = await CreateService().AcknowledgeAlertAsync(alert.Id, "handled", CancellationToken.None);

        acknowledged.Should().NotBeNull();
        acknowledged!.Status.Should().Be(SecurityAlertStatus.Acknowledged);
        acknowledged.AcknowledgedByUserId.Should().Be(_admin);
        acknowledged.AcknowledgementNotes.Should().Be("handled");
    }

    [Fact]
    public async Task AcknowledgeAlertAsync_ReturnsNullForAlertsOfOtherTenants()
    {
        var alert = SecurityAlert.Raise(Guid.NewGuid(), SecurityAlertRules.FailedAuthenticationBurst,
            SecurityEventKind.Authentication, AuditRiskLevel.High, "Burst", "desc", AuditActionTypes.LoginFailed,
            Guid.NewGuid(), null, null, SystemClock.UtcNow);
        _context.Set<SecurityAlert>().Add(alert);
        await _context.SaveChangesAsync();

        var acknowledged = await CreateService().AcknowledgeAlertAsync(alert.Id, notes: null, CancellationToken.None);

        acknowledged.Should().BeNull();
    }
}
