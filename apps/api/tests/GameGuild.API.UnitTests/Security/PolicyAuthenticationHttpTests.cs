using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using GameGuild.API.Core.Security;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.API.UnitTests.Security;

/// <summary>Composition controls with explicit synthetic credentials. Real API-key persistence
/// and middleware acceptance are covered separately by ApiKeyPolicyPostgreSqlHttpTests.</summary>
public sealed class PolicyAuthenticationHttpTests
{
    [Theory]
    [InlineData("default", "ApiKey")]
    [InlineData("named", "ApiKey")]
    [InlineData("explicit", "ApiKey")]
    [InlineData("embedded", "ApiKey")]
    [InlineData("fallback", "ApiKey")]
    [InlineData("cookie", "Cookies")]
    [InlineData("bearer", "Bearer")]
    public async Task SelectedCredentialIsEstablishedBeforeContextCapture(string mode, string scheme)
    {
        await using var app = await CreateApp(mode);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-" + scheme, "owner-a");
        using var response = await client.GetAsync("/protected");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("owner-a|owner-a", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("default")]
    [InlineData("named")]
    [InlineData("explicit")]
    [InlineData("embedded")]
    [InlineData("fallback")]
    public async Task MissingSelectedCredentialNeverDispatches(string mode)
    {
        await using var app = await CreateApp(mode);
        using var response = await app.GetTestClient().GetAsync("/protected");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MerelySupplyingAKeyDoesNotBroadenBearerOnlyAuthorization()
    {
        await using var app = await CreateApp("bearer");
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-ApiKey", "owner-a");
        using var response = await client.GetAsync("/protected");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("owner-a")]
    [InlineData("owner-b")]
    public async Task MultipleSuccessfulCredentialsCannotCombineSubjectsOrScopes(string secondOwner)
    {
        await using var app = await CreateApp("mixed");
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Bearer", "owner-a");
        client.DefaultRequestHeaders.Add("X-Test-ApiKey", secondOwner);
        using var response = await client.GetAsync("/protected");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DatabasePolicyCannotReplaceTheAlreadyEstablishedActor(bool initiallyAuthenticated)
    {
        await using var app = await CreateApp("database");
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-ApiKey", "owner-b");
        if (initiallyAuthenticated)
        {
            client.DefaultRequestHeaders.Add("X-Test-Bearer", "owner-a");
        }
        using var response = await client.GetAsync("/protected");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, app.Services.GetRequiredService<DeferredPolicyProvider>().ResolvedAfterContext);
    }

    [Fact]
    public async Task DatabasePolicyCanUseTheSameEstablishedBearer()
    {
        await using var app = await CreateApp("database-bearer");
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Bearer", "owner-a");
        using var response = await client.GetAsync("/protected");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("owner-a|owner-a", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, app.Services.GetRequiredService<DeferredPolicyProvider>().ResolvedAfterContext);
    }

    [Fact]
    public async Task AnUnregisteredConfiguredSchemeFailsClosed()
    {
        await using var app = await CreateApp("default", registerApiKey: false);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-ApiKey", "owner-a");
        using var response = await client.GetAsync("/protected");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownPolicyNameFailsLoudlyInsteadOfDegradingToAuthenticatedUser()
    {
        await using var app = await CreateApp("unknown-policy");
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Bearer", "owner-a");
        // The test server rethrows the unhandled middleware exception; in production this
        // surfaces as a 500 instead of silently authorizing any authenticated user.
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync("/protected"));
        Assert.Contains("NoSuchPolicy", error.Message);
    }

    [Fact]
    public async Task AnonymousRecoveryRetainsNoActorAfterOptionalCredentialIsRejected()
    {
        await using var app = await CreateApp("public");
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Bearer", "stale-owner");
        using var response = await client.GetAsync("/protected");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("|", await response.Content.ReadAsStringAsync());
    }

    private static async Task<WebApplication> CreateApp(string mode, bool registerApiKey = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var authentication = builder.Services.AddAuthentication("Bearer")
            .AddScheme<AuthenticationSchemeOptions, SyntheticCredentialHandler>("Bearer", _ => { })
            .AddScheme<AuthenticationSchemeOptions, SyntheticCredentialHandler>("Cookies", _ => { });
        if (registerApiKey)
        {
            authentication.AddScheme<AuthenticationSchemeOptions, SyntheticCredentialHandler>("ApiKey", _ => { });
        }
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddAuthorization(options =>
        {
            if (mode is "default" or "mixed")
            {
                options.DefaultPolicy = BuildPolicy(mode == "mixed" ? ["Bearer", "ApiKey"] : ["ApiKey"]);
            }
            if (mode == "fallback")
            {
                options.FallbackPolicy = BuildPolicy(["ApiKey"]);
            }
            options.AddPolicy("StaticApiKey", BuildPolicy(["ApiKey"]));
        });
        builder.Services.AddSingleton<DeferredPolicyProvider>();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider>(services =>
            services.GetRequiredService<DeferredPolicyProvider>());
        builder.Services.AddTransient<IPolicyEvaluator, ActorBoundPolicyEvaluator>();
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseMiddleware<PolicyAuthenticationMiddleware>();
        app.Use(async (context, next) =>
        {
            // Models optional-token rejection before capture; full revocation behavior has native tests.
            if (mode == "public")
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
            context.Items["CapturedOwner"] = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            context.Items["TenantEstablished"] = true;
            await next(context);
        });
        app.UseAuthorization();
        var endpoint = app.MapGet("/protected", (HttpContext context) =>
            (string)context.Items["CapturedOwner"]! + "|" + context.User.FindFirstValue(ClaimTypes.NameIdentifier));
        switch (mode)
        {
            case "named": endpoint.RequireAuthorization("StaticApiKey"); break;
            case "explicit": endpoint.RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "ApiKey" }); break;
            case "cookie": endpoint.RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "Cookies" }); break;
            case "embedded": endpoint.RequireAuthorization(BuildPolicy(["ApiKey"])); break;
            case "database": endpoint.RequireAuthorization(Policies.UsersAdmin); break;
            case "database-bearer": endpoint.RequireAuthorization(Policies.UsersReadSelf); break;
            case "unknown-policy": endpoint.RequireAuthorization("NoSuchPolicy"); break;

            case "public": endpoint.AllowAnonymous(); break;
            case "fallback": break;
            default: endpoint.RequireAuthorization(); break;
        }
        await app.StartAsync();
        return app;
    }

    private static AuthorizationPolicy BuildPolicy(string[] schemes) =>
        new AuthorizationPolicyBuilder(schemes).RequireAuthenticatedUser().Build();

    private sealed class DeferredPolicyProvider(IOptions<AuthorizationOptions> options, IHttpContextAccessor accessor)
        : DefaultAuthorizationPolicyProvider(options)
    {
        public int ResolvedAfterContext { get; private set; }
        public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (policyName is not (Policies.UsersAdmin or Policies.UsersReadSelf))
            {
                return base.GetPolicyAsync(policyName);
            }
            if (accessor.HttpContext?.Items["TenantEstablished"] is not true)
            {
                throw new InvalidOperationException("Tenant-dependent policy resolved before context.");
            }
            ResolvedAfterContext++;
            return Task.FromResult<AuthorizationPolicy?>(BuildPolicy([policyName == Policies.UsersAdmin ? "ApiKey" : "Bearer"]));
        }
    }

    private sealed class SyntheticCredentialHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var owner = Request.Headers["X-Test-" + Scheme.Name].ToString();
            if (string.IsNullOrEmpty(owner))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, owner)], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
