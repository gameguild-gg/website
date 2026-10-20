using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Identity.Authorization.Models;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Query-operations permission tests (issue #375): the List/Search/Query contract
///     (enum members, typed keys, registry/facade exposure, fail-closed persistence
///     round-trip) and its enforcement through the declarative DAC machinery —
///     grant/deny per permission, user/tenant context isolation (tenant from the actor
///     context, never from the route), content-type and resource resolution contexts,
///     the SystemAdmin bypass, and denied-response hygiene (no resource identifiers or
///     cross-tenant data leak through the 403 body).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Security", "Authorization")]
public sealed class QueryOperationsPermissionTests
{
    // ── Contract: enum members, keys, registry, facade ─────────────────────

    [Fact]
    public void PermissionType_QueryOperationMembers_HaveDistinctContractValues()
    {
        ((int)PermissionType.List).Should().Be(215);
        ((int)PermissionType.Search).Should().Be(216);
        ((int)PermissionType.Query).Should().Be(217);

        // Distinct from each other and from single-resource Read (= 1): the query
        // operations are separate capabilities, not Read aliases.
        PermissionType.List.Should().NotBe(PermissionType.Search)
            .And.NotBe(PermissionType.Query)
            .And.NotBe(PermissionType.Read);
        PermissionType.Search.Should().NotBe(PermissionType.Query);
    }

    [Fact]
    public void ContentTypePermissionGrant_ParsesQueryOperationNamesAndNumbers_FailClosed()
    {
        // Grants persist string payloads parsed by name or number; the additive enum
        // members round-trip through the fail-closed mapping layer.
        var grant = new ContentTypePermissionGrant
        {
            ContentTypeName = "SavedAuditQuery",
            Permissions = ["List", "Search", "217", "NotARealPermission"]
        };

        var parsed = grant.GetGrantedPermissionTypes();

        parsed.Should().BeEquivalentTo(
        [
            PermissionType.List,
            PermissionType.Search,
            PermissionType.Query
        ]);
    }

    [Fact]
    public void QueryPermission_Keys_AreExposedThroughTheRegistry()
    {
        PermissionRegistry.IsValidKey(QueryPermission.Keys.List).Should().BeTrue();
        PermissionRegistry.IsValidKey(QueryPermission.Keys.Search).Should().BeTrue();
        PermissionRegistry.IsValidKey(QueryPermission.Keys.Query).Should().BeTrue();

        var list = PermissionRegistry.GetByKey(QueryPermission.Keys.List);
        list.Should().NotBeNull();
        list!.Resource.Should().Be("queries");
        list.Action.Should().Be("list");

        // The typed instances describe the operation classes they govern.
        QueryPermission.List.Description.Should().NotBeNullOrWhiteSpace();
        QueryPermission.Search.Description.Should().NotBeNullOrWhiteSpace();
        QueryPermission.Query.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Permissions_Facade_ExposesQueryOperationKeys()
    {
        Permissions.QueriesList.Should().Be("queries:list");
        Permissions.QueriesSearch.Should().Be("queries:search");
        Permissions.QueriesQuery.Should().Be("queries:query");
    }

    // ── Filter enforcement harness ─────────────────────────────────────────

    private readonly Mock<IActorContextAccessor> _actorAccessor = new();
    private readonly Mock<IPermissionQueryService> _permissionQuery = new();
    private readonly Mock<IDacPermissionResolver> _dacResolver = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private ResourcePermissionAuthorizationFilter CreateFilter()
        => new(NullLogger<ResourcePermissionAuthorizationFilter>.Instance);

    private ActorContext AuthenticatedActor(bool systemAdmin = false)
        => new()
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = _userId.ToString(),
            TenantId = _tenantId,
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>(systemAdmin ? ["SystemAdmin"] : [])
        };

    private AuthorizationFilterContext CreateFilterContext(
        string actionName,
        ActorContext? actor = null,
        Action<HttpContext>? configureHttpContext = null)
    {
        _actorAccessor.SetupGet(a => a.ActorContext).Returns(actor ?? AuthenticatedActor());

        var services = new ServiceCollection();
        services.AddSingleton(_actorAccessor.Object);
        services.AddSingleton(_permissionQuery.Object);
        services.AddSingleton(_dacResolver.Object);
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(
            new GameGuild.Configuration.PresentationLayer.Controllers.ControllersOptions()));
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        configureHttpContext?.Invoke(httpContext);
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(),
            "gated-action"));

        var controllerType = typeof(QueryOperationsProbeController).GetTypeInfo();
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

    // ── Grant/deny per new permission ──────────────────────────────────────

    [Theory]
    [InlineData(nameof(QueryOperationsProbeController.ExecuteStructuredQuery), QueryPermission.Keys.Query)]
    [InlineData(nameof(QueryOperationsProbeController.SearchByText), QueryPermission.Keys.Search)]
    [InlineData("ListTemplates", QueryPermission.Keys.List)]
    public async Task RequireDacPermission_GrantedQueryOperationKey_Passes(string actionName, string requiredKey)
    {
        SetupDacResolution((query, permission) =>
            query.Layer == PermissionLayer.Tenant && permission == requiredKey);
        var context = CreateFilterContext(actionName);

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeNull($"{requiredKey} was granted at the tenant layer");
    }

    [Theory]
    [InlineData(nameof(QueryOperationsProbeController.ExecuteStructuredQuery), QueryPermission.Keys.Query)]
    [InlineData(nameof(QueryOperationsProbeController.SearchByText), QueryPermission.Keys.Search)]
    [InlineData("ListTemplates", QueryPermission.Keys.List)]
    public async Task RequireDacPermission_UngrantedQueryOperationKey_DeniesFailClosed(string actionName, string requiredKey)
    {
        // Everything except the required key is granted: the exact operation key must
        // still decide (no cross-satisfaction between List/Search/Query).
        SetupDacResolution((query, permission) =>
            query.Layer == PermissionLayer.Tenant &&
            new[] { QueryPermission.Keys.List, QueryPermission.Keys.Search, QueryPermission.Keys.Query }
                .Except([requiredKey])
                .Contains(permission));
        var context = CreateFilterContext(actionName);

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task RequireDacPermission_ReadGrantsDoNotSatisfyQueryRequirements()
    {
        // Relationship to Read (issue #375): Read-family grants never satisfy a
        // List/Search/Query requirement.
        SetupDacResolution((query, permission) =>
            permission is "content:read" or "audit:read" or "users:read" or "Read");
        var context = CreateFilterContext(nameof(QueryOperationsProbeController.ExecuteStructuredQuery));

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // ── Context isolation: user and tenant from the actor context ──────────

    [Fact]
    public async Task RequireDacPermission_ResolvesAgainstActorTenant_NeverTheRouteTenant()
    {
        var routeTenantId = Guid.NewGuid();
        DacPermissionQuery? seen = null;
        _dacResolver
            .Setup(r => r.HasPermissionAsync(It.IsAny<DacPermissionQuery>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<DacPermissionQuery, string, CancellationToken>((query, _, _) => seen = query)
            .ReturnsAsync(true);
        var context = CreateFilterContext(
            nameof(QueryOperationsProbeController.ListSavedViews),
            configureHttpContext: http =>
            {
                // A foreign tenant id in the route must not steer the permission check.
                http.Request.RouteValues["tenantId"] = routeTenantId.ToString();
            });

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        seen.Should().NotBeNull();
        seen!.UserId.Should().Be(_userId, "the acting user identity comes from the actor context");
        seen.TenantId.Should().Be(_tenantId, "the tenant comes from the request (actor) context, not the route");
        seen.TenantId.Should().NotBe(routeTenantId);
    }

    [Fact]
    public async Task RequireDacPermission_ActorWithoutTenantContext_DeniesFailClosed()
    {
        var tenantlessActor = new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = _userId.ToString(),
            TenantId = null,
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>()
        };
        SetupDacResolution((_, _) => true);
        var context = CreateFilterContext(
            nameof(QueryOperationsProbeController.ExecuteStructuredQuery),
            actor: tenantlessActor);

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _dacResolver.VerifyNoOtherCalls();
    }

    // ── Denied-response hygiene: no leakage through the 403 body ───────────

    [Fact]
    public async Task RequireDacPermission_DeniedQueryOperation_ResponseLeaksNoResourceIdentifiers()
    {
        var routeTenantId = Guid.NewGuid();
        var routeResourceId = Guid.NewGuid();
        SetupDacResolution((_, _) => false);
        var context = CreateFilterContext(
            nameof(QueryOperationsProbeController.ListSavedViews),
            configureHttpContext: http =>
            {
                http.Request.RouteValues["tenantId"] = routeTenantId.ToString();
                http.Request.RouteValues["id"] = routeResourceId.ToString();
            });

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var problem = ((ObjectResult)context.Result!).Value.Should().BeOfType<ProblemDetails>().Which;
        problem.Extensions.Should().ContainKey("reason");
        problem.Extensions.Should().NotContainKey("requiredPermissions",
            "production denials never name the missing permission set");

        var serialized = JsonSerializer.Serialize(problem);
        serialized.Should().NotContain(routeTenantId.ToString());
        serialized.Should().NotContain(routeResourceId.ToString());
        serialized.Should().NotContain(_userId.ToString());
        serialized.Should().NotContain(_tenantId.ToString());
    }

    // ── Content-type and resource resolution contexts ──────────────────────

    [Fact]
    public async Task RequireContentTypeDacPermission_QueryOperation_ResolvesThroughContentTypeLayer()
    {
        DacPermissionQuery? seen = null;
        _dacResolver
            .Setup(r => r.HasPermissionAsync(It.IsAny<DacPermissionQuery>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<DacPermissionQuery, string, CancellationToken>((query, _, _) => seen = query)
            .ReturnsAsync(true);
        var context = CreateFilterContext(nameof(QueryOperationsProbeController.ContentTypeQueryCatalog));

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        seen.Should().NotBeNull();
        seen!.Layer.Should().Be(PermissionLayer.ContentType);
        seen.ContentType.Should().Be("SavedAuditQueryResource");
    }

    [Fact]
    public async Task RequireResourceDacPermission_QueryOperation_ResolvesThroughResourceContext()
    {
        var resourceId = Guid.NewGuid();
        DacPermissionQuery? seen = null;
        _dacResolver
            .Setup(r => r.HasPermissionAsync(It.IsAny<DacPermissionQuery>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<DacPermissionQuery, string, CancellationToken>((query, _, _) => seen = query)
            .ReturnsAsync(true);
        var context = CreateFilterContext(
            nameof(QueryOperationsProbeController.ExecuteSavedViewById),
            configureHttpContext: http => http.Request.RouteValues["id"] = resourceId.ToString());

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        seen.Should().NotBeNull();
        seen!.Layer.Should().Be(PermissionLayer.Resource);
        seen.ResourceType.Should().Be("SavedAuditQueryResource");
        seen.ResourceId.Should().Be(resourceId.ToString("D"));
        seen.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task RequireResourceDacPermission_MissingResourceId_DeniesFailClosed()
    {
        SetupDacResolution((_, _) => true);
        var context = CreateFilterContext(nameof(QueryOperationsProbeController.ExecuteSavedById));

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // ── SystemAdmin bypass ─────────────────────────────────────────────────

    [Fact]
    public async Task RequireDacPermission_SystemAdminActor_BypassesTheQueryGate()
    {
        var context = CreateFilterContext(
            nameof(QueryOperationsProbeController.ExecuteStructuredQuery),
            actor: AuthenticatedActor(systemAdmin: true));

        await CreateFilter().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        _dacResolver.VerifyNoOtherCalls();
    }

    // ── Resolver layer semantics for the new permission names ──────────────

    [Fact]
    public async Task DacPermissionResolver_QueryOperationNames_UseLayerQualifiedConventions()
    {
        var resourceId = Guid.NewGuid();
        var effective = new EffectivePermissions
        {
            UserId = _userId,
            TenantId = _tenantId,
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                QueryPermission.Keys.List,                              // tenant layer (unqualified colon key)
                "SavedAuditQueryResource.Search",                        // content-type layer mint
                $"SavedAuditQueryResource.{resourceId:D}.Query"         // resource layer mint
            },
            Sources = new Dictionary<string, PermissionSource>(StringComparer.OrdinalIgnoreCase)
        };
        var effectiveResolver = new Mock<IEffectivePermissionResolver>();
        effectiveResolver
            .Setup(r => r.ResolveAsync(It.IsAny<EffectivePermissionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(effective);
        var resolver = new DacPermissionResolver(
            effectiveResolver.Object,
            NullLogger<DacPermissionResolver>.Instance);

        // Tenant layer: the unqualified colon key satisfies the check...
        (await resolver.HasPermissionAsync(new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Tenant
        }, QueryPermission.Keys.List)).Should().BeTrue();

        // ...while List does not satisfy Search or Query (independent keys).
        (await resolver.HasPermissionAsync(new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Tenant
        }, QueryPermission.Keys.Search)).Should().BeFalse();

        // Content-type layer: only the {ContentType}.{Permission} mint satisfies.
        (await resolver.HasPermissionAsync(new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.ContentType,
            ContentType = "SavedAuditQueryResource"
        }, "Search")).Should().BeTrue();
        (await resolver.HasPermissionAsync(new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.ContentType,
            ContentType = "SavedAuditQueryResource"
        }, "List")).Should().BeFalse();

        // Resource layer: only the exact resource's qualified mint satisfies — grants
        // for one resource never leak into another resource's query operations.
        (await resolver.HasPermissionAsync(new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Resource,
            ResourceType = "SavedAuditQueryResource",
            ResourceId = resourceId.ToString("D")
        }, "Query")).Should().BeTrue();
        (await resolver.HasPermissionAsync(new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Resource,
            ResourceType = "SavedAuditQueryResource",
            ResourceId = Guid.NewGuid().ToString("D")
        }, "Query")).Should().BeFalse();
    }

    // ── Probe controller mirroring the gated query surfaces ────────────────

    private sealed class QueryOperationsProbeController
    {
        [RequireDacPermission(QueryPermission.Keys.Query)]
        public void ExecuteStructuredQuery()
        {
        }

        [RequireDacPermission(QueryPermission.Keys.Search)]
        public void SearchByText()
        {
        }

        [RequireDacPermission(QueryPermission.Keys.List)]
        public void ListTemplates()
        {
        }

        [RequireDacPermission(QueryPermission.Keys.List)]
        public void ListSavedViews()
        {
        }

        [RequireContentTypeDacPermission(typeof(SavedAuditQueryResource), "Query")]
        public void ContentTypeQueryCatalog()
        {
        }

        [RequireResourceDacPermission(typeof(SavedAuditQueryResource), "id", "Query")]
        public void ExecuteSavedViewById()
        {
        }

        [RequireResourceDacPermission(typeof(SavedAuditQueryResource), "id", "Query")]
        public void ExecuteSavedById()
        {
        }
    }

    private sealed class SavedAuditQueryResource;
}
