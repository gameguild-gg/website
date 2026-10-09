using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using Asp.Versioning;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.CQRS;
using GameGuild.Features;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using PresentationAuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;

namespace GameGuild.API.UnitTests.Security;

public sealed class FeatureEvaluationContextHttpTests
{
    private static readonly string Secret = new('f', 64);
    private static readonly Guid UserId = Guid.Parse("a1854f88-774d-4e1b-a97e-3545b47a94a4");
    private static readonly Guid TenantId = Guid.Parse("f019044f-fdc4-4df3-bcd6-76ea6578f290");
    private static readonly Guid OtherId = Guid.Parse("5f83e57a-0e3b-4ce9-b6cf-474b73112c2a");

    [Theory]
    [InlineData("evaluate")]
    [InlineData("bulk")]
    [InlineData("value")]
    [InlineData("enabled")]
    public async Task RuntimeEndpoints_BindCurrentActorAndTransportContext(string endpoint)
    {
        var contexts = new ConcurrentQueue<FeatureContext>();
        await using var app = await CreateAppAsync(contexts);
        using var client = AuthenticatedClient(app, UserId.ToString(), TenantId);
        var supplied = new FeatureContext
        {
            Environment = "staging", Permissions = ["admin:*"],
            IpAddress = "198.51.100.99", UserAgent = "payload-agent",
            SubscriptionPlanId = "unearned-plan", Country = "XX", RequestTime = DateTime.MinValue,
            CustomAttributes = new Dictionary<string, object> { ["theme"] = "dark" }
        };
        var before = DateTime.UtcNow;

        using var response = await SendAsync(client, endpoint, supplied);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var context = Assert.Single(contexts);
        Assert.Equal(UserId, context.UserId);
        Assert.Equal(TenantId, context.TenantId);
        Assert.Equal(["features:read"], context.Permissions);
        Assert.Equal("http-agent", context.UserAgent);
        Assert.NotEqual(supplied.IpAddress, context.IpAddress);
        Assert.Equal("trusted-plan", context.SubscriptionPlanId);
        Assert.Null(context.Country);
        Assert.InRange(context.RequestTime, before, DateTime.UtcNow);
        Assert.Equal("staging", context.Environment);
        if (endpoint is "evaluate" or "bulk")
        {
            Assert.Equal("dark", context.CustomAttributes["theme"].ToString());
        }
    }

    public static IEnumerable<object[]> MismatchedSelectors()
    {
        foreach (var endpoint in new[] { "evaluate", "bulk", "value", "enabled" })
        foreach (var selector in new[] { "user", "tenant" })
        foreach (var systemAdmin in new[] { false, true })
        {
            yield return [endpoint, selector, systemAdmin];
        }
    }

    [Theory]
    [MemberData(nameof(MismatchedSelectors))]
    public async Task RuntimeEndpoints_RejectOtherIdentityBeforeEvaluation(string endpoint, string selector, bool systemAdmin)
    {
        var contexts = new ConcurrentQueue<FeatureContext>();
        await using var app = await CreateAppAsync(contexts);
        using var client = AuthenticatedClient(app, UserId.ToString(), TenantId, systemAdmin);
        var context = new FeatureContext
        {
            UserId = selector == "user" ? OtherId : UserId,
            TenantId = selector == "tenant" ? OtherId : TenantId
        };

        using var response = await SendAsync(client, endpoint, context);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(contexts);
    }

    [Theory]
    [InlineData("evaluate")]
    [InlineData("bulk")]
    [InlineData("value")]
    [InlineData("enabled")]
    public async Task RuntimeEndpoints_RequireAuthentication(string endpoint)
    {
        var contexts = new ConcurrentQueue<FeatureContext>();
        await using var app = await CreateAppAsync(contexts);
        using var client = app.GetTestClient();

        using var response = await SendAsync(client, endpoint, new FeatureContext());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(contexts);
    }

    public static IEnumerable<object[]> InvalidSubjects()
    {
        foreach (var endpoint in new[] { "evaluate", "bulk", "value", "enabled" })
        foreach (var subject in new[] { "missing", "invalid", Guid.Empty.ToString() })
        {
            yield return [endpoint, subject];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidSubjects))]
    public async Task RuntimeEndpoints_RejectInvalidUserActor(string endpoint, string subject)
    {
        var contexts = new ConcurrentQueue<FeatureContext>();
        await using var app = await CreateAppAsync(contexts);
        using var client = AuthenticatedClient(app, subject, TenantId);

        using var response = await SendAsync(client, endpoint, new FeatureContext());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(contexts);
    }

    [Theory]
    [InlineData("evaluate")]
    [InlineData("bulk")]
    [InlineData("value")]
    [InlineData("enabled")]
    public async Task RuntimeEndpoints_FailClosedWithoutResolvedActor(string endpoint)
    {
        var contexts = new ConcurrentQueue<FeatureContext>();
        await using var app = await CreateAppAsync(contexts, false);
        using var client = AuthenticatedClient(app, UserId.ToString(), TenantId);

        using var response = await SendAsync(client, endpoint, new FeatureContext());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(contexts);
    }

    [Theory]
    [InlineData("evaluate")]
    [InlineData("bulk")]
    [InlineData("value")]
    [InlineData("enabled")]
    public async Task RuntimeEndpoints_AllowExplicitCurrentSelectors(string endpoint)
    {
        var contexts = new ConcurrentQueue<FeatureContext>();
        await using var app = await CreateAppAsync(contexts);
        using var client = AuthenticatedClient(app, UserId.ToString(), TenantId);

        using var response = await SendAsync(client, endpoint, new FeatureContext { UserId = UserId, TenantId = TenantId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TenantId, Assert.Single(contexts).TenantId);
    }

    [Theory]
    [InlineData("evaluate")]
    [InlineData("bulk")]
    public async Task RuntimeEndpoints_PreserveAuthenticatedServiceAndGlobalContexts(string endpoint)
    {
        var contexts = new ConcurrentQueue<FeatureContext>();
        await using var app = await CreateAppAsync(contexts);
        using var client = AuthenticatedClient(app, "service-client", null, actorType: "service");

        using var response = await SendAsync(client, endpoint, new FeatureContext());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var context = Assert.Single(contexts);
        Assert.Null(context.UserId);
        Assert.Null(context.TenantId);
        Assert.Equal(["features:read"], context.Permissions);
    }

    [Theory]
    [InlineData("evaluate")]
    [InlineData("bulk")]
    public async Task RuntimeEndpoints_DoNotEnableAnotherTenantsTargetedFlag(string endpoint)
    {
        var contexts = new ConcurrentQueue<FeatureContext>();
        await using var app = await CreateAppAsync(contexts);
        using var client = AuthenticatedClient(app, UserId.ToString(), TenantId);

        using var ownResponse = await SendAsync(client, endpoint, new FeatureContext());
        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);
        var ownBody = await ownResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"isEnabled\":false", ownBody);
        using var spoofedResponse = await SendAsync(client, endpoint, new FeatureContext { TenantId = OtherId });

        Assert.Equal(HttpStatusCode.Forbidden, spoofedResponse.StatusCode);
        Assert.Single(contexts);
    }

    private static async Task<WebApplication> CreateAppAsync(ConcurrentQueue<FeatureContext> contexts, bool resolveActor = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = Secret, ["Jwt:Issuer"] = "feature-test-issuer",
            ["Jwt:Audience"] = "feature-test-audience"
        });
        builder.Services.SetupAuthentication(builder.Configuration, new PresentationAuthenticationOptions
        {
            JwtSecretKey = Secret, JwtIssuer = "feature-test-issuer", JwtAudience = "feature-test-audience"
        });
        builder.Services.AddAuthorization();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader()).AddMvc();
        builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Clear();
            manager.ApplicationParts.Add(new RuntimeControllerPart());
        });
        builder.Services.AddHttpContextAccessor();
        var actorAccessor = new ActorContextAccessor();
        builder.Services.AddSingleton<IActorContextAccessor>(actorAccessor);
        builder.Services.AddScoped<IClaimsPrincipalAccessor, HttpContextClaimsPrincipalAccessor>();
        var tenantResolver = new Mock<IAuthorizationTenantResolver>();
        tenantResolver.Setup(resolver => resolver.ResolveTenantIdAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .Returns<HttpContext, CancellationToken>((context, _) => Task.FromResult(context.User.FindFirst("tenant_id")?.Value));
        builder.Services.AddSingleton(tenantResolver.Object);
        var permissionService = new Mock<IAuthorizationPermissionService>();
        permissionService.Setup(service => service.GetPermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["features:read"]);
        builder.Services.AddSingleton(permissionService.Object);
        var repository = new Mock<IFeatureFlagQueryRepository>();
        var flag = new FeatureFlag
        {
            Key = "flag", Name = "flag", IsEnabled = true, Type = FeatureFlagType.UserSegment,
            EnabledValue = "true", DefaultValue = "false", Environment = "",
            Targets = [new FeatureFlagTarget { TargetType = "tenant", TargetIdentifier = OtherId.ToString(), IsEnabled = true }]
        };
        repository.Setup(repo => repo.GetByKeyAsync("flag", It.IsAny<CancellationToken>())).ReturnsAsync(flag);
        repository.Setup(repo => repo.GetByEnvironmentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([flag]);
        var realEvaluation = new FeatureFlagEvaluationService(repository.Object,
            [new TargetedEvaluationStrategy([new TenantTargetingHandler(NullLogger<TenantTargetingHandler>.Instance)])],
            NullLogger<FeatureFlagEvaluationService>.Instance, Options.Create(new FeatureFlagOptions()));
        var evaluation = new Mock<IFeatureFlagEvaluationService>();
        evaluation.Setup(service => service.EvaluateAsync(It.IsAny<string>(), It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .Returns<string, FeatureContext, CancellationToken>((key, context, ct) =>
            {
                contexts.Enqueue(context);
                return realEvaluation.EvaluateAsync(key, context, ct);
            });
        evaluation.Setup(service => service.GetValueAsync(It.IsAny<string>(), It.IsAny<FeatureContext>(), false, It.IsAny<CancellationToken>()))
            .Returns<string, FeatureContext, bool, CancellationToken>((key, context, value, ct) =>
            {
                contexts.Enqueue(context);
                return realEvaluation.GetValueAsync(key, context, value, ct);
            });
        evaluation.Setup(service => service.GetEnabledFeaturesAsync(It.IsAny<FeatureContext>(), It.IsAny<CancellationToken>()))
            .Returns<FeatureContext, CancellationToken>((context, ct) =>
            {
                contexts.Enqueue(context);
                return realEvaluation.GetEnabledFeaturesAsync(context, ct);
            });
        builder.Services.AddSingleton(evaluation.Object);
        var handler = new FeatureOperationCommandHandler(evaluation.Object, Mock.Of<ICapabilityService>(), NullLogger<FeatureOperationCommandHandler>.Instance,
            actorAccessor);
        var sender = new Mock<ISender>();
        sender.Setup(service => service.Send(It.IsAny<EvaluateFeatureOperationCommand>(), It.IsAny<CancellationToken>()))
            .Returns<EvaluateFeatureOperationCommand, CancellationToken>(handler.Handle);
        sender.Setup(service => service.Send(It.IsAny<BulkEvaluateFeatureOperationCommand>(), It.IsAny<CancellationToken>()))
            .Returns<BulkEvaluateFeatureOperationCommand, CancellationToken>(handler.Handle);
        builder.Services.AddSingleton(sender.Object);
        var app = builder.Build();
        app.UseAuthentication();
        if (resolveActor)
        {
            app.UseMiddleware<ActorContextMiddleware>();
        }
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private static HttpClient AuthenticatedClient(WebApplication app, string subject, Guid? tenant, bool systemAdmin = false, string actorType = "user")
    {
        var claims = new List<Claim>
        {
            new("actor_type", actorType), new("permission", "features:read"),
            new("subscription_plan", "trusted-plan")
        };
        if (subject != "missing") claims.Add(new Claim("sub", subject));
        if (tenant.HasValue) claims.Add(new Claim("tenant_id", tenant.Value.ToString()));
        if (systemAdmin) claims.Add(new Claim("role", "SystemAdmin"));
        var token = new JwtSecurityToken("feature-test-issuer", "feature-test-audience", claims,
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), "HS256"));
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("http-agent");
        return client;
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string endpoint, FeatureContext context)
    {
        var query = $"?environment={context.Environment}";
        if (context.UserId.HasValue) query += $"&userId={context.UserId}";
        if (context.TenantId.HasValue) query += $"&tenantId={context.TenantId}";
        return endpoint switch
        {
            "evaluate" => client.PostAsJsonAsync("/v1.0/features/:evaluate", new FeatureEvaluationRequest { FeatureKey = "flag", Context = context }),
            "bulk" => client.PostAsJsonAsync("/v1.0/features/:evaluate-bulk", new BulkEvaluationRequest { FeatureKeys = ["flag"], Context = context }),
            "value" => client.GetAsync("/v1.0/features/flag/value" + query),
            "enabled" => client.GetAsync("/v1.0/features/enabled" + query),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
        };
    }

    private sealed class RuntimeControllerPart : ApplicationPart, IApplicationPartTypeProvider
    {
        public override string Name => nameof(RuntimeControllerPart);
        public IEnumerable<TypeInfo> Types => [typeof(FeatureFlagsController).GetTypeInfo()];
    }
}
