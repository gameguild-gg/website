using System.Security.Claims;
using System.Text.Json;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Middleware;

public sealed class SessionBoundTokenRevocationTests
{
    public static TheoryData<string, bool> InvalidSessionCases
    {
        get
        {
            var cases = new TheoryData<string, bool>();
            foreach (var scenario in new[] { "missing", "inactive", "terminated", "expired", "other-user", "malformed", "empty", "no-user", "duplicate" })
            {
                cases.Add(scenario, false);
                cases.Add(scenario, true);
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(InvalidSessionCases))]
    public async Task InvalidSessionClearsIdentityAndOnlyExplicitPublicEndpointsContinue(string scenario, bool anonymous)
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var claims = new List<Claim>();
        if (scenario != "no-user") { claims.Add(new Claim("sub", userId.ToString())); }
        claims.Add(new Claim(JwtClaimTypes.SessionId, scenario switch
        {
            "malformed" => "invalid-session", "empty" => Guid.Empty.ToString(), _ => sessionId.ToString()
        }));
        if (scenario == "duplicate") { claims.Add(new Claim(JwtClaimTypes.SessionId, sessionId.ToString())); }
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) };
        context.Response.Body = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        context.RequestAborted = cancellation.Token;
        if (anonymous)
        {
            context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AllowAnonymousAttribute()), "public"));
        }
        var sessions = new Mock<IUserSessionRepository>(MockBehavior.Strict);
        var readsStorage = scenario is "missing" or "inactive" or "terminated" or "expired" or "other-user";
        if (readsStorage)
        {
            sessions.Setup(value => value.GetByIdAsync(sessionId, cancellation.Token)).ReturnsAsync(scenario == "missing" ? null : new UserSession
            {
                Id = sessionId, UserId = scenario == "other-user" ? Guid.NewGuid() : userId,
                IsActive = scenario != "inactive", TerminatedAt = scenario == "terminated" ? DateTime.UtcNow : null,
                ExpiresAt = scenario == "expired" ? DateTime.UtcNow.AddSeconds(-1) : DateTime.UtcNow.AddHours(1)
            });
        }
        var revocation = new Mock<ITokenRevocationService>(MockBehavior.Strict);
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        var called = false;
        var middleware = new TokenRevocationMiddleware(_ =>
        {
            called = true;
            Assert.False(context.User.Identity?.IsAuthenticated);
            return Task.CompletedTask;
        }, NullLogger<TokenRevocationMiddleware>.Instance);

        await middleware.InvokeAsync(context, revocation.Object, users.Object, sessions.Object);

        Assert.False(context.User.Identity?.IsAuthenticated);
        Assert.Equal(anonymous, called);
        if (!anonymous)
        {
            Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
            Assert.Equal("Bearer", context.Response.Headers.WWWAuthenticate.ToString());
            Assert.Contains("application/problem+json", context.Response.ContentType);
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            using var problem = JsonDocument.Parse(body);
            Assert.Equal("Invalid access token", problem.RootElement.GetProperty("detail").GetString());
            Assert.DoesNotContain(userId.ToString(), body, StringComparison.Ordinal);
            Assert.DoesNotContain(sessionId.ToString(), body, StringComparison.Ordinal);
        }
        if (readsStorage) { sessions.Verify(value => value.GetByIdAsync(sessionId, cancellation.Token), Times.Once); }
        sessions.VerifyNoOtherCalls();
        revocation.VerifyNoOtherCalls();
        users.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ActiveOwnedUnexpiredSessionPreservesAuthenticatedIdentity()
    {
        var user = Guid.NewGuid();
        var session = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", user.ToString()), new Claim(JwtClaimTypes.SessionId, session.ToString())], "Bearer"))
        };
        var sessions = new Mock<IUserSessionRepository>(MockBehavior.Strict);
        sessions.Setup(value => value.GetByIdAsync(session, It.IsAny<CancellationToken>())).ReturnsAsync(new UserSession
        {
            Id = session, UserId = user, IsActive = true, ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        var called = false;
        var middleware = new TokenRevocationMiddleware(_ =>
        {
            called = true;
            Assert.True(context.User.Identity?.IsAuthenticated);
            return Task.CompletedTask;
        }, NullLogger<TokenRevocationMiddleware>.Instance);

        await middleware.InvokeAsync(context, Mock.Of<ITokenRevocationService>(), Mock.Of<IUserRepository>(), sessions.Object);

        Assert.True(called);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        sessions.Verify(value => value.GetByIdAsync(session, context.RequestAborted), Times.Once);
        sessions.VerifyNoOtherCalls();
    }
}
