using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Lti;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace GameGuild.Learning.Lti.Tests;

/// <summary>
/// The login redirect target must be an absolute https URL from the registered
/// deployment; anything else fails closed with 400 instead of redirecting.
/// </summary>
public class LtiRedirectValidationTests
{
    private const string ClientId = "client-1";

    private readonly TestLtiDbContext _db = CreateContext();
    private readonly LtiLaunchStateStore _stateStore = new();
    private readonly Mock<IJwtTokenService> _jwtTokenService = new();

    [Theory]
    [InlineData("http://canvas.test/api/lti/authorize_redirect")]
    [InlineData("//attacker.test/authorize")]
    [InlineData("https://user:pass@canvas.test/api/lti/authorize_redirect")]
    [InlineData("canvas.test/api/lti/authorize_redirect")]
    [InlineData("file:///etc/passwd")]
    public async Task Login_WithMisconfiguredAuthorizationUrl_FailsClosedWith400(string authorizationUrl)
    {
        var deployment = CreateDeployment(authorizationUrl);
        _db.Set<LtiDeployment>().Add(deployment);
        _db.SaveChanges();

        var result = await CreateController().Login();

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("LTI platform authorization URL is misconfigured.");
    }

    [Fact]
    public async Task Login_WithHttpUrlOnLocalhost_StillFailsClosed()
    {
        var deployment = CreateDeployment("http://localhost:8900/api/lti/authorize_redirect");
        _db.Set<LtiDeployment>().Add(deployment);
        _db.SaveChanges();

        var result = await CreateController().Login();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    private static Dictionary<string, string> LoginForm() => new()
    {
        ["iss"] = "https://canvas.test",
        ["client_id"] = ClientId,
        ["deployment_id"] = "deployment-1",
        ["login_hint"] = "student-777"
    };

    private static LtiDeployment CreateDeployment(string authorizationUrl) => LtiDeployment.Create(
        "https://canvas.test", ClientId, "deployment-1",
        "https://canvas.test/api/lti/security/token",
        "https://canvas.test/api/lti/security/jwks",
        authorizationUrl,
        "tool-key-1",
        System.Security.Cryptography.RSA.Create(2048).ExportPkcs8PrivateKeyPem());

    private LtiController CreateController()
    {
        var controller = new LtiController(
            _db,
            _stateStore,
            Mock.Of<IActorContextAccessor>(),
            NullLogger<LtiController>.Instance,
            new SingleCommandSender());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request =
                {
                    Method = "POST",
                    ContentType = "application/x-www-form-urlencoded",
                    Form = new FormCollection(
                        LoginForm().ToDictionary(kv => kv.Key, kv => new StringValues(kv.Value)))
                }
            }
        };
        return controller;
    }

    private static TestLtiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestLtiDbContext>()
            .UseInMemoryDatabase($"LtiRedirect_{Guid.NewGuid()}")
            .Options;
        return new TestLtiDbContext(options);
    }

    private sealed class SingleCommandSender : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Login should not dispatch commands before redirect validation.");

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest => throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
