using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     Wire-contract tests for the generic OAuth provider registry (issues #235/#246):
///     Steam OpenID 2.0, Epic Games OAuth 2.0, and Facebook Graph OAuth flows through
///     the provider-agnostic <see cref="OAuthService" /> dispatcher, plus the registry
///     extension acceptance (a new provider plugs in without editing a switch).
/// </summary>
public class OAuthProviderRegistryTests : IDisposable
{
    private const string RedirectUri = "https://web.example.com/api/auth/callback/steam";

    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    private readonly HttpClient _httpClient;

    private readonly Mock<IConfiguration> _configurationMock;

    private readonly Mock<ILogger<OAuthService>> _loggerMock;

    private readonly OAuthService _oauthService;

    public OAuthProviderRegistryTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<OAuthService>>();

        _oauthService = new OAuthService(_httpClient, _configurationMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    // ── Steam OpenID 2.0 ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithSteam_BuildsOpenIdCheckidSetupRedirect()
    {
        _configurationMock.Setup(x => x["OAuth:Steam:ApiKey"]).Returns("steam-web-api-key");

        var url = await _oauthService.GetAuthorizationUrlAsync("steam", RedirectUri, "state-123");

        url.Should().StartWith("https://steamcommunity.com/openid/login?");
        url.Should().Contain("openid.ns=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0");
        url.Should().Contain("openid.mode=checkid_setup");
        url.Should().Contain($"openid.return_to={Uri.EscapeDataString(RedirectUri)}");
        url.Should().Contain("openid.realm=https%3A%2F%2Fweb.example.com");
        url.Should().Contain("openid.identity=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0%2Fidentifier_select");
        url.Should().Contain("openid.claimed_id=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0%2Fidentifier_select");
        url.Should().Contain("state=state-123");
    }

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithSteamAndMissingApiKey_FailsClosed()
    {
        _configurationMock.Setup(x => x["OAuth:Steam:ApiKey"]).Returns((string?)null);

        await _oauthService
            .Invoking(x => x.GetAuthorizationUrlAsync("steam", RedirectUri, "state-123"))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Steam Web API key not configured*");
    }

    [Fact]
    public void ResolveAuthorizationScopes_Steam_HasNoScopes_OpenIdHasNoScopeConcept()
    {
        _oauthService.ResolveAuthorizationScopes("steam").Should().BeEmpty();
    }

    [Fact]
    public async Task HandleCallbackAsync_WithSteam_VerifiesAssertionAndNormalizesPlayerSummary()
    {
        _configurationMock.Setup(x => x["OAuth:Steam:ApiKey"]).Returns("steam-web-api-key");
        var requests = new List<CapturedRequest>();
        SetupHttpHandler(
            requests,
            static request => request.RequestUri!.Host == "steamcommunity.com"
                ? "ns:http://specs.openid.net/auth/2.0\nis_valid:true\n"
                : """{"response":{"players":[{"steamid":"76561198000000001","personaname":"steamuser","realname":"Steam User","avatarfull":"https://avatars.steamstatic.com/full.jpg"}]}}""");

        var profile = await _oauthService.HandleCallbackAsync("steam", SteamAssertion(), "state-123", RedirectUri);

        profile.Provider.Should().Be("Steam");
        profile.ProviderId.Should().Be("76561198000000001");
        profile.Email.Should().BeNull();
        profile.EmailVerified.Should().BeFalse();
        profile.Name.Should().Be("Steam User");
        profile.Username.Should().Be("steamuser");
        profile.AvatarUrl.Should().Be("https://avatars.steamstatic.com/full.jpg");

        requests.Should().HaveCount(2);

        // The assertion is re-verified with the OP: openid.mode switches to check_authentication.
        var verification = requests[0];
        verification.Request.Method.Should().Be(HttpMethod.Post);
        verification.Request.RequestUri!.ToString().Should().Be("https://steamcommunity.com/openid/login");
        verification.Body.Should().Contain("openid.mode=check_authentication");
        verification.Body.Should().Contain("openid.sig=");

        // The profile lookup carries the SteamID and the configured Web API key.
        var summary = requests[1];
        summary.Request.Method.Should().Be(HttpMethod.Get);
        summary.Request.RequestUri!.ToString().Should().Contain("steamids=76561198000000001");
        summary.Request.RequestUri!.ToString().Should().Contain("key=steam-web-api-key");
    }

    [Fact]
    public async Task HandleCallbackAsync_WithSteamAndInvalidAssertion_ThrowsUnauthorized()
    {
        _configurationMock.Setup(x => x["OAuth:Steam:ApiKey"]).Returns("steam-web-api-key");
        SetupHttpHandler(
            new List<CapturedRequest>(),
            static request => "ns:http://specs.openid.net/auth/2.0\nis_valid:false\n");

        await _oauthService
            .Invoking(x => x.HandleCallbackAsync("steam", SteamAssertion(), "state-123", RedirectUri))
            .Should()
            .ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*failed provider verification*");
    }

    [Fact]
    public async Task HandleCallbackAsync_WithSteamAndMismatchedReturnTo_Throws()
    {
        _configurationMock.Setup(x => x["OAuth:Steam:ApiKey"]).Returns("steam-web-api-key");

        await _oauthService
            .Invoking(x => x.HandleCallbackAsync("steam", SteamAssertion(), "state-123", "https://attacker.example.com/callback"))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*return_to does not match*");
    }

    [Fact]
    public async Task HandleCallbackAsync_WithSteamAndDisabledProvider_FailsClosed()
    {
        var options = new AuthenticationOptions
        {
            ExternalProviders = new ExternalProviderOptions
            {
                Providers = new Dictionary<string, OAuthProviderOptions>
                {
                    ["steam"] = new() { Enabled = false, ApiKey = "steam-web-api-key" }
                }
            }
        };
        var service = new OAuthService(_httpClient, _configurationMock.Object, _loggerMock.Object, options);

        await service
            .Invoking(x => x.HandleCallbackAsync("steam", SteamAssertion(), "state-123", RedirectUri))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("OAuth provider 'steam' is disabled.");
    }

    // ── Epic Games OAuth 2.0 ───────────────────────────────────────────────

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithEpic_ReturnsCorrectUrl()
    {
        _configurationMock.Setup(x => x["OAuth:Epic:ClientId"]).Returns("epic-client-id");

        var url = await _oauthService.GetAuthorizationUrlAsync("epic", "https://web.example.com/api/auth/callback/epic", "state-epic");

        url.Should().StartWith("https://www.epicgames.com/id/authorize?");
        url.Should().Contain("client_id=epic-client-id");
        url.Should().Contain($"redirect_uri={Uri.EscapeDataString("https://web.example.com/api/auth/callback/epic")}");
        url.Should().Contain("scope=basic_profile%20email");
        url.Should().Contain("state=state-epic");
        url.Should().Contain("response_type=code");
    }

    [Fact]
    public void ResolveAuthorizationScopes_Epic_ReturnsSafeDefaults()
    {
        _oauthService.ResolveAuthorizationScopes("epic").Should().Equal("basic_profile", "email");
    }

    [Fact]
    public async Task HandleCallbackAsync_WithEpic_ExchangesCodeWithBasicCredentials()
    {
        _configurationMock.Setup(x => x["OAuth:Epic:ClientId"]).Returns("epic-client-id");
        _configurationMock.Setup(x => x["OAuth:Epic:ClientSecret"]).Returns("epic-client-secret");
        var requests = new List<CapturedRequest>();
        SetupHttpHandler(
            requests,
            static request => request.RequestUri!.AbsolutePath.EndsWith("/token", StringComparison.Ordinal)
                ? """{"access_token":"epic-access-token","token_type":"bearer"}"""
                : """{"sub":"epic-account-1","email":"epic@example.com","email_verified":true,"name":"Epic User"}""");

        var profile = await _oauthService.HandleCallbackAsync("epic", "epic-auth-code", "state-epic", "https://web.example.com/api/auth/callback/epic");

        profile.Provider.Should().Be("Epic");
        profile.ProviderId.Should().Be("epic-account-1");
        profile.Email.Should().Be("epic@example.com");
        profile.EmailVerified.Should().BeTrue();
        profile.Name.Should().Be("Epic User");

        requests.Should().HaveCount(2);

        // Epic's token endpoint takes HTTP Basic client credentials (RFC 6749 §2.3.1).
        var tokenRequest = requests[0];
        tokenRequest.Request.Method.Should().Be(HttpMethod.Post);
        tokenRequest.Request.Headers.Authorization?.Scheme.Should().Be("Basic");
        tokenRequest.Body.Should().Contain("grant_type=authorization_code");
        tokenRequest.Body.Should().Contain("code=epic-auth-code");

        var profileRequest = requests[1];
        profileRequest.Request.Headers.Authorization?.Scheme.Should().Be("Bearer");
        profileRequest.Request.Headers.Authorization?.Parameter.Should().Be("epic-access-token");
    }

    [Fact]
    public async Task HandleCallbackAsync_WithEpicAndMissingCredentials_FailsClosed()
    {
        _configurationMock.Setup(x => x["OAuth:Epic:ClientId"]).Returns((string?)null);
        _configurationMock.Setup(x => x["OAuth:Epic:ClientSecret"]).Returns((string?)null);

        await _oauthService
            .Invoking(x => x.HandleCallbackAsync("epic", "epic-auth-code", "state-epic", "https://web.example.com/api/auth/callback/epic"))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*OAuth client credentials are not configured*");
    }

    // ── Facebook Graph OAuth 2.0 ───────────────────────────────────────────

    [Fact]
    public async Task GetAuthorizationUrlAsync_WithFacebook_ReturnsCorrectUrlWithCommaJoinedScopes()
    {
        _configurationMock.Setup(x => x["OAuth:Facebook:ClientId"]).Returns("facebook-app-id");

        var url = await _oauthService.GetAuthorizationUrlAsync("facebook", "https://web.example.com/api/auth/callback/facebook", "state-fb");

        url.Should().StartWith("https://www.facebook.com/v19.0/dialog/oauth?");
        url.Should().Contain("client_id=facebook-app-id");
        url.Should().Contain("scope=email%2Cpublic_profile");
        url.Should().Contain("state=state-fb");
        url.Should().Contain("response_type=code");
    }

    [Fact]
    public void ResolveAuthorizationScopes_Facebook_ReturnsSafeDefaults()
    {
        _oauthService.ResolveAuthorizationScopes("facebook").Should().Equal("email", "public_profile");
    }

    [Fact]
    public async Task HandleCallbackAsync_WithFacebook_ExchangesCodeAsGraphQueryAndNormalizesMe()
    {
        _configurationMock.Setup(x => x["OAuth:Facebook:ClientId"]).Returns("facebook-app-id");
        _configurationMock.Setup(x => x["OAuth:Facebook:ClientSecret"]).Returns("facebook-app-secret");
        var requests = new List<CapturedRequest>();
        SetupHttpHandler(
            requests,
            static request => request.RequestUri!.AbsolutePath.EndsWith("/oauth/access_token", StringComparison.Ordinal)
                ? """{"access_token":"facebook-access-token","token_type":"bearer"}"""
                : """{"id":"facebook-user-1","name":"Facebook User","email":"facebook@example.com","first_name":"Facebook","last_name":"User","picture":{"data":{"url":"https://graph.facebook.com/picture.jpg"}}}""");

        var profile = await _oauthService.HandleCallbackAsync("facebook", "facebook-auth-code", "state-fb", "https://web.example.com/api/auth/callback/facebook");

        profile.Provider.Should().Be("Facebook");
        profile.ProviderId.Should().Be("facebook-user-1");
        profile.Email.Should().Be("facebook@example.com");
        profile.EmailVerified.Should().BeFalse();
        profile.FirstName.Should().Be("Facebook");
        profile.LastName.Should().Be("User");
        profile.AvatarUrl.Should().Be("https://graph.facebook.com/picture.jpg");

        requests.Should().HaveCount(2);

        // Facebook's Graph token endpoint takes the client credentials as query parameters.
        var tokenRequest = requests[0];
        tokenRequest.Request.Method.Should().Be(HttpMethod.Get);
        tokenRequest.Request.RequestUri!.ToString().Should().Contain("client_id=facebook-app-id");
        tokenRequest.Request.RequestUri!.ToString().Should().Contain("client_secret=facebook-app-secret");
        tokenRequest.Request.RequestUri!.ToString().Should().Contain("code=facebook-auth-code");

        var profileRequest = requests[1];
        profileRequest.Request.RequestUri!.ToString().Should().Contain("fields=");
        profileRequest.Request.RequestUri!.ToString().Should().Contain("access_token=facebook-access-token");
    }

    // ── Registry extension contract (issue #235 acceptance) ────────────────

    [Fact]
    public async Task CustomProvider_RegistersWithoutEditingTheDispatcher()
    {
        // A provider plugs in through IOAuthProvider alone: OAuthService has no
        // per-provider switch to touch, which is the #235 extension contract.
        var custom = new StubOAuthProvider("acme", "Acme", ["acme.read"]);
        var service = new OAuthService(_httpClient, _configurationMock.Object, _loggerMock.Object, providers: [custom]);

        (await service.GetAuthorizationUrlAsync("acme", "https://callback", "state")).Should().Be("https://acme.example.com/authorize?state=state");
        service.ResolveAuthorizationScopes("acme").Should().Equal("acme.read");

        var profile = await service.HandleCallbackAsync("acme", "acme-code", "state", "https://callback");
        profile.Provider.Should().Be("Acme");
        profile.ProviderId.Should().Be("acme-1");

        // Unregistered providers still fail with the legacy unsupported-provider error.
        await service
            .Invoking(x => x.GetAuthorizationUrlAsync("twitter", "https://callback", "state"))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*OAuth client ID not configured*");
    }

    [Fact]
    public void Registry_WithDuplicateProviderKeys_FailsLoudly()
    {
        var providers = new IOAuthProvider[] { new StubOAuthProvider("acme", "Acme", []), new StubOAuthProvider("acme", "Evil Twin", []) };

        var act = () => new OAuthService(_httpClient, _configurationMock.Object, _loggerMock.Object, providers: providers);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    ///     Snapshot of an outgoing HTTP request. Body and content type are read eagerly
    ///     because the providers dispose request content (via <c>using</c>) after sending.
    /// </summary>
    private sealed record CapturedRequest(HttpRequestMessage Request, string Body);

    private static string SteamAssertion() =>
        "openid.ns=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0" +
        "&openid.mode=id_res" +
        "&openid.op_endpoint=https%3A%2F%2Fsteamcommunity.com%2Fopenid%2Flogin" +
        "&openid.claimed_id=https%3A%2F%2Fsteamcommunity.com%2Fopenid%2Fid%2F76561198000000001" +
        "&openid.identity=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0%2Fidentifier_select" +
        $"&openid.return_to={Uri.EscapeDataString(RedirectUri)}" +
        "&openid.response_nonce=2026-10-20T12%3A00%3A00Zabc" +
        "&openid.assoc_handle=1234567890" +
        "&openid.signed=signed%2Cop_endpoint%2Cclaimed_id%2Cidentity%2Creturn_to%2Cresponse_nonce%2Cassoc_handle" +
        "&openid.sig=abc123%3D%3D";

    private void SetupHttpHandler(List<CapturedRequest> requests, Func<HttpRequestMessage, string> respond)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
                requests.Add(new CapturedRequest(request, body));

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(respond(request), Encoding.UTF8, "application/json")
                };
            });
    }

    private sealed class StubOAuthProvider(
        string providerKey,
        string displayName,
        string[] defaultScopes
    ) : IOAuthProvider
    {
        public string ProviderKey { get; } = providerKey;

        public string DisplayName { get; } = displayName;

        public IReadOnlyList<string> DefaultScopes { get; } = defaultScopes;

        public bool IsConfigured() => true;

        public string BuildAuthorizationUrl(string redirectUri, string state, string[]? scopes = null) =>
            $"https://{ProviderKey}.example.com/authorize?state={Uri.EscapeDataString(state)}";

        public string[] ResolveScopes(string[]? requestedScopes = null) =>
            requestedScopes is { Length: > 0 } ? requestedScopes : [.. DefaultScopes];

        public Task<string> ExchangeCodeAsync(string code, string redirectUri) => Task.FromResult("stub-token");

        public Task<OAuthUserProfile> GetUserProfileAsync(string accessToken) =>
            Task.FromResult(new OAuthUserProfile { ProviderId = $"{ProviderKey}-1", Provider = DisplayName, Email = $"{ProviderKey}@example.com", EmailVerified = true, AccessToken = accessToken });
    }
}
