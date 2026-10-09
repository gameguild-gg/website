using System.Net;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Tests for the OAuth2 client-credentials external authorization-decision provider
///     (issue #146): disabled no-op behavior, token acquisition and caching, wire
///     mapping, fail modes (Enforce = fail-closed deny, Observe = no decision) and the
///     short-TTL decision cache. All HTTP traffic goes through an in-memory handler
///     double — no real outbound calls.
/// </summary>
public class HttpExternalAuthorizationDecisionProviderTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private static ExternalAuthorizationQuery Query(Guid userId, Guid tenantId, string permission = "reports:read") =>
        new() { UserId = userId, TenantId = tenantId, Permission = permission };

    private static HttpResponseMessage TokenResponse(string token = "token-1", long expiresIn = 3600) =>
        Json(new { access_token = token, expires_in = expiresIn });

    private static HttpResponseMessage Json(object payload, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
        return new HttpResponseMessage(statusCode) { Content = content };
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri? Uri, string Body, string? Authorization)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri, body, request.Headers.Authorization?.ToString()));
            return responder(request);
        }
    }

    private static HttpExternalAuthorizationDecisionProvider CreateProvider(
        RecordingHandler handler,
        ExternalAuthorizationOptions? options = null)
    {
        options ??= new ExternalAuthorizationOptions
        {
            Enabled = true,
            Endpoint = "https://pdp.example.test/decisions",
            TokenEndpoint = "https://idp.example.test/token",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            FailMode = ExternalAuthorizationFailMode.Enforce
        };

        return new HttpExternalAuthorizationDecisionProvider(
            new HttpClient(handler),
            Options.Create(options),
            NullLogger<HttpExternalAuthorizationDecisionProvider>.Instance);
    }

    private static ExternalAuthorizationOptions ObserveOptions() =>
        new()
        {
            Enabled = true,
            Endpoint = "https://pdp.example.test/decisions",
            TokenEndpoint = "https://idp.example.test/token",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            FailMode = ExternalAuthorizationFailMode.Observe
        };

    private static RecordingHandler AllowAllHandler() =>
        new(request => TokenThenDecision(_ => Json(new { decision = "allow" }))(request));

    private static Func<HttpRequestMessage, HttpResponseMessage> TokenThenDecision(
        Func<HttpRequestMessage, HttpResponseMessage> decisionResponder,
        Func<HttpRequestMessage, HttpResponseMessage>? tokenResponder = null) =>
        request => IsTokenRequest(request)
            ? (tokenResponder ?? (_ => TokenResponse()))(request)
            : decisionResponder(request);

    private static bool IsTokenRequest(HttpRequestMessage request) =>
        request.RequestUri?.AbsolutePath.EndsWith("/token", StringComparison.OrdinalIgnoreCase) == true;

    // ── Disabled: zero overhead, no decision, no outbound call ──────────────

    [Fact]
    public async Task EvaluateAsync_Disabled_ReturnsNull_AndMakesNoHttpCall()
    {
        var handler = AllowAllHandler();
        var provider = CreateProvider(handler, new ExternalAuthorizationOptions { Enabled = false });

        var decision = await provider.EvaluateAsync(Query(_userId, _tenantId));

        decision.Should().BeNull();
        handler.Requests.Should().BeEmpty();
    }

    // ── Wire contract: token, bearer header, JSON payload, mapping ──────────

    [Fact]
    public async Task EvaluateAsync_AcquiresTokenViaClientCredentials_AndAuthorizesDecisionWithBearer()
    {
        var handler = AllowAllHandler();
        var provider = CreateProvider(handler);

        await provider.EvaluateAsync(Query(_userId, _tenantId, "documents:write"));

        handler.Requests.Should().HaveCount(2);

        var tokenRequest = handler.Requests[0];
        tokenRequest.Uri!.ToString().Should().Be("https://idp.example.test/token");
        tokenRequest.Body.Should().Contain("grant_type=client_credentials");
        tokenRequest.Body.Should().Contain("client_id=client-id");
        tokenRequest.Body.Should().Contain("client_secret=client-secret");

        var decisionRequest = handler.Requests[1];
        decisionRequest.Uri!.ToString().Should().Be("https://pdp.example.test/decisions");
        decisionRequest.Authorization.Should().Be("Bearer token-1");
        decisionRequest.Body.Should().Contain(_userId.ToString());
        decisionRequest.Body.Should().Contain(_tenantId.ToString());
        decisionRequest.Body.Should().Contain("documents:write");
    }

    [Fact]
    public async Task EvaluateAsync_ResourceScopedQuery_IncludesResourceFields()
    {
        var handler = AllowAllHandler();
        var provider = CreateProvider(handler);

        await provider.EvaluateAsync(new ExternalAuthorizationQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Permission = "documents:write",
            ResourceType = "document",
            ResourceId = "42"
        });

        var body = handler.Requests[^1].Body;
        body.Should().Contain("document");
        body.Should().Contain("42");
    }

    [Fact]
    public async Task EvaluateAsync_TenantScopedQuery_OmitsResourceFields()
    {
        var handler = AllowAllHandler();
        var provider = CreateProvider(handler);

        await provider.EvaluateAsync(Query(_userId, _tenantId));

        var body = handler.Requests[^1].Body;
        body.Should().NotContain("resourceType");
        body.Should().NotContain("resourceId");
    }

    [Fact]
    public async Task EvaluateAsync_MapsAllowDenyAndNotApplicableVariants()
    {
        foreach (var (wire, expected) in new[]
                 {
                     ("allow", ExternalAuthorizationOutcome.Allow),
                     ("deny", ExternalAuthorizationOutcome.Deny),
                     ("not_applicable", ExternalAuthorizationOutcome.NotApplicable),
                     ("notApplicable", ExternalAuthorizationOutcome.NotApplicable),
                     ("Not-Applicable", ExternalAuthorizationOutcome.NotApplicable)
                 })
        {
            var handler = new RecordingHandler(TokenThenDecision(_ => Json(new { decision = wire })));
            var provider = CreateProvider(handler);

            var decision = await provider.EvaluateAsync(Query(_userId, _tenantId, $"permission:{wire}"));

            decision.Should().NotBeNull(because: $"wire value '{wire}' must map");
            decision!.Outcome.Should().Be(expected);
        }
    }

    [Fact]
    public async Task EvaluateAsync_Deny_PreservesReasons()
    {
        var handler = new RecordingHandler(TokenThenDecision(_ =>
            Json(new { decision = "deny", reasons = new[] { "outside-business-hours", "policy-7" } })));
        var provider = CreateProvider(handler);

        var decision = await provider.EvaluateAsync(Query(_userId, _tenantId));

        decision!.Outcome.Should().Be(ExternalAuthorizationOutcome.Deny);
        decision.Reasons.Should().Equal("outside-business-hours", "policy-7");
    }

    // ── Token and decision caching ──────────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_CachesTokenAcrossEvaluations()
    {
        var handler = AllowAllHandler();
        var provider = CreateProvider(handler);

        await provider.EvaluateAsync(Query(_userId, _tenantId, "a:read"));
        await provider.EvaluateAsync(Query(_userId, _tenantId, "b:read"));

        handler.Requests.Count(r => IsTokenRequestUri(r.Uri)).Should().Be(1, "the bearer token must be reused until shortly before expiry");
        handler.Requests.Count(r => !IsTokenRequestUri(r.Uri)).Should().Be(2);
    }

    [Fact]
    public async Task EvaluateAsync_CachesDecisionWithinTtl()
    {
        var handler = AllowAllHandler();
        var provider = CreateProvider(handler);

        await provider.EvaluateAsync(Query(_userId, _tenantId));
        await provider.EvaluateAsync(Query(_userId, _tenantId));

        handler.Requests.Count(r => !IsTokenRequestUri(r.Uri)).Should().Be(1, "identical queries within the TTL reuse the cached decision");
    }

    [Fact]
    public async Task EvaluateAsync_ZeroCacheTtl_RequeriesEveryTime()
    {
        var handler = AllowAllHandler();
        var options = new ExternalAuthorizationOptions
        {
            Enabled = true,
            Endpoint = "https://pdp.example.test/decisions",
            TokenEndpoint = "https://idp.example.test/token",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            CacheTtl = TimeSpan.Zero
        };
        var provider = CreateProvider(handler, options);

        await provider.EvaluateAsync(Query(_userId, _tenantId));
        await provider.EvaluateAsync(Query(_userId, _tenantId));

        handler.Requests.Count(r => !IsTokenRequestUri(r.Uri)).Should().Be(2);
    }

    private static bool IsTokenRequestUri(Uri? uri) =>
        uri?.AbsolutePath.EndsWith("/token", StringComparison.OrdinalIgnoreCase) == true;

    // ── Fail modes: Enforce = fail-closed deny, Observe = no decision ───────

    [Fact]
    public async Task EvaluateAsync_TokenEndpointFails_Enforce_ReturnsDeny()
    {
        var handler = new RecordingHandler(TokenThenDecision(
            _ => Json(new { }, HttpStatusCode.InternalServerError),
            _ => Json(new { }, HttpStatusCode.Unauthorized)));
        var provider = CreateProvider(handler);

        var decision = await provider.EvaluateAsync(Query(_userId, _tenantId));

        decision.Should().NotBeNull();
        decision!.Outcome.Should().Be(ExternalAuthorizationOutcome.Deny);
        decision.Reasons.Should().Contain(r => r.Contains("unavailable", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EvaluateAsync_TokenEndpointFails_Observe_ReturnsNull()
    {
        var handler = new RecordingHandler(TokenThenDecision(
            _ => Json(new { }, HttpStatusCode.InternalServerError),
            _ => Json(new { }, HttpStatusCode.Unauthorized)));
        var provider = CreateProvider(handler, ObserveOptions());

        var decision = await provider.EvaluateAsync(Query(_userId, _tenantId));

        decision.Should().BeNull("observe mode must fall back to local resolution instead of denying");
    }

    [Fact]
    public async Task EvaluateAsync_DecisionEndpointNonSuccess_Enforce_ReturnsDeny()
    {
        var handler = new RecordingHandler(TokenThenDecision(_ => Json(new { }, HttpStatusCode.BadGateway)));
        var provider = CreateProvider(handler);

        var decision = await provider.EvaluateAsync(Query(_userId, _tenantId));

        decision!.Outcome.Should().Be(ExternalAuthorizationOutcome.Deny);
    }

    [Fact]
    public async Task EvaluateAsync_DecisionEndpointNonSuccess_Observe_ReturnsNull()
    {
        var handler = new RecordingHandler(TokenThenDecision(_ => Json(new { }, HttpStatusCode.BadGateway)));
        var provider = CreateProvider(handler, ObserveOptions());

        var decision = await provider.EvaluateAsync(Query(_userId, _tenantId));

        decision.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_MalformedPayload_Enforce_ReturnsDeny()
    {
        var handler = new RecordingHandler(TokenThenDecision(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json{", System.Text.Encoding.UTF8, "application/json")
        }));
        var provider = CreateProvider(handler);

        var decision = await provider.EvaluateAsync(Query(_userId, _tenantId));

        decision!.Outcome.Should().Be(ExternalAuthorizationOutcome.Deny);
    }

    [Fact]
    public async Task EvaluateAsync_UnknownDecisionValue_Enforce_ReturnsDeny()
    {
        var handler = new RecordingHandler(TokenThenDecision(_ => Json(new { decision = "maybe" })));
        var provider = CreateProvider(handler);

        var decision = await provider.EvaluateAsync(Query(_userId, _tenantId));

        decision!.Outcome.Should().Be(ExternalAuthorizationOutcome.Deny);
        decision.Reasons.Should().Contain(r => r.Contains("unknown-decision", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EvaluateAsync_NetworkError_Enforce_ReturnsDeny_Observe_ReturnsNull()
    {
        var enforcing = new RecordingHandler(_ => throw new HttpRequestException("connection refused"));
        var enforcingProvider = CreateProvider(enforcing);

        var observing = new RecordingHandler(_ => throw new HttpRequestException("connection refused"));
        var observingProvider = CreateProvider(observing, ObserveOptions());

        var enforceDecision = await enforcingProvider.EvaluateAsync(Query(_userId, _tenantId));
        var observeDecision = await observingProvider.EvaluateAsync(Query(_userId, _tenantId));

        enforceDecision!.Outcome.Should().Be(ExternalAuthorizationOutcome.Deny);
        observeDecision.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_DecisionRequestThrows_Enforce_ReturnsDeny()
    {
        var handler = new RecordingHandler(request => IsTokenRequest(request)
            ? TokenResponse()
            : throw new HttpRequestException("connection reset"));
        var provider = CreateProvider(handler);

        var decision = await provider.EvaluateAsync(Query(_userId, _tenantId));

        decision!.Outcome.Should().Be(ExternalAuthorizationOutcome.Deny);
    }

    [Fact]
    public async Task EvaluateAsync_TokenEndpointThrows_Enforce_ReturnsDeny()
    {
        var handler = new RecordingHandler(_ => throw new HttpRequestException("dns failure"));
        var provider = CreateProvider(handler);

        var decision = await provider.EvaluateAsync(Query(_userId, _tenantId));

        decision!.Outcome.Should().Be(ExternalAuthorizationOutcome.Deny);
    }

    [Fact]
    public async Task EvaluateAsync_TokenResponseWithoutAccessToken_Enforce_ReturnsDeny()
    {
        var handler = new RecordingHandler(TokenThenDecision(
            _ => Json(new { },
                HttpStatusCode.InternalServerError),
            _ => Json(new { expires_in = 3600 })));
        var provider = CreateProvider(handler);

        var decision = await provider.EvaluateAsync(Query(_userId, _tenantId));

        decision!.Outcome.Should().Be(ExternalAuthorizationOutcome.Deny);
    }

    [Fact]
    public async Task EvaluateAsync_EnabledButMisconfigured_Enforce_ReturnsDeny()
    {
        var handler = AllowAllHandler();
        var provider = CreateProvider(handler, new ExternalAuthorizationOptions
        {
            Enabled = true,
            Endpoint = null,
            TokenEndpoint = null
        });

        var decision = await provider.EvaluateAsync(Query(_userId, _tenantId));

        decision!.Outcome.Should().Be(ExternalAuthorizationOutcome.Deny);
        handler.Requests.Should().BeEmpty("a misconfigured provider must not call anything");
    }
}
