using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public class OAuthServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<OAuthService>> _loggerMock;
    private readonly OAuthService _oauthService;

    public OAuthServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<OAuthService>>();

        _oauthService = new OAuthService(_httpClient, _configurationMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithGitHub_ReturnsCorrectUrl()
    {
        // Arrange
        var clientId = "test-github-client-id";
        _configurationMock.Setup(x => x["OAuth:github:ClientId"]).Returns(clientId);
        var redirectUri = "https://example.com/callback";
        var state = "test-state";

        // Act
        var url = await _oauthService.GetAuthorizationUrlAsync("github", redirectUri, state);

        // Assert
        url.Should().Contain("https://github.com/login/oauth/authorize");
        url.Should().Contain($"client_id={clientId}");
        url.Should().Contain($"redirect_uri={Uri.EscapeDataString(redirectUri)}");
        url.Should().Contain($"state={state}");
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithGoogle_ReturnsCorrectUrl()
    {
        // Arrange
        var clientId = "test-google-client-id";
        _configurationMock.Setup(x => x["OAuth:google:ClientId"]).Returns(clientId);
        var redirectUri = "https://example.com/callback";
        var state = "test-state";

        // Act
        var url = await _oauthService.GetAuthorizationUrlAsync("google", redirectUri, state);

        // Assert
        url.Should().Contain("https://accounts.google.com/o/oauth2/v2/auth");
        url.Should().Contain($"client_id={clientId}");
        url.Should().Contain($"redirect_uri={Uri.EscapeDataString(redirectUri)}");
        url.Should().Contain($"state={state}");
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithMissingClientId_ThrowsException()
    {
        // Arrange
        _configurationMock.Setup(x => x["OAuth:github:ClientId"]).Returns((string?)null);

        // Act & Assert
        await _oauthService
            .Invoking(x => x.GetAuthorizationUrlAsync("github", "https://example.com", "state"))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*OAuth client ID not configured*");
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithUnsupportedProvider_ThrowsException()
    {
        // Arrange
        _configurationMock.Setup(x => x["OAuth:unsupported:ClientId"]).Returns("test-id");

        // Act & Assert
        await _oauthService
            .Invoking(x => x.GetAuthorizationUrlAsync("unsupported", "https://example.com", "state"))
            .Should()
            .ThrowAsync<NotSupportedException>()
            .WithMessage("*OAuth provider not supported*");
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithScopes_IncludesScopesInUrl()
    {
        // Arrange
        var clientId = "test-github-client-id";
        _configurationMock.Setup(x => x["OAuth:github:ClientId"]).Returns(clientId);
        var scopes = new[] { "user:email", "read:user" };

        // Act
        var url = await _oauthService.GetAuthorizationUrlAsync("github", "https://example.com", "state", scopes);

        // Assert
        url.Should().Contain("scope=");
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_UsesTypedProviderCredentialsScopesAndEndpoint()
    {
        var options = new AuthenticationOptions
        {
            ExternalProviders = new ExternalProviderOptions
            {
                Providers = new Dictionary<string, OAuthProviderOptions>
                {
                    ["github"] = new()
                    {
                        Enabled = true,
                        ClientId = "typed-client",
                        ClientSecret = "typed-secret",
                        Scopes = ["read:user"],
                        AuthorizationEndpoint = "https://github.example.test/oauth/authorize",
                        TokenEndpoint = "https://github.example.test/oauth/token",
                        UserInformationEndpoint = "https://github.example.test/api/user"
                    }
                }
            }
        };
        var service = new OAuthService(_httpClient, _configurationMock.Object, _loggerMock.Object, options);

        var url = await service.GetAuthorizationUrlAsync("GitHub", "https://example.com/callback", "csrf-state");

        url.Should().StartWith("https://github.example.test/oauth/authorize?");
        url.Should().Contain("client_id=typed-client");
        url.Should().Contain("scope=read%3Auser");
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_RejectsExplicitlyDisabledProviderEvenWhenLegacyCredentialsExist()
    {
        _configurationMock.Setup(x => x["OAuth:github:ClientId"]).Returns("legacy-client");
        var options = new AuthenticationOptions
        {
            ExternalProviders = new ExternalProviderOptions
            {
                Providers = new Dictionary<string, OAuthProviderOptions>
                {
                    ["github"] = new() { Enabled = false }
                }
            }
        };
        var service = new OAuthService(_httpClient, _configurationMock.Object, _loggerMock.Object, options);

        var act = () => service.GetAuthorizationUrlAsync("github", "https://example.com/callback", "state");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*disabled*");
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithMicrosoft_UsesTenantAndRequiredDefaults()
    {
        var options = new AuthenticationOptions
        {
            ExternalProviders = new ExternalProviderOptions
            {
                Providers = new Dictionary<string, OAuthProviderOptions>
                {
                    ["microsoft"] = new()
                    {
                        Enabled = true,
                        ClientId = "microsoft-client",
                        ClientSecret = "microsoft-secret",
                        Tenant = "organizations"
                    }
                }
            }
        };
        var service = new OAuthService(_httpClient, _configurationMock.Object, _loggerMock.Object, options);

        var url = await service.GetAuthorizationUrlAsync(
            "microsoft", "https://example.com/callback", "state", ["User.Read"]);

        url.Should().StartWith("https://login.microsoftonline.com/organizations/oauth2/v2.0/authorize?");
        url.Should().Contain("response_type=code");
        url.Should().Contain("scope=User.Read%20openid%20email%20profile");
    }

    [Fact]
    public async Task GetUserProfileAsync_WithCustomGitHubEndpoint_DoesNotSendTokenToPublicGitHubEmailEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":123,\"login\":\"test-user\",\"name\":\"Test User\"}")
            });
        var options = new AuthenticationOptions
        {
            ExternalProviders = new ExternalProviderOptions
            {
                Providers = new Dictionary<string, OAuthProviderOptions>
                {
                    ["github"] = new()
                    {
                        Enabled = true,
                        ClientId = "typed-client",
                        ClientSecret = "typed-secret",
                        AuthorizationEndpoint = "https://github.example.test/oauth/authorize",
                        TokenEndpoint = "https://github.example.test/oauth/token",
                        UserInformationEndpoint = "https://github.example.test/api/user"
                    }
                }
            }
        };
        var service = new OAuthService(_httpClient, _configurationMock.Object, _loggerMock.Object, options);

        var profile = await service.GetUserProfileAsync("github", "access-token");

        profile.Email.Should().BeNullOrEmpty();
        capturedRequest!.RequestUri!.Host.Should().Be("github.example.test");
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task HandleCallbackAsync_WithMicrosoft_ExchangesCodeAndDoesNotAssumeEmailIsVerified()
    {
        _httpMessageHandlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"access-token\"}")
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"sub\":\"subject-123\",\"email\":\"user@example.com\",\"name\":\"Example User\",\"given_name\":\"Example\",\"family_name\":\"User\"}")
            });
        var options = new AuthenticationOptions
        {
            ExternalProviders = new ExternalProviderOptions
            {
                Providers = new Dictionary<string, OAuthProviderOptions>
                {
                    ["microsoft"] = new()
                    {
                        Enabled = true,
                        ClientId = "microsoft-client",
                        ClientSecret = "microsoft-secret",
                        Tenant = "organizations"
                    }
                }
            }
        };
        var service = new OAuthService(_httpClient, _configurationMock.Object, _loggerMock.Object, options);

        var profile = await service.HandleCallbackAsync(
            "microsoft", "authorization-code", "csrf-state", "https://example.com/callback");

        profile.ProviderId.Should().Be("subject-123");
        profile.Email.Should().Be("user@example.com");
        profile.EmailVerified.Should().BeFalse();
        profile.Name.Should().Be("Example User");
        profile.AccessToken.Should().Be("access-token");
        _httpClient.DefaultRequestHeaders.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task RevokeTokenAsync_WithValidProvider_ReturnsTrue()
    {
        // Act
        var result = await _oauthService.RevokeTokenAsync("github", "test-token");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserProfileAsync_WithUnsupportedProvider_ThrowsException()
    {
        // Act & Assert
        await _oauthService
            .Invoking(x => x.GetUserProfileAsync("unsupported", "test-token"))
            .Should()
            .ThrowAsync<NotSupportedException>()
            .WithMessage("*Provider not supported*");
    }

    // ── ResolveAuthorizationScopes (issue #250) ──────────────────────────

    [Fact]
    public void ResolveAuthorizationScopes_WithoutConfiguration_ReturnsProviderSafeDefaults()
    {
        _oauthService.ResolveAuthorizationScopes("github").Should().Equal("read:user", "user:email");
        _oauthService.ResolveAuthorizationScopes("google").Should().Equal("openid", "email", "profile");
        _oauthService.ResolveAuthorizationScopes("discord").Should().Equal("identify", "email");
        _oauthService.ResolveAuthorizationScopes("microsoft").Should().Equal("openid", "email", "profile");
    }

    [Fact]
    public void ResolveAuthorizationScopes_UsesConfiguredTypedProviderScopes_WhenNoScopesRequested()
    {
        var options = new AuthenticationOptions
        {
            ExternalProviders = new ExternalProviderOptions
            {
                Providers = new Dictionary<string, OAuthProviderOptions>
                {
                    ["discord"] = new() { Enabled = true, ClientId = "c", ClientSecret = "s", Scopes = ["identify"] }
                }
            }
        };
        var service = new OAuthService(_httpClient, _configurationMock.Object, _loggerMock.Object, options);

        service.ResolveAuthorizationScopes("discord").Should().Equal("identify");
    }

    [Fact]
    public void ResolveAuthorizationScopes_RequestedScopesWin_OverConfiguration()
    {
        var options = new AuthenticationOptions
        {
            ExternalProviders = new ExternalProviderOptions
            {
                Providers = new Dictionary<string, OAuthProviderOptions>
                {
                    ["github"] = new() { Enabled = true, ClientId = "c", ClientSecret = "s", Scopes = ["read:user"] }
                }
            }
        };
        var service = new OAuthService(_httpClient, _configurationMock.Object, _loggerMock.Object, options);

        service.ResolveAuthorizationScopes("github", ["user:email"]).Should().Equal("user:email");
    }

    [Fact]
    public void ResolveAuthorizationScopes_Microsoft_UnionsRequiredOidcScopes_AndDeduplicates()
    {
        _oauthService.ResolveAuthorizationScopes("microsoft", ["User.Read", "user.read"])
            .Should().Equal("User.Read", "openid", "email", "profile");
    }

    [Fact]
    public void ResolveAuthorizationScopes_UnsupportedProvider_Throws()
    {
        var act = () => _oauthService.ResolveAuthorizationScopes("steam");

        act.Should().Throw<NotSupportedException>().WithMessage("*OAuth provider not supported*");
    }

    [Fact]
    public async Task ResolveAuthorizationScopes_MatchesTheScopeParameterOfTheAuthorizationUrl()
    {
        // The recorded grant must be exactly what the authorization request asks for.
        foreach (var provider in new[] { "github", "google", "discord", "microsoft" })
        {
            _configurationMock.Setup(x => x[$"OAuth:{provider}:ClientId"]).Returns("client-id");

            var url = await _oauthService.GetAuthorizationUrlAsync(provider, "https://example.com/callback", "state");
            var scopes = _oauthService.ResolveAuthorizationScopes(provider);

            var scopeQuery = Uri.UnescapeDataString(url.Split('&').Single(p => p.StartsWith("scope=", StringComparison.Ordinal))["scope=".Length..]);
            scopeQuery.Should().Be(string.Join(" ", scopes), $"provider {provider}: URL scope parameter must equal the resolved grant list");
        }
    }
}
