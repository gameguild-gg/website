using FluentAssertions;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using GameGuild.Tests.Audit.Unit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

/// <summary>
///     Issue #294: the audit self-service tier (Audit.Read / Audit.Export) must never leak
///     cross-tenant audit data. Non-system callers are clamped to their own tenant — an
///     explicit cross-tenant request or a missing tenant context fails closed with 403.
/// </summary>
public sealed class AuditTenantClampTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task GetAuditLogs_TenantActor_OwnTenantRequest_IsClampedToOwnTenant()
    {
        var (controller, auditService) = CreateAuditController(TenantAdminActor());
        AuditLogQuery? captured = null;
        auditService
            .Setup(service => service.GetAuditLogsAsync(It.IsAny<AuditLogQuery>()))
            .Callback<AuditLogQuery>(query => captured = query)
            .ReturnsAsync([]);
        auditService
            .Setup(service => service.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>()))
            .ReturnsAsync(0);

        var result = await controller.GetAuditLogs(new AuditLogQueryRequest());

        result.Result.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(_tenantId, "a tenant caller without an explicit filter is pinned to their own tenant");
    }

    [Fact]
    public async Task GetAuditLogs_TenantActor_ExplicitOwnTenantRequest_IsAllowed()
    {
        var (controller, auditService) = CreateAuditController(TenantAdminActor());
        AuditLogQuery? captured = null;
        auditService
            .Setup(service => service.GetAuditLogsAsync(It.IsAny<AuditLogQuery>()))
            .Callback<AuditLogQuery>(query => captured = query)
            .ReturnsAsync([]);
        auditService
            .Setup(service => service.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>()))
            .ReturnsAsync(0);

        var result = await controller.GetAuditLogs(new AuditLogQueryRequest { TenantId = _tenantId });

        result.Result.Should().BeOfType<OkObjectResult>();
        captured!.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task GetAuditLogs_TenantActor_ForeignTenantRequest_IsForbidden()
    {
        var (controller, auditService) = CreateAuditController(TenantAdminActor());

        var result = await controller.GetAuditLogs(new AuditLogQueryRequest { TenantId = Guid.NewGuid() });

        result.Result.Should().BeOfType<ForbidResult>();
        auditService.Verify(service => service.GetAuditLogsAsync(It.IsAny<AuditLogQuery>()), Times.Never);
    }

    [Fact]
    public async Task GetAuditLogs_TenantlessActor_IsForbidden()
    {
        var actor = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = _userId.ToString(),
            Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TenantAdmin" },
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "audit:read" },
            IsAuthenticated = true
        };
        var (controller, auditService) = CreateAuditController(actor);

        var result = await controller.GetAuditLogs(new AuditLogQueryRequest());

        result.Result.Should().BeOfType<ForbidResult>();
        auditService.Verify(service => service.GetAuditLogsAsync(It.IsAny<AuditLogQuery>()), Times.Never);
    }

    [Fact]
    public async Task GetAuditLogs_SystemAdmin_KeepsRequestedScope()
    {
        var (controller, auditService) = CreateAuditController(SystemAdminActor());
        AuditLogQuery? captured = null;
        auditService
            .Setup(service => service.GetAuditLogsAsync(It.IsAny<AuditLogQuery>()))
            .Callback<AuditLogQuery>(query => captured = query)
            .ReturnsAsync([]);
        auditService
            .Setup(service => service.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>()))
            .ReturnsAsync(0);

        var requestedTenant = Guid.NewGuid();
        var result = await controller.GetAuditLogs(new AuditLogQueryRequest { TenantId = requestedTenant });

        result.Result.Should().BeOfType<OkObjectResult>();
        captured!.TenantId.Should().Be(requestedTenant);
    }

    [Fact]
    public async Task SearchAuditLogsWithConditions_TenantActor_IsClampedToOwnTenant()
    {
        var (controller, auditService) = CreateAuditController(TenantAdminActor());
        AuditLogQuery? captured = null;
        auditService
            .Setup(service => service.GetAuditLogsAsync(It.IsAny<AuditLogQuery>()))
            .Callback<AuditLogQuery>(query => captured = query)
            .ReturnsAsync([]);
        auditService
            .Setup(service => service.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>()))
            .ReturnsAsync(0);

        var result = await controller.SearchAuditLogsWithConditions(ConditionSearchRequest());

        result.Result.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task SearchAuditLogsWithConditions_TenantActor_ForeignTenantRequest_IsForbidden()
    {
        var (controller, auditService) = CreateAuditController(TenantAdminActor());
        var request = ConditionSearchRequest();
        request.TenantId = Guid.NewGuid();

        var result = await controller.SearchAuditLogsWithConditions(request);

        result.Result.Should().BeOfType<ForbidResult>();
        auditService.Verify(service => service.GetAuditLogsAsync(It.IsAny<AuditLogQuery>()), Times.Never);
    }

    [Fact]
    public async Task ExportAuditLogs_TenantActor_ForeignTenantRequest_IsForbidden()
    {
        var (controller, _) = CreateAuditController(TenantAdminActor());

        var result = await controller.ExportAuditLogs(new AuditExportRequest { TenantId = Guid.NewGuid() });

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetSecurityDashboard_TenantActor_IsClampedToOwnTenant()
    {
        var (controller, aggregator) = CreateSecurityAuditController(TenantAdminActor());
        Guid? capturedTenant = null;
        aggregator
            .Setup(service => service.GetSecurityDashboardAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, DateTime, Guid?, CancellationToken>((_, _, tenant, _) => capturedTenant = tenant)
            .ReturnsAsync(new SecurityAuditDashboard());

        var result = await controller.GetSecurityDashboard();

        result.Result.Should().BeOfType<OkObjectResult>();
        capturedTenant.Should().Be(_tenantId);
    }

    [Fact]
    public async Task GetSecurityAuditLogs_TenantActor_ForeignTenantRequest_IsForbidden()
    {
        var (controller, aggregator) = CreateSecurityAuditController(TenantAdminActor());

        var result = await controller.GetSecurityAuditLogs(new UnifiedSecurityAuditRequest { TenantId = Guid.NewGuid() });

        result.Result.Should().BeOfType<ForbidResult>();
        aggregator.Verify(service => service.GetUnifiedAuditLogsAsync(It.IsAny<UnifiedSecurityAuditRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private ActorContext TenantAdminActor() => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = _userId.ToString(),
        TenantId = _tenantId,
        Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TenantAdmin" },
        Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "audit:read" },
        IsAuthenticated = true
    };

    private static AuditConditionSearchRequest ConditionSearchRequest() => new()
    {
        Condition = new AuditConditionNode
        {
            Kind = AuditConditionNodeKind.Field,
            Field = AuditFilterField.ActionType,
            Comparison = AuditFilterComparison.Equals,
            Value = "ViewAuditLogs"
        }
    };

    private ActorContext SystemAdminActor() => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = _userId.ToString(),
        Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SystemAdmin" },
        Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        IsAuthenticated = true
    };

    private (AuditController Controller, Mock<IAuditService> AuditService) CreateAuditController(ActorContext actor)
    {
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAdminActionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>()))
            .Returns(Task.CompletedTask);
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(accessor => accessor.ActorContext).Returns(actor);

        var controller = new AuditController(
            auditService.Object,
            actorAccessor.Object,
            NullLogger<AuditController>.Instance,
            new CommandHandlerSender(auditService.Object),
            Mock.Of<IAuditExportProgressTracker>(),
            Mock.Of<IAuditExportWebhookNotifier>(),
            Mock.Of<IScheduledAuditExportService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            ObjectValidator = Mock.Of<IObjectModelValidator>()
        };

        return (controller, auditService);
    }

    private (SecurityAuditController Controller, Mock<ISecurityAuditAggregator> Aggregator) CreateSecurityAuditController(ActorContext actor)
    {
        var aggregator = new Mock<ISecurityAuditAggregator>();
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAdminActionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>()))
            .Returns(Task.CompletedTask);
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(accessor => accessor.ActorContext).Returns(actor);

        var controller = new SecurityAuditController(
            aggregator.Object,
            auditService.Object,
            actorAccessor.Object,
            NullLogger<SecurityAuditController>.Instance,
            new CommandHandlerSender(aggregator.Object, auditService.Object, actorAccessor.Object));

        return (controller, aggregator);
    }
}
