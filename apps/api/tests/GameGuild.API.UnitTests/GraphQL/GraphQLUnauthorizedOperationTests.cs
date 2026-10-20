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
///     Dynamic schema filtering for unauthorized operations and conditional field
///     exposure (issue #335): root operations the caller may not execute are hidden at
///     operation-planning time with the nonexistent-field error shape (no data payload,
///     no path, no authorization code — indistinguishable from querying an unknown
///     field), execution-time denials never carry the denied field's path, and denied
///     nullable fields resolve to null when masking is enabled.
/// </summary>
public sealed class GraphQLUnauthorizedOperationTests
{
    private static readonly Guid UserId = Guid.Parse("4b50fdd6-2e85-42bb-a9fa-27f6cb7e97c6");
    private static readonly Guid TenantId = Guid.Parse("7b37d70c-6ecd-4eb2-9f21-c08fc9563e85");

    [Fact]
    public async Task OperationGate_HiddenOperationIsIndistinguishableFromNonexistentField()
    {
        var engine = new GraphQLDynamicSchemaFakePermissionResolver();
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var hidden = await ExecuteAsync(client, "{ protectedQuery }");
        var nonexistent = await ExecuteAsync(client, "{ definitelyNotAField }");

        hidden.Status.Should().Be(HttpStatusCode.OK, hidden.Body);
        nonexistent.Status.Should().Be(HttpStatusCode.OK, nonexistent.Body);

        // The hidden-operation response must not reveal that the protected operation
        // exists: no data payload, no path, no authorization code. The field name only
        // appears inside the replicated "does not exist" message, exactly as it does for
        // a genuinely unknown field.
        hidden.Json.RootElement.TryGetProperty("data", out _).Should().BeFalse(hidden.Body);
        hidden.Body.Should().NotContain("AUTHORIZATION");
        hidden.Body.Should().Contain("does not exist");

        var hiddenError = ExtractError(hidden);
        var nonexistentError = ExtractError(nonexistent);
        hiddenError.GetProperty("message").GetString()
            .Should().Be(nonexistentError.GetProperty("message").GetString());
        hiddenError.TryGetProperty("path", out _).Should()
            .Be(nonexistentError.TryGetProperty("path", out _));
        hiddenError.GetProperty("extensions").EnumerateObject()
            .Select(extension => (extension.Name, extension.Value.ToString()))
            .Should().BeEquivalentTo(
                nonexistentError.GetProperty("extensions").EnumerateObject()
                    .Select(extension => (extension.Name, extension.Value.ToString())));
    }

    [Fact]
    public async Task OperationGate_AuthorizedCallerExecutesTheProtectedOperation()
    {
        var engine = new GraphQLDynamicSchemaFakePermissionResolver()
            .WithTenantPermissions(PermissionType.Read);
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, "{ publicQuery protectedQuery }");

        result.Status.Should().Be(HttpStatusCode.OK, result.Body);
        result.Json.RootElement.GetProperty("data").GetProperty("protectedQuery")
            .GetString().Should().Be("protected");
        result.Json.RootElement.GetProperty("data").GetProperty("publicQuery")
            .GetString().Should().Be("public");
    }

    [Fact]
    public async Task OperationGate_MixedPublicAndUnauthorizedFieldsFailTheWholeRequest()
    {
        var engine = new GraphQLDynamicSchemaFakePermissionResolver();
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, "{ publicQuery protectedQuery }");

        // Fail closed: no partial data is served when an unauthorized operation is referenced.
        result.Json.RootElement.TryGetProperty("data", out _).Should().BeFalse(result.Body);
        result.Json.RootElement.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task OperationGate_FragmentWrappedProtectedOperationsAreHidden()
    {
        var engine = new GraphQLDynamicSchemaFakePermissionResolver();
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(
            client,
            "query { ...ProtectedFragment } fragment ProtectedFragment on Query { protectedQuery }");

        result.Json.RootElement.TryGetProperty("data", out _).Should().BeFalse(result.Body);
        var error = ExtractError(result);
        error.GetProperty("message").GetString()
            .Should().Be("The field `protectedQuery` does not exist on the type `Query`.");
        error.TryGetProperty("path", out _).Should().BeFalse();
    }

    [Fact]
    public async Task OperationGate_MutationRootOperationsAreHidden()
    {
        var engine = new GraphQLDynamicSchemaFakePermissionResolver();
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, "mutation { protectedMutation }");

        result.Json.RootElement.TryGetProperty("data", out _).Should().BeFalse(result.Body);
        var error = ExtractError(result);
        error.GetProperty("message").GetString()
            .Should().Be("The field `protectedMutation` does not exist on the type `Mutation`.");
        error.TryGetProperty("path", out _).Should().BeFalse();
    }

    [Fact]
    public async Task OperationGate_ResourceScopedOperationsRemainExecutableAtRuntime()
    {
        // Resource-scoped rules cannot be decided at planning time; they stay enforced
        // during execution (owner override etc.), so authorized callers still execute.
        var resourceId = Guid.Parse("1f6dd2ac-53b6-4c0e-9a54-6f4a30d3b111");
        var engine = new GraphQLDynamicSchemaFakePermissionResolver()
            .WithResourcePermission("Project", resourceId, PermissionType.Read);
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, $"{{ resourceScopedQuery(id: \"{resourceId}\") }}");

        result.Status.Should().Be(HttpStatusCode.OK, result.Body);
        result.Json.RootElement.GetProperty("data").GetProperty("resourceScopedQuery")
            .GetString().Should().Be("resource");
    }

    [Fact]
    public async Task OperationGate_ResourceScopedDenialsAtExecutionCarryNoPath()
    {
        var resourceId = Guid.Parse("1f6dd2ac-53b6-4c0e-9a54-6f4a30d3b111");
        var engine = new GraphQLDynamicSchemaFakePermissionResolver();
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, $"{{ resourceScopedQuery(id: \"{resourceId}\") }}");

        result.Json.RootElement.GetProperty("data").GetProperty("resourceScopedQuery")
            .ValueKind.Should().Be(JsonValueKind.Null);
        var error = ExtractError(result);
        error.GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("AUTHORIZATION_DENIED");
        // The error filter strips the path so the denied field's identity never leaks.
        error.TryGetProperty("path", out _).Should().BeFalse();
    }

    [Fact]
    public async Task OperationGate_FailsClosedWhenEvaluationThrows()
    {
        var engine = new GraphQLDynamicSchemaFakePermissionResolver
        {
            ErrorToThrow = new InvalidOperationException("sensitive store details"),
        };
        await using var app = await CreateApplicationAsync(engine);
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, "{ protectedQuery }");

        result.Json.RootElement.TryGetProperty("data", out _).Should().BeFalse(result.Body);
        result.Body.Should().NotContain("sensitive store details");
        result.Body.Should().NotContain("AUTHORIZATION");
    }

    [Fact]
    public async Task OperationGate_DisabledByOptionFallsBackToTheExecutionTimeError()
    {
        var engine = new GraphQLDynamicSchemaFakePermissionResolver();
        await using var app = await CreateApplicationAsync(
            engine,
            schemaAuthorization: new GraphQLSchemaAuthorizationOptions { HideUnauthorizedOperations = false });
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, "{ protectedQuery }");

        result.Json.RootElement.GetProperty("data").GetProperty("protectedQuery")
            .ValueKind.Should().Be(JsonValueKind.Null);
        var error = ExtractError(result);
        error.GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("AUTHORIZATION_DENIED");
        error.TryGetProperty("path", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Masking_DefaultMasksDeniedNullableFieldsPerRequest()
    {
        var engine = new GraphQLDynamicSchemaFakePermissionResolver()
            .WithTenantPermissions(PermissionType.Read);
        await using var app = await CreateApplicationAsync(
            engine,
            schemaAuthorization: new GraphQLSchemaAuthorizationOptions { MaskUnauthorizedFieldsByDefault = true });
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, "{ publicQuery maskedQuery }");

        result.Status.Should().Be(HttpStatusCode.OK, result.Body);
        // Conditional field exposure: the unauthorized field is nulled out with no error,
        // sibling public fields keep resolving.
        result.Json.RootElement.GetProperty("data").GetProperty("maskedQuery")
            .ValueKind.Should().Be(JsonValueKind.Null);
        result.Json.RootElement.GetProperty("data").GetProperty("publicQuery")
            .GetString().Should().Be("public");
        result.Json.RootElement.TryGetProperty("errors", out _).Should().BeFalse(result.Body);
    }

    [Fact]
    public async Task Masking_NonNullableDeniedFieldsStillProduceTheBoundedError()
    {
        var engine = new GraphQLDynamicSchemaFakePermissionResolver()
            .WithTenantPermissions(PermissionType.Read);
        await using var app = await CreateApplicationAsync(
            engine,
            schemaAuthorization: new GraphQLSchemaAuthorizationOptions { MaskUnauthorizedFieldsByDefault = true });
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, "{ nonNullableProtectedQuery }");

        // Nullability is static: a non-nullable field cannot be nulled without producing
        // an inconsistent non-null violation error, so it keeps the bounded denial.
        var error = ExtractError(result);
        error.GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("AUTHORIZATION_DENIED");
        error.TryGetProperty("path", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Masking_PrivilegedCallersSeeTheSameQueryResolvedFully()
    {
        var engine = new GraphQLDynamicSchemaFakePermissionResolver()
            .WithTenantPermissions(PermissionType.Read, PermissionType.Edit);
        await using var app = await CreateApplicationAsync(
            engine,
            schemaAuthorization: new GraphQLSchemaAuthorizationOptions { MaskUnauthorizedFieldsByDefault = true });
        using var client = app.GetTestClient();

        var result = await ExecuteAsync(client, "{ publicQuery maskedQuery }");

        result.Json.RootElement.GetProperty("data").GetProperty("maskedQuery")
            .GetString().Should().Be("masked");
        result.Json.RootElement.TryGetProperty("errors", out _).Should().BeFalse(result.Body);
    }

    private static JsonElement ExtractError(ExecutionResult result) =>
        result.Json.RootElement.GetProperty("errors").EnumerateArray().First();

    private sealed record ExecutionResult(HttpStatusCode Status, string Body, JsonDocument Json);

    private static async Task<ExecutionResult> ExecuteAsync(HttpClient client, string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = JsonContent.Create(new { query }),
        };
        request.Headers.Add("Authorization", "Bearer user");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return new ExecutionResult(
            response.StatusCode,
            body,
            JsonDocument.Parse(body));
    }

    private static async Task<WebApplication> CreateApplicationAsync(
        GraphQLDynamicSchemaFakePermissionResolver engine,
        GraphQLSchemaAuthorizationOptions? schemaAuthorization = null)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("test")
            .AddScheme<AuthenticationSchemeOptions, GraphQLDynamicSchemaTestAuthenticationHandler>("test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IEffectivePermissionResolver>(engine);
        builder.Services.AddSingleton<IActorContextAccessor>(new GraphQLDynamicSchemaTestActorContextAccessor());

        var graphQlOptions = new GraphQLOptions
        {
            EnableGraphQL = true,
            SchemaAuthorization = schemaAuthorization ?? new GraphQLSchemaAuthorizationOptions(),
        };
        builder.Services.AddSingleton(graphQlOptions);
        builder.Services.TryAddScoped<GraphQLSchemaVisibilityEvaluator>();

        // Mirrors the host's SetupGraphQL pipeline shape: explicit request pipeline with
        // the operation-planning authorization gate between validation and operation
        // compilation, the authorization error filter and the field middleware.
        builder.Services.AddGraphQLServer()
            .AddQueryType<GraphQLDynamicSchemaProbe>()
            .AddMutationType<GraphQLDynamicSchemaMutations>(descriptor => descriptor.Name("Mutation"))
            .AddDirectiveType<GraphQLAuthorizeDirectiveType>()
            .ModifyOptions(options => options.FieldMiddleware = FieldMiddlewareApplication.AllFields)
            .DisableIntrospection(false)
            .AddErrorFilter<GraphQLAuthorizationErrorFilter>()
            .UseField<GraphQLAuthorizeFieldMiddleware>()
            .UseInstrumentation()
            .UseExceptions()
            .UseTimeout()
            .UseDocumentCache()
            .UseDocumentParser()
            .UseDocumentValidation()
            .UseOperationCache()
            .UseRequest<GraphQLUnauthorizedOperationMiddleware>()
            .UseOperationResolver()
            .UseSkipWarmupExecution()
            .UseOperationVariableCoercion()
            .UseOperationExecution();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGraphQL("/graphql").RequireAuthorization();
        await app.StartAsync();
        return app;
    }

    private sealed class GraphQLDynamicSchemaTestAuthenticationHandler(
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

    private sealed class GraphQLDynamicSchemaTestActorContextAccessor : IActorContextAccessor
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

        private sealed class RestoreScope(GraphQLDynamicSchemaTestActorContextAccessor accessor, ActorContext previous) : IActorContextScope
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

    private sealed class GraphQLDynamicSchemaFakePermissionResolver : IEffectivePermissionResolver
    {
        private readonly HashSet<string> _tenantPermissions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(string ResourceType, string ResourceId), HashSet<string>> _resourcePermissions = new();

        public Exception? ErrorToThrow { get; init; }

        public GraphQLDynamicSchemaFakePermissionResolver WithTenantPermissions(params PermissionType[] permissions)
        {
            foreach (var permission in permissions)
            {
                _tenantPermissions.Add(permission.ToString());
            }

            return this;
        }

        public GraphQLDynamicSchemaFakePermissionResolver WithResourcePermission(
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

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<EffectivePermissions>(cancellationToken);
            }

            var contextValid = context.IsValid;
            HashSet<string> effective;
            if (context.HasResource
                && _resourcePermissions.TryGetValue((context.ResourceType!, context.ResourceId!), out var resource))
            {
                effective = new HashSet<string>(resource, StringComparer.OrdinalIgnoreCase);
                effective.UnionWith(_tenantPermissions);
            }
            else
            {
                effective = new HashSet<string>(_tenantPermissions, StringComparer.OrdinalIgnoreCase);
            }

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

/// <summary>Probe schema for the dynamic schema surface tests.</summary>
public class GraphQLDynamicSchemaProbe
{
    public string PublicQuery() => "public";

    [GraphQLAuthorize(PermissionType.Read)]
    public string ProtectedQuery() => "protected";

    [GraphQLAuthorize(PermissionType.Edit)]
    public string? MaskedQuery() => "masked";

    [GraphQLAuthorize(PermissionType.Delete)]
    public string NonNullableProtectedQuery() => "non-null";

    [GraphQLAuthorize(
        PermissionType.Read,
        ResourceType = "Project",
        ResourceIdArgumentName = "id")]
    public string? ResourceScopedQuery(Guid id) => "resource";
}

/// <summary>Probe mutations for the dynamic schema surface tests.</summary>
public class GraphQLDynamicSchemaMutations
{
    [GraphQLAuthorize(PermissionType.Create)]
    public bool ProtectedMutation() => true;
}
