using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using GameGuild.API.Core.GraphQL;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.GraphQL;
using GameGuild.Identity.Context.Actors;
using HotChocolate;
using HotChocolate.Configuration;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.API.UnitTests.GraphQL;

/// <summary>
///     Computed-field authorization from related entity permissions (issue #335): fields
///     whose authorization is derived from a RELATED entity's resource permission — the
///     related entity identified by a parent property path (nested object or scalar id of
///     a different resource type) — plus the imperative helper for computed resolvers
///     (<see cref="GraphQLAuthorizationSupport.AuthorizeRelatedEntityAsync"/>).
/// </summary>
public sealed class GraphQLRelatedEntityAuthorizationTests
{
    private static readonly Guid UserId = Guid.Parse("4b50fdd6-2e85-42bb-a9fa-27f6cb7e97c6");
    private static readonly Guid TenantId = Guid.Parse("7b37d70c-6ecd-4eb2-9f21-c08fc9563e85");
    public static readonly Guid AuthorId = Guid.Parse("9a0f4a7c-1d3e-4f5a-8b2c-7d6e5f4a3b2c");
    public static readonly Guid CategoryId = Guid.Parse("5c1d8e2b-9f4a-4c3b-a1d0-2e3f4a5b6c7d");

    [Fact]
    public async Task RelatedEntity_NestedParentPathAuthorizesAgainstTheRelatedEntity()
    {
        // The authorBio field is authorized by the RELATED author entity's Read permission
        // (resource type User, id resolved from the nested parent path Author.Id), not by
        // the parent type's own permission.
        var engine = new GraphQLRelatedEntityFakePermissionResolver()
            .WithResourcePermission("User", AuthorId, PermissionType.Read);
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, "{ item { title authorBio } }");

        result.Json.RootElement.GetProperty("data").GetProperty("item")
            .GetProperty("authorBio").GetString().Should().Be("author-bio");
    }

    [Fact]
    public async Task RelatedEntity_NestedParentPathDeniesWithoutTheRelatedPermission()
    {
        var engine = new GraphQLRelatedEntityFakePermissionResolver();
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, "{ item { title authorBio } }");

        var item = result.Json.RootElement.GetProperty("data").GetProperty("item");
        item.GetProperty("authorBio").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("title").GetString().Should().Be("title");
    }

    [Fact]
    public async Task RelatedEntity_DifferentResourceTypeViaScalarParentProperty()
    {
        // categoryLabel is authorized by the related ProjectCategory entity's permission,
        // reached through the parent's scalar CategoryId property.
        var engine = new GraphQLRelatedEntityFakePermissionResolver()
            .WithResourcePermission("ProjectCategory", CategoryId, PermissionType.Read);
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, "{ item { title categoryLabel } }");

        result.Json.RootElement.GetProperty("data").GetProperty("item")
            .GetProperty("categoryLabel").GetString().Should().Be("category-label");
    }

    [Fact]
    public async Task RelatedEntity_ComputedFieldUsesTheImperativeRelatedEntityApi()
    {
        // canEditCategory is a computed field that resolves its authorization from the
        // related category entity through the imperative helper.
        var engine = new GraphQLRelatedEntityFakePermissionResolver()
            .WithResourcePermission("ProjectCategory", CategoryId, PermissionType.Edit);
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, "{ item { canEditCategory } }");

        result.Json.RootElement.GetProperty("data").GetProperty("item")
            .GetProperty("canEditCategory").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task RelatedEntity_ComputedFieldDeniesClosedWithoutThePermission()
    {
        var engine = new GraphQLRelatedEntityFakePermissionResolver();
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, "{ item { canEditCategory } }");

        result.Json.RootElement.GetProperty("data").GetProperty("item")
            .GetProperty("canEditCategory").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task RelatedEntity_ComputedFieldFailsClosedWhenEvaluationThrows()
    {
        var engine = new GraphQLRelatedEntityFakePermissionResolver
        {
            ErrorToThrow = new InvalidOperationException("sensitive store details"),
        };
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, "{ item { canEditCategory } }");

        result.Json.RootElement.GetProperty("data").GetProperty("item")
            .GetProperty("canEditCategory").GetBoolean().Should().BeFalse();
        result.Body.Should().NotContain("sensitive store details");
    }

    private static async Task<(string Body, JsonDocument Json)> ExecuteAsync(HttpClient client, string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = JsonContent.Create(new { query }),
        };
        request.Headers.Add("Authorization", "Bearer user");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return (body, JsonDocument.Parse(body));
    }

    private static async Task<WebApplication> CreateApplicationAsync(
        GraphQLRelatedEntityFakePermissionResolver engine)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("test")
            .AddScheme<AuthenticationSchemeOptions, GraphQLRelatedEntityTestAuthenticationHandler>("test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IEffectivePermissionResolver>(engine);
        builder.Services.AddSingleton<IActorContextAccessor>(new GraphQLRelatedEntityTestActorContextAccessor());
        builder.Services.TryAddSingleton<IPermissionEvaluationLogService>(
            new GraphQLRelatedEntityRecordingLogService());

        builder.Services.AddGraphQLServer()
            .AddQueryType<GraphQLRelatedEntityProbeQuery>()
            .AddTypeExtension<GraphQLRelatedEntityProbeResolvers>()
            .AddDirectiveType<GraphQLAuthorizeDirectiveType>()
            .ModifyOptions(options => options.FieldMiddleware = FieldMiddlewareApplication.AllFields)
            .DisableIntrospection(false)
            .AddErrorFilter<GraphQLAuthorizationErrorFilter>()
            .UseField<GraphQLAuthorizeFieldMiddleware>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGraphQL("/graphql").RequireAuthorization();
        await app.StartAsync();
        return app;
    }

    private sealed class GraphQLRelatedEntityTestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out _))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, UserId.ToString("D"))],
                Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class GraphQLRelatedEntityTestActorContextAccessor : IActorContextAccessor
    {
        private ActorContext _actorContext = ActorContext.Anonymous with
        {
            SubjectId = UserId.ToString("D"),
            TenantId = TenantId,
            Roles = new HashSet<string>(["User"], StringComparer.OrdinalIgnoreCase),
            IsAuthenticated = true,
        };

        public ActorContext ActorContext => _actorContext;

        public void SetActorContext(ActorContext context) => _actorContext = context;

        public void ClearActorContext() => _actorContext = ActorContext.Anonymous;

        public IActorContextScope BeginActorContextScope(ActorContext context)
        {
            var previous = _actorContext;
            SetActorContext(context);
            return new RestoreScope(this, previous);
        }

        private sealed class RestoreScope(GraphQLRelatedEntityTestActorContextAccessor accessor, ActorContext previous) : IActorContextScope
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                accessor.SetActorContext(previous);
            }
        }
    }

    /// <summary>
    ///     Records evaluation observations in memory so tests can assert that
    ///     related-entity computed-field decisions are logged durably by the framework.
    /// </summary>
    private sealed class GraphQLRelatedEntityRecordingLogService : IPermissionEvaluationLogService
    {
        public List<PermissionEvaluationRecord> Records { get; } = [];

        public Task<PermissionEvaluationLogResult> RecordAsync(
            PermissionEvaluationRecord record,
            CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.FromResult(new PermissionEvaluationLogResult(true, 1, 0));
        }
    }

    private sealed class GraphQLRelatedEntityFakePermissionResolver : IEffectivePermissionResolver
    {
        private readonly Dictionary<(string ResourceType, string ResourceId), HashSet<string>> _resourcePermissions = new();

        public Exception? ErrorToThrow { get; init; }

        public GraphQLRelatedEntityFakePermissionResolver WithResourcePermission(
            string resourceType,
            Guid resourceId,
            params PermissionType[] permissions)
        {
            var key = (resourceType, resourceId.ToString("D"));
            if (!_resourcePermissions.TryGetValue(key, out var granted))
            {
                granted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _resourcePermissions[key] = granted;
            }

            foreach (var permission in permissions)
            {
                granted.Add(permission.ToString());
            }

            return this;
        }

        public Task<EffectivePermissions> ResolveAsync(
            EffectivePermissionContext context,
            CancellationToken cancellationToken = default)
        {
            if (ErrorToThrow is { } exception)
            {
                return Task.FromException<EffectivePermissions>(exception);
            }

            var contextValid = context.IsValid;
            var effective = context.HasResource
                && _resourcePermissions.TryGetValue((context.ResourceType!, context.ResourceId!), out var granted)
                ? new HashSet<string>(granted, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(new EffectivePermissions
            {
                UserId = context.UserId,
                TenantId = context.TenantId,
                Permissions = contextValid ? effective : new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Sources = effective.ToDictionary(
                    permission => permission, _ => PermissionSource.DirectGrant),
                Context = context,
                ContextValid = contextValid,
            });
        }

        public Task<EffectivePermissions> ResolveAsync(
            Guid userId,
            Guid? tenantId,
            CancellationToken cancellationToken = default)
            => ResolveAsync(
                tenantId is { } tenant
                    ? EffectivePermissionContext.ForTenant(userId, tenant)
                    : new EffectivePermissionContext { UserId = userId, TenantId = Guid.Empty },
                cancellationToken);

        public async Task<bool> HasPermissionAsync(
            Guid userId,
            Guid? tenantId,
            string permission,
            CancellationToken cancellationToken = default)
            => (await ResolveAsync(userId, tenantId, cancellationToken).ConfigureAwait(false))
                .HasPermission(permission);

        public async Task<bool> HasAllPermissionsAsync(
            Guid userId,
            Guid? tenantId,
            IEnumerable<string> permissions,
            CancellationToken cancellationToken = default)
            => (await ResolveAsync(userId, tenantId, cancellationToken).ConfigureAwait(false))
                .HasAllPermissions(permissions);

        public async Task<bool> HasAnyPermissionAsync(
            Guid userId,
            Guid? tenantId,
            IEnumerable<string> permissions,
            CancellationToken cancellationToken = default)
            => (await ResolveAsync(userId, tenantId, cancellationToken).ConfigureAwait(false))
                .HasAnyPermission(permissions);
    }
}

/// <summary>Related-author projection used by the related-entity tests.</summary>
public sealed class GraphQLRelatedEntityAuthor
{
    public Guid Id { get; set; }
}

/// <summary>Parent entity carrying related-entity references for the tests.</summary>
public sealed class GraphQLRelatedEntityItem
{
    public string Title { get; set; } = "title";

    public GraphQLRelatedEntityAuthor Author { get; set; } = new() { Id = GraphQLRelatedEntityAuthorizationTests.AuthorId };

    public Guid CategoryId { get; set; } = GraphQLRelatedEntityAuthorizationTests.CategoryId;
}

/// <summary>Probe query returning the parent entity.</summary>
public class GraphQLRelatedEntityProbeQuery
{
    public GraphQLRelatedEntityItem Item() => new();
}

/// <summary>
///     Computed fields authorized by RELATED entity permissions: directive-bound fields
///     (nested path and scalar path, different resource type than the parent) and an
///     imperative computed field using the framework helper.
/// </summary>
[ExtendObjectType(nameof(GraphQLRelatedEntityItem))]
public sealed class GraphQLRelatedEntityProbeResolvers
{
    /// <summary>Authorized by the related author entity's Read permission (nested path).</summary>
    [GraphQLAuthorize(
        PermissionType.Read,
        ResourceType = "User",
        ResourceIdParentPropertyName = "Author.Id")]
    public string? AuthorBio() => "author-bio";

    /// <summary>Authorized by the related category entity's Read permission (scalar path).</summary>
    [GraphQLAuthorize(
        PermissionType.Read,
        ResourceType = "ProjectCategory",
        ResourceIdParentPropertyName = "CategoryId")]
    public string? CategoryLabel() => "category-label";

    /// <summary>
    ///     Computed field that resolves its authorization from the related category
    ///     entity through the imperative helper (allow/deny, fail-closed).
    /// </summary>
    public async Task<bool> CanEditCategoryAsync(
        [Parent] GraphQLRelatedEntityItem item,
        IResolverContext resolverContext)
    {
        var outcome = await GraphQLAuthorizationSupport.AuthorizeRelatedEntityAsync(
            resolverContext,
            "ProjectCategory",
            item.CategoryId,
            [PermissionType.Edit]).ConfigureAwait(false);
        return outcome.Allowed;
    }
}
