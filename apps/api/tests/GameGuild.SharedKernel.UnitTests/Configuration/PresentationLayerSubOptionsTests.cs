using FluentAssertions;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Configuration.PresentationLayer.Authentication;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Configuration.PresentationLayer.CORS;
using GameGuild.Configuration.PresentationLayer.GraphQL;
using GameGuild.Configuration.PresentationLayer.HealthChecks;
using GameGuild.Configuration.PresentationLayer.OpenAPI;
using GameGuild.Configuration.PresentationLayer.RateLimiting;

namespace GameGuild.SharedKernel.UnitTests.Configuration;

public class CorsOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeCors()
    {
        CorsOptions.SectionName.Should().Be("Cors");
    }

    [Fact]
    public void Defaults_ShouldHaveEmptyArrays()
    {
        var options = new CorsOptions();
        options.AllowedOrigins.Should().BeEmpty();
        options.AllowedMethods.Should().BeEmpty();
        options.AllowedHeaders.Should().BeEmpty();
    }

    [Fact]
    public void CreateDefault_ShouldReturnInstance()
    {
        var options = CorsOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void Validate_WithWildcardOnly_ShouldNotThrow()
    {
        var options = new CorsOptions { AllowedOrigins = ["*"] };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithWildcardAndOthers_ShouldThrow()
    {
        var options = new CorsOptions { AllowedOrigins = ["*", "http://example.com"] };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*wildcard*");
    }

    [Fact]
    public void Validate_WithSpecificOrigins_ShouldNotThrow()
    {
        var options = new CorsOptions { AllowedOrigins = ["http://localhost:3000", "https://example.com"] };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }
}

public class AuthenticationOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeAuthentication()
    {
        AuthenticationOptions.SectionName.Should().Be("Authentication");
    }

    [Fact]
    public void Defaults_ShouldEnableAuthentication()
    {
        var options = new AuthenticationOptions();
        options.EnableAuthentication.Should().BeTrue();
        options.EnableAuthorization.Should().BeTrue();
        options.EnableDacAuthorization.Should().BeTrue();
        options.JwtSecretKey.Should().BeEmpty();
        options.JwtIssuer.Should().BeEmpty();
        options.JwtAudience.Should().BeEmpty();
        options.JwtExpiration.Should().Be(TimeSpan.FromHours(1));
        options.RefreshTokenExpirationDays.Should().Be(30);
    }

    [Fact]
    public void CreateDefault_ShouldReturnInstance()
    {
        var options = AuthenticationOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void Validate_WhenEnabled_WithEmptySecretKey_ShouldThrow()
    {
        var options = new AuthenticationOptions { EnableAuthentication = true };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*secret key*");
    }

    [Fact]
    public void Validate_WhenDisabled_ShouldNotThrow()
    {
        var options = new AuthenticationOptions { EnableAuthentication = false };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenEnabled_WithAllConfig_ShouldNotThrow()
    {
        var options = new AuthenticationOptions
        {
            EnableAuthentication = true,
            JwtSecretKey = "supersecretkey",
            JwtIssuer = "TestIssuer",
            JwtAudience = "TestAudience",
            JwtExpiration = TimeSpan.FromHours(1)
        };
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ZeroExpiration_ShouldThrow()
    {
        var options = new AuthenticationOptions
        {
            EnableAuthentication = true,
            JwtSecretKey = "key",
            JwtIssuer = "issuer",
            JwtAudience = "audience",
            JwtExpiration = TimeSpan.Zero
        };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*expiration*");
    }

    [Fact]
    public void Validate_NonPositiveRefreshTokenExpiration_ShouldThrow()
    {
        var options = new AuthenticationOptions
        {
            JwtSecretKey = "key",
            JwtIssuer = "issuer",
            JwtAudience = "audience",
            RefreshTokenExpirationDays = 0
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Refresh token expiration*");
    }
}

public class ExternalProviderOptionsTests
{
    [Fact]
    public void Validate_WithEnabledProviderAndSecureEndpointOverrides_DoesNotThrow()
    {
        var options = new ExternalProviderOptions
        {
            Providers = new Dictionary<string, OAuthProviderOptions>
            {
                ["github"] = new()
                {
                    Enabled = true,
                    ClientId = "client-id",
                    ClientSecret = "secret",
                    Scopes = ["read:user", "user:email"],
                    AuthorizationEndpoint = "https://github.example.test/oauth/authorize",
                    TokenEndpoint = "https://github.example.test/oauth/token",
                    UserInformationEndpoint = "https://github.example.test/api/user"
                }
            }
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithEnabledProviderMissingSecret_Throws()
    {
        var options = new ExternalProviderOptions
        {
            Providers = new Dictionary<string, OAuthProviderOptions>
            {
                ["google"] = new() { Enabled = true, ClientId = "client-id" }
            }
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*client secret is required*");
    }

    [Fact]
    public void Validate_WithPartialEndpointOverrides_Throws()
    {
        var options = new ExternalProviderOptions
        {
            Providers = new Dictionary<string, OAuthProviderOptions>
            {
                ["google"] = new()
                {
                    Enabled = true,
                    ClientId = "client-id",
                    ClientSecret = "secret",
                    AuthorizationEndpoint = "https://identity.example.test/authorize"
                }
            }
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*endpoint overrides*");
    }

    [Fact]
    public void Validate_WithMicrosoftScopesMissingRequiredOpenIdScopes_Throws()
    {
        var options = new ExternalProviderOptions
        {
            Providers = new Dictionary<string, OAuthProviderOptions>
            {
                ["microsoft"] = new()
                {
                    Enabled = true,
                    ClientId = "client-id",
                    ClientSecret = "secret",
                    Scopes = ["User.Read"]
                }
            }
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*openid, email, and profile*");
    }
}

public class OidcProviderOptionsTests
{
    private static OidcProviderOptions ValidProvider() => new()
    {
        Enabled = true,
        Authority = "https://login.corp.example.test",
        ClientId = "gameguild-web",
        ClientSecret = "secret",
        Scopes = ["openid", "profile", "email"],
        ClaimMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sub"] = "sub",
            ["email"] = "email",
            ["name"] = "name",
            ["email_verified"] = "email_verified"
        },
        EmailDomains = ["corp.example.test"]
    };

    [Fact]
    public void Validate_WithFullyConfiguredProvider_DoesNotThrow()
    {
        var act = () => ValidProvider().Validate("corp-idp");
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithDisabledProvider_RequiresNothing()
    {
        // Fail-closed default: an empty, disabled entry is valid configuration.
        var act = () => new OidcProviderOptions().Validate("corp-idp");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("http://login.corp.example.test")]
    [InlineData("https://user:pass@login.corp.example.test")]
    [InlineData("https://login.corp.example.test/?tenant=x")]
    [InlineData("https://login.corp.example.test/#fragment")]
    [InlineData("not-a-uri")]
    [InlineData("")]
    public void Validate_WithNonHttpsOrMalformedAuthority_Throws(string authority)
    {
        var provider = ValidProvider();
        provider.Authority = authority;

        var act = () => provider.Validate("corp-idp");

        act.Should().Throw<InvalidOperationException>().WithMessage("*authority*");
    }

    [Fact]
    public void Validate_EnabledWithoutClientId_Throws()
    {
        var provider = ValidProvider();
        provider.ClientId = " ";

        var act = () => provider.Validate("corp-idp");

        act.Should().Throw<InvalidOperationException>().WithMessage("*client ID*");
    }

    [Fact]
    public void Validate_EnabledWithoutClientSecret_Throws()
    {
        var provider = ValidProvider();
        provider.ClientSecret = null;

        var act = () => provider.Validate("corp-idp");

        act.Should().Throw<InvalidOperationException>().WithMessage("*client secret*");
    }

    [Fact]
    public void Validate_ScopesMissingOpenId_Throws()
    {
        var provider = ValidProvider();
        provider.Scopes = ["profile", "email"];

        var act = () => provider.Validate("corp-idp");

        act.Should().Throw<InvalidOperationException>().WithMessage("*include 'openid'*");
    }

    [Fact]
    public void Validate_DuplicateScopes_Throws()
    {
        var provider = ValidProvider();
        provider.Scopes = ["openid", "openid"];

        var act = () => provider.Validate("corp-idp");

        act.Should().Throw<InvalidOperationException>().WithMessage("*duplicates*");
    }

    [Theory]
    [InlineData("Corp-Idp")]
    [InlineData("corp_idp")]
    [InlineData("-corp")]
    [InlineData("corp-")]
    [InlineData("")]
    public void Validate_WithInvalidSlug_Throws(string slug)
    {
        var act = () => ValidProvider().Validate(slug);

        act.Should().Throw<InvalidOperationException>().WithMessage("*slug*");
    }

    [Fact]
    public void Validate_UnknownClaimMappingKey_Throws()
    {
        var provider = ValidProvider();
        provider.ClaimMapping["roles"] = "groups";

        var act = () => provider.Validate("corp-idp");

        act.Should().Throw<InvalidOperationException>().WithMessage("*not a supported logical claim*");
    }

    [Fact]
    public void Validate_ClaimMappingToEmptyClaimName_Throws()
    {
        var provider = ValidProvider();
        provider.ClaimMapping["email"] = " ";

        var act = () => provider.Validate("corp-idp");

        act.Should().Throw<InvalidOperationException>().WithMessage("*non-empty claim name*");
    }

    [Theory]
    [InlineData("@corp.example.test")]
    [InlineData("localhost")]
    [InlineData("corp example.test")]
    public void Validate_InvalidEmailDomain_Throws(string domain)
    {
        var provider = ValidProvider();
        provider.EmailDomains = [domain];

        var act = () => provider.Validate("corp-idp");

        act.Should().Throw<InvalidOperationException>().WithMessage("*email domains*");
    }

    [Fact]
    public void Validate_NullOidcMap_OnExternalProviders_Throws()
    {
        var options = new ExternalProviderOptions { Oidc = null! };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*OIDC federation provider settings*");
    }

    [Fact]
    public void Validate_OidcEntry_ValidatedThroughExternalProviders()
    {
        var options = new ExternalProviderOptions
        {
            Oidc = new Dictionary<string, OidcProviderOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["corp-idp"] = ValidProvider()
            }
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void AuthenticationOptions_Validate_OidcEnabledWithAuthenticationDisabled_Throws()
    {
        var options = new AuthenticationOptions
        {
            EnableAuthentication = false,
            ExternalProviders = new ExternalProviderOptions
            {
                Oidc = new Dictionary<string, OidcProviderOptions>(StringComparer.OrdinalIgnoreCase)
                {
                    ["corp-idp"] = ValidProvider()
                }
            }
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot be enabled when authentication is disabled*");
    }
}

public class AuthorizationOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeAuthorization()
    {
        GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions.SectionName.Should().Be("Authorization");
    }

    [Fact]
    public void Defaults_ShouldBeReasonable()
    {
        var options = new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions();
        options.DefaultPolicy.Should().Be("Default");
        options.RequireAuthenticatedUser.Should().BeTrue();
        options.SystemAccountId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CreateDefault_ShouldReturnInstance()
    {
        var options = GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void Validate_WithEmptyPolicy_ShouldThrow()
    {
        var options = new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions { DefaultPolicy = "" };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*policy*");
    }

    [Fact]
    public void Validate_WithEmptyFallbackPolicyName_ShouldThrow()
    {
        var options = new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions
        {
            FallbackPolicyName = " "
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Fallback policy name*");
    }

    [Fact]
    public void Validate_WithEmptyGuidSystemAccount_ShouldThrow()
    {
        var options = new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions { SystemAccountId = Guid.Empty };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*SystemAccountId*");
    }

    [Fact]
    public void Validate_WithDefaults_ShouldNotThrow()
    {
        var options = new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithConfiguredPolicyAndRoleHierarchy_ShouldNotThrow()
    {
        var options = new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions
        {
            Policies = new Dictionary<string, GameGuild.Configuration.PresentationLayer.Authorization.ConfiguredAuthorizationPolicyOptions>
            {
                ["CanModerate"] = new() { Roles = ["Moderator"] }
            },
            RoleHierarchy = new Dictionary<string, List<string>>
            {
                ["Administrator"] = ["Moderator"],
                ["SystemAdministrator"] = ["Administrator"]
            }
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithUnsecuredPolicy_ShouldThrow()
    {
        var options = new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions
        {
            Policies = new Dictionary<string, GameGuild.Configuration.PresentationLayer.Authorization.ConfiguredAuthorizationPolicyOptions>
            {
                ["AllowEveryone"] = new() { RequireAuthenticatedUser = false }
            }
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*must require authentication*");
    }

    [Fact]
    public void Validate_WithCyclicRoleHierarchy_ShouldThrow()
    {
        var options = new GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions
        {
            RoleHierarchy = new Dictionary<string, List<string>>
            {
                ["Administrator"] = ["Editor"],
                ["Editor"] = ["Administrator"]
            }
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*contains a cycle*");
    }
}

public class HealthChecksOptionsTests
{
    [Fact]
    public void Defaults_ShouldHaveEndpoint()
    {
        var options = new HealthChecksOptions();
        options.Endpoints.Should().Contain("/health");
        options.EnableLiveness.Should().BeTrue();
        options.EnableReadiness.Should().BeTrue();
        options.HealthCheckPath.Should().Be("/health");
        options.TimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void CreateDefault_ShouldReturnInstance()
    {
        var options = HealthChecksOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void Validate_WithEndpoints_ShouldNotThrow()
    {
        var options = new HealthChecksOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithEmptyEndpoints_ShouldThrow()
    {
        var options = new HealthChecksOptions { Endpoints = [] };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*endpoint*");
    }

    [Fact]
    public void Validate_WithNullEndpoints_ShouldThrow()
    {
        var options = new HealthChecksOptions { Endpoints = null! };
        var act = () => options.Validate();
        act.Should().Throw<Exception>();
    }
}

public class GraphQLOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeGraphQL()
    {
        GraphQLOptions.SectionName.Should().Be("GraphQL");
    }

    [Fact]
    public void Defaults_ShouldBeDisabled()
    {
        var options = new GraphQLOptions();
        options.EnableGraphQL.Should().BeFalse();
        options.Endpoint.Should().Be("/graphql");
    }

    [Fact]
    public void CreateDefault_ShouldReturnInstance()
    {
        var options = GraphQLOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void Validate_WithEndpoint_ShouldNotThrow()
    {
        var options = new GraphQLOptions();
        var act = () => options.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WithEmptyEndpoint_ShouldThrow()
    {
        var options = new GraphQLOptions { Endpoint = "" };
        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>();
    }
}

public class OpenApiOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeOpenApi()
    {
        OpenApiOptions.SectionName.Should().Be("OpenApi");
    }

    [Fact]
    public void Defaults_ShouldBeReasonable()
    {
        var options = new OpenApiOptions();
        options.EnableOpenApi.Should().BeTrue();
        options.Title.Should().Be("GameGuild API");
        options.Version.Should().Be("v1");
        options.Description.Should().BeEmpty();
        options.ContactName.Should().BeEmpty();
        options.ContactEmail.Should().BeEmpty();
        options.ContactUrl.Should().BeEmpty();
        options.TermsOfServiceUrl.Should().BeEmpty();
        options.LicenseName.Should().BeEmpty();
        options.LicenseUrl.Should().BeEmpty();
    }

    [Fact]
    public void CreateDefault_ShouldReturnInstance()
    {
        var options = OpenApiOptions.CreateDefault();
        options.Should().NotBeNull();
    }

    [Fact]
    public void AllPropertiesShouldBeSettable()
    {
        var options = new OpenApiOptions
        {
            Title = "Test",
            Version = "v2",
            Description = "desc",
            ContactName = "Name",
            ContactEmail = "email@test.com",
            ContactUrl = "https://test.com",
            TermsOfServiceUrl = "https://tos.com",
            LicenseName = "MIT",
            LicenseUrl = "https://license.com"
        };
        options.Title.Should().Be("Test");
        options.LicenseName.Should().Be("MIT");
    }
}

public class RateLimitingOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeRateLimiting()
    {
        RateLimitingOptions.SectionName.Should().Be("RateLimiting");
    }

    [Fact]
    public void Defaults_ShouldBeDisabled()
    {
        var options = new RateLimitingOptions();
        options.EnableRateLimiting.Should().BeFalse();
    }

    [Fact]
    public void Defaults_ShouldHaveReasonableValues()
    {
        var options = new RateLimitingOptions();
        options.Limit.Should().Be(100);
        options.Period.Should().Be(TimeSpan.FromMinutes(1));
        options.RequestsPerMinute.Should().Be(60);
        options.BurstSize.Should().Be(10);
        options.ExemptPaths.Should().BeEmpty();
        options.AuthenticationRequestsPerMinute.Should().Be(10);
        options.AuthorizationRequestsPerMinute.Should().Be(100);
        options.ApiRequestsPerMinute.Should().Be(60);
        options.QueueLimit.Should().Be(2);
        options.TenantRequestsPerMinute.Should().Be(1000);
        options.UserRequestsPerMinute.Should().Be(300);
        options.StandardApiKeyRequestsPerMinute.Should().Be(100);
        options.PremiumApiKeyRequestsPerMinute.Should().Be(1000);
        options.TokenBucketLimit.Should().Be(100);
        options.TokenReplenishmentPeriod.Should().Be(TimeSpan.FromSeconds(10));
        options.TokensPerPeriod.Should().Be(20);
        options.MaxConcurrentRequests.Should().Be(10);
    }

    [Fact]
    public void CreateDefault_ShouldReturnInstance()
    {
        var options = RateLimitingOptions.CreateDefault();
        options.Should().NotBeNull();
    }
}

public class AuthenticationAnomalyOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeAuthenticationAnomaly()
    {
        AuthenticationAnomalyOptions.SectionName.Should().Be("AuthenticationAnomaly");
    }

    [Fact]
    public void Defaults_ShouldBeValid()
    {
        var options = new AuthenticationAnomalyOptions();
        var (isValid, _) = options.Validate();
        isValid.Should().BeTrue();
    }

    [Fact]
    public void Defaults_ShouldHaveReasonableValues()
    {
        var options = new AuthenticationAnomalyOptions();
        options.Enabled.Should().BeTrue();
        options.MaxFailedAttemptsPerHour.Should().Be(5);
        options.MaxFailedAttemptsPerDay.Should().Be(20);
        options.SuspiciousThreshold.Should().Be(3);
        options.ThrottleDurationMinutes.Should().Be(15);
        options.MaxAttemptsPerIpPerHour.Should().Be(50);
        options.EnableLocationTracking.Should().BeTrue();
        options.FlagNewDevices.Should().BeTrue();
        options.FlagNewLocations.Should().BeTrue();
        options.EnableVelocityChecks.Should().BeTrue();
        options.MinTimeBetweenAttemptsSeconds.Should().Be(5);
        options.EnableBehavioralAnalysis.Should().BeTrue();
    }

    [Fact]
    public void Validate_HourlyExceedsDaily_ShouldReportError()
    {
        var options = new AuthenticationAnomalyOptions
        {
            MaxFailedAttemptsPerHour = 30,
            MaxFailedAttemptsPerDay = 10
        };
        var (isValid, errors) = options.Validate();
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("cannot exceed"));
    }

    [Fact]
    public void Validate_OutOfRange_ShouldReportErrors()
    {
        var options = new AuthenticationAnomalyOptions
        {
            MaxFailedAttemptsPerHour = 0,
            SuspiciousThreshold = 99
        };
        var (isValid, _) = options.Validate();
        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithDefaults_ShouldBeTrue()
    {
        var options = new AuthenticationAnomalyOptions();
        options.IsValid.Should().BeTrue();
    }
}

public class UserEnumerationProtectionOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeUserEnumerationProtection()
    {
        UserEnumerationProtectionOptions.SectionName.Should().Be("UserEnumerationProtection");
    }

    [Fact]
    public void Defaults_ShouldBeValid()
    {
        var options = new UserEnumerationProtectionOptions();
        var (isValid, _) = options.Validate();
        isValid.Should().BeTrue();
    }

    [Fact]
    public void Defaults_ShouldHaveReasonableValues()
    {
        var options = new UserEnumerationProtectionOptions();
        options.Enabled.Should().BeTrue();
        options.MinProcessingTimeMs.Should().Be(200);
        options.MaxProcessingTimeMs.Should().Be(800);
        options.TargetProcessingTimeMs.Should().Be(400);
        options.ConsistentErrorMessage.Should().NotBeEmpty();
        options.EnableRandomJitter.Should().BeTrue();
        options.MaxJitterMs.Should().Be(100);
    }

    [Fact]
    public void Validate_MinExceedsTarget_ShouldReportError()
    {
        var options = new UserEnumerationProtectionOptions
        {
            MinProcessingTimeMs = 500,
            TargetProcessingTimeMs = 200,
            MaxProcessingTimeMs = 800
        };
        var (isValid, errors) = options.Validate();
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("MinProcessingTimeMs"));
    }

    [Fact]
    public void Validate_TargetExceedsMax_ShouldReportError()
    {
        var options = new UserEnumerationProtectionOptions
        {
            MinProcessingTimeMs = 100,
            TargetProcessingTimeMs = 900,
            MaxProcessingTimeMs = 800
        };
        var (isValid, errors) = options.Validate();
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("TargetProcessingTimeMs"));
    }

    [Fact]
    public void Validate_EmptyErrorMessage_ShouldReportError()
    {
        var options = new UserEnumerationProtectionOptions { ConsistentErrorMessage = "" };
        var (isValid, errors) = options.Validate();
        isValid.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("ConsistentErrorMessage"));
    }

    [Fact]
    public void IsValid_WithDefaults_ShouldBeTrue()
    {
        var options = new UserEnumerationProtectionOptions();
        options.IsValid.Should().BeTrue();
    }
}

public class SharedJsonOptionsTests
{
    [Fact]
    public void Api_ShouldNotBeNull()
    {
        SharedJsonOptions.Api.Should().NotBeNull();
    }

    [Fact]
    public void Api_ShouldUseCamelCase()
    {
        SharedJsonOptions.Api.PropertyNamingPolicy.Should().Be(System.Text.Json.JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public void Api_ShouldBeCaseInsensitive()
    {
        SharedJsonOptions.Api.PropertyNameCaseInsensitive.Should().BeTrue();
    }

    [Fact]
    public void Strict_ShouldNotBeNull()
    {
        SharedJsonOptions.Strict.Should().NotBeNull();
    }

    [Fact]
    public void Strict_ShouldBeCaseSensitive()
    {
        SharedJsonOptions.Strict.PropertyNameCaseInsensitive.Should().BeFalse();
    }

    [Fact]
    public void Strict_ShouldNotIgnoreNulls()
    {
        SharedJsonOptions.Strict.DefaultIgnoreCondition.Should().Be(System.Text.Json.Serialization.JsonIgnoreCondition.Never);
    }

    [Fact]
    public void Web_ShouldNotBeNull()
    {
        SharedJsonOptions.Web.Should().NotBeNull();
    }

    [Fact]
    public void Web_ShouldIgnoreWhenWritingNull()
    {
        SharedJsonOptions.Web.DefaultIgnoreCondition.Should().Be(System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);
    }

    [Fact]
    public void Api_ShouldHaveEnumConverter()
    {
        SharedJsonOptions.Api.Converters.Should().Contain(c => c is System.Text.Json.Serialization.JsonStringEnumConverter);
    }

    [Fact]
    public void Strict_ShouldHaveEnumConverter()
    {
        SharedJsonOptions.Strict.Converters.Should().Contain(c => c is System.Text.Json.Serialization.JsonStringEnumConverter);
    }
}
