using System.Security.Claims;
using System.Text;
using FluentAssertions;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.API;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using AuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;
using AuthorizationOptions = GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions;
using AuthorizationClaimTransformationOptions = GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationClaimTransformationOptions;
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
    }

    [Fact]
    public void SetupAuthentication_UsesJwtValuesFromAuthenticationOptions()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration("Production");
        var authenticationOptions = CreateAuthenticationOptions();
        authenticationOptions.JwtSecretKey = new string('o', 64);
        authenticationOptions.JwtIssuer = "OptionsIssuer";
        authenticationOptions.JwtAudience = "OptionsAudience";
        authenticationOptions.JwtExpiration = TimeSpan.FromMinutes(15);
        authenticationOptions.RefreshTokenExpirationDays = 14;

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.SetupAuthentication(configuration, authenticationOptions);

        using var provider = services.BuildServiceProvider();
        var tokenValidationParameters = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme)
            .TokenValidationParameters;

        tokenValidationParameters.ValidIssuer.Should().Be("OptionsIssuer");
        tokenValidationParameters.ValidAudience.Should().Be("OptionsAudience");
        var signingKey = tokenValidationParameters.IssuerSigningKey.Should()
            .BeOfType<SymmetricSecurityKey>().Subject;
        signingKey.Key.Should().Equal(Encoding.UTF8.GetBytes(authenticationOptions.JwtSecretKey));

        var issuedTokenOptions = provider.GetRequiredService<IOptions<JwtOptions>>().Value;
        issuedTokenOptions.SecretKey.Should().Be(authenticationOptions.JwtSecretKey);
        issuedTokenOptions.Issuer.Should().Be(authenticationOptions.JwtIssuer);
        issuedTokenOptions.Audience.Should().Be(authenticationOptions.JwtAudience);
        issuedTokenOptions.AccessTokenExpirationMinutes.Should().Be(15);
        issuedTokenOptions.RefreshTokenExpirationDays.Should().Be(14);
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
            new DefaultHttpContext { RequestServices = provider },
            new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)),
            options)
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user-42")], "test"))
        };

        var action = () => options.Events.OnTokenValidated(context);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetupAuthentication_TokenValidatedCallback_ShouldAuditEachJwtOnlyOnce()
    {
        var auditEventSink = new Mock<IAuthenticationAuditEventSink>();
        auditEventSink
            .Setup(x => x.RecordAsync(It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(auditEventSink.Object);
        services.SetupAuthentication(CreateConfiguration("Testing"), CreateAuthenticationOptions());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        TokenValidatedContext CreateContext() => new(
            new DefaultHttpContext { RequestServices = provider },
            scheme,
            options)
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString()),
                new Claim("jti", Guid.NewGuid().ToString()),
                new Claim(JwtClaimTypes.TenantId, tenantId.ToString()),
                new Claim(JwtClaimTypes.SessionId, sessionId.ToString())
            ],
            "test"))
        };

        var firstContext = CreateContext();
        var tokenId = firstContext.Principal!.FindFirst("jti")!.Value;
        var secondContext = new TokenValidatedContext(
            new DefaultHttpContext { RequestServices = provider },
            scheme,
            options)
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString()),
                new Claim("jti", tokenId),
                new Claim(JwtClaimTypes.TenantId, tenantId.ToString()),
                new Claim(JwtClaimTypes.SessionId, sessionId.ToString())
            ],
            "test"))
        };

        await options.Events.OnTokenValidated(firstContext);
        await options.Events.OnTokenValidated(secondContext);

        auditEventSink.Verify(x => x.RecordAsync(
            It.Is<AuthenticationAuditEvent>(auditEvent =>
                auditEvent.ActionType == "Authentication.JwtTokenValidated" &&
                auditEvent.Success &&
                auditEvent.Method == "JWT" &&
                auditEvent.UserId == userId &&
                auditEvent.TenantId == tenantId &&
                auditEvent.SessionId == sessionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetupAuthentication_AuthenticationFailedCallback_ShouldAuditSafeFailureDetails()
    {
        var auditEventSink = new Mock<IAuthenticationAuditEventSink>();
        auditEventSink
            .Setup(x => x.RecordAsync(It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(auditEventSink.Object);
        services.SetupAuthentication(CreateConfiguration("Testing"), CreateAuthenticationOptions());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var context = new AuthenticationFailedContext(
            new DefaultHttpContext { RequestServices = provider },
            new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)),
            options)
        {
            Exception = new SecurityTokenException("sensitive token content")
        };

        await options.Events.OnAuthenticationFailed(context);

        auditEventSink.Verify(x => x.RecordAsync(
            It.Is<AuthenticationAuditEvent>(auditEvent =>
                auditEvent.ActionType == "Authentication.JwtValidationFailed" &&
                !auditEvent.Success &&
                auditEvent.Method == "JWT" &&
                auditEvent.ErrorMessage == nameof(SecurityTokenException)),
            It.IsAny<CancellationToken>()), Times.Once);
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

    [Fact]
    public void SetupAuthorization_TenantMemberIsDatabaseBacked()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.SetupAuthorization(new ConfigurationBuilder().Build(), new AuthorizationOptions());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Authorization.AuthorizationOptions>>().Value;
        options.GetPolicy(Policies.TenantMember).Should().BeNull();
    }

    [Fact]
    public void SetupAuthorization_TenantAdminIsDatabaseBacked()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.SetupAuthorization(new ConfigurationBuilder().Build(), new AuthorizationOptions());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Authorization.AuthorizationOptions>>().Value;
        options.GetPolicy(Policies.TenantAdmin).Should().BeNull();
    }

    [Fact]
    public void SetupAuthorization_SecureAdminIsDatabaseBacked()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.SetupAuthorization(new ConfigurationBuilder().Build(), new AuthorizationOptions());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Authorization.AuthorizationOptions>>().Value;
        options.GetPolicy(Policies.SecureAdmin).Should().BeNull();
    }

    [Fact]
    public async Task SetupAuthorization_AppliesConfiguredAllowlistedClaimTransformationsOnce()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authorization:ClaimTransformations:0:SourceClaimType"] = "groups",
            ["Authorization:ClaimTransformations:0:TargetClaimType"] = "role",
            ["Authorization:ClaimTransformations:0:ValueMappings:gameguild-admins"] = "Admin"
        }).Build();
        services.SetupAuthorization(configuration, options: null);

        using var provider = services.BuildServiceProvider();
        var transformation = provider.GetRequiredService<IClaimsTransformation>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("groups", "gameguild-admins"), new Claim("groups", "unmapped")],
            "test",
            "sub",
            "role"));

        var transformed = await transformation.TransformAsync(principal);
        await transformation.TransformAsync(transformed);

        transformed.FindAll("role").Select(claim => claim.Value).Should().ContainSingle().Which.Should().Be("Admin");
        (await EvaluatePolicyAsync(provider, transformed, "RequireAdminRole")).Should().BeTrue();
    }

    [Fact]
    public void AuthorizationOptions_RejectsClaimTransformationsWithoutExplicitMappings()
    {
        var options = new AuthorizationOptions
        {
            ClaimTransformations =
            [
                new AuthorizationClaimTransformationOptions
                {
                    SourceClaimType = "groups",
                    TargetClaimType = ClaimTypes.Role
                }
            ]
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*at least one non-empty value mapping*");
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
