using FluentAssertions;
using GameGuild.Commerce.Payments.Commands.AcknowledgeRevenueAnomalyAlert;
using GameGuild.Commerce.Payments.Commands.RunRevenueAnomalyDetection;
using GameGuild.Commerce.Payments.Commands.RunRevenueReconciliation;
using GameGuild.Identity.Context.Actors;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.RevenueAuditing;

public class RunRevenueReconciliationCommandHandlerTests
{
    private readonly Mock<IRevenueReconciliationService> _reconciliationService = new();
    private readonly Mock<IRevenueAuditService> _auditService = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();

    private RunRevenueReconciliationCommandHandler CreateHandler(ActorContext actor)
    {
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(actor);
        return new RunRevenueReconciliationCommandHandler(
            _reconciliationService.Object,
            _auditService.Object,
            _actorAccessor.Object);
    }

    private static ActorContext SystemAdminActor()
        => new()
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            Roles = new HashSet<string>(["SystemAdmin"]),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        };

    private static ActorContext TenantActor(Guid tenantId)
        => new()
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        };

    [Fact]
    public async Task Handle_ForcesTenantScope_ForNonAdminActors()
    {
        var actorTenant = Guid.NewGuid();
        var run = new RevenueReconciliationRun { Status = RevenueReconciliationStatus.Completed };
        RevenueReconciliationRequest? captured = null;
        _reconciliationService
            .Setup(service => service.ReconcileAsync(It.IsAny<RevenueReconciliationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RevenueReconciliationRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(run);

        var result = await CreateHandler(TenantActor(actorTenant)).Handle(
            new RunRevenueReconciliationCommand(
                TenantId: Guid.NewGuid(), // attacker-supplied foreign tenant must be overridden
                Source: "erp",
                PeriodStartUtc: DateTime.UtcNow.AddDays(-1),
                PeriodEndUtc: DateTime.UtcNow,
                Lines: []),
            CancellationToken.None);

        result.Should().BeSameAs(run);
        captured!.TenantId.Should().Be(actorTenant);
    }

    [Fact]
    public async Task Handle_RejectsNonAdminActors_WithoutTenant()
    {
        var anonymousActor = new ActorContext
        {
            ActorKind = ActorKind.User,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = false
        };

        var act = () => CreateHandler(anonymousActor).Handle(
            new RunRevenueReconciliationCommand(null, "erp", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*own tenant*");
        _reconciliationService.Verify(
            service => service.ReconcileAsync(It.IsAny<RevenueReconciliationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_RecordsAuditTrail_WithRunCounters()
    {
        var run = new RevenueReconciliationRun { Status = RevenueReconciliationStatus.Completed, MatchedCount = 8, DiscrepancyCount = 2 };
        _reconciliationService
            .Setup(service => service.ReconcileAsync(It.IsAny<RevenueReconciliationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var actor = SystemAdminActor();
        await CreateHandler(actor).Handle(
            new RunRevenueReconciliationCommand(null, "erp", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, null),
            CancellationToken.None);

        _auditService.Verify(
            audit => audit.RecordAuditTrailAsync(
                "RevenueReconciliationRun",
                run.Id,
                "ReconciliationRun",
                actor.SubjectIdAsGuid!.Value,
                It.IsAny<string?>(),
                It.Is<string?>(value => value!.Contains("\"matchedCount\":8") && value.Contains("\"discrepancyCount\":2")),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class RunRevenueAnomalyDetectionCommandHandlerTests
{
    private readonly Mock<IRevenueAnomalyService> _anomalyService = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();

    [Fact]
    public async Task Handle_ForcesTenantScope_ForNonAdminActors()
    {
        var actorTenant = Guid.NewGuid();
        var actor = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = actorTenant,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        };
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(actor);
        _anomalyService
            .Setup(service => service.DetectAndPersistAsync(It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        var handler = new RunRevenueAnomalyDetectionCommandHandler(_anomalyService.Object, _actorAccessor.Object);

        var created = await handler.Handle(
            new RunRevenueAnomalyDetectionCommand(TenantId: Guid.NewGuid(), EvaluationDateUtc: DateTime.UtcNow),
            CancellationToken.None);

        created.Should().Be(3);
        _anomalyService.Verify(
            service => service.DetectAndPersistAsync(It.IsAny<DateTime>(), actorTenant, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UsesEvaluationDate_WhenProvided()
    {
        var actor = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            Roles = new HashSet<string>(["SystemAdmin"]),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        };
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(actor);
        _anomalyService
            .Setup(service => service.DetectAndPersistAsync(It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var handler = new RunRevenueAnomalyDetectionCommandHandler(_anomalyService.Object, _actorAccessor.Object);
        var evaluationDate = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);

        await handler.Handle(new RunRevenueAnomalyDetectionCommand(null, evaluationDate), CancellationToken.None);

        _anomalyService.Verify(
            service => service.DetectAndPersistAsync(evaluationDate, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class AcknowledgeRevenueAnomalyAlertCommandHandlerTests
{
    private readonly Mock<IRevenueAnomalyAlertRepository> _alerts = new();
    private readonly Mock<IRevenueAuditService> _auditService = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();

    private AcknowledgeRevenueAnomalyAlertCommandHandler CreateHandler(ActorContext actor)
    {
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(actor);
        return new AcknowledgeRevenueAnomalyAlertCommandHandler(
            _alerts.Object,
            _auditService.Object,
            _actorAccessor.Object);
    }

    private static ActorContext SystemAdminActor()
        => new()
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            Roles = new HashSet<string>(["SystemAdmin"]),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        };

    [Fact]
    public async Task Handle_ThrowsKeyNotFound_WhenAlertDoesNotExist()
    {
        var alertId = Guid.NewGuid();
        _alerts
            .Setup(alerts => alerts.GetByIdAsync(alertId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RevenueAnomalyAlert?)null);

        var act = () => CreateHandler(SystemAdminActor()).Handle(
            new AcknowledgeRevenueAnomalyAlertCommand(alertId),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_RejectsTenantOperator_FromAnotherTenant()
    {
        var alert = new RevenueAnomalyAlert { TenantId = Guid.NewGuid() };
        _alerts
            .Setup(alerts => alerts.GetByIdAsync(alert.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);
        var foreignTenantOperator = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        };

        var act = () => CreateHandler(foreignTenantOperator).Handle(
            new AcknowledgeRevenueAnomalyAlertCommand(alert.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*own tenant*");
        _alerts.Verify(alerts => alerts.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AcknowledgesAlert_AndRecordsAuditTrail_WithOperatorFromContext()
    {
        var alert = new RevenueAnomalyAlert { TenantId = Guid.NewGuid() };
        _alerts
            .Setup(alerts => alerts.GetByIdAsync(alert.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alert);
        var actor = SystemAdminActor();

        await CreateHandler(actor).Handle(
            new AcknowledgeRevenueAnomalyAlertCommand(alert.Id, "checked with the gateway"),
            CancellationToken.None);

        alert.Status.Should().Be(RevenueAnomalyStatus.Acknowledged);
        alert.AcknowledgedByUserId.Should().Be(actor.SubjectIdAsGuid);
        _alerts.Verify(alerts => alerts.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _auditService.Verify(
            audit => audit.RecordAuditTrailAsync(
                "RevenueAnomalyAlert",
                alert.Id,
                "Acknowledged",
                actor.SubjectIdAsGuid!.Value,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                "checked with the gateway",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class RevenueAuditingQueryHandlerTests
{
    private readonly Mock<IRevenueReconciliationRepository> _runs = new();
    private readonly Mock<IRevenueAnomalyAlertRepository> _alerts = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();

    [Fact]
    public async Task GetRevenueReconciliationRunsQueryHandler_ScopesToActorTenant_ForNonAdmins()
    {
        var actorTenant = Guid.NewGuid();
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = actorTenant,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        });
        _runs
            .Setup(repos => repos.GetRunsAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<RevenueReconciliationRun>([], 0, 0, 20));
        var handler = new Queries.RevenueAuditing.GetRevenueReconciliationRunsQueryHandler(_runs.Object, _actorAccessor.Object);

        await handler.Handle(new Queries.RevenueAuditing.GetRevenueReconciliationRunsQuery(TenantId: Guid.NewGuid()), CancellationToken.None);

        _runs.Verify(
            repos => repos.GetRunsAsync(actorTenant, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRevenueReconciliationRunByIdQueryHandler_HidesForeignTenantRuns()
    {
        var run = new RevenueReconciliationRun { TenantId = Guid.NewGuid() };
        _runs
            .Setup(repos => repos.GetRunByIdAsync(run.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        });
        var handler = new Queries.RevenueAuditing.GetRevenueReconciliationRunByIdQueryHandler(_runs.Object, _actorAccessor.Object);

        var act = () => handler.Handle(new Queries.RevenueAuditing.GetRevenueReconciliationRunByIdQuery(run.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*own tenant*");
    }

    [Fact]
    public async Task GetRevenueAnomalyAlertsQueryHandler_ScopesToActorTenant_ForNonAdmins()
    {
        var actorTenant = Guid.NewGuid();
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = actorTenant,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        });
        _alerts
            .Setup(alerts => alerts.GetAlertsAsync(It.IsAny<Guid?>(), It.IsAny<RevenueAnomalyStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<RevenueAnomalyAlert>([], 0, 0, 20));
        var handler = new Queries.RevenueAuditing.GetRevenueAnomalyAlertsQueryHandler(_alerts.Object, _actorAccessor.Object);

        await handler.Handle(new Queries.RevenueAuditing.GetRevenueAnomalyAlertsQuery(), CancellationToken.None);

        _alerts.Verify(
            alerts => alerts.GetAlertsAsync(actorTenant, It.IsAny<RevenueAnomalyStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void ResolveTenantScope_FavorsActorTenant_ForNonAdmins_AndKeepsRequestForAdmins()
    {
        var requestedTenant = Guid.NewGuid();
        var actorTenant = Guid.NewGuid();
        var tenantActor = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = actorTenant,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        };
        var adminActor = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            Roles = new HashSet<string>(["SystemAdmin"]),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        };

        Queries.RevenueAuditingReports.GetRevenueComplianceReportQueryHandler
            .ResolveTenantScope(requestedTenant, tenantActor)
            .Should().Be(actorTenant);
        Queries.RevenueAuditingReports.GetRevenueComplianceReportQueryHandler
            .ResolveTenantScope(requestedTenant, adminActor)
            .Should().Be(requestedTenant);
    }
}
