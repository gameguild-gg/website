using System.Security.Claims;
using FluentAssertions;
using GameGuild.API;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using AuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;
using AuthorizationOptions = GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions;
using CorsOptions = GameGuild.Configuration.PresentationLayer.CORS.CorsOptions;
using FrameworkAuthenticationOptions = Microsoft.AspNetCore.Authentication.AuthenticationOptions;
using Xunit;

namespace GameGuild.API.UnitTests.Core;

public sealed class SecurityServiceCollectionExtensionsTests
{
    [Fact]
    public void SetupAuthentication_UsesValidatedJwtContractInProduction()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("Production");

        services.SetupAuthentication(configuration, CreateAuthenticationOptions());

        using var provider = services.BuildServiceProvider();
        var bearer = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        bearer.RequireHttpsMetadata.Should().BeTrue();
        bearer.TokenValidationParameters.ValidIssuer.Should().Be("ProductIssuer");
        bearer.TokenValidationParameters.ValidAudience.Should().Be("ProductAudience");
        bearer.TokenValidationParameters.ClockSkew.Should().Be(TimeSpan.FromSeconds(15));
        bearer.TokenValidationParameters.RoleClaimType.Should().Be("role");
        var authentication = provider.GetRequiredService<IOptions<FrameworkAuthenticationOptions>>().Value;
        authentication.DefaultAuthenticateScheme.Should().Be(JwtBearerDefaults.AuthenticationScheme);
        authentication.DefaultChallengeScheme.Should().Be(JwtBearerDefaults.AuthenticationScheme);
        authentication.DefaultScheme.Should().Be(JwtBearerDefaults.AuthenticationScheme);
    }

    [Fact]
    public void SetupAuthentication_WhenDisabledDoesNotRegisterAuthenticationServices()
    {
        var services = new ServiceCollection();
        var options = new AuthenticationOptions { EnableAuthentication = false };

        var result = services.SetupAuthentication(new ConfigurationBuilder().Build(), options);

        result.Should().BeSameAs(services);
        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(IAuthenticationService));
    }

    [Fact]
    public void SetupAuthentication_WhenDisabledThroughConfiguration_ShouldBindOptionsAndReturn()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:EnableAuthentication"] = "false"
            })
            .Build();

        services.SetupAuthentication(configuration, null);

        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(IAuthenticationService));
    }

    [Fact]
    public void SetupAuthentication_WhenAuthorizationIsDisabled_ShouldNotRegisterAuthorizationServices()
    {
        var services = new ServiceCollection();
        var options = CreateAuthenticationOptions();
        options.EnableAuthorization = false;

        services.SetupAuthentication(CreateConfiguration("Production"), options);

        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(IAuthorizationService));
    }

    [Fact]
    public void SetupAuthentication_AllowsHttpMetadataOnlyInDevelopment()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("Development");

        services.SetupAuthentication(configuration, CreateAuthenticationOptions());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .RequireHttpsMetadata.Should().BeFalse();
    }

    [Fact]
    public void SetupAuthentication_UsesDotnetEnvironmentWhenAspNetEnvironmentIsAbsent()
    {
        var values = new Dictionary<string, string?>
        {
            ["DOTNET_ENVIRONMENT"] = "Testing",
            ["Jwt:Secret"] = "0123456789abcdef0123456789abcdef",
            ["Jwt:Issuer"] = "ProductIssuer",
            ["Jwt:Audience"] = "ProductAudience"
        };
        var services = new ServiceCollection();

        services.SetupAuthentication(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            CreateAuthenticationOptions());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .RequireHttpsMetadata.Should().BeFalse();
    }

    [Fact]
    public void SetupAuthentication_AcceptsTestEnvironmentAlias()
    {
        var services = new ServiceCollection();

        services.SetupAuthentication(CreateConfiguration("Test"), CreateAuthenticationOptions());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .RequireHttpsMetadata.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task SetupAuthentication_EventCallbacks_ShouldHandleOptionalLoggingAndPrincipal(
        bool registerLogging,
        bool includePrincipal)
    {
        var services = new ServiceCollection();
        if (registerLogging)
            services.AddLogging();
        services.SetupAuthentication(CreateConfiguration("Testing"), CreateAuthenticationOptions());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var failure = new AuthenticationFailedContext(httpContext, scheme, options)
        {
            Exception = new SecurityTokenException("invalid")
        };
        var validated = new TokenValidatedContext(httpContext, scheme, options)
        {
            Principal = includePrincipal
                ? new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-42")], "test"))
                : null
        };

        await options.Events.OnAuthenticationFailed(failure);
        await options.Events.OnTokenValidated(validated);
    }

    [Fact]
    public async Task SetupAuthentication_TokenValidatedCallback_ShouldHandlePrincipalWithoutSubjectClaim()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.SetupAuthentication(CreateConfiguration("Testing"), CreateAuthenticationOptions());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var context = new TokenValidatedContext(
            new DefaultHttpContext { RequestServices = provider },
            new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)),
            options)
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity([], "test"))
        };

        await options.Events.OnTokenValidated(context);
    }

    [Fact]
    public async Task SetupAuthentication_TokenValidatedCallback_ShouldNotRequireLogging()
    {
        var services = new ServiceCollection();
        services.SetupAuthentication(CreateConfiguration("Testing"), CreateAuthenticationOptions());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var context = new TokenValidatedContext(
            new DefaultHttpContext { RequestServices = EmptyServiceProvider.Instance },
            new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)),
            options)
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-42")], "test"))
        };

        var action = () => options.Events.OnTokenValidated(context);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public void SetupCors_WhenListsAreEmpty_ShouldAllowAnyRequestComponent()
    {
        var services = new ServiceCollection();

        services.SetupCors(new ConfigurationBuilder().Build(), null);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>>().Value;
        var policy = options.GetPolicy(options.DefaultPolicyName);
        policy.Should().NotBeNull();
        policy!.AllowAnyOrigin.Should().BeTrue();
        policy.Methods.Should().ContainSingle().Which.Should().Be("*");
        policy.Headers.Should().ContainSingle().Which.Should().Be("*");
    }

    [Fact]
    public void SetupCors_WhenListsAreConfigured_ShouldRestrictRequestComponents()
    {
        var services = new ServiceCollection();
        var configured = new CorsOptions
        {
            AllowedOrigins = ["https://example.test"],
            AllowedMethods = ["GET", "POST"],
            AllowedHeaders = ["Authorization", "Content-Type"]
        };

        services.SetupCors(new ConfigurationBuilder().Build(), configured);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>>().Value;
        var policy = options.GetPolicy(options.DefaultPolicyName);
        policy.Should().NotBeNull();
        policy!.AllowAnyOrigin.Should().BeFalse();
        policy.Origins.Should().Equal("https://example.test");
        policy.Methods.Should().Equal("GET", "POST");
        policy.Headers.Should().Equal("Authorization", "Content-Type");
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType) => null;
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SystemAdmin")]
    public async Task SetupAuthorization_AdminPoliciesAcceptSupportedAdministrativeRoles(string role)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.SetupAuthorization(new ConfigurationBuilder().Build(), new AuthorizationOptions());
        using var provider = services.BuildServiceProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("role", role)], "test", "sub", "role"));

        var result = await EvaluatePolicyAsync(provider, principal, "RequireAdminRole");

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("tenant_id")]
    [InlineData("TenantId")]
    public async Task SetupAuthorization_TenantPoliciesAcceptSupportedTenantClaims(string claimType)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.SetupAuthorization(new ConfigurationBuilder().Build(), new AuthorizationOptions());
        using var provider = services.BuildServiceProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim(claimType, Guid.NewGuid().ToString())],
            "test"));

        var result = await EvaluatePolicyAsync(provider, principal, "TenantMember");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetupAuthorization_TenantAdminRequiresTenantClaimEvenForTenantAdministrator()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.SetupAuthorization(new ConfigurationBuilder().Build(), new AuthorizationOptions());
        using var provider = services.BuildServiceProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("role", "TenantAdmin")],
            "test",
            "sub",
            "role"));

        var result = await EvaluatePolicyAsync(provider, principal, "TenantAdmin");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetupAuthorization_TenantAdminAcceptsTenantAdministratorWithTenantClaim()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.SetupAuthorization(new ConfigurationBuilder().Build(), null);
        using var provider = services.BuildServiceProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("tenant_id", Guid.NewGuid().ToString()), new Claim("role", "TenantAdmin")],
            "test",
            "sub",
            "role"));

        var result = await EvaluatePolicyAsync(provider, principal, "TenantAdmin");

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task SetupAuthorization_SecureAdminRequiresAdministrativeRoleAndMfa(
        bool mfaVerified,
        bool expected)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.SetupAuthorization(new ConfigurationBuilder().Build(), new AuthorizationOptions());
        using var provider = services.BuildServiceProvider();
        var claims = new List<Claim> { new("role", "Admin") };
        if (mfaVerified)
            claims.Add(new Claim("mfa_verified", "true"));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", "sub", "role"));

        var result = await EvaluatePolicyAsync(provider, principal, "SecureAdmin");

        result.Should().Be(expected);
    }

    private static async Task<bool> EvaluatePolicyAsync(
        IServiceProvider provider,
        ClaimsPrincipal principal,
        string policyName)
    {
        var options = provider
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Authorization.AuthorizationOptions>>()
            .Value;
        var policy = options.GetPolicy(policyName)
                     ?? throw new InvalidOperationException($"Policy '{policyName}' was not registered.");
        var context = new AuthorizationHandlerContext(policy.Requirements, principal, resource: null);

        foreach (var handler in policy.Requirements.OfType<IAuthorizationHandler>())
        {
            await handler.HandleAsync(context);
        }

        return context.HasSucceeded;
    }

    private static IConfiguration CreateConfiguration(string environmentName) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = environmentName,
            ["Jwt:Secret"] = "0123456789abcdef0123456789abcdef",
            ["Jwt:Issuer"] = "ProductIssuer",
            ["Jwt:Audience"] = "ProductAudience",
            ["Jwt:ClockSkewSeconds"] = "15"
        }).Build();

    private static AuthenticationOptions CreateAuthenticationOptions() => new()
    {
        EnableAuthentication = true,
        EnableAuthorization = true,
        JwtSecretKey = "0123456789abcdef0123456789abcdef",
        JwtIssuer = "ProductIssuer",
        JwtAudience = "ProductAudience"
    };
}
