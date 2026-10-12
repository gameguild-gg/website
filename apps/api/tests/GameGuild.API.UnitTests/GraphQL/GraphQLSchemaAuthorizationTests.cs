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
///     Schema filtering and introspection control for the GraphQL authorization
///     framework (issue #335): permission-aware schema visibility (callers that cannot
///     see every protected field are denied introspection instead of receiving a partial
///     schema, fail-closed) and the config-gated Production restriction to schema admins.
/// </summary>
public sealed class GraphQLSchemaAuthorizationTests
{
    private static readonly Guid UserId = Guid.Parse("4b50fdd6-2e85-42bb-a9fa-27f6cb7e97c6");
    private static readonly Guid TenantId = Guid.Parse("7b37d70c-6ecd-4eb2-9f21-c08fc9563e85");

    private const string IntrospectionQuery = "{ __schema { queryType { fields { name } } } }";

    [Fact]
    public async Task Introspection_IsAllowedForCallersThatCanSeeEveryProtectedField()
    {
        var engine = new GraphQLSchemaFakePermissionResolver()
            .WithTenantPermissions(PermissionType.Read);
        await using var app = await CreateApplicationAsync(engine, environment: "Development");
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest(IntrospectionQuery));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        var fieldNames = document.RootElement.GetProperty("data").GetProperty("__schema")
            .GetProperty("queryType").GetProperty("fields").EnumerateArray()
            .Select(field => field.GetProperty("name").GetString())
            .ToList();
        fieldNames.Should().Contain("publicData");
        fieldNames.Should().Contain("protectedData", "the caller can see every protected field");
    }

    [Fact]
    public async Task Introspection_IsDeniedForCallersLackingTheProtectedFieldPermission()
    {
        var engine = new GraphQLSchemaFakePermissionResolver();
        await using var app = await CreateApplicationAsync(engine, environment: "Development");
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest(IntrospectionQuery));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var data = document.RootElement.GetProperty("data");
        (data.ValueKind == JsonValueKind.Null
            || data.GetProperty("__schema").ValueKind == JsonValueKind.Null).Should().BeTrue();
        var error = document.RootElement.GetProperty("errors").EnumerateArray().Single();
        error.GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("AUTHORIZATION_DENIED");
        // No partial schema is served and protected field names never leak.
        body.Should().NotContain("protectedData");
    }

    [Fact]
    public async Task Introspection_NormalQueriesStillWorkForRestrictedCallers()
    {
        var engine = new GraphQLSchemaFakePermissionResolver()
            .WithTenantPermissions(PermissionType.Read);
        await using var app = await CreateApplicationAsync(engine, environment: "Development");
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest("{ publicData __typename }"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("data").GetProperty("publicData")
            .GetString().Should().Be("public");
        document.RootElement.GetProperty("data").GetProperty("__typename")
            .GetString().Should().Be(nameof(GraphQLSchemaProbe));
    }

    [Fact]
    public async Task Introspection_InProductionIsRestrictedToSchemaAdmins()
    {
        var engine = new GraphQLSchemaFakePermissionResolver()
            .WithTenantPermissions(PermissionType.Read);
        await using var app = await CreateApplicationAsync(engine, environment: "Production");
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest(IntrospectionQuery));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var data = document.RootElement.GetProperty("data");
        (data.ValueKind == JsonValueKind.Null
            || data.GetProperty("__schema").ValueKind == JsonValueKind.Null).Should().BeTrue();
        document.RootElement.GetProperty("errors").EnumerateArray().Single()
            .GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("AUTHORIZATION_DENIED");
        body.Should().NotContain("protectedData");
    }

    [Fact]
    public async Task Introspection_InProductionIsAllowedForSchemaAdmins()
    {
        var engine = new GraphQLSchemaFakePermissionResolver()
            .WithTenantPermissions(PermissionType.Read, PermissionType.SystemAdmin);
        await using var app = await CreateApplicationAsync(engine, environment: "Production");
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest(IntrospectionQuery));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        document.RootElement.GetProperty("data").GetProperty("__schema").GetProperty("queryType")
            .GetProperty("fields").EnumerateArray()
            .Select(field => field.GetProperty("name").GetString())
            .Should().Contain("protectedData");
    }

    [Fact]
    public async Task Introspection_FailsClosedForResourceScopedProtectedFields()
    {
        // A resource-scoped rule cannot be proven visible without a concrete resource:
        // non-admin callers are denied introspection (fail-closed).
        var engine = new GraphQLSchemaFakePermissionResolver()
            .WithTenantPermissions(PermissionType.Read);
        await using var app = await CreateApplicationAsync(
            engine,
            environment: "Development",
            addResourceScopedField: true);
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest(IntrospectionQuery));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var data = document.RootElement.GetProperty("data");
        (data.ValueKind == JsonValueKind.Null
            || data.GetProperty("__schema").ValueKind == JsonValueKind.Null).Should().BeTrue();
        document.RootElement.GetProperty("errors").EnumerateArray().Single()
            .GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("AUTHORIZATION_DENIED");
    }

    [Fact]
    public async Task Introspection_SchemaAdminsBypassResourceScopedVisibilityFailClosed()
    {
        var engine = new GraphQLSchemaFakePermissionResolver()
            .WithTenantPermissions(PermissionType.Read, PermissionType.SystemAdmin);
        await using var app = await CreateApplicationAsync(
            engine,
            environment: "Development",
            addResourceScopedField: true);
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest(IntrospectionQuery));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        document.RootElement.GetProperty("data").GetProperty("__schema").GetProperty("queryType")
            .GetProperty("fields").EnumerateArray()
            .Select(field => field.GetProperty("name").GetString())
            .Should().Contain("resourceScoped");
    }

    [Fact]
    public async Task Introspection_FailsClosedWhenVisibilityEvaluationThrows()
    {
        var engine = new GraphQLSchemaFakePermissionResolver
        {
            ErrorToThrow = new InvalidOperationException("sensitive store details"),
        };
        await using var app = await CreateApplicationAsync(engine, environment: "Development");
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest(IntrospectionQuery));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var data = document.RootElement.GetProperty("data");
        (data.ValueKind == JsonValueKind.Null
            || data.GetProperty("__schema").ValueKind == JsonValueKind.Null).Should().BeTrue();
        document.RootElement.GetProperty("errors").EnumerateArray().Single()
            .GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("AUTHORIZATION_DENIED");
        body.Should().NotContain("sensitive store details");
    }

    [Fact]
    public async Task Introspection_IsAllowedForEveryoneWhenFilteringIsDisabled()
    {
        var engine = new GraphQLSchemaFakePermissionResolver();
        await using var app = await CreateApplicationAsync(
            engine,
            environment: "Development",
            schemaAuthorization: new GraphQLSchemaAuthorizationOptions { FilterSchema = false });
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest(IntrospectionQuery));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        document.RootElement.GetProperty("data").GetProperty("__schema").GetProperty("queryType")
            .GetProperty("fields").EnumerateArray()
            .Select(field => field.GetProperty("name").GetString())
            .Should().Contain("protectedData");
    }

    [Fact]
    public async Task Introspection_ProductionRestrictionCanBeDisabledByConfiguration()
    {
        var engine = new GraphQLSchemaFakePermissionResolver()
            .WithTenantPermissions(PermissionType.Read);
        await using var app = await CreateApplicationAsync(
            engine,
            environment: "Production",
            schemaAuthorization: new GraphQLSchemaAuthorizationOptions
            {
                RestrictIntrospectionToSchemaAdminsInProduction = false,
            });
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest(IntrospectionQuery));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        document.RootElement.GetProperty("data").GetProperty("__schema").GetProperty("queryType")
            .GetProperty("fields").EnumerateArray()
            .Select(field => field.GetProperty("name").GetString())
            .Should().Contain("protectedData");
    }

    [Fact]
    public async Task Introspection_ByTypeIsDeniedUnderTheSameRules()
    {
        var engine = new GraphQLSchemaFakePermissionResolver();
        await using var app = await CreateApplicationAsync(engine, environment: "Development");
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(
            CreateRequest("{ __type(name: \"Query\") { fields { name } } }"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var data = document.RootElement.GetProperty("data");
        (data.ValueKind == JsonValueKind.Null
            || data.GetProperty("__type").ValueKind == JsonValueKind.Null).Should().BeTrue();
        document.RootElement.GetProperty("errors").EnumerateArray().Single()
            .GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("AUTHORIZATION_DENIED");
        body.Should().NotContain("protectedData");
    }

    [Fact]
    public void SchemaAuthorizationOptions_RejectBlankSchemaAdminPermission()
    {
        var options = new GraphQLSchemaAuthorizationOptions { SchemaAdminPermission = " " };
        var act = options.Validate;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GraphQLOptions_ValidatesSchemaAuthorizationSection()
    {
        var options = new GraphQLOptions
        {
            EnableGraphQL = true,
            SchemaAuthorization = new GraphQLSchemaAuthorizationOptions { SchemaAdminPermission = "" },
        };
        var act = options.Validate;
        act.Should().Throw<InvalidOperationException>();
    }

    private static HttpRequestMessage CreateRequest(string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = JsonContent.Create(new { query }),
        };
        request.Headers.Add("Authorization", "Bearer user");
        return request;
    }

    private static async Task<WebApplication> CreateApplicationAsync(
        GraphQLSchemaFakePermissionResolver engine,
        string environment,
        bool addResourceScopedField = false,
        GraphQLSchemaAuthorizationOptions? schemaAuthorization = null)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = environment });
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("test")
            .AddScheme<AuthenticationSchemeOptions, GraphQLSchemaTestAuthenticationHandler>("test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IEffectivePermissionResolver>(engine);
        builder.Services.AddSingleton<IActorContextAccessor>(new GraphQLSchemaTestActorContextAccessor());

        var graphQlOptions = new GraphQLOptions
        {
            EnableGraphQL = true,
            SchemaAuthorization = schemaAuthorization ?? new GraphQLSchemaAuthorizationOptions(),
        };
        builder.Services.AddSingleton(graphQlOptions);
        builder.Services.TryAddScoped<GraphQLSchemaVisibilityEvaluator>();

        if (addResourceScopedField)
        {
            builder.Services.AddGraphQLServer()
                .AddQueryType<GraphQLSchemaProbeWithResourceScopedField>()
                .AddDirectiveType<GraphQLAuthorizeDirectiveType>()
                .ModifyOptions(options => options.FieldMiddleware = FieldMiddlewareApplication.AllFields)
                .DisableIntrospection(false)
                .UseField<GraphQLAuthorizeFieldMiddleware>();
        }
        else
        {
            builder.Services.AddGraphQLServer()
                .AddQueryType<GraphQLSchemaProbe>()
                .AddDirectiveType<GraphQLAuthorizeDirectiveType>()
                .ModifyOptions(options => options.FieldMiddleware = FieldMiddlewareApplication.AllFields)
                .DisableIntrospection(false)
                .UseField<GraphQLAuthorizeFieldMiddleware>();
        }

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGraphQL("/graphql").RequireAuthorization();
        await app.StartAsync();
        return app;
    }

    private sealed class GraphQLSchemaTestAuthenticationHandler(
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

    private sealed class GraphQLSchemaTestActorContextAccessor : IActorContextAccessor
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
            ArgumentNullException.ThrowIfNull(context);
            var previous = _actorContext;
            _actorContext = context;
            return new RestoreScope(this, previous);
        }

        private sealed class RestoreScope(GraphQLSchemaTestActorContextAccessor owner, ActorContext previous) : IActorContextScope
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                owner.SetActorContext(previous);
            }
        }
    }

    private sealed class GraphQLSchemaFakePermissionResolver : IEffectivePermissionResolver
    {
        private readonly HashSet<string> _tenantPermissions = new(StringComparer.OrdinalIgnoreCase);

        public Exception? ErrorToThrow { get; init; }

        public GraphQLSchemaFakePermissionResolver WithTenantPermissions(params PermissionType[] permissions)
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
            if (ErrorToThrow is { } exception)
            {
                return Task.FromException<EffectivePermissions>(exception);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<EffectivePermissions>(cancellationToken);
            }

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

    public class GraphQLSchemaProbe
    {
        public string PublicData() => "public";

        [GraphQLAuthorize(PermissionType.Read)]
        public string ProtectedData() => "protected";
    }

    public sealed class GraphQLSchemaProbeWithResourceScopedField : GraphQLSchemaProbe
    {
        [GraphQLAuthorize(
            PermissionType.Read,
            ResourceType = "Project",
            ResourceIdArgumentName = "id")]
        public string? ResourceScoped(Guid id) => "resource";
    }
}
