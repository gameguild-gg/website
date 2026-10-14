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
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Handlers;

/// <summary>
///     Declarative-enforcement tests for the DAC attribute family through the shared
///     <c>ResourcePermissionAuthorizationFilter</c> (issues #320/#321): the extensible
///     <see cref="IDacPermissionAttribute"/> contract routed through
///     <see cref="IDacPermissionResolver"/> (content-type layer, resource layer with
///     owner override), conditional application, flexible resource-id binding
///     (route/query/header/claims), fail-closed behavior and structured denial results.
/// </summary>
public class ResourcePermissionAuthorizationFilterDacAttributeTests
{
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();
    private readonly Mock<IPermissionQueryService> _permissionQuery = new();
    private readonly Mock<IDacPermissionResolver> _dacResolver = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private ResourcePermissionAuthorizationFilter CreateFilter()
        => new(NullLogger<ResourcePermissionAuthorizationFilter>.Instance);

    private ActorContext AuthenticatedActor()
        => new()
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = _userId.ToString(),
            TenantId = _tenantId,
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>()
        };

    private AuthorizationFilterContext CreateFilterContext(
        TypeInfo controllerType,
        string actionName,
        Action<HttpContext>? configureHttpContext = null,
        bool enforceLegacyMarkers = false)
    {
        _actorAccessor.SetupGet(a => a.ActorContext).Returns(AuthenticatedActor());

        var services = new ServiceCollection();
        services.AddSingleton(_actorAccessor.Object);
        services.AddSingleton(_permissionQuery.Object);
        services.AddSingleton(_dacResolver.Object);
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(
            new GameGuild.Configuration.PresentationLayer.Controllers.ControllersOptions
            {
                EnforceLegacyPermissionMarkers = enforceLegacyMarkers
            }));
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        configureHttpContext?.Invoke(httpContext);
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

    private void SetupDacResolution(Func<DacPermissionQuery, string, bool> grants)
        => _dacResolver
            .Setup(r => r.HasPermissionAsync(It.IsAny<DacPermissionQuery>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DacPermissionQuery query, string permission, CancellationToken _) => grants(query, permission));

    // ── RequireDacPermissionAttribute (tenant layer) ───────────────────────

    [Fact]
    public async Task RequireDacPermission_TenantGranted_Passes()
    {
        SetupDacResolution((query, permission) => query.Layer == PermissionLayer.Tenant && permission == "content:read");
        var context = CreateFilterContext(typeof(ProbeController).GetTypeInfo(), nameof(ProbeController.TenantGated));

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task RequireDacPermission_Denied_ReturnsStructured403()
    {
        SetupDacResolution((_, _) => false);
        var context = CreateFilterContext(typeof(ProbeController).GetTypeInfo(), nameof(ProbeController.TenantGated));

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var problem = ((ObjectResult)context.Result!).Value.Should().BeOfType<ProblemDetails>().Which;
        problem.Extensions.Should().ContainKey("reason");
        // Production hygiene: no attribute/permission diagnostics outside Development.
        problem.Extensions.Should().NotContainKey("requiredPermissions");
    }

    [Fact]
    public async Task RequireDacPermission_MultipleEntries_UseAndLogic()
    {
        SetupDacResolution((query, permission) => permission == "first" ? true : false);
        var context = CreateFilterContext(typeof(ProbeController).GetTypeInfo(), nameof(ProbeController.MultiPermissionGated));

        await CreateFilter().OnAuthorizationAsync(context);

        // Only "first" is granted: the AND composition denies the request.
        context.Result.Should().BeOfType<ObjectResult>();
        _dacResolver.Verify(
            r => r.HasPermissionAsync(It.IsAny<DacPermissionQuery>(), It.Is<string>(p => p == "first"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Content-type layer through the scoped store ────────────────────────

    [Fact]
    public async Task RequireContentTypeDacPermission_ResolvesThroughContentTypeLayer()
    {
        DacPermissionQuery? seen = null;
        _dacResolver
            .Setup(r => r.HasPermissionAsync(It.IsAny<DacPermissionQuery>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<DacPermissionQuery, string, CancellationToken>((query, _, _) => seen = query)
            .ReturnsAsync(true);
        var context = CreateFilterContext(typeof(ProbeController).GetTypeInfo(), nameof(ProbeController.ContentTypeGated));

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        seen.Should().NotBeNull();
        seen!.ContentType.Should().Be("CourseResource");
        seen.Layer.Should().Be(PermissionLayer.ContentType);
    }

    [Fact]
    public async Task LegacyContentTypeMarker_ResolvesThroughDacResolver()
    {
        DacPermissionQuery? seen = null;
        _dacResolver
            .Setup(r => r.HasPermissionAsync(It.IsAny<DacPermissionQuery>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<DacPermissionQuery, string, CancellationToken>((query, _, _) => seen = query)
            .ReturnsAsync(true);
        var context = CreateFilterContext(
            typeof(ProbeController).GetTypeInfo(),
            nameof(ProbeController.LegacyContentTypeGated),
            enforceLegacyMarkers: true);

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        seen.Should().NotBeNull();
        // The legacy marker no longer builds a tenant-composite name: it routes through
        // the resolver's content-type path with the type name as content type.
        seen!.ContentType.Should().Be("CourseResource");
        seen.Layer.Should().Be(PermissionLayer.Auto);
    }

    // ── Resource layer (route extraction + owner-override path) ────────────

    [Fact]
    public async Task RequireResourceDacPermission_ExtractsRouteIdAndResolvesResourceContext()
    {
        var resourceGuid = Guid.NewGuid();
        DacPermissionQuery? seen = null;
        _dacResolver
            .Setup(r => r.HasPermissionAsync(It.IsAny<DacPermissionQuery>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<DacPermissionQuery, string, CancellationToken>((query, _, _) => seen = query)
            .ReturnsAsync(true);
        var context = CreateFilterContext(
            typeof(ProbeController).GetTypeInfo(),
            nameof(ProbeController.ResourceGatedRoute));
        context.HttpContext.Request.RouteValues["id"] = resourceGuid.ToString();

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        seen.Should().NotBeNull();
        seen!.ResourceType.Should().Be("ProjectResource");
        seen.ResourceId.Should().Be(resourceGuid.ToString("D"));
        seen.Layer.Should().Be(PermissionLayer.Resource);
    }

    [Fact]
    public async Task RequireResourceDacPermission_HeaderBinding_ExtractsFromHeader()
    {
        DacPermissionQuery? seen = null;
        _dacResolver
            .Setup(r => r.HasPermissionAsync(It.IsAny<DacPermissionQuery>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<DacPermissionQuery, string, CancellationToken>((query, _, _) => seen = query)
            .ReturnsAsync(true);
        var context = CreateFilterContext(
            typeof(ProbeController).GetTypeInfo(),
            nameof(ProbeController.ResourceGatedHeader));
        context.HttpContext.Request.Headers["X-Resource-Id"] = Guid.NewGuid().ToString();

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        seen.Should().NotBeNull();
        seen!.ResourceId.Should().NotBeNullOrEmpty("the resource id was bound from the header");
    }

    [Fact]
    public async Task RequireResourceDacPermission_MissingResourceId_DeniesFailClosed()
    {
        var context = CreateFilterContext(
            typeof(ProbeController).GetTypeInfo(),
            nameof(ProbeController.ResourceGatedRoute));

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task LegacyResourceMarker_ResolvesThroughDacResolverResourceContext()
    {
        var resourceGuid = Guid.NewGuid();
        DacPermissionQuery? seen = null;
        _dacResolver
            .Setup(r => r.HasPermissionAsync(It.IsAny<DacPermissionQuery>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<DacPermissionQuery, string, CancellationToken>((query, _, _) => seen = query)
            .ReturnsAsync(true);
        var context = CreateFilterContext(
            typeof(ProbeController).GetTypeInfo(),
            nameof(ProbeController.LegacyResourceGated),
            enforceLegacyMarkers: true);
        context.HttpContext.Request.RouteValues["id"] = resourceGuid.ToString();

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        seen.Should().NotBeNull();
        seen!.ResourceType.Should().Be("ProjectResource");
        seen.ResourceId.Should().Be(resourceGuid.ToString("D"));
        // The legacy resource marker resolves through the Auto layer, which activates
        // the owner-override engine for the exact resource (issues #372/#364).
        seen.Layer.Should().Be(PermissionLayer.Auto);
    }

    [Fact]
    public async Task OwnerOverrideThroughDacPath_OwnerMintedPermission_SatisfiesResourceMarker()
    {
        // The owner-override path: the resolver (engine-backed) reports the
        // resource-qualified owner permission for the exact resource.
        SetupDacResolution((query, permission) =>
            query.Layer == PermissionLayer.Auto
            && query.HasResource
            && permission == "Read"
            && query.ResourceType == "ProjectResource");
        var context = CreateFilterContext(
            typeof(ProbeController).GetTypeInfo(),
            nameof(ProbeController.LegacyResourceGated),
            enforceLegacyMarkers: true);
        context.HttpContext.Request.RouteValues["id"] = Guid.NewGuid().ToString();

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeNull("the owner-override mint satisfied the declarative resource gate");
    }

    [Fact]
    public async Task LegacyContentTypeMarker_IsInertWithoutTheCompatSwitch()
    {
        SetupDacResolution((_, _) => false);
        var context = CreateFilterContext(
            typeof(ProbeController).GetTypeInfo(),
            nameof(ProbeController.LegacyContentTypeGated),
            enforceLegacyMarkers: false);

        await CreateFilter().OnAuthorizationAsync(context);

        // Compat: without Controllers:EnforceLegacyPermissionMarkers the legacy markers
        // stay inert (their surfaces keep their own capability checks); the resolver is
        // never consulted for them.
        context.Result.Should().BeNull();
        _dacResolver.VerifyNoOtherCalls();
    }

    // ── Extensibility + conditional application ────────────────────────────

    [Fact]
    public async Task CustomDerivedAttribute_IsEnforcedThroughTheSamePath()
    {
        SetupDacResolution((query, permission) => query.Layer == PermissionLayer.Tenant && permission == "custom:gate");
        var context = CreateFilterContext(typeof(ProbeController).GetTypeInfo(), nameof(ProbeController.CustomGated));

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task ConditionalAttribute_DoesNotApplyWhenShouldApplyIsFalse()
    {
        SetupDacResolution((_, _) => false);
        var context = CreateFilterContext(
            typeof(ProbeController).GetTypeInfo(),
            nameof(ProbeController.ConditionallyGated));
        context.HttpContext.Request.Headers["X-Skip-Gate"] = "true";

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeNull("the conditional attribute did not contribute its requirement");
    }

    [Fact]
    public async Task ConditionalAttribute_AppliesByDefault()
    {
        SetupDacResolution((_, _) => false);
        var context = CreateFilterContext(
            typeof(ProbeController).GetTypeInfo(),
            nameof(ProbeController.ConditionallyGated));

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ObjectResult>("the fail-closed default applies the requirement");
    }

    // ── Precedence: controller-level and action-level requirements all apply ──

    [Fact]
    public async Task ControllerLevelAndActionLevelRequirements_AllApply_AndMostSpecificGoverns()
    {
        // The controller requires tenant:base; the action additionally requires tenant:action.
        // Granting only the controller-level permission must deny (action requirement
        // governs); granting both must pass.
        SetupDacResolution((query, permission) => permission == "tenant:base");
        var context = CreateFilterContext(
            typeof(PrecedenceProbeController).GetTypeInfo(),
            nameof(PrecedenceProbeController.Action));

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ObjectResult>(
            "the stricter conjunction (controller AND action) governs");

        SetupDacResolution((query, permission) => permission is "tenant:base" or "tenant:action");
        context = CreateFilterContext(
            typeof(PrecedenceProbeController).GetTypeInfo(),
            nameof(PrecedenceProbeController.Action));

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeNull("both requirements are satisfied");
    }

    // ── Fail-closed ────────────────────────────────────────────────────────

    [Fact]
    public async Task MissingDacResolver_DeniesFailClosed()
    {
        _actorAccessor.SetupGet(a => a.ActorContext).Returns(AuthenticatedActor());
        var services = new ServiceCollection();
        services.AddSingleton(_actorAccessor.Object);
        services.AddSingleton(_permissionQuery.Object);
        // IDacPermissionResolver deliberately NOT registered.
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        httpContext.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "gated"));
        var actionDescriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = typeof(ProbeController).GetTypeInfo(),
            MethodInfo = typeof(ProbeController).GetMethod(nameof(ProbeController.TenantGated))!,
            ControllerName = "Probe",
            ActionName = nameof(ProbeController.TenantGated)
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor, new ModelStateDictionary());
        var context = new AuthorizationFilterContext(actionContext, []);

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // ─── Probe controllers carrying the attribute family ───

    private sealed class ProbeController
    {
        [RequireDacPermission("content:read")]
        public void TenantGated()
        {
        }

        [RequireDacPermission("first", "second")]
        public void MultiPermissionGated()
        {
        }

        [RequireContentTypeDacPermission(typeof(CourseResource), "Read")]
        public void ContentTypeGated()
        {
        }

        [RequireResourceDacPermission(typeof(ProjectResource), "id", "Read")]
        public void ResourceGatedRoute()
        {
        }

        [RequireResourceDacPermission(typeof(ProjectResource), "X-Resource-Id", DacResourceIdSource.Header, "Read")]
        public void ResourceGatedHeader()
        {
        }

        [RequireContentTypePermissionAttribute<CourseResource>(ProbeResourcePermission.Read)]
        public void LegacyContentTypeGated()
        {
        }

        [RequireResourcePermissionAttribute<ProbeResourcePermission, ProjectResource>(ProbeResourcePermission.Read, "id")]
        public void LegacyResourceGated()
        {
        }

        [ConditionalDacPermission("custom:gate")]
        public void CustomGated()
        {
        }

        [ConditionalDacPermission("custom:gate")]
        public void ConditionallyGated()
        {
        }
    }

    [RequireDacPermission("tenant:base")]
    private sealed class PrecedenceProbeController
    {
        [RequireDacPermission("tenant:action")]
        public void Action()
        {
        }
    }

    private sealed class ConditionalDacPermission(string permission)
        : DacPermissionAttributeBase(PermissionLayer.Tenant, permission)
    {
        public override bool ShouldApply(HttpContext httpContext)
            => !httpContext.Request.Headers.TryGetValue("X-Skip-Gate", out var value)
               || value.FirstOrDefault() != "true";
    }

    private sealed class CourseResource
    {
    }

    private sealed class ProjectResource
    {
    }

    /// <summary>Probe permission enum for the legacy generic attribute tests.</summary>
    public enum ProbeResourcePermission
    {
        Read = 1,
        Write = 2
    }
}
