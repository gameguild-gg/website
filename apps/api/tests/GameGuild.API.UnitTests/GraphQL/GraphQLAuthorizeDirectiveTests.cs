using System.Collections.Concurrent;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.API.UnitTests.GraphQL;

/// <summary>
///     Enforcement matrix for the general <c>@authorize</c> directive (issue #335):
///     allow/deny/mask, AND/OR composition, repeatable applications, role and policy
///     variants, resource-scoped evaluation through the effective-permission engine
///     (including owner-override and expiration-aware semantics), anonymous handling,
///     request-scoped caching, evaluation logging and fail-closed behavior.
/// </summary>
public sealed class GraphQLAuthorizeDirectiveTests
{
    private static readonly Guid UserId = Guid.Parse("4b50fdd6-2e85-42bb-a9fa-27f6cb7e97c6");
    private static readonly Guid TenantId = Guid.Parse("7b37d70c-6ecd-4eb2-9f21-c08fc9563e85");
    private static readonly Guid OwnedProjectId = Guid.Parse("b659b7bf-6281-42e6-a7ef-23d296cff5dd");

    [Fact]
    public async Task Authorize_AllowsWhenEngineGrantsThePermissionAndRecordsAllowEvaluation()
    {
        var engine = new GraphQLFakeEffectivePermissionResolver()
            .WithTenantPermissions(PermissionType.Read);
        var counters = new GraphQLAuthorizeCounters();
        var evaluationLog = new GraphQLPermissionEvaluationLogFake();
        await using var app = await CreateApplicationAsync(engine, counters, evaluationLog);
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest("{ guarded }"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("data").GetProperty("guarded").GetString().Should().Be("allowed");
        document.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        counters.ResolverCount.Should().Be(1);
        evaluationLog.Records.Should().ContainSingle().Which.Should().Match<PermissionEvaluationRecord>(
            record =>
                record.UserId == UserId &&
                record.TenantId == TenantId &&
                record.ResourceType == "Tenant" &&
                record.RequiredPermissions.SequenceEqual(new[] { PermissionType.Read.ToString() }) &&
                record.Outcome == PermissionEvaluationOutcome.Allow &&
                record.Source == "graphql" &&
                record.Operation == "guarded" &&
                record.Reason == null);
    }

    [Fact]
    public async Task Authorize_DeniesByDefaultWithoutLeakingDetailsAndSkipsTheResolver()
    {
        var engine = new GraphQLFakeEffectivePermissionResolver();
        var counters = new GraphQLAuthorizeCounters();
        var evaluationLog = new GraphQLPermissionEvaluationLogFake();
        await using var app = await CreateApplicationAsync(engine, counters, evaluationLog);
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest("{ guarded }"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var data = document.RootElement.GetProperty("data");
        (data.ValueKind == JsonValueKind.Null
            || data.GetProperty("guarded").ValueKind == JsonValueKind.Null).Should().BeTrue();
        var error = document.RootElement.GetProperty("errors").EnumerateArray().Single();
        error.GetProperty("message").GetString().Should().Be("Not authorized to access this resource.");
        error.GetProperty("extensions").GetProperty("code").GetString().Should().Be("AUTHORIZATION_DENIED");
        counters.ResolverCount.Should().Be(0);
        evaluationLog.Records.Should().ContainSingle().Which.Should().Match<PermissionEvaluationRecord>(
            record =>
                record.Outcome == PermissionEvaluationOutcome.Deny &&
                record.Operation == "guarded" &&
                record.Reason == "permission_denied");
    }

    [Fact]
    public async Task Authorize_MasksUnauthorizedFieldsAsNullWithoutErrors()
    {
        var engine = new GraphQLFakeEffectivePermissionResolver();
        var counters = new GraphQLAuthorizeCounters();
        await using var app = await CreateApplicationAsync(engine, counters);
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest("{ masked }"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var data = document.RootElement.GetProperty("data");
        (data.ValueKind == JsonValueKind.Null
            || data.GetProperty("masked").ValueKind == JsonValueKind.Null).Should().BeTrue();
        document.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        counters.ResolverCount.Should().Be(0);
    }

    [Fact]
    public async Task Authorize_ComposesPermissionsWithAnyAndAllModes()
    {
        var engine = new GraphQLFakeEffectivePermissionResolver()
            .WithTenantPermissions(PermissionType.Edit);
        var counters = new GraphQLAuthorizeCounters();
        await using var app = await CreateApplicationAsync(engine, counters);
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest("{ anyOf allOf }"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var data = document.RootElement.GetProperty("data");
        data.GetProperty("anyOf").GetString().Should().Be("allowed");
        data.GetProperty("allOf").ValueKind.Should().Be(JsonValueKind.Null);
        counters.ResolverCount.Should().Be(1);
    }

    [Fact]
    public async Task Authorize_CombinesRepeatedApplicationsWithDenyWins()
    {
        var engine = new GraphQLFakeEffectivePermissionResolver()
            .WithTenantPermissions(PermissionType.Read);
        var counters = new GraphQLAuthorizeCounters();
        await using var app = await CreateApplicationAsync(engine, counters);
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest("{ repeated }"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var data = document.RootElement.GetProperty("data");
        (data.ValueKind == JsonValueKind.Null
            || data.GetProperty("repeated").ValueKind == JsonValueKind.Null).Should().BeTrue();
        document.RootElement.GetProperty("errors").EnumerateArray().Single()
            .GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("AUTHORIZATION_DENIED");
        counters.ResolverCount.Should().Be(0);
    }

    [Fact]
    public async Task Authorize_EnforcesRoleVariantAgainstTheTrustedActorContext()
    {
        var engine = new GraphQLFakeEffectivePermissionResolver();
        var counters = new GraphQLAuthorizeCounters();

        await using var adminApp = await CreateApplicationAsync(engine, counters, roles: ["Admin"]);
        using var adminClient = adminApp.GetTestClient();
        using var adminResponse = await adminClient.SendAsync(CreateRequest("{ roleGuarded }"));
        var adminBody = await adminResponse.Content.ReadAsStringAsync();
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK, adminBody);
        JsonDocument.Parse(adminBody).RootElement.GetProperty("data").GetProperty("roleGuarded")
            .GetString().Should().Be("allowed");

        await using var userApp = await CreateApplicationAsync(engine, counters, roles: ["User"]);
        using var userClient = userApp.GetTestClient();
        using var userResponse = await userClient.SendAsync(CreateRequest("{ roleGuarded }"));
        var userBody = await userResponse.Content.ReadAsStringAsync();
        userResponse.StatusCode.Should().Be(HttpStatusCode.OK, userBody);
        using var userDocument = JsonDocument.Parse(userBody);
        var userData = userDocument.RootElement.GetProperty("data");
        (userData.ValueKind == JsonValueKind.Null
            || userData.GetProperty("roleGuarded").ValueKind == JsonValueKind.Null).Should().BeTrue();
        userDocument.RootElement.GetProperty("errors").EnumerateArray().Single()
            .GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("AUTHORIZATION_DENIED");
    }

    [Fact]
    public async Task Authorize_EnforcesPolicyVariantThroughAspNetCoreAuthorization()
    {
        var engine = new GraphQLFakeEffectivePermissionResolver();
        var counters = new GraphQLAuthorizeCounters();

        await using var adminApp = await CreateApplicationAsync(engine, counters, roles: ["Admin"]);
        using var adminClient = adminApp.GetTestClient();
        using var adminResponse = await adminClient.SendAsync(
            CreateRequest("{ policyGuarded }", token: "admin"));
        var adminBody = await adminResponse.Content.ReadAsStringAsync();
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK, adminBody);
        JsonDocument.Parse(adminBody).RootElement.GetProperty("data").GetProperty("policyGuarded")
            .GetString().Should().Be("allowed");

        await using var userApp = await CreateApplicationAsync(engine, counters, roles: ["User"]);
        using var userClient = userApp.GetTestClient();
        using var userResponse = await userClient.SendAsync(CreateRequest("{ policyGuarded }"));
        var userBody = await userResponse.Content.ReadAsStringAsync();
        userResponse.StatusCode.Should().Be(HttpStatusCode.OK, userBody);
        using var userDocument = JsonDocument.Parse(userBody);
        var userData = userDocument.RootElement.GetProperty("data");
        (userData.ValueKind == JsonValueKind.Null
            || userData.GetProperty("policyGuarded").ValueKind == JsonValueKind.Null).Should().BeTrue();
        userDocument.RootElement.GetProperty("errors").EnumerateArray().Single()
            .GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("AUTHORIZATION_DENIED");
    }

    [Fact]
    public async Task Authorize_EvaluatesResourceScopedRulesThroughTheEffectivePermissionEngine()
    {
        var engine = new GraphQLFakeEffectivePermissionResolver()
            .WithResourcePermissions("Project", OwnedProjectId, PermissionType.Read);
        var counters = new GraphQLAuthorizeCounters();
        await using var app = await CreateApplicationAsync(engine, counters);
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(
            CreateRequest($"{{ resource(id: \"{OwnedProjectId}\") }}"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("resource")
            .GetString().Should().Be("allowed");

        // The engine received an exact resource context (tenant + resource type + id):
        // the shape that activates owner override and temporal precedence inside the
        // effective-permission engine (issues #372/#363).
        engine.ReceivedContexts.Should().Contain(context =>
            context.UserId == UserId &&
            context.TenantId == TenantId &&
            context.ResourceType == "Project" &&
            context.ResourceId == OwnedProjectId.ToString("D"));
        counters.ResolverCount.Should().Be(1);
    }

    [Fact]
    public async Task Authorize_DeniesResourceScopedRulesWhenTheEngineGrantsNothingForThatResource()
    {
        var engine = new GraphQLFakeEffectivePermissionResolver()
            .WithResourcePermissions("Project", OwnedProjectId, PermissionType.Read);
        var counters = new GraphQLAuthorizeCounters();
        var evaluationLog = new GraphQLPermissionEvaluationLogFake();
        await using var app = await CreateApplicationAsync(engine, counters, evaluationLog);
        using var client = app.GetTestClient();

        var otherProject = Guid.Parse("8f1a2c34-56b7-48d9-9e01-234567890abc");
        using var response = await client.SendAsync(
            CreateRequest($"{{ resource(id: \"{otherProject}\") }}"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var denyData = JsonDocument.Parse(body).RootElement.GetProperty("data");
        (denyData.ValueKind == JsonValueKind.Null
            || denyData.GetProperty("resource").ValueKind == JsonValueKind.Null).Should().BeTrue();
        counters.ResolverCount.Should().Be(0);
        evaluationLog.Records.Should().ContainSingle().Which.Reason.Should().Be("permission_denied");
    }

    [Fact]
    public async Task Authorize_OwnerOverrideSatisfiesResourceScopedRulesThroughTheEngine()
    {
        // The fake engine simulates the #372/#819 contract: ownership of the exact
        // resource mints the Read permission; a lapsed ownership bound (issue #363
        // temporal precedence) mints nothing and the engine fails closed.
        var engine = new GraphQLFakeEffectivePermissionResolver()
            .WithOwnerOverride("Project", OwnedProjectId, PermissionType.Read, lapsed: false);
        var counters = new GraphQLAuthorizeCounters();
        await using var app = await CreateApplicationAsync(engine, counters);
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(
            CreateRequest($"{{ resource(id: \"{OwnedProjectId}\") }}"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("resource")
            .GetString().Should().Be("allowed");
    }

    [Fact]
    public async Task Authorize_LapsedOwnerOverrideFailsClosedThroughTheEngine()
    {
        var engine = new GraphQLFakeEffectivePermissionResolver()
            .WithOwnerOverride("Project", OwnedProjectId, PermissionType.Read, lapsed: true);
        var counters = new GraphQLAuthorizeCounters();
        await using var app = await CreateApplicationAsync(engine, counters);
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(
            CreateRequest($"{{ resource(id: \"{OwnedProjectId}\") }}"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var denyData = JsonDocument.Parse(body).RootElement.GetProperty("data");
        (denyData.ValueKind == JsonValueKind.Null
            || denyData.GetProperty("resource").ValueKind == JsonValueKind.Null).Should().BeTrue();
        counters.ResolverCount.Should().Be(0);
    }

    [Fact]
    public async Task Authorize_FailsClosedWithUnavailableErrorWhenTheEngineThrows()
    {
        var engine = new GraphQLFakeEffectivePermissionResolver
        {
            ErrorToThrow = new InvalidOperationException("sensitive store details"),
        };
        var counters = new GraphQLAuthorizeCounters();
        var evaluationLog = new GraphQLPermissionEvaluationLogFake();
        await using var app = await CreateApplicationAsync(engine, counters, evaluationLog);
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest("{ guarded }"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var error = document.RootElement.GetProperty("errors").EnumerateArray().Single();
        error.GetProperty("message").GetString().Should().Be("Authorization could not be evaluated.");
        error.GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("AUTHORIZATION_UNAVAILABLE");
        body.Should().NotContain("sensitive store details");
        counters.ResolverCount.Should().Be(0);
        evaluationLog.Records.Should().ContainSingle().Which.Should().Match<PermissionEvaluationRecord>(
            record =>
                record.Outcome == PermissionEvaluationOutcome.Error &&
                record.Reason == "authorization_evaluation_failed");
    }

    [Fact]
    public async Task Authorize_DeniesAnonymousCallersOnProtectedFields()
    {
        var engine = new GraphQLFakeEffectivePermissionResolver()
            .WithTenantPermissions(PermissionType.Read);
        var counters = new GraphQLAuthorizeCounters();
        await using var app = await CreateApplicationAsync(engine, counters, actorAuthenticated: false);
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest("{ guarded }"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var anonymousData = JsonDocument.Parse(body).RootElement.GetProperty("data");
        (anonymousData.ValueKind == JsonValueKind.Null
            || anonymousData.GetProperty("guarded").ValueKind == JsonValueKind.Null).Should().BeTrue();
        counters.ResolverCount.Should().Be(0);
    }

    [Fact]
    public async Task Authorize_CachesEngineResolutionPerRequest()
    {
        var engine = new GraphQLFakeEffectivePermissionResolver()
            .WithTenantPermissions(PermissionType.Read);
        var counters = new GraphQLAuthorizeCounters();
        await using var app = await CreateApplicationAsync(engine, counters);
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest("{ cachedFirst cachedSecond }"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("data").GetProperty("cachedFirst").GetString().Should().Be("allowed");
        document.RootElement.GetProperty("data").GetProperty("cachedSecond").GetString().Should().Be("allowed");
        engine.ResolveCount.Should().Be(1, "effective-permission resolution is cached per request");
        counters.ResolverCount.Should().Be(2);
    }

    [Fact]
    public async Task Authorize_RecordsRoleAttributionOnAllowedAndDeniedEvaluations()
    {
        var engine = new GraphQLFakeEffectivePermissionResolver()
            .WithTenantPermissions(PermissionType.Read);
        var counters = new GraphQLAuthorizeCounters();
        var evaluationLog = new GraphQLPermissionEvaluationLogFake();
        var roleAttribution = new GraphQLRoleAttributionFake();
        await using var app = await CreateApplicationAsync(
            engine, counters, evaluationLog, rbacResolver: roleAttribution);
        using var client = app.GetTestClient();

        // "guarded" requires Read (allowed); "allOf" requires Edit+Delete (denied), so one
        // query produces both an Allow and a Deny evaluation record.
        using var response = await client.SendAsync(CreateRequest("{ guarded allOf }"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        evaluationLog.Records.Should().HaveCount(2);
        evaluationLog.Records.Should().Contain(record => record.Outcome == PermissionEvaluationOutcome.Allow)
            .Which.Roles.Should().Equal("Member", "Admin (inherited)");
        evaluationLog.Records.Should().Contain(record => record.Outcome == PermissionEvaluationOutcome.Deny)
            .Which.Roles.Should().Equal("Member", "Admin (inherited)");
        roleAttribution.ResolveCount.Should().Be(1, "role attribution resolves once per request");
    }

    [Fact]
    public async Task Authorize_StillAllowsWhenEvaluationLoggingFails()
    {
        var engine = new GraphQLFakeEffectivePermissionResolver()
            .WithTenantPermissions(PermissionType.Read);
        var counters = new GraphQLAuthorizeCounters();
        var evaluationLog = new GraphQLPermissionEvaluationLogFake
        {
            ErrorToThrow = new InvalidOperationException("sensitive audit storage details"),
        };
        await using var app = await CreateApplicationAsync(engine, counters, evaluationLog);
        using var client = app.GetTestClient();

        using var response = await client.SendAsync(CreateRequest("{ guarded }"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        JsonDocument.Parse(body).RootElement.GetProperty("data").GetProperty("guarded")
            .GetString().Should().Be("allowed");
        body.Should().NotContain("sensitive audit storage details");
        counters.ResolverCount.Should().Be(1);
    }

    private static HttpRequestMessage CreateRequest(string query, string token = "user")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = JsonContent.Create(new { query }),
        };
        request.Headers.Add("Authorization", $"Bearer {token}");
        return request;
    }

    private static async Task<WebApplication> CreateApplicationAsync(
        GraphQLFakeEffectivePermissionResolver engine,
        GraphQLAuthorizeCounters counters,
        GraphQLPermissionEvaluationLogFake? evaluationLog = null,
        IRbacPermissionResolver? rbacResolver = null,
        bool actorAuthenticated = true,
        string[]? roles = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("test")
            .AddScheme<AuthenticationSchemeOptions, GraphQLAuthorizeTestAuthenticationHandler>("test", _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
        });
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(engine);
        builder.Services.AddSingleton<IEffectivePermissionResolver>(engine);
        builder.Services.AddSingleton(counters);
        builder.Services.AddSingleton<IActorContextAccessor>(
            new GraphQLAuthorizeTestActorContextAccessor(actorAuthenticated, roles ?? ["User"]));
        if (rbacResolver is not null)
        {
            builder.Services.AddSingleton(rbacResolver);
        }

        if (evaluationLog is not null)
        {
            builder.Services.AddSingleton<IPermissionEvaluationLogSink>(evaluationLog);
        }

        builder.Services.AddSingleton<IPermissionEvaluationLogService, PermissionEvaluationLogService>();
        builder.Services.AddGraphQLServer()
            .AddQueryType<GraphQLAuthorizeProbe>()
            .AddTypeExtension<GraphQLAuthorizeNestedResolvers>()
            .AddDirectiveType<GraphQLAuthorizeDirectiveType>()
            .ModifyOptions(options => options.FieldMiddleware = FieldMiddlewareApplication.AllFields)
            .DisableIntrospection(false)
            .UseField<GraphQLAuthorizeFieldMiddleware>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGraphQL("/graphql").RequireAuthorization();
        await app.StartAsync();
        return app;
    }

    private sealed class GraphQLAuthorizeTestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out var authorization))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var role = authorization.ToString().EndsWith("admin", StringComparison.OrdinalIgnoreCase)
                ? "Admin"
                : "User";
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, UserId.ToString("D")),
                    new Claim(ClaimTypes.Role, role),
                ],
                Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class GraphQLAuthorizeTestActorContextAccessor(
        bool authenticated,
        string[] roles)
        : IActorContextAccessor
    {
        private ActorContext _actorContext = ActorContext.Anonymous with
        {
            SubjectId = UserId.ToString("D"),
            TenantId = TenantId,
            Roles = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase),
            IsAuthenticated = authenticated,
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

        private sealed class RestoreScope(GraphQLAuthorizeTestActorContextAccessor owner, ActorContext previous) : IActorContextScope
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

    /// <summary>
    ///     Simulates the effective-permission engine contract (issue #330, owner override
    ///     #372, temporal precedence #363): records every resolution context and answers
    ///     from configurable tenant grants, exact-resource grants, or an ownership
    ///     designation whose lapsed bound fails closed (nothing is minted).
    /// </summary>
    private sealed class GraphQLFakeEffectivePermissionResolver : IEffectivePermissionResolver
    {
        private readonly HashSet<string> _tenantPermissions = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<(string ResourceType, Guid ResourceId, string Permission)> _resourcePermissions = new();
        private readonly List<(string ResourceType, Guid ResourceId, string Permission, bool Lapsed)> _ownerOverrides = [];

        private readonly ConcurrentQueue<EffectivePermissionContext> _receivedContexts = new();
        private int _resolveCount;

        public Exception? ErrorToThrow { get; init; }

        public int ResolveCount => Volatile.Read(ref _resolveCount);

        public IReadOnlyCollection<EffectivePermissionContext> ReceivedContexts => _receivedContexts.ToArray();

        public GraphQLFakeEffectivePermissionResolver WithTenantPermissions(params PermissionType[] permissions)
        {
            foreach (var permission in permissions)
            {
                _tenantPermissions.Add(permission.ToString());
            }

            return this;
        }

        public GraphQLFakeEffectivePermissionResolver WithResourcePermissions(
            string resourceType,
            Guid resourceId,
            params PermissionType[] permissions)
        {
            foreach (var permission in permissions)
            {
                _resourcePermissions.Add((resourceType, resourceId, permission.ToString()));
            }

            return this;
        }

        public GraphQLFakeEffectivePermissionResolver WithOwnerOverride(
            string resourceType,
            Guid resourceId,
            PermissionType permission,
            bool lapsed)
        {
            _ownerOverrides.Add((resourceType, resourceId, permission.ToString(), lapsed));
            return this;
        }

        public Task<EffectivePermissions> ResolveAsync(
            EffectivePermissionContext context,
            CancellationToken cancellationToken = default)
        {
            _receivedContexts.Enqueue(context);
            Interlocked.Increment(ref _resolveCount);
            if (ErrorToThrow is { } exception)
            {
                return Task.FromException<EffectivePermissions>(exception);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<EffectivePermissions>(cancellationToken);
            }

            var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            permissions.UnionWith(_tenantPermissions);

            foreach (var (resourceType, resourceId, permission) in _resourcePermissions)
            {
                if (context.HasResource &&
                    string.Equals(context.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(context.ResourceId, resourceId.ToString("D"), StringComparison.OrdinalIgnoreCase))
                {
                    permissions.Add(permission);
                }
            }

            OwnerOverrideEvaluation? ownerOverride = null;
            foreach (var (resourceType, resourceId, permission, lapsed) in _ownerOverrides)
            {
                if (!context.HasResource ||
                    !string.Equals(context.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(context.ResourceId, resourceId.ToString("D"), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // A lapsed ownership bound mints nothing (fail closed, issue #363); an
                // in-force designation mints the owner's permission set (issue #372).
                if (!lapsed)
                {
                    permissions.Add(permission);
                    ownerOverride = new OwnerOverrideEvaluation(
                        GameGuild.Configuration.PresentationLayer.Authorization.OwnerOverrideMode.AllowUnlessDenied,
                        context.UserId,
                        [permission],
                        EffectiveThroughUtc: DateTime.UtcNow.AddHours(1));
                }
                else
                {
                    ownerOverride = new OwnerOverrideEvaluation(
                        GameGuild.Configuration.PresentationLayer.Authorization.OwnerOverrideMode.AllowUnlessDenied,
                        context.UserId,
                        [],
                        EffectiveThroughUtc: DateTime.UtcNow.AddHours(-1));
                }
            }

            return Task.FromResult(new EffectivePermissions
            {
                UserId = context.UserId,
                TenantId = context.TenantId,
                Permissions = permissions,
                Sources = permissions.ToDictionary(
                    permission => permission,
                    _ => ownerOverride is not null
                        ? PermissionSource.Owner
                        : PermissionSource.DirectGrant),
                Context = context,
                ContextValid = context.IsValid,
                OwnerOverride = ownerOverride,
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

    private sealed class GraphQLRoleAttributionFake : IRbacPermissionResolver
    {
        private int _resolveCount;

        public int ResolveCount => Volatile.Read(ref _resolveCount);

        public Task<RbacResolutionResult> ResolvePermissionsAsync(
            Guid userId,
            Guid? tenantId,
            CancellationToken ct = default)
        {
            Interlocked.Increment(ref _resolveCount);
            return Task.FromResult(new RbacResolutionResult(
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new[]
                {
                    new RoleContribution(
                        Guid.NewGuid(),
                        "Member",
                        Array.Empty<string>(),
                        IsInherited: false,
                        InheritedFromRoleId: null),
                    new RoleContribution(
                        Guid.NewGuid(),
                        "Admin",
                        Array.Empty<string>(),
                        IsInherited: true,
                        InheritedFromRoleId: Guid.NewGuid()),
                }));
        }
    }

    private sealed class GraphQLPermissionEvaluationLogFake : IPermissionEvaluationLogSink
    {
        private readonly ConcurrentQueue<PermissionEvaluationRecord> _records = new();

        public Exception? ErrorToThrow { get; init; }

        public IReadOnlyCollection<PermissionEvaluationRecord> Records => _records.ToArray();

        public Task<bool> TryRecordAsync(
            PermissionEvaluationRecord record,
            CancellationToken cancellationToken = default)
        {
            if (ErrorToThrow is { } exception)
            {
                return Task.FromException<bool>(exception);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<bool>(cancellationToken);
            }

            _records.Enqueue(record);
            return Task.FromResult(true);
        }
    }

    public sealed class GraphQLAuthorizeCounters
    {
        private int _resolverCount;

        public int ResolverCount => Volatile.Read(ref _resolverCount);

        public string? Resolve()
        {
            Interlocked.Increment(ref _resolverCount);
            return "allowed";
        }
    }

    public sealed class GraphQLAuthorizeProbe(GraphQLAuthorizeCounters counters)
    {
        [GraphQLAuthorize(PermissionType.Read)]
        public string? Guarded() => counters.Resolve();

        [GraphQLAuthorize(PermissionType.Read, MaskUnauthorized = true)]
        public string? Masked() => counters.Resolve();

        [GraphQLAuthorize(PermissionType.Edit, PermissionType.Delete, Mode = GraphQLAuthorizationMode.Any)]
        public string? AnyOf() => counters.Resolve();

        [GraphQLAuthorize(PermissionType.Edit, PermissionType.Delete)]
        public string? AllOf() => counters.Resolve();

        [GraphQLAuthorize(Roles = ["Admin"])]
        public string? RoleGuarded() => counters.Resolve();

        [GraphQLAuthorize(Policy = "RequireAdminRole")]
        public string? PolicyGuarded() => counters.Resolve();

        [GraphQLAuthorize(PermissionType.Read)]
        [GraphQLAuthorize(PermissionType.Edit)]
        public string? Repeated() => counters.Resolve();

        [GraphQLAuthorize(
            PermissionType.Read,
            ResourceType = "Project",
            ResourceIdArgumentName = "id")]
        public string? Resource(Guid id) => counters.Resolve();

        [GraphQLAuthorize(PermissionType.Read)]
        public string? CachedFirst() => counters.Resolve();

        [GraphQLAuthorize(PermissionType.Read)]
        public string? CachedSecond() => counters.Resolve();

        public GraphQLAuthorizeNestedResource Nested() => new(OwnedProjectId);
    }

    public sealed record GraphQLAuthorizeNestedResource(Guid ProjectId);

    [ExtendObjectType(typeof(GraphQLAuthorizeNestedResource))]
    public sealed class GraphQLAuthorizeNestedResolvers(GraphQLAuthorizeCounters counters)
    {
        [GraphQLAuthorize(
            PermissionType.Read,
            ResourceType = "Project",
            ResourceIdParentPropertyName = "ProjectId")]
        public string? NestedGuarded([Parent] GraphQLAuthorizeNestedResource resource) => counters.Resolve();
    }
}
