using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;
using GameGuild.Learning.Lti;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace GameGuild.Learning.Lti.Tests;

/// <summary>
/// LTI 1.3 launch validation tests: OIDC login initiation, id_token validation
/// (signature/issuer/audience/lifetime), single-use state+nonce, form-body-only tokens,
/// and fail-closed user mapping (no auto-provisioning).
/// </summary>
public class LtiLaunchValidationTests
{
    private const string Issuer = "https://canvas.test";
    private const string ClientId = "client-1";
    private const string PlatformKid = "platform-key-1";

    private readonly TestLtiDbContext _db = CreateContext();
    private readonly LtiLaunchStateStore _stateStore = new();
    private readonly RSA _platformRsa = RSA.Create(2048);
    private readonly LtiDeployment _deployment;
    private readonly Mock<IJwtTokenService> _jwtTokenService = new();
    private readonly Guid _userId = Guid.NewGuid();

    public LtiLaunchValidationTests()
    {
        _deployment = LtiDeployment.Create(
            Issuer, ClientId, "deployment-1",
            "https://canvas.test/api/lti/security/token",
            "https://canvas.test/api/lti/security/jwks",
            "https://canvas.test/api/lti/authorize_redirect",
            "tool-key-1",
            RSA.Create(2048).ExportPkcs8PrivateKeyPem());
        _db.Set<LtiDeployment>().Add(_deployment);
        _db.Set<User>().Add(new User { Id = _userId, Email = "student@test.com", Name = "Student" });
        _db.SaveChanges();

        _jwtTokenService
            .Setup(s => s.GenerateAccessTokenAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("gg-test-token");
    }

    private static TestLtiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestLtiDbContext>()
            .UseInMemoryDatabase($"LtiLaunch_{Guid.NewGuid()}")
            .Options;
        return new TestLtiDbContext(options);
    }

    // ===== helpers =====

    private LtiController CreateController(HttpContext? http = null, IActorContextAccessor? actor = null)
    {
        var handler = new LtiEndpointCommandHandler(
            _db,
            _stateStore,
            new LtiPlatformJwksService(PlatformJwksFactory(), NullLogger<LtiPlatformJwksService>.Instance),
            _jwtTokenService.Object,
            NullLogger<LtiEndpointCommandHandler>.Instance);
        var controller = new LtiController(
            _db,
            _stateStore,
            actor ?? Mock.Of<IActorContextAccessor>(),
            NullLogger<LtiController>.Instance,
            new LtiTestSender(handler));
        controller.ControllerContext = new ControllerContext { HttpContext = http ?? new DefaultHttpContext() };
        return controller;
    }

    private sealed class LtiTestSender(LtiEndpointCommandHandler handler) : ISender
    {
        public async Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            object result = request switch
            {
                LaunchLtiCommand command => await handler.Handle(command, cancellationToken),
                CreateLtiDeploymentCommand command => await handler.Handle(command, cancellationToken),
                CreateLtiLineItemCommand command => await handler.Handle(command, cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported LTI test request: {request.GetType().Name}.")
            };

            return (TResponse)result;
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest => throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private StubHttpClientFactory PlatformJwksFactory()
    {
        var jwks = BuildJwksJson(_platformRsa, PlatformKid);
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jwks, System.Text.Encoding.UTF8, "application/json")
        });
        return new StubHttpClientFactory(new HttpClient(handler));
    }

    private static string BuildJwksJson(RSA rsa, string kid)
    {
        var p = rsa.ExportParameters(false);
        var n = Base64UrlEncoder.Encode(p.Modulus!);
        var e = Base64UrlEncoder.Encode(p.Exponent!);
        return $$"""{"keys":[{"kty":"RSA","alg":"RS256","use":"sig","kid":"{{kid}}","n":"{{n}}","e":"{{e}}"}]}""";
    }

    private static HttpContext FormPost(Dictionary<string, string> form)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Form = new FormCollection(
            form.ToDictionary(kv => kv.Key, kv => new StringValues(kv.Value)));
        return http;
    }

    private (string State, string Nonce) LoginForState()
    {
        var http = FormPost(new Dictionary<string, string>
        {
            ["iss"] = Issuer,
            ["client_id"] = ClientId,
            ["deployment_id"] = "deployment-1",
            ["login_hint"] = "student-777",
            ["lti_message_hint"] = "hint-1"
        });
        var result = CreateController(http).Login().GetAwaiter().GetResult();
        var url = result.Should().BeOfType<RedirectResult>().Which.Url!;
        var query = ParseQuery(url);
        return (query["state"], query["nonce"]);
    }

    private static Dictionary<string, string> ParseQuery(string url)
    {
        var query = new Uri(url).Query.TrimStart('?');
        return query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(kv => Uri.UnescapeDataString(kv[0]), kv => Uri.UnescapeDataString(kv.Length > 1 ? kv[1] : ""));
    }

    private string BuildIdToken(string? sub, string? email, string nonce, string? issuerOverride = null, DateTime? expiresOverride = null)
    {
        var claims = new List<Claim> { new("sub", sub ?? "user-abc") };
        if (email != null)
        {
            claims.Add(new Claim("email", email));
        }
        claims.Add(new Claim("nonce", nonce));
        claims.Add(new Claim("https://purl.imsglobal.org/spec/lti/claim/deployment_id", "deployment-1"));

        var exp = expiresOverride ?? DateTime.UtcNow.AddMinutes(10);
        var handler = new JwtSecurityTokenHandler();
        return handler.CreateEncodedJwt(
            issuerOverride ?? Issuer,
            ClientId,
            new ClaimsIdentity(claims),
            notBefore: exp.AddMinutes(-10),
            expires: exp,
            issuedAt: exp.AddMinutes(-5),
            signingCredentials: new SigningCredentials(
                new RsaSecurityKey(_platformRsa) { KeyId = PlatformKid },
                SecurityAlgorithms.RsaSha256));
    }

    private HttpContext LaunchForm(string state, string idToken) =>
        FormPost(new Dictionary<string, string> { ["state"] = state, ["id_token"] = idToken });

    private static IActorContextAccessor ActorContext(bool isSystemAdmin)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.Setup(a => a.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            IsAuthenticated = true,
            Roles = isSystemAdmin ? new HashSet<string> { "SystemAdmin" } : new HashSet<string>(),
            Permissions = new HashSet<string>()
        });
        return accessor.Object;
    }

    // ===== login (OIDC third-party initiation) =====

    [Fact]
    public async Task Login_WithMatchingActiveDeployment_RedirectsWithStateAndNonce()
    {
        var http = FormPost(new Dictionary<string, string>
        {
            ["iss"] = Issuer,
            ["client_id"] = ClientId,
            ["deployment_id"] = "deployment-1",
            ["login_hint"] = "student-777",
            ["lti_message_hint"] = "hint-1"
        });

        var result = await CreateController(http).Login();

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().StartWith("https://canvas.test/api/lti/authorize_redirect");
        redirect.Permanent.Should().BeFalse();
        var query = ParseQuery(redirect.Url!);
        query["scope"].Should().Be("openid");
        query["response_type"].Should().Be("id_token");
        query["response_mode"].Should().Be("form_post");
        query["client_id"].Should().Be(ClientId);
        query["login_hint"].Should().Be("student-777");
        query["lti_message_hint"].Should().Be("hint-1");
        query["redirect_uri"].Should().EndWith("/lti/launch");
        query["state"].Should().NotBeNullOrEmpty();
        query["nonce"].Should().NotBeNullOrEmpty();
        query["state"].Should().NotBe(query["nonce"]);
    }

    [Fact]
    public async Task Login_WithUnknownIssuer_Returns401()
    {
        var http = FormPost(new Dictionary<string, string>
        {
            ["iss"] = "https://evil.test",
            ["client_id"] = ClientId,
            ["deployment_id"] = "deployment-1",
            ["login_hint"] = "x"
        });

        var result = await CreateController(http).Login();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ===== launch validation =====

    [Fact]
    public async Task Launch_WithValidTokenAndEmailMatch_RedirectsAndUpsertsUserMapping()
    {
        var (state, nonce) = LoginForState();
        var token = BuildIdToken(sub: "user-abc", email: "Student@Test.Com", nonce); // case-insensitive email match

        var result = await CreateController(LaunchForm(state, token)).Launch();

        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("/dashboard/tasks");
        var mapping = _db.Set<LtiUserMapping>().Should().ContainSingle().Subject;
        mapping.UserId.Should().Be(_userId);
        mapping.Sub.Should().Be("user-abc");
        mapping.DeploymentId.Should().Be(_deployment.Id);
        _db.Set<User>().Should().HaveCount(1); // no provisioning
    }

    [Fact]
    public async Task Launch_WithExistingUserMapping_DoesNotNeedEmailClaim()
    {
        _db.Set<LtiUserMapping>().Add(LtiUserMapping.Create(_deployment.Id, _userId, "known-sub"));
        _db.SaveChanges();
        var (state, nonce) = LoginForState();
        var token = BuildIdToken(sub: "known-sub", email: null, nonce);

        var result = await CreateController(LaunchForm(state, token)).Launch();

        result.Should().BeOfType<RedirectResult>().Which.Url.Should().Be("/dashboard/tasks");
    }

    [Fact]
    public async Task Launch_SetsSessionCookieFromTokenService()
    {
        var (state, nonce) = LoginForState();
        var token = BuildIdToken(sub: "known", email: "Student@Test.Com", nonce);
        var http = LaunchForm(state, token);

        await CreateController(http).Launch();

        http.Response.Headers.SetCookie.Should().Contain(cookie => cookie.Contains("gg_session=gg-test-token"));
        _jwtTokenService.Verify(
            s => s.GenerateAccessTokenAsync(_userId, "student@test.com", It.IsAny<string[]>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Launch_WithWrongState_Returns401()
    {
        var (_, nonce) = LoginForState();
        var token = BuildIdToken(sub: "user-abc", email: "Student@Test.Com", nonce);

        var result = await CreateController(LaunchForm("forged-state", token)).Launch();

        result.Should().BeOfType<UnauthorizedObjectResult>();
        _db.Set<LtiUserMapping>().Should().BeEmpty();
    }

    [Fact]
    public async Task Launch_WithReplayedTokenAndState_Returns401SecondTime()
    {
        var (state, nonce) = LoginForState();
        var token = BuildIdToken(sub: "user-abc", email: "Student@Test.Com", nonce);

        (await CreateController(LaunchForm(state, token)).Launch()).Should().BeOfType<RedirectResult>();
        (await CreateController(LaunchForm(state, token)).Launch()).Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Launch_WithExpiredToken_Returns401()
    {
        var (state, nonce) = LoginForState();
        var token = BuildIdToken(sub: "user-abc", email: "Student@test.com", nonce, expiresOverride: DateTime.UtcNow.AddHours(-1));

        var result = await CreateController(LaunchForm(state, token)).Launch();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Launch_WithTamperedIssuer_Returns401()
    {
        // Signature is valid (platform key) but the iss claim points elsewhere: must fail closed.
        var (state, nonce) = LoginForState();
        var token = BuildIdToken(sub: "user-abc", email: "Student@test.com", nonce, issuerOverride: "https://evil.test");

        var result = await CreateController(LaunchForm(state, token)).Launch();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Launch_WithIdTokenInQueryString_Returns400()
    {
        var (state, nonce) = LoginForState();
        var token = BuildIdToken(sub: "user-abc", email: "Student@test.com", nonce);
        var http = LaunchForm(state, token);
        http.Request.QueryString = new QueryString("?id_token=" + Uri.EscapeDataString(token));

        var result = await CreateController(http).Launch();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Launch_WithUnknownSubAndEmail_Returns401AndDoesNotCreateAnything()
    {
        var (state, nonce) = LoginForState();
        var token = BuildIdToken(sub: "stranger", email: "stranger@elsewhere.test", nonce);

        var result = await CreateController(LaunchForm(state, token)).Launch();

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.Value!.ToString().Should().Contain("No gameguild account matches this launch");
        _db.Set<LtiUserMapping>().Should().BeEmpty();
        _db.Set<User>().Should().HaveCount(1); // nothing provisioned
    }

    [Fact]
    public async Task Launch_WithoutIdToken_Returns400()
    {
        var result = await CreateController(FormPost(new Dictionary<string, string> { ["state"] = "x" })).Launch();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ===== jwks =====

    [Fact]
    public async Task Jwks_ServesUnionOfActiveDeploymentKeysWithKid()
    {
        var inactive = LtiDeployment.Create(
            "https://other.test", "client-2", "deployment-2",
            "https://other.test/token", "https://other.test/jwks",
            "https://other.test/auth", "inactive-key",
            RSA.Create(2048).ExportPkcs8PrivateKeyPem(), active: false);
        _db.Set<LtiDeployment>().Add(inactive);
        _db.SaveChanges();

        var result = await CreateController().Jwks();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value!);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        var keys = doc.RootElement.GetProperty("keys");
        keys.GetArrayLength().Should().Be(1);
        var key = keys[0];
        key.GetProperty("kid").GetString().Should().Be("tool-key-1");
        key.GetProperty("kty").GetString().Should().Be("RSA");
        key.GetProperty("use").GetString().Should().Be("sig");
        key.GetProperty("n").GetString().Should().NotBeNullOrEmpty();
        key.GetProperty("e").GetString().Should().NotBeNullOrEmpty();
    }

    // ===== admin endpoints =====

    [Fact]
    public async Task CreateDeployment_WhenActorIsNotSystemAdmin_ReturnsForbid()
    {
        var controller = CreateController(actor: ActorContext(isSystemAdmin: false));

        var result = await controller.CreateDeployment(new CreateLtiDeploymentRequest(
            "https://canvas2.test", "c", "d", "https://t", "https://j", "https://a", "k", "secret-pem"));

        result.Should().BeOfType<ForbidResult>();
        _db.Set<LtiDeployment>().Should().HaveCount(1); // only the fixture deployment
    }

    [Fact]
    public async Task CreateDeployment_WhenSystemAdmin_Returns201WithoutPrivateKey()
    {
        var controller = CreateController(actor: ActorContext(isSystemAdmin: true));

        var result = await controller.CreateDeployment(new CreateLtiDeploymentRequest(
            "https://canvas2.test", "c", "d", "https://t", "https://j", "https://a", "k", "secret-pem"));

        var created = result.Should().BeAssignableTo<ObjectResult>().Subject;
        created.StatusCode.Should().Be(201);
        System.Text.Json.JsonSerializer.Serialize(created.Value!)
            .Should().NotContain("secret-pem");
        typeof(LtiDeploymentDto).GetProperty("PrivateKeyPem").Should().BeNull();
    }

    [Fact]
    public async Task CreateLineItem_WhenSystemAdmin_Returns201()
    {
        var controller = CreateController(actor: ActorContext(isSystemAdmin: true));
        var assessmentId = Guid.NewGuid();

        var result = await controller.CreateLineItem(
            _deployment.Id,
            new CreateLtiLineItemRequest(assessmentId, "line-1", "https://canvas.test/api/lti/courses/1/line_items/9", 100));

        result.Should().BeAssignableTo<ObjectResult>().Which.StatusCode.Should().Be(201);
        _db.Set<LtiLineItemMapping>().Should().ContainSingle()
            .Which.MaxScore.Should().Be(100);
    }

    [Fact]
    public async Task CreateLineItem_WhenDeploymentMissing_Returns404()
    {
        var controller = CreateController(actor: ActorContext(isSystemAdmin: true));

        var result = await controller.CreateLineItem(
            Guid.NewGuid(),
            new CreateLtiLineItemRequest(Guid.NewGuid(), "line-1", "https://canvas.test/line_items/9", 100));

        result.Should().BeOfType<NotFoundResult>();
    }
}
