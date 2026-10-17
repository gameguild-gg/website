using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     OidcFederationService tests against an in-memory stub OIDC provider (test-double
/// discovery): discovery + JWKS endpoints, authorization-code exchange, and cryptographic
/// ID-token validation failure modes mirroring the GoogleIdTokenVerifier coverage.
/// </summary>
public class OidcFederationServiceTests : IDisposable
{
    private const string Authority = "https://login.corp.example.test";
    private const string Slug = "corp-idp";
    private const string ClientId = "gameguild-web";
    private static readonly string ClientSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private readonly RSA _signingKey = RSA.Create(2048);
    private readonly RSA _foreignKey = RSA.Create(2048);

    public void Dispose()
    {
        _signingKey.Dispose();
        _foreignKey.Dispose();
    }

    private static OidcProviderOptions ProviderOptions(
        string? authority = Authority,
        string[]? scopes = null,
        Dictionary<string, string>? claimMapping = null,
        string[]? emailDomains = null) => new()
    {
        Enabled = true,
        Authority = authority,
        ClientId = ClientId,
        ClientSecret = ClientSecret,
        Scopes = (scopes ?? ["openid", "profile", "email"]).ToList(),
        ClaimMapping = claimMapping ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        EmailDomains = (emailDomains ?? []).ToList()
    };

    private static AuthenticationOptions AuthOptionsWith(params (string Slug, OidcProviderOptions Options)[] providers)
    {
        var oidc = new Dictionary<string, OidcProviderOptions>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slug, options) in providers)
        {
            oidc[slug] = options;
        }

        return new AuthenticationOptions
        {
            EnableAuthentication = false,
            ExternalProviders = new ExternalProviderOptions { Oidc = oidc }
        };
    }

    private OidcFederationService CreateSut(AuthenticationOptions options, StubOidcProvider stub) =>
        new(stub.HttpClientFactory, options, NullLogger<OidcFederationService>.Instance);

    // ── Discovery + authorization URL ─────────────────────────────────────────

    [Fact]
    public async Task BuildAuthorizationUrlAsync_UsesDiscoveredAuthorizationEndpoint()
    {
        var stub = new StubOidcProvider(_signingKey);
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var challenge = await sut.BuildAuthorizationUrlAsync(Slug, "https://web.example.test/api/auth/callback/oidc", "state-123");

        challenge.State.Should().Be("state-123");
        challenge.AuthUrl.Should().StartWith("https://login.corp.example.test/authorize");
        challenge.AuthUrl.Should().Contain("client_id=gameguild-web");
        challenge.AuthUrl.Should().Contain("response_type=code");
        challenge.AuthUrl.Should().Contain("scope=openid%20profile%20email");
        challenge.AuthUrl.Should().Contain($"state={Uri.EscapeDataString("state-123")}");
        challenge.AuthUrl.Should().Contain($"redirect_uri={Uri.EscapeDataString("https://web.example.test/api/auth/callback/oidc")}");
    }

    [Fact]
    public async Task Discovery_IsCached_SecondCallDoesNotRefetch()
    {
        var stub = new StubOidcProvider(_signingKey);
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        await sut.BuildAuthorizationUrlAsync(Slug, "https://web.example.test/cb", "s1");
        await sut.BuildAuthorizationUrlAsync(Slug, "https://web.example.test/cb", "s2");

        stub.DiscoveryHits.Should().Be(1, "the discovery document is cached for the authority");
    }

    [Fact]
    public async Task BuildAuthorizationUrlAsync_UnknownSlug_FailsClosed()
    {
        var stub = new StubOidcProvider(_signingKey);
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var act = () => sut.BuildAuthorizationUrlAsync("other-idp", "https://web.example.test/cb", "s");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not configured*");
        stub.DiscoveryHits.Should().Be(0);
    }

    [Fact]
    public async Task BuildAuthorizationUrlAsync_DisabledSlug_FailsClosed()
    {
        var disabled = ProviderOptions();
        disabled.Enabled = false;
        var stub = new StubOidcProvider(_signingKey);
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, disabled)), stub);

        var act = () => sut.BuildAuthorizationUrlAsync(Slug, "https://web.example.test/cb", "s");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*disabled*");
    }

    [Fact]
    public async Task Discovery_IssuerMismatchWithAuthority_Rejected()
    {
        // The stub advertises an issuer different from the configured authority.
        var stub = new StubOidcProvider(_signingKey) { Issuer = "https://someone-else.example.test" };
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var act = () => sut.BuildAuthorizationUrlAsync(Slug, "https://web.example.test/cb", "s");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not match the configured authority*");
    }

    // ── Callback: happy path + claim mapping ──────────────────────────────────

    [Fact]
    public async Task AuthenticateCallbackAsync_ForwardsEphemeralConfiguredClientSecret()
    {
        Convert.FromHexString(ClientSecret).Should().HaveCount(32);
        using var stub = new StubOidcProvider(_signingKey);
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var identity = await sut.AuthenticateCallbackAsync(Slug, "auth-code", "state-1", "https://web.example.test/cb");

        identity.ProviderKey.Should().Be("corp-sub-42");
        stub.TokenHits.Should().Be(1);
        stub.LastTokenRequestBody.Should().Contain($"client_secret={Uri.EscapeDataString(ClientSecret)}");
    }

    [Fact]
    public async Task AuthenticateCallbackAsync_ValidToken_MapsClaims()
    {
        var stub = new StubOidcProvider(_signingKey);
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var identity = await sut.AuthenticateCallbackAsync(Slug, "auth-code", "state-1", "https://web.example.test/cb");

        identity.Slug.Should().Be(Slug);
        identity.ProviderKey.Should().Be("corp-sub-42");
        identity.Email.Should().Be("jane@corp.example.test");
        identity.EmailVerified.Should().BeTrue();
        identity.Name.Should().Be("Jane Corp");
        identity.Amr.Should().BeEquivalentTo(["pwd", "mfa"]);
        identity.Acr.Should().Be("urn:corp:acr:2fa");
        identity.HasMfaProof.Should().BeTrue();
        stub.TokenHits.Should().Be(1);
    }

    [Fact]
    public async Task AuthenticateCallbackAsync_CustomClaimMapping_RemapsClaims()
    {
        var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["sub"] = "custom_subject",
            ["email"] = "custom_email",
            ["name"] = "custom_name"
        };
        var stub = new StubOidcProvider(_signingKey);
        using var _ = stub;
        stub.CustomClaims = claims =>
        {
            claims.Add(new Claim("custom_subject", "mapped-sub"));
            claims.Add(new Claim("custom_email", "mapped@corp.example.test"));
            claims.Add(new Claim("custom_name", "Mapped Name"));
        };
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions(claimMapping: mapping))), stub);

        var identity = await sut.AuthenticateCallbackAsync(Slug, "auth-code", "state-1", "https://web.example.test/cb");

        identity.ProviderKey.Should().Be("mapped-sub");
        identity.Email.Should().Be("mapped@corp.example.test");
        identity.Name.Should().Be("Mapped Name");
    }

    [Fact]
    public async Task AuthenticateCallbackAsync_MissingState_ThrowsCsrf()
    {
        var stub = new StubOidcProvider(_signingKey);
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var act = () => sut.AuthenticateCallbackAsync(Slug, "auth-code", "", "https://web.example.test/cb");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*state*");
    }

    [Fact]
    public async Task AuthenticateCallbackAsync_JwksCachedAcrossCallbacks()
    {
        var stub = new StubOidcProvider(_signingKey);
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        await sut.AuthenticateCallbackAsync(Slug, "code-1", "state-1", "https://web.example.test/cb");
        await sut.AuthenticateCallbackAsync(Slug, "code-2", "state-2", "https://web.example.test/cb");

        stub.DiscoveryHits.Should().Be(1);
        stub.JwksHits.Should().Be(1);
        stub.TokenHits.Should().Be(2);
    }

    [Fact]
    public async Task AuthenticateCallbackAsync_TokenResponseWithoutIdToken_Rejected()
    {
        var stub = new StubOidcProvider(_signingKey) { OmitIdToken = true };
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var act = () => sut.AuthenticateCallbackAsync(Slug, "auth-code", "state-1", "https://web.example.test/cb");

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*id_token*");
    }

    [Fact]
    public async Task AuthenticateCallbackAsync_TokenEndpointError_FailsClosed()
    {
        var stub = new StubOidcProvider(_signingKey) { TokenEndpointStatus = HttpStatusCode.BadRequest };
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var act = () => sut.AuthenticateCallbackAsync(Slug, "auth-code", "state-1", "https://web.example.test/cb");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*token exchange failed*");
    }

    // ── ID-token failure modes (mirror GoogleIdTokenVerifier coverage) ─────────

    [Fact]
    public async Task AuthenticateCallbackAsync_WrongIssuer_Rejected()
    {
        var stub = new StubOidcProvider(_signingKey) { TokenIssuerOverride = "https://evil.example.test" };
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var act = () => sut.AuthenticateCallbackAsync(Slug, "auth-code", "state-1", "https://web.example.test/cb");

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*failed validation*");
    }

    [Fact]
    public async Task AuthenticateCallbackAsync_WrongAudience_Rejected()
    {
        var stub = new StubOidcProvider(_signingKey) { TokenAudienceOverride = "some-other-client" };
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var act = () => sut.AuthenticateCallbackAsync(Slug, "auth-code", "state-1", "https://web.example.test/cb");

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*failed validation*");
    }

    [Fact]
    public async Task AuthenticateCallbackAsync_ExpiredToken_Rejected()
    {
        var stub = new StubOidcProvider(_signingKey) { TokenExpired = true };
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var act = () => sut.AuthenticateCallbackAsync(Slug, "auth-code", "state-1", "https://web.example.test/cb");

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*failed validation*");
    }

    [Fact]
    public async Task AuthenticateCallbackAsync_ForgedSignature_Rejected()
    {
        // The stub serves the real JWKS, but the token is signed by a foreign key.
        var stub = new StubOidcProvider(_signingKey) { SigningKeyOverride = _foreignKey };
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var act = () => sut.AuthenticateCallbackAsync(Slug, "auth-code", "state-1", "https://web.example.test/cb");

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*failed validation*");
    }

    [Fact]
    public async Task AuthenticateCallbackAsync_MalformedToken_RejectedBeforeJwks()
    {
        var stub = new StubOidcProvider(_signingKey) { RawIdTokenOverride = "not.a.jwt" };
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var act = () => sut.AuthenticateCallbackAsync(Slug, "auth-code", "state-1", "https://web.example.test/cb");

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*malformed*");
        stub.JwksHits.Should().Be(0, "a structurally malformed token is rejected before the JWKS round-trip");
    }

    [Fact]
    public async Task AuthenticateCallbackAsync_MissingSubject_Rejected()
    {
        var stub = new StubOidcProvider(_signingKey) { OmitSubject = true };
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var act = () => sut.AuthenticateCallbackAsync(Slug, "auth-code", "state-1", "https://web.example.test/cb");

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*subject claim*");
    }

    // ── Email-domain gate ──────────────────────────────────────────────────────

    [Fact]
    public async Task AuthenticateCallbackAsync_EmailOutsideConfiguredDomains_Rejected()
    {
        var stub = new StubOidcProvider(_signingKey);
        using var _ = stub;
        stub.CustomClaims = claims =>
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, "corp-sub-42"));
            claims.Add(new Claim("email", "intruder@attacker.example.test"));
        };
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions(emailDomains: ["corp.example.test"]))), stub);

        var act = () => sut.AuthenticateCallbackAsync(Slug, "auth-code", "state-1", "https://web.example.test/cb");

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*not served by OIDC provider*");
    }

    [Fact]
    public async Task AuthenticateCallbackAsync_EmailWithinConfiguredDomains_Accepted()
    {
        var stub = new StubOidcProvider(_signingKey);
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions(emailDomains: ["corp.example.test", "subsidiary.example.test"]))), stub);

        var identity = await sut.AuthenticateCallbackAsync(Slug, "auth-code", "state-1", "https://web.example.test/cb");

        identity.Email.Should().Be("jane@corp.example.test");
    }

    // ── Logout forwarding + domain discovery ──────────────────────────────────

    [Fact]
    public async Task GetEndSessionEndpointAsync_WhenAdvertised_ReturnsUrl()
    {
        var stub = new StubOidcProvider(_signingKey) { EndSessionEndpoint = "https://login.corp.example.test/logout" };
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var endpoint = await sut.GetEndSessionEndpointAsync(Slug);

        endpoint.Should().Be("https://login.corp.example.test/logout");
    }

    [Fact]
    public async Task GetEndSessionEndpointAsync_WhenNotAdvertised_ReturnsNull()
    {
        var stub = new StubOidcProvider(_signingKey) { EndSessionEndpoint = null };
        using var _ = stub;
        var sut = CreateSut(AuthOptionsWith((Slug, ProviderOptions())), stub);

        var endpoint = await sut.GetEndSessionEndpointAsync(Slug);

        endpoint.Should().BeNull();
    }

    [Fact]
    public void FindProvidersForEmailDomain_MatchesEnabledProvidersCaseInsensitively()
    {
        var disabled = ProviderOptions(emailDomains: ["corp.example.test"]);
        disabled.Enabled = false;
        var options = AuthOptionsWith(
            (Slug, ProviderOptions(emailDomains: ["corp.example.test"])),
            ("partner-idp", ProviderOptions(emailDomains: ["corp.Example.TEST", "partner.example.test"])),
            ("disabled-idp", disabled));

        var sut = CreateSut(options, new StubOidcProvider(_signingKey));

        var providers = sut.FindProvidersForEmailDomain("CORP.example.test");

        providers.Select(p => p.Slug).Should().BeEquivalentTo([Slug, "partner-idp"]);
        providers.Single(p => p.Slug == "partner-idp").DisplayName.Should().Be("partner-idp");
    }

    [Fact]
    public void FindProvidersForEmailDomain_NoMatch_ReturnsEmpty()
    {
        var options = AuthOptionsWith((Slug, ProviderOptions(emailDomains: ["corp.example.test"])));
        var sut = CreateSut(options, new StubOidcProvider(_signingKey));

        sut.FindProvidersForEmailDomain("unknown.example.test").Should().BeEmpty();
    }

    // ── Stub OIDC provider (test double) ──────────────────────────────────────

    private sealed class StubOidcProvider : IDisposable
    {
        private readonly HttpMessageHandler _handler;
        private readonly Mock<IHttpClientFactory> _factory = new();
        private readonly RSA _jwksKey;
        private readonly string _kid = "test-kid-1";

        public StubOidcProvider(RSA jwksKey)
        {
            _jwksKey = jwksKey;
            _handler = new InlineHandler(this);
            // A fresh HttpClient per CreateClient call — the service disposes each client it creates.
            _factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(_handler, disposeHandler: false));
        }

        public string Issuer { get; set; } = Authority;

        public string? EndSessionEndpoint { get; set; } = "https://login.corp.example.test/oidc/logout";

        public string? TokenIssuerOverride { get; set; }

        public string? TokenAudienceOverride { get; set; }

        public bool TokenExpired { get; set; }

        public RSA? SigningKeyOverride { get; set; }

        public string? RawIdTokenOverride { get; set; }

        public bool OmitIdToken { get; set; }

        public bool OmitSubject { get; set; }

        public HttpStatusCode TokenEndpointStatus { get; set; } = HttpStatusCode.OK;

        public Action<List<Claim>>? CustomClaims { get; set; }

        public int DiscoveryHits { get; private set; }

        public int JwksHits { get; private set; }

        public int TokenHits { get; private set; }

        public string? LastTokenRequestBody { get; private set; }

        public IHttpClientFactory HttpClientFactory => _factory.Object;

        public void Dispose()
        {
            _handler.Dispose();
        }

        private HttpResponseMessage Handle(HttpRequestMessage request)
        {
            var url = request.RequestUri!.ToString();

            if (request.Method == HttpMethod.Get && url.EndsWith(".well-known/openid-configuration", StringComparison.OrdinalIgnoreCase))
            {
                DiscoveryHits++;
                var payload = new Dictionary<string, object?>
                {
                    ["issuer"] = Issuer,
                    ["authorization_endpoint"] = $"{Issuer}/authorize",
                    ["token_endpoint"] = $"{Issuer}/token",
                    ["jwks_uri"] = $"{Issuer}/.well-known/jwks.json",
                    ["response_types_supported"] = new[] { "code" }
                };
                if (EndSessionEndpoint is not null)
                {
                    payload["end_session_endpoint"] = EndSessionEndpoint;
                }

                return Json(payload);
            }

            if (request.Method == HttpMethod.Get && url.EndsWith(".well-known/jwks.json", StringComparison.OrdinalIgnoreCase))
            {
                JwksHits++;
                var parameters = _jwksKey.ExportParameters(includePrivateParameters: false);
                return Json(new Dictionary<string, object?>
                {
                    ["keys"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["kty"] = "RSA",
                            ["use"] = "sig",
                            ["alg"] = "RS256",
                            ["kid"] = _kid,
                            ["n"] = Base64UrlEncoder.Encode(parameters.Modulus!),
                            ["e"] = Base64UrlEncoder.Encode(parameters.Exponent!)
                        }
                    }
                });
            }

            if (request.Method == HttpMethod.Post && url.EndsWith("/token", StringComparison.OrdinalIgnoreCase))
            {
                TokenHits++;
                LastTokenRequestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                if (TokenEndpointStatus != HttpStatusCode.OK)
                {
                    return new HttpResponseMessage(TokenEndpointStatus) { Content = new StringContent("{\"error\":\"invalid_grant\"}") };
                }

                var idToken = RawIdTokenOverride ?? CreateDefaultIdToken();
                var body = OmitIdToken
                    ? new Dictionary<string, object?> { ["access_token"] = "provider-access-token", ["token_type"] = "Bearer" }
                    : new Dictionary<string, object?> { ["access_token"] = "provider-access-token", ["token_type"] = "Bearer", ["id_token"] = idToken };
                return Json(body);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent($"Unexpected request {request.Method} {url}") };
        }

        private string CreateDefaultIdToken()
        {
            var issuer = TokenIssuerOverride ?? Issuer;
            var audience = TokenAudienceOverride ?? ClientId;
            // An expired token still needs a well-formed lifetime window (nbf < exp):
            // JwtPayload refuses to construct when expires precedes notBefore.
            var notBefore = TokenExpired ? DateTimeOffset.UtcNow.AddMinutes(-30) : DateTimeOffset.UtcNow.AddMinutes(-5);
            var expires = TokenExpired ? DateTimeOffset.UtcNow.AddMinutes(-10) : DateTimeOffset.UtcNow.AddMinutes(10);

            List<Claim> claims;
            if (CustomClaims is not null)
            {
                claims = [];
                CustomClaims(claims);
            }
            else
            {
                claims = new List<Claim>();
                if (!OmitSubject)
                {
                    claims.Add(new Claim(JwtRegisteredClaimNames.Sub, "corp-sub-42"));
                }

                claims.Add(new Claim(JwtRegisteredClaimNames.Email, "jane@corp.example.test"));
                claims.Add(new Claim("email_verified", "true", ClaimValueTypes.Boolean));
                claims.Add(new Claim(JwtRegisteredClaimNames.Name, "Jane Corp"));
                claims.Add(new Claim("amr", "pwd", ClaimValueTypes.String));
                claims.Add(new Claim("amr", "mfa", ClaimValueTypes.String));
                claims.Add(new Claim("acr", "urn:corp:acr:2fa"));
            }

            return CreateSignedToken(SigningKeyOverride ?? _jwksKey, issuer, audience, notBefore, expires, claims);
        }

        private static string CreateSignedToken(RSA key, string issuer, string audience, DateTimeOffset notBefore, DateTimeOffset expires, IEnumerable<Claim> claims)
        {
            var credentials = new SigningCredentials(new RsaSecurityKey(key) { KeyId = "test-kid-1" }, SecurityAlgorithms.RsaSha256);
            var header = new JwtHeader(credentials);
            var payload = new JwtPayload(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: notBefore.UtcDateTime,
                expires: expires.UtcDateTime,
                issuedAt: DateTime.UtcNow);

            var token = new JwtSecurityToken(header, payload);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static HttpResponseMessage Json(Dictionary<string, object?> payload)
        {
            var json = JsonSerializer.Serialize(payload);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }

        private sealed class InlineHandler(StubOidcProvider owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(owner.Handle(request));
        }
    }
}
