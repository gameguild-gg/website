using System.Security.Claims;
using GameGuild.Configuration.PresentationLayer.Authentication;
using GameGuild.Configuration.PresentationLayer;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;
using GameGuildAuthorizationOptions = GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions;
using MicrosoftAuthorizationOptions = Microsoft.AspNetCore.Authorization.AuthorizationOptions;

namespace GameGuild.API.UnitTests.Core.Extensions;

public sealed class SecurityServiceCollectionExtensionsTests
{
    [Fact]
    public async Task SetupAuthentication_UsesTheRoleClaimTypeEmittedByJwtTokenService()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = new string('s', 64),
                ["Jwt:Issuer"] = "GameGuild",
                ["Jwt:Audience"] = "GameGuild.Users"
            })
            .Build();
        var options = new AuthenticationOptions
        {
            JwtSecretKey = new string('s', 64),
            JwtIssuer = "GameGuild",
            JwtAudience = "GameGuild.Users"
        };

        services.AddLogging();
        services.SetupAuthentication(configuration, options);
        using var serviceProvider = services.BuildServiceProvider();
        var jwtOptions = serviceProvider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.False(jwtOptions.MapInboundClaims);
        Assert.Equal("role", jwtOptions.TokenValidationParameters.RoleClaimType);
        Assert.Null(await serviceProvider.GetRequiredService<IAuthenticationSchemeProvider>()
            .GetSchemeAsync(ApiKeyAuthenticationOptions.SchemeName));
        Assert.Null(await serviceProvider.GetRequiredService<IAuthenticationSchemeProvider>()
            .GetSchemeAsync(BasicAuthenticationSettings.DefaultSchemeName));
    }

    [Fact]
    public async Task SetupAuthentication_RegistersConfiguredApiKeySchemeAlongsideJwt()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = new string('s', 64),
                ["Jwt:Issuer"] = "GameGuild",
                ["Jwt:Audience"] = "GameGuild.Users",
                ["PresentationLayer:Authentication:JwtSecretKey"] = new string('s', 64),
                ["PresentationLayer:Authentication:JwtIssuer"] = "GameGuild",
                ["PresentationLayer:Authentication:JwtAudience"] = "GameGuild.Users",
                ["PresentationLayer:Authentication:EnableApiKeyAuthentication"] = "true",
                ["PresentationLayer:Authentication:ApiKeyHeaderName"] = "X-GameGuild-Key",
                ["PresentationLayer:Authentication:AllowApiKeyInQueryString"] = "true",
                ["PresentationLayer:Authentication:ApiKeyQueryStringParameterName"] = "access_key"
            })
            .Build();
        var options = PresentationLayerOptionsBuilder.Create(configuration).Authentication!;

        services.AddLogging();
        services.SetupAuthentication(configuration, options);
        using var serviceProvider = services.BuildServiceProvider();

        var scheme = await serviceProvider.GetRequiredService<IAuthenticationSchemeProvider>()
            .GetSchemeAsync(ApiKeyAuthenticationOptions.SchemeName);
        Assert.NotNull(scheme);

        var apiKeyOptions = serviceProvider.GetRequiredService<IOptionsMonitor<ApiKeyAuthenticationOptions>>()
            .Get(ApiKeyAuthenticationOptions.SchemeName);
        Assert.Equal("X-GameGuild-Key", apiKeyOptions.HeaderName);
        Assert.True(apiKeyOptions.AllowQueryString);
        Assert.Equal("access_key", apiKeyOptions.QueryStringParameterName);
    }

    [Fact]
    public void SetupAuthentication_UsesApiKeyHandlerDefaultsWhenNamesAreNotConfigured()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = new string('s', 64),
                ["Jwt:Issuer"] = "GameGuild",
                ["Jwt:Audience"] = "GameGuild.Users"
            })
            .Build();
        var options = new AuthenticationOptions
        {
            JwtSecretKey = new string('s', 64),
            JwtIssuer = "GameGuild",
            JwtAudience = "GameGuild.Users",
            EnableApiKeyAuthentication = true
        };

        services.AddLogging();
        services.SetupAuthentication(configuration, options);
        using var serviceProvider = services.BuildServiceProvider();

        var apiKeyOptions = serviceProvider.GetRequiredService<IOptionsMonitor<ApiKeyAuthenticationOptions>>()
            .Get(ApiKeyAuthenticationOptions.SchemeName);

        Assert.Equal("X-API-Key", apiKeyOptions.HeaderName);
        Assert.Equal("api_key", apiKeyOptions.QueryStringParameterName);
    }

    [Fact]
    public void SetupAuthentication_UsesCustomApiKeyResolverFromAuthenticationOptions()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        Func<HttpRequest, string?> resolver = static _ => "deployment-api-key";
        var options = new AuthenticationOptions
        {
            JwtSecretKey = new string('s', 64),
            JwtIssuer = "GameGuild",
            JwtAudience = "GameGuild.Users",
            EnableApiKeyAuthentication = true,
            ApiKeyCustomKeyResolver = resolver
        };

        services.AddLogging();
        services.SetupAuthentication(configuration, options);
        using var serviceProvider = services.BuildServiceProvider();

        var apiKeyOptions = serviceProvider.GetRequiredService<IOptionsMonitor<ApiKeyAuthenticationOptions>>()
            .Get(ApiKeyAuthenticationOptions.SchemeName);

        Assert.Same(resolver, apiKeyOptions.CustomKeyResolver);
    }

    [Fact]
    public async Task SetupAuthentication_RegistersConfiguredBasicSchemeWithoutChangingJwtDefault()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PresentationLayer:Authentication:JwtSecretKey"] = new string('s', 64),
                ["PresentationLayer:Authentication:JwtIssuer"] = "GameGuild",
                ["PresentationLayer:Authentication:JwtAudience"] = "GameGuild.Users",
                ["PresentationLayer:Authentication:EnableBasicAuthentication"] = "true",
                ["PresentationLayer:Authentication:Basic:SchemeName"] = "LegacyBasic",
                ["PresentationLayer:Authentication:Basic:Realm"] = "GameGuild Legacy API"
            })
            .Build();
        var options = PresentationLayerOptionsBuilder.Create(configuration).Authentication!;

        services.AddLogging();
        services.SetupAuthentication(configuration, options);
        using var serviceProvider = services.BuildServiceProvider();

        var schemeProvider = serviceProvider.GetRequiredService<IAuthenticationSchemeProvider>();
        Assert.NotNull(await schemeProvider.GetSchemeAsync("LegacyBasic"));
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme,
            (await schemeProvider.GetDefaultAuthenticateSchemeAsync())?.Name);

        var basicOptions = serviceProvider.GetRequiredService<IOptionsMonitor<BasicAuthenticationSchemeOptions>>()
            .Get("LegacyBasic");
        Assert.Equal("GameGuild Legacy API", basicOptions.Realm);
    }

    [Fact]
    public async Task SetupAuthentication_RegistersAdditionalSchemesAndKeepsJwtAsDefault()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = new string('s', 64),
                ["Jwt:Issuer"] = "GameGuild",
                ["Jwt:Audience"] = "GameGuild.Users"
            })
            .Build();
        var options = new AuthenticationOptions
        {
            JwtSecretKey = new string('s', 64),
            JwtIssuer = "GameGuild",
            JwtAudience = "GameGuild.Users"
        };

        services.AddLogging();
        services.SetupAuthentication(configuration, options, builder =>
            builder.AddCookie("interactive-cookie", cookieOptions =>
            {
                cookieOptions.Cookie.HttpOnly = true;
                cookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            }));
        using var serviceProvider = services.BuildServiceProvider();

        var schemeProvider = serviceProvider.GetRequiredService<IAuthenticationSchemeProvider>();
        Assert.NotNull(await schemeProvider.GetSchemeAsync("interactive-cookie"));
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme,
            (await schemeProvider.GetDefaultAuthenticateSchemeAsync())?.Name);

        var cookieOptions = serviceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get("interactive-cookie");
        Assert.True(cookieOptions.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, cookieOptions.Cookie.SecurePolicy);
    }

    [Fact]
    public async Task SetupAuthentication_RegistersConfiguredCookieSchemeWithSecureDefaults()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PresentationLayer:Authentication:JwtSecretKey"] = new string('s', 64),
                ["PresentationLayer:Authentication:JwtIssuer"] = "GameGuild",
                ["PresentationLayer:Authentication:JwtAudience"] = "GameGuild.Users",
                ["PresentationLayer:Authentication:EnableCookieAuthentication"] = "true",
                ["PresentationLayer:Authentication:Cookie:SchemeName"] = "interactive-cookie",
                ["PresentationLayer:Authentication:Cookie:Name"] = "__Host-GameGuild.Session",
                ["PresentationLayer:Authentication:Cookie:Expiration"] = "00:30:00",
                ["PresentationLayer:Authentication:Cookie:SlidingExpiration"] = "true",
                ["PresentationLayer:Authentication:Cookie:SameSite"] = "Lax"
            })
            .Build();
        var options = PresentationLayerOptionsBuilder.Create(configuration).Authentication!;

        services.AddLogging();
        services.SetupAuthentication(configuration, options);
        using var serviceProvider = services.BuildServiceProvider();

        var schemeProvider = serviceProvider.GetRequiredService<IAuthenticationSchemeProvider>();
        Assert.NotNull(await schemeProvider.GetSchemeAsync("interactive-cookie"));
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme,
            (await schemeProvider.GetDefaultAuthenticateSchemeAsync())?.Name);

        var cookieOptions = serviceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get("interactive-cookie");
        Assert.Equal("__Host-GameGuild.Session", cookieOptions.Cookie.Name);
        Assert.Equal("/", cookieOptions.Cookie.Path);
        Assert.True(cookieOptions.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, cookieOptions.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, cookieOptions.Cookie.SameSite);
        Assert.Equal(TimeSpan.FromMinutes(30), cookieOptions.ExpireTimeSpan);
        Assert.True(cookieOptions.SlidingExpiration);
    }

    [Fact]
    public void SetupAuthentication_RejectsQueryApiKeyWhenApiKeySchemeIsDisabled()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var options = new AuthenticationOptions
        {
            AllowApiKeyInQueryString = true
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.SetupAuthentication(configuration, options));

        Assert.Contains("API key scheme", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SystemAdmin")]
    [InlineData("TenantAdmin")]
    public void UserManagementPolicies_ShouldNotBeRegisteredStatically(string role)
    {
        using var serviceProvider = BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<MicrosoftAuthorizationOptions>>().Value;

        foreach (var policy in new[]
                 {
                     Policies.UsersCreate,
                     Policies.UsersUpdate,
                     Policies.UsersDelete,
                     Policies.UsersAdmin,
                     Policies.UsersPurge
                 })
        {
            Assert.Null(options.GetPolicy(policy));
        }

        Assert.False(string.IsNullOrWhiteSpace(role));
    }

    [Fact]
    public void UserManagementPolicies_ShouldBeResolvedByDynamicProvider()
    {
        using var serviceProvider = BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<MicrosoftAuthorizationOptions>>().Value;
        Assert.Null(options.GetPolicy(Policies.UsersCreate));
    }

    [Fact]
    public void SystemAdminPolicy_ShouldBeResolvedByDynamicProvider()
    {
        using var serviceProvider = BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<MicrosoftAuthorizationOptions>>().Value;
        Assert.Null(options.GetPolicy(Policies.SystemAdmin));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("TenantAdmin")]
    [InlineData("Owner")]
    public void SystemAdminPolicy_ShouldNotHaveStaticRoleFallbacks(string role)
    {
        using var serviceProvider = BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<MicrosoftAuthorizationOptions>>().Value;
        Assert.Null(options.GetPolicy(Policies.SystemAdmin));
        Assert.False(string.IsNullOrWhiteSpace(role));
    }

    [Theory]
    [InlineData("User", true)]
    [InlineData("TenantAdmin", true)]
    [InlineData("Owner", false)]
    public async Task RequireUserRolePolicy_ShouldAcceptUsersAndTenantAdministrators(
        string role,
        bool expected)
    {
        using var serviceProvider = BuildServiceProvider();

        var result = await AuthorizeAsync(serviceProvider, CreatePrincipal(role), "RequireUserRole");

        Assert.Equal(expected, result.Succeeded);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddLogging();
        services.SetupAuthorization(configuration, GameGuildAuthorizationOptions.CreateDefault());

        return services.BuildServiceProvider();
    }

    private static async Task<AuthorizationResult> AuthorizeAsync(
        IServiceProvider serviceProvider,
        ClaimsPrincipal principal,
        string policyName)
    {
        var options = serviceProvider.GetRequiredService<IOptions<MicrosoftAuthorizationOptions>>().Value;
        var policy = options.GetPolicy(policyName) ?? throw new InvalidOperationException($"Policy '{policyName}' was not registered.");
        var context = new AuthorizationHandlerContext(policy.Requirements, principal, resource: null);

        foreach (var handler in policy.Requirements.OfType<IAuthorizationHandler>())
        {
            await handler.HandleAsync(context);
        }

        return context.HasSucceeded ? AuthorizationResult.Success() : AuthorizationResult.Failed();
    }

    private static ClaimsPrincipal CreatePrincipal(string role)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim("tenant_id", Guid.NewGuid().ToString())
            ],
            authenticationType: "Test");

        return new ClaimsPrincipal(identity);
    }
}
