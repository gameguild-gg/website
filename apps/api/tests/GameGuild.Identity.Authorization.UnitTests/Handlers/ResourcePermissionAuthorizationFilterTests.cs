using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using GameGuild.Identity.Context.Actors;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests.Handlers;

public sealed class ResourcePermissionAuthorizationFilterTests
{
    [Fact]
    public async Task OnAuthorizationAsync_AllowsAnonymousActionWhenEndpointMetadataIsIncomplete()
    {
        var httpContext = new DefaultHttpContext();
        var actorContextAccessor = new Mock<IActorContextAccessor>();
        actorContextAccessor.SetupGet(x => x.ActorContext).Returns(ActorContext.Anonymous);
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(actorContextAccessor.Object)
            .AddSingleton(Mock.Of<IPermissionQueryService>())
            .BuildServiceProvider();
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(),
            "anonymous-action"));

        var actionDescriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = typeof(AnonymousController).GetTypeInfo(),
            MethodInfo = typeof(AnonymousController).GetMethod(nameof(AnonymousController.SignIn))!
        };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            actionDescriptor,
            new ModelStateDictionary());
        var filterContext = new AuthorizationFilterContext(actionContext, []);
        var filter = new ResourcePermissionAuthorizationFilter(
            NullLogger<ResourcePermissionAuthorizationFilter>.Instance);

        await filter.OnAuthorizationAsync(filterContext);

        filterContext.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnAuthorizationAsync_AllowsAnonymousEndpointWithoutActorAuthentication()
    {
        var httpContext = new DefaultHttpContext();
        var actorContextAccessor = new Mock<IActorContextAccessor>();
        actorContextAccessor.SetupGet(x => x.ActorContext).Returns(ActorContext.Anonymous);
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(actorContextAccessor.Object)
            .AddSingleton(Mock.Of<IPermissionQueryService>())
            .BuildServiceProvider();
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()),
            "anonymous"));

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor(),
            new ModelStateDictionary());
        var filterContext = new AuthorizationFilterContext(actionContext, []);
        var filter = new ResourcePermissionAuthorizationFilter(
            NullLogger<ResourcePermissionAuthorizationFilter>.Instance);

        await filter.OnAuthorizationAsync(filterContext);

        filterContext.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnAuthorizationAsync_MissingRequiredServices_DeniesInsteadOfSkipping()
    {
        // Deny-by-default / fail-safe defaults: a host that forgot to register the
        // authorization dependencies must not silently bypass permission checks.
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection().BuildServiceProvider();
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(),
            "protected-action"));

        var actionDescriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = typeof(ProtectedController).GetTypeInfo(),
            MethodInfo = typeof(ProtectedController).GetMethod(nameof(ProtectedController.Dashboard))!
        };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            actionDescriptor,
            new ModelStateDictionary());
        var filterContext = new AuthorizationFilterContext(actionContext, []);
        var filter = new ResourcePermissionAuthorizationFilter(
            NullLogger<ResourcePermissionAuthorizationFilter>.Instance);

        await filter.OnAuthorizationAsync(filterContext);

        filterContext.Result.Should().BeOfType<ForbidResult>();
    }

    private sealed class AnonymousController
    {
        [AllowAnonymous]
        public void SignIn()
        {
        }
    }

    // ─── Issue #346: durable decision audit for endpoint-level permission attributes ───

    [Fact]
    public async Task OnAuthorizationAsync_GrantedMonetizationPermission_RecordsCheckAuditEntry()
    {
        var auditService = new Mock<IPermissionAuditService>();
        var permissionQuery = new Mock<IPermissionQueryService>();
        permissionQuery
            .Setup(q => q.HasTenantPermissionAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var filterContext = CreateFilterContext(typeof(MetricsController).GetTypeInfo(),
            nameof(MetricsController.Get), auditService.Object, permissionQuery.Object, authenticated: true);

        await new ResourcePermissionAuthorizationFilter(
            NullLogger<ResourcePermissionAuthorizationFilter>.Instance).OnAuthorizationAsync(filterContext);

        filterContext.Result.Should().BeNull();
        auditService.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.Check,
            It.IsAny<Guid?>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid?>(),
            MonetizationPermission.Keys.ViewAnalytics,
            It.IsAny<Guid?>(),
            "Metrics.Get",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            true,
            null,
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnAuthorizationAsync_DeniedMonetizationPermission_ForbidsAndRecordsCheckAuditEntry()
    {
        var auditService = new Mock<IPermissionAuditService>();
        var permissionQuery = new Mock<IPermissionQueryService>();
        permissionQuery
            .Setup(q => q.HasTenantPermissionAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var filterContext = CreateFilterContext(typeof(MetricsController).GetTypeInfo(),
            nameof(MetricsController.Get), auditService.Object, permissionQuery.Object, authenticated: true);

        await new ResourcePermissionAuthorizationFilter(
            NullLogger<ResourcePermissionAuthorizationFilter>.Instance).OnAuthorizationAsync(filterContext);

        filterContext.Result.Should().BeOfType<ForbidResult>();
        auditService.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.Check,
            It.IsAny<Guid?>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid?>(),
            MonetizationPermission.Keys.ViewAnalytics,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            false,
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnAuthorizationAsync_AuditFailure_DoesNotChangeTheDecision()
    {
        var auditService = new Mock<IPermissionAuditService>();
        auditService
            .Setup(a => a.LogPermissionChangeAsync(
                It.IsAny<PermissionOperationType>(),
                It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit store unavailable"));
        var permissionQuery = new Mock<IPermissionQueryService>();
        permissionQuery
            .Setup(q => q.HasTenantPermissionAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var filterContext = CreateFilterContext(typeof(MetricsController).GetTypeInfo(),
            nameof(MetricsController.Get), auditService.Object, permissionQuery.Object, authenticated: true);

        var filter = new ResourcePermissionAuthorizationFilter(
            NullLogger<ResourcePermissionAuthorizationFilter>.Instance);

        var act = () => filter.OnAuthorizationAsync(filterContext);

        await act.Should().NotThrowAsync();
        filterContext.Result.Should().BeNull("an audit failure must not change an allow decision");
    }

    private static AuthorizationFilterContext CreateFilterContext(
        TypeInfo controllerType,
        string actionName,
        IPermissionAuditService auditService,
        IPermissionQueryService permissionQueryService,
        bool authenticated)
    {
        var actor = new ActorContext
        {
            IsAuthenticated = authenticated,
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>()
        };
        var actorContextAccessor = new Mock<IActorContextAccessor>();
        actorContextAccessor.SetupGet(a => a.ActorContext).Returns(actor);

        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(actorContextAccessor.Object)
            .AddSingleton(permissionQueryService)
            .AddSingleton(auditService)
            .BuildServiceProvider();
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(),
            "gated-action"));

        var actionDescriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = controllerType,
            MethodInfo = controllerType.DeclaredMethods.Single(m => m.Name == actionName),
            ControllerName = controllerType.Name.Replace("Controller", string.Empty),
            ActionName = actionName
        };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            actionDescriptor,
            new ModelStateDictionary());
        return new AuthorizationFilterContext(actionContext, []);
    }

    private sealed class MetricsController
    {
        [RequirePermission(MonetizationPermission.Keys.ViewAnalytics)]
        public void Get()
        {
        }
    }

    private sealed class ProtectedController
    {
        public void Dashboard()
        {
        }
    }
}
