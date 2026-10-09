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

    private sealed class ProtectedController
    {
        public void Dashboard()
        {
        }
    }
}
