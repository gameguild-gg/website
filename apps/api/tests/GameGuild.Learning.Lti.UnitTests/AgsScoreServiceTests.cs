using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;using GameGuild.Learning.Assessments;
using GameGuild.Learning.Lti;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Learning.Lti.Tests;

/// <summary>
/// AGS score passback tests: request shape asserted against an HttpMessageHandler double,
/// no-op without mapping, and failures never propagate to the grading caller.
/// </summary>
public class AgsScoreServiceTests
{
    private readonly TestLtiDbContext _db = CreateContext();
    private readonly CapturingHandler _handler = new(request => request.RequestUri!.AbsolutePath.EndsWith("/token")
        ? Json(new { access_token = "tok-1", expires_in = 3600 })
        : Json(new { }));
    private readonly LtiDeployment _deployment;
    private readonly Guid _assessmentId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly string _sub = "platform-sub-1";

    public AgsScoreServiceTests()
    {
        _deployment = LtiDeployment.Create(
            "https://canvas.test", "client-1", "deployment-1",
            "https://canvas.test/api/lti/security/token",
            "https://canvas.test/api/lti/security/jwks",
            "https://canvas.test/api/lti/authorize_redirect",
            "tool-key-1",
            RSA.Create(2048).ExportPkcs8PrivateKeyPem());
        _db.Set<LtiDeployment>().Add(_deployment);
        _db.SaveChanges();
    }

    private static TestLtiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestLtiDbContext>()
            .UseInMemoryDatabase($"AgsScore_{Guid.NewGuid()}")
            .Options;
        return new TestLtiDbContext(options);
    }

    private static HttpResponseMessage Json(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        };

    private AgsScoreService CreateService() =>
        new(_db, new StubHttpClientFactory(new HttpClient(_handler)), NullLogger<AgsScoreService>.Instance);

    private void SeedMapping(int maxScore = 100)
    {
        _db.Set<LtiLineItemMapping>().Add(LtiLineItemMapping.Create(
            _assessmentId, _deployment.Id, "line-1",
            "https://canvas.test/api/lti/courses/1/line_items/9", maxScore));
        _db.Set<LtiUserMapping>().Add(LtiUserMapping.Create(_deployment.Id, _userId, _sub));
        _db.SaveChanges();
    }

    [Fact]
    public async Task PostScore_WithoutLineItemMapping_MakesNoHttpCall()
    {
        await CreateService().PostScoreIfMappedAsync(_assessmentId, _userId, 85, 100);

        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PostScore_WithoutUserMapping_MakesNoHttpCall()
    {
        _db.Set<LtiLineItemMapping>().Add(LtiLineItemMapping.Create(
            _assessmentId, _deployment.Id, "line-1",
            "https://canvas.test/api/lti/courses/1/line_items/9", 100));
        _db.SaveChanges();

        await CreateService().PostScoreIfMappedAsync(_assessmentId, _userId, 85, 100);

        _handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PostScore_WithMapping_PostsTokenRequestThenSpecShapedScore()
    {
        SeedMapping(maxScore: 25);

        await CreateService().PostScoreIfMappedAsync(_assessmentId, _userId, 18, 25);

        _handler.Requests.Should().HaveCount(2);

        // 1. platform access token: client_credentials + private_key_jwt at AuthTokenUrl
        var tokenRequest = _handler.Requests[0];
        tokenRequest.Method.Should().Be(HttpMethod.Post);
        tokenRequest.RequestUri!.ToString().Should().Be("https://canvas.test/api/lti/security/token");
        tokenRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/x-www-form-urlencoded");
        var form = _handler.Bodies[0].Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(kv => Uri.UnescapeDataString(kv[0]), kv => Uri.UnescapeDataString(kv.Length > 1 ? kv[1] : ""));
        form["grant_type"].Should().Be("client_credentials");
        form["client_assertion_type"].Should().Be("urn:ietf:params:oauth:client-assertion-type:jwt-bearer");
        var assertion = new JwtSecurityTokenHandler().ReadJwtToken(form["client_assertion"]);
        assertion.Header.Alg.Should().Be("RS256");
        assertion.Header.Kid.Should().Be("tool-key-1");
        assertion.Issuer.Should().Be("client-1");
        assertion.Audiences.Should().Contain("https://canvas.test/api/lti/security/token");
        assertion.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == "client-1");

        // 2. AGS score POST with the exact spec body shape
        var scoreRequest = _handler.Requests[1];
        scoreRequest.Method.Should().Be(HttpMethod.Post);
        scoreRequest.RequestUri!.ToString().Should().Be("https://canvas.test/api/lti/courses/1/line_items/9/scores");
        scoreRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        scoreRequest.Headers.Authorization.Parameter.Should().Be("tok-1");
        scoreRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
        var body = System.Text.Json.JsonDocument.Parse(_handler.Bodies[1]).RootElement;
        body.GetProperty("userId").GetString().Should().Be(_sub);
        body.GetProperty("scoreGiven").GetInt32().Should().Be(18);
        body.GetProperty("scoreMaximum").GetInt32().Should().Be(25);
        body.GetProperty("activityProgress").GetString().Should().Be("Completed");
        body.GetProperty("gradingProgress").GetString().Should().Be("FullyGraded");
        body.EnumerateObject().Should().HaveCount(5);
    }

    [Fact]
    public async Task PostScore_WhenPlatformReturns500_DoesNotThrow()
    {
        SeedMapping();
        var failing = new CapturingHandler(request =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("{}") });
        var service = new AgsScoreService(_db, new StubHttpClientFactory(new HttpClient(failing)), NullLogger<AgsScoreService>.Instance);

        var act = () => service.PostScoreIfMappedAsync(_assessmentId, _userId, 85, 100);

        await act.Should().NotThrowAsync();
        failing.Requests.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void AddLtiModule_RegistersAgsScoreServiceAsPassback()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IApplicationDbContext>(_ => CreateContext());
        services.AddLtiModule();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ILtiScorePassback>()
            .Should().BeOfType<AgsScoreService>();
    }
}

internal sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}

internal sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        return responder(request);
    }
}
