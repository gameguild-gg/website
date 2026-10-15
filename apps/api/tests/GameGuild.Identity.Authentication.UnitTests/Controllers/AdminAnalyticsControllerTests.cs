using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Controllers;

public sealed class AccessReviewAnalyticsControllerTests
{
    [Fact]
    public async Task RevokeAccess_ShouldReturnOkAndForwardCommand()
    {
        var mediator = new Mock<IMediator>();
        var command = new RevokeAccessCommand();

        mediator
            .Setup(x => x.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new AccessReviewAnalyticsController(mediator.Object, NullLogger<AccessReviewAnalyticsController>.Instance);

        var result = await controller.RevokeAccess(command);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(true);
    }

    [Fact]
    public async Task BulkRevokeAccess_ShouldReturnOkAndForwardCommand()
    {
        var mediator = new Mock<IMediator>();
        var command = new BulkRevokeAccessCommand();

        mediator
            .Setup(x => x.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BulkAccessRevocationResult)null!);

        var controller = new AccessReviewAnalyticsController(mediator.Object, NullLogger<AccessReviewAnalyticsController>.Instance);

        var result = await controller.BulkRevokeAccess(command);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeNull();
    }

    [Fact]
    public async Task GetAccessRevocationHistory_ShouldMapQueryParameters()
    {
        var mediator = new Mock<IMediator>();
        GetAccessRevocationHistoryQuery? captured = null;
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var fromDate = DateTime.UtcNow.AddDays(-10);
        var toDate = DateTime.UtcNow.AddDays(-1);

        mediator
            .Setup(x => x.Send(It.IsAny<GetAccessRevocationHistoryQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (GetAccessRevocationHistoryQuery)request)
            .ReturnsAsync((PagedResult<AccessRevocationRecord>)null!);

        var controller = new AccessReviewAnalyticsController(mediator.Object, NullLogger<AccessReviewAnalyticsController>.Instance);

        var result = await controller.GetAccessRevocationHistory(userId, resourceId, fromDate, toDate, page: 3, pageSize: 25);

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(userId);
        captured.ResourceId.Should().Be(resourceId);
        captured.FromDate.Should().Be(fromDate);
        captured.ToDate.Should().Be(toDate);
        captured.Page.Should().Be(3);
        captured.PageSize.Should().Be(25);
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAccessReviewAnalytics_ShouldApplyDefaultDatesWhenMissing()
    {
        var mediator = new Mock<IMediator>();
        GetAccessReviewAnalyticsQuery? captured = null;
        var tenantId = Guid.NewGuid();

        mediator
            .Setup(x => x.Send(It.IsAny<GetAccessReviewAnalyticsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (GetAccessReviewAnalyticsQuery)request)
            .ReturnsAsync((AccessReviewAnalyticsDto)null!);

        var controller = new AccessReviewAnalyticsController(mediator.Object, NullLogger<AccessReviewAnalyticsController>.Instance);

        var result = await controller.GetAccessReviewAnalytics(tenantId, null, null);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(tenantId);
        captured.FromDate.Should().BeCloseTo(DateTime.UtcNow.AddMonths(-3), TimeSpan.FromSeconds(5));
        captured.ToDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetComplianceStatus_ShouldReturnOkAndForwardTenantId()
    {
        var mediator = new Mock<IMediator>();
        GetComplianceStatusQuery? captured = null;
        var tenantId = Guid.NewGuid();

        mediator
            .Setup(x => x.Send(It.IsAny<GetComplianceStatusQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (GetComplianceStatusQuery)request)
            .ReturnsAsync((ComplianceStatusDto)null!);

        var controller = new AccessReviewAnalyticsController(mediator.Object, NullLogger<AccessReviewAnalyticsController>.Instance);

        var result = await controller.GetComplianceStatus(tenantId);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(tenantId);
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GenerateAccessReviewReport_ShouldReturnOkAndForwardCommand()
    {
        var mediator = new Mock<IMediator>();
        var command = new GenerateAccessReviewReportCommand();

        mediator
            .Setup(x => x.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccessReviewReportDto)null!);

        var controller = new AccessReviewAnalyticsController(mediator.Object, NullLogger<AccessReviewAnalyticsController>.Instance);

        var result = await controller.GenerateAccessReviewReport(command);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeNull();
    }
}

public sealed class PermissionAdminControllerTests
{
    [Fact]
    public async Task GetPermissionAnalytics_ShouldApplyDefaultDatesWhenMissing()
    {
        var mediator = new Mock<IMediator>();
        GetPermissionAnalyticsQuery? captured = null;
        var tenantId = Guid.NewGuid();

        mediator
            .Setup(x => x.Send(It.IsAny<GetPermissionAnalyticsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (GetPermissionAnalyticsQuery)request)
            .ReturnsAsync((PermissionAnalyticsDto)null!);

        var controller = new PermissionAdminController(mediator.Object, NullLogger<PermissionAdminController>.Instance);

        var result = await controller.GetPermissionAnalytics(tenantId, null, null);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(tenantId);
        captured.FromDate.Should().BeCloseTo(DateTime.UtcNow.AddDays(-30), TimeSpan.FromSeconds(5));
        captured.ToDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPermissionAuditTrail_ShouldMapQueryParameters()
    {
        var mediator = new Mock<IMediator>();
        GetPermissionAuditTrailQuery? captured = null;
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var fromDate = DateTime.UtcNow.AddDays(-14);
        var toDate = DateTime.UtcNow.AddDays(-2);

        mediator
            .Setup(x => x.Send(It.IsAny<GetPermissionAuditTrailQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (GetPermissionAuditTrailQuery)request)
            .ReturnsAsync((PermissionAuditTrailDto)null!);

        var controller = new PermissionAdminController(mediator.Object, NullLogger<PermissionAdminController>.Instance);

        var result = await controller.GetPermissionAuditTrail(userId, tenantId, fromDate, toDate, page: 4, pageSize: 75);

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(userId);
        captured.TenantId.Should().Be(tenantId);
        captured.FromDate.Should().Be(fromDate);
        captured.ToDate.Should().Be(toDate);
        captured.Page.Should().Be(4);
        captured.PageSize.Should().Be(75);
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetCacheStatistics_ShouldReturnOk()
    {
        var mediator = new Mock<IMediator>();

        mediator
            .Setup(x => x.Send(It.IsAny<GetPermissionCacheStatsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PermissionCacheStatsDto)null!);

        var controller = new PermissionAdminController(mediator.Object, NullLogger<PermissionAdminController>.Instance);

        var result = await controller.GetCacheStatistics();

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeNull();
    }

    [Fact]
    public async Task ClearPermissionCache_ShouldReturnOkAndMapIdentifiers()
    {
        var mediator = new Mock<IMediator>();
        ClearPermissionCacheCommand? captured = null;
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        mediator
            .Setup(x => x.Send(It.IsAny<ClearPermissionCacheCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (ClearPermissionCacheCommand)request)
            .ReturnsAsync(true);

        var controller = new PermissionAdminController(mediator.Object, NullLogger<PermissionAdminController>.Instance);

        var result = await controller.ClearPermissionCache(userId, tenantId);

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(userId);
        captured.TenantId.Should().Be(tenantId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        GetAnonymousProperty<string>(ok.Value!, "message").Should().Be("Permission cache cleared successfully");
    }

    [Fact]
    public async Task GetPermissionTemplates_ShouldReturnOk()
    {
        var mediator = new Mock<IMediator>();

        mediator
            .Setup(x => x.Send(It.IsAny<GetPermissionTemplatesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<PermissionTemplateDto>)Array.Empty<PermissionTemplateDto>());

        var controller = new PermissionAdminController(mediator.Object, NullLogger<PermissionAdminController>.Instance);

        var result = await controller.GetPermissionTemplates();

        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ApplyPermissionTemplate_ShouldMapTemplateAndRequest()
    {
        var mediator = new Mock<IMediator>();
        ApplyPermissionTemplateCommand? captured = null;
        var templateId = Guid.NewGuid();
        var request = new ApplyPermissionTemplateRequest(Guid.NewGuid(), Guid.NewGuid());

        mediator
            .Setup(x => x.Send(It.IsAny<ApplyPermissionTemplateCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((command, _) => captured = (ApplyPermissionTemplateCommand)command)
            .ReturnsAsync((ApplyPermissionTemplateResult)null!);

        var controller = new PermissionAdminController(mediator.Object, NullLogger<PermissionAdminController>.Instance);

        var result = await controller.ApplyPermissionTemplate(templateId, request);

        captured.Should().NotBeNull();
        captured!.TemplateId.Should().Be(templateId);
        captured.UserId.Should().Be(request.UserId);
        captured.TenantId.Should().Be(request.TenantId);
        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeNull();
    }

    private static T GetAnonymousProperty<T>(object target, string propertyName)
    {
        return (T)target.GetType().GetProperty(propertyName)!.GetValue(target)!;
    }
}

public sealed class PermissionEvaluationControllerTests
{
    [Fact]
    public async Task CheckTenantPermission_ShouldReturnOkAndForwardQuery()
    {
        var mediator = new Mock<IMediator>();
        var query = new HasTenantPermissionQuery();

        mediator
            .Setup(x => x.Send(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new PermissionEvaluationController(mediator.Object, NullLogger<PermissionEvaluationController>.Instance);

        var result = await controller.CheckTenantPermission(query);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(true);
    }

    [Fact]
    public async Task GetTenantPermissions_ShouldMapIdentifiersAndPaging()
    {
        var mediator = new Mock<IMediator>();
        GetTenantPermissionsQuery? captured = null;
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        mediator
            .Setup(x => x.Send(It.IsAny<GetTenantPermissionsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (GetTenantPermissionsQuery)request)
            .ReturnsAsync(new GetTenantPermissionsResponse
            {
                UserId = userId,
                TenantId = tenantId,
                Permissions = [],
                Page = 2,
                PageSize = 25,
                TotalCount = 0
            });

        var controller = new PermissionEvaluationController(mediator.Object, NullLogger<PermissionEvaluationController>.Instance);

        var result = await controller.GetTenantPermissions(userId, tenantId, page: 2, pageSize: 25, sortDescending: true);

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(userId);
        captured.TenantId.Value.Should().Be(tenantId);
        captured.Page.Should().Be(2);
        captured.PageSize.Should().Be(25);
        captured.SortDescending.Should().BeTrue();
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckContentTypePermission_ShouldReturnOkAndForwardQuery()
    {
        var mediator = new Mock<IMediator>();
        var query = new HasContentTypePermissionQuery();

        mediator
            .Setup(x => x.Send(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = new PermissionEvaluationController(mediator.Object, NullLogger<PermissionEvaluationController>.Instance);

        var result = await controller.CheckContentTypePermission(query);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(false);
    }

    [Fact]
    public async Task GetContentTypePermissions_ShouldMapOptionalParameters()
    {
        var mediator = new Mock<IMediator>();
        GetContentTypePermissionsQuery? captured = null;
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        mediator
            .Setup(x => x.Send(It.IsAny<GetContentTypePermissionsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (GetContentTypePermissionsQuery)request)
            .ReturnsAsync(Array.Empty<PermissionType>());

        var controller = new PermissionEvaluationController(mediator.Object, NullLogger<PermissionEvaluationController>.Instance);

        var result = await controller.GetContentTypePermissions(userId, tenantId, "listing");

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(userId);
        captured.TenantId.Should().Be(tenantId);
        captured.ContentType.Should().Be("listing");
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckResourcePermission_ShouldReturnOkAndForwardQuery()
    {
        var mediator = new Mock<IMediator>();
        var query = new HasResourcePermissionQuery();

        mediator
            .Setup(x => x.Send(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new PermissionEvaluationController(mediator.Object, NullLogger<PermissionEvaluationController>.Instance);

        var result = await controller.CheckResourcePermission(query);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(true);
    }

    [Fact]
    public async Task GetResourcePermissions_ShouldMapIdentifiers()
    {
        var mediator = new Mock<IMediator>();
        GetResourcePermissionsQuery? captured = null;
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        mediator
            .Setup(x => x.Send(It.IsAny<GetResourcePermissionsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (GetResourcePermissionsQuery)request)
            .ReturnsAsync(Array.Empty<PermissionType>());

        var controller = new PermissionEvaluationController(mediator.Object, NullLogger<PermissionEvaluationController>.Instance);

        var result = await controller.GetResourcePermissions(userId, resourceId);

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(userId);
        captured.ResourceId.Should().Be(resourceId);
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUserPermissions_ShouldMapIdentifiers()
    {
        var mediator = new Mock<IMediator>();
        GetUserPermissionsQuery? captured = null;
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        mediator
            .Setup(x => x.Send(It.IsAny<GetUserPermissionsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (GetUserPermissionsQuery)request)
            .ReturnsAsync((UserPermissionsDto)null!);

        var controller = new PermissionEvaluationController(mediator.Object, NullLogger<PermissionEvaluationController>.Instance);

        var result = await controller.GetUserPermissions(userId, tenantId);

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(userId);
        captured.TenantId.Should().Be(tenantId);
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetEffectivePermissions_ShouldMapOptionalIdentifiers()
    {
        var mediator = new Mock<IMediator>();
        GetEffectivePermissionsQuery? captured = null;
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        mediator
            .Setup(x => x.Send(It.IsAny<GetEffectivePermissionsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (GetEffectivePermissionsQuery)request)
            .ReturnsAsync((EffectivePermissionsDto)null!);

        var controller = new PermissionEvaluationController(mediator.Object, NullLogger<PermissionEvaluationController>.Instance);

        var result = await controller.GetEffectivePermissions(userId, tenantId, resourceId);

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(userId);
        captured.TenantId.Should().Be(tenantId);
        captured.ResourceId.Should().Be(resourceId);
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ResolvePermissionHierarchy_ShouldReturnOkAndForwardQuery()
    {
        var mediator = new Mock<IMediator>();
        var query = new ResolvePermissionHierarchyQuery();

        mediator
            .Setup(x => x.Send(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PermissionHierarchyResult)null!);

        var controller = new PermissionEvaluationController(mediator.Object, NullLogger<PermissionEvaluationController>.Instance);

        var result = await controller.ResolvePermissionHierarchy(query);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeNull();
    }
}

public sealed class PermissionGrantsControllerTests
{
    [Fact]
    public async Task CreateTenantGrant_ShouldReturnCreatedAndForwardGuardedCommand()
    {
        var mediator = new Mock<IMediator>();
        var actorId = Guid.NewGuid();
        var request = new GrantTenantPermissionRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ["users:read"]);

        mediator
            .Setup(x => x.Send(It.IsAny<Authorization.GrantTenantPermissionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var controller = new PermissionGrantsController(
            mediator.Object,
            ActorAccessor(actorId),
            NullLogger<PermissionGrantsController>.Instance);

        var result = await controller.CreateTenantGrant(request);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(PermissionEvaluationController.GetTenantPermissions));
        created.ControllerName.Should().Be("PermissionEvaluation");
        created.RouteValues!["userId"].Should().Be(request.UserId);
        created.RouteValues["tenantId"].Should().Be((Guid)request.TenantId);

        // The acting user comes from the actor context, never the request body.
        mediator.Verify(x => x.Send(
            It.Is<Authorization.GrantTenantPermissionCommand>(command =>
                command.UserId == request.UserId &&
                command.Permissions.SequenceEqual(request.Permissions) &&
                command.GrantedBy == actorId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTenantGrant_ShouldReturnNoContentAndMapGrantId()
    {
        var mediator = new Mock<IMediator>();
        RevokeTenantPermissionByIdCommand? captured = null;
        var grantId = Guid.NewGuid();

        mediator
            .Setup(x => x.Send(It.IsAny<RevokeTenantPermissionByIdCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (RevokeTenantPermissionByIdCommand)request)
            .Returns(Task.CompletedTask);

        var controller = new PermissionGrantsController(
            mediator.Object,
            ActorAccessor(),
            NullLogger<PermissionGrantsController>.Instance);

        var result = await controller.DeleteTenantGrant(grantId);

        captured.Should().NotBeNull();
        captured!.GrantId.Should().Be(grantId);
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RevokeTenantPermission_ShouldReturnNoContentAndForwardGuardedCommand()
    {
        var mediator = new Mock<IMediator>();
        var actorId = Guid.NewGuid();
        var request = new RevokeTenantPermissionRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ["users:read"]);

        mediator
            .Setup(x => x.Send(It.IsAny<Authorization.RevokeTenantPermissionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new PermissionGrantsController(
            mediator.Object,
            ActorAccessor(actorId),
            NullLogger<PermissionGrantsController>.Instance);

        var result = await controller.RevokeTenantPermission(request);

        result.Should().BeOfType<NoContentResult>();
        mediator.Verify(x => x.Send(
            It.Is<Authorization.RevokeTenantPermissionCommand>(command =>
                command.UserId == request.UserId &&
                command.Permissions.SequenceEqual(request.Permissions) &&
                command.RevokedBy == actorId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BatchCreateTenantGrants_ShouldReturnOkAndForwardCommand()
    {
        var mediator = new Mock<IMediator>();
        var command = new BulkGrantTenantPermissionsCommand();

        mediator
            .Setup(x => x.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BulkPermissionResult)null!);

        var controller = new PermissionGrantsController(mediator.Object, ActorAccessor(), NullLogger<PermissionGrantsController>.Instance);

        var result = await controller.BatchCreateTenantGrants(command);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeNull();
    }

    [Fact]
    public async Task BatchDeleteTenantGrants_ShouldReturnOkAndForwardCommand()
    {
        var mediator = new Mock<IMediator>();
        var command = new BulkRevokeTenantPermissionsCommand();

        mediator
            .Setup(x => x.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BulkPermissionResult)null!);

        var controller = new PermissionGrantsController(mediator.Object, ActorAccessor(), NullLogger<PermissionGrantsController>.Instance);

        var result = await controller.BatchDeleteTenantGrants(command);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeNull();
    }

    [Fact]
    public async Task CreateContentTypeGrant_ShouldReturnCreatedAtActionAndForwardCommand()
    {
        var mediator = new Mock<IMediator>();
        var command = new GrantContentTypePermissionCommand { UserId = Guid.NewGuid() };

        mediator
            .Setup(x => x.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContentTypePermission)null!);

        var controller = new PermissionGrantsController(mediator.Object, ActorAccessor(), NullLogger<PermissionGrantsController>.Instance);

        var result = await controller.CreateContentTypeGrant(command);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(PermissionEvaluationController.GetContentTypePermissions));
        created.ControllerName.Should().Be("PermissionEvaluation");
        created.RouteValues!["userId"].Should().Be(command.UserId);
        created.Value.Should().BeNull();
    }

    [Fact]
    public async Task DeleteContentTypeGrant_ShouldReturnNoContentAndMapGrantId()
    {
        var mediator = new Mock<IMediator>();
        RevokeContentTypePermissionByIdCommand? captured = null;
        var grantId = Guid.NewGuid();

        mediator
            .Setup(x => x.Send(It.IsAny<RevokeContentTypePermissionByIdCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (RevokeContentTypePermissionByIdCommand)request)
            .Returns(Task.CompletedTask);

        var controller = new PermissionGrantsController(mediator.Object, ActorAccessor(), NullLogger<PermissionGrantsController>.Instance);

        var result = await controller.DeleteContentTypeGrant(grantId);

        captured.Should().NotBeNull();
        captured!.GrantId.Should().Be(grantId);
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RevokeContentTypePermission_ShouldReturnNoContentAndForwardCommand()
    {
        var mediator = new Mock<IMediator>();
        var command = new RevokeContentTypePermissionCommand();

        mediator
            .Setup(x => x.Send(command, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new PermissionGrantsController(mediator.Object, ActorAccessor(), NullLogger<PermissionGrantsController>.Instance);

        var result = await controller.RevokeContentTypePermission(command);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task CreateResourceGrant_ShouldReturnCreatedAtActionAndForwardCommand()
    {
        var mediator = new Mock<IMediator>();
        var command = new GrantResourcePermissionCommand { UserId = Guid.NewGuid(), ResourceId = Guid.NewGuid() };

        mediator
            .Setup(x => x.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new PermissionGrantsController(mediator.Object, ActorAccessor(), NullLogger<PermissionGrantsController>.Instance);

        var result = await controller.CreateResourceGrant(command);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(PermissionEvaluationController.GetResourcePermissions));
        created.ControllerName.Should().Be("PermissionEvaluation");
        created.RouteValues!["userId"].Should().Be(command.UserId);
        created.RouteValues["resourceId"].Should().Be(command.ResourceId);
        created.Value.Should().Be(true);
    }

    [Fact]
    public async Task DeleteResourceGrant_ShouldReturnNoContentAndMapGrantId()
    {
        var mediator = new Mock<IMediator>();
        RevokeResourcePermissionByIdCommand? captured = null;
        var grantId = Guid.NewGuid();

        mediator
            .Setup(x => x.Send(It.IsAny<RevokeResourcePermissionByIdCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (RevokeResourcePermissionByIdCommand)request)
            .Returns(Task.CompletedTask);

        var controller = new PermissionGrantsController(mediator.Object, ActorAccessor(), NullLogger<PermissionGrantsController>.Instance);

        var result = await controller.DeleteResourceGrant(grantId);

        captured.Should().NotBeNull();
        captured!.GrantId.Should().Be(grantId);
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task RevokeResourcePermission_ShouldReturnNoContentAndForwardCommand()
    {
        var mediator = new Mock<IMediator>();
        var command = new RevokeResourcePermissionCommand();

        mediator
            .Setup(x => x.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = new PermissionGrantsController(mediator.Object, ActorAccessor(), NullLogger<PermissionGrantsController>.Instance);

        var result = await controller.RevokeResourcePermission(command);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task BatchCreateResourceGrants_ShouldReturnOkAndForwardCommand()
    {
        var mediator = new Mock<IMediator>();
        var command = new BulkGrantResourcePermissionsCommand();

        mediator
            .Setup(x => x.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BulkPermissionResult)null!);

        var controller = new PermissionGrantsController(mediator.Object, ActorAccessor(), NullLogger<PermissionGrantsController>.Instance);

        var result = await controller.BatchCreateResourceGrants(command);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeNull();
    }

    private static IActorContextAccessor ActorAccessor(Guid? subjectId = null)
    {
        var actor = new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = (subjectId ?? Guid.NewGuid()).ToString(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        };
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);
        return accessor.Object;
    }
}