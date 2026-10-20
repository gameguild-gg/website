using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using GameGuild.API.Core.GraphQL;
using GameGuild.Configuration.PresentationLayer.GraphQL;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.GraphQL;
using GameGuild.Identity.Context.Actors;
using HotChocolate;
using HotChocolate.Configuration;
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
///     Permission-level schema versioning (issue #335): the <c>schemaVersion</c> query
///     returns a deterministic revision of the caller's effective schema surface, the
///     visibility tier and the introspection eligibility. HotChocolate compiles one static
///     schema per process, so the revision is the honest contract: same permission level,
///     same revision; a different level (or a changed protected surface) yields a
///     different revision. The response never exposes a field inventory.
/// </summary>
public sealed class GraphQLSchemaVersionTests
{
    private static readonly Guid UserId = Guid.Parse("4b50fdd6-2e85-42bb-a9fa-27f6cb7e97c6");
    private static readonly Guid TenantId = Guid.Parse("7b37d70c-6ecd-4eb2-9f21-c08fc9563e85");

    private const string VersionQuery =
        "{ schemaVersion { revision visibilityTier introspectionAllowed } }";

    [Fact]
    public async Task SchemaVersion_CallersOnTheSamePermissionLevelShareTheRevision()
    {
        var engine = new GraphQLVersionFakePermissionResolver()
            .WithTenantPermissions(PermissionType.Read);
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var first = await ExecuteAsync(client, VersionQuery);
        var second = await ExecuteAsync(client, VersionQuery);

        first.Revision.Should().Be(second.Revision);
        first.Revision.Should().MatchRegex("^[0-9A-F]{16}$");
        first.VisibilityTier.Should().Be(nameof(GraphQLSchemaVisibilityTier.Full));
        first.IntrospectionAllowed.Should().Be(true);
    }

    [Fact]
    public async Task SchemaVersion_DifferentPermissionLevelsProduceDifferentRevisions()
    {
        var fullEngine = new GraphQLVersionFakePermissionResolver()
            .WithTenantPermissions(PermissionType.Read, PermissionType.SystemAdmin);
        await using var fullApp = await CreateApplicationAsync(fullEngine);
        using var fullClient = fullApp.GetTestClient();

        var restrictedEngine = new GraphQLVersionFakePermissionResolver();
        await using var restrictedApp = await CreateApplicationAsync(restrictedEngine);
        using var restrictedClient = restrictedApp.GetTestClient();

        var full = await ExecuteAsync(fullClient, VersionQuery);
        var restricted = await ExecuteAsync(restrictedClient, VersionQuery);

        full.VisibilityTier.Should().Be(nameof(GraphQLSchemaVisibilityTier.Full));
        full.IntrospectionAllowed.Should().Be(true);
        restricted.VisibilityTier.Should().Be(nameof(GraphQLSchemaVisibilityTier.Restricted));
        restricted.IntrospectionAllowed.Should().Be(false);
        full.Revision.Should().NotBe(restricted.Revision);
    }

    [Fact]
    public async Task SchemaVersion_RevisionsChangeWhenTheProtectedSurfaceChanges()
    {
        var engine = new GraphQLVersionFakePermissionResolver();
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        await using var otherApp = await CreateApplicationAsync(engine, addExtraProtectedField: true);
        using var otherClient = otherApp.GetTestClient();

        var original = await ExecuteAsync(client, VersionQuery);
        var changed = await ExecuteAsync(otherClient, VersionQuery);

        original.Revision.Should().NotBe(changed.Revision);
        original.VisibilityTier.Should().Be(changed.VisibilityTier);
    }

    [Fact]
    public async Task SchemaVersion_DoesNotExposeTheProtectedFieldInventory()
    {
        var engine = new GraphQLVersionFakePermissionResolver();
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, VersionQuery);

        result.Body.Should().NotContain("protectedData");
        result.Body.Should().NotContain("extraProtected");
    }

    private sealed record VersionResult(string Revision, string VisibilityTier, bool IntrospectionAllowed, string Body);

    private static async Task<VersionResult> ExecuteAsync(HttpClient client, string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = JsonContent.Create(new { query }),
        };
        request.Headers.Add("Authorization", "Bearer user");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.TryGetProperty("errors", out _).Should().BeFalse(body);
        var version = document.RootElement.GetProperty("data").GetProperty("schemaVersion");
        return new VersionResult(
            version.GetProperty("revision").GetString()!,
            version.GetProperty("visibilityTier").GetString()!,
            version.GetProperty("introspectionAllowed").GetBoolean(),
            body);
    }

    private static async Task<WebApplication> CreateApplicationAsync(
        GraphQLVersionFakePermissionResolver engine,
        bool addExtraProtectedField = false)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("test")
            .AddScheme<AuthenticationSchemeOptions, GraphQLVersionTestAuthenticationHandler>("test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IEffectivePermissionResolver>(engine);
        builder.Services.AddSingleton<IActorContextAccessor>(new GraphQLVersionTestActorContextAccessor());

        var graphQlOptions = new GraphQLOptions
        {
            EnableGraphQL = true,
            SchemaAuthorization = new GraphQLSchemaAuthorizationOptions(),
        };
        builder.Services.AddSingleton(graphQlOptions);
        builder.Services.TryAddScoped<GraphQLSchemaVisibilityEvaluator>();
        builder.Services.TryAddScoped<GraphQLSchemaVersionEvaluator>();

        var server = builder.Services.AddGraphQLServer();
        if (addExtraProtectedField)
        {
            server = server.AddQueryType<GraphQLVersionProbeWithExtraProtectedField>();
        }
        else
        {
            server = server.AddQueryType<GraphQLVersionProbe>();
        }

        server
            .AddTypeExtension<GraphQLSchemaVersionQueries>()
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

    private sealed class GraphQLVersionTestAuthenticationHandler(
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

    private sealed class GraphQLVersionTestActorContextAccessor : IActorContextAccessor
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

        private sealed class RestoreScope(GraphQLVersionTestActorContextAccessor accessor, ActorContext previous) : IActorContextScope
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

    private sealed class GraphQLVersionFakePermissionResolver : IEffectivePermissionResolver
    {
        private readonly HashSet<string> _tenantPermissions = new(StringComparer.OrdinalIgnoreCase);

        public GraphQLVersionFakePermissionResolver WithTenantPermissions(params PermissionType[] permissions)
        {
            foreach (var permission in permissions)
            {
                _tenantPermissions.Add(permission.ToString());
            }

            return this;
        }

        public Task<EffectivePermissions> ResolveAsync(
            EffectivePermissionContext context,
            CancellationToken cancellationToken = default)
        {
            var contextValid = context.IsValid;
            return Task.FromResult(new EffectivePermissions
            {
                UserId = context.UserId,
                TenantId = context.TenantId,
                Permissions = contextValid ? _tenantPermissions : new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                Sources = _tenantPermissions.ToDictionary(
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

/// <summary>Probe schema for the schema-version tests.</summary>
public class GraphQLVersionProbe
{
    public string PublicData() => "public";

    [GraphQLAuthorize(PermissionType.Read)]
    public string ProtectedData() => "protected";
}

/// <summary>Probe schema variant with one extra protected field.</summary>
public sealed class GraphQLVersionProbeWithExtraProtectedField : GraphQLVersionProbe
{
    [GraphQLAuthorize(PermissionType.SystemAdmin)]
    public string ExtraProtected() => "extra";
}
