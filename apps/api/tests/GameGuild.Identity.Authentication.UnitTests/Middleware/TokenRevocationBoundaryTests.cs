using System.Security.Claims;
using System.Text.Json;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Middleware;

public sealed class TokenRevocationBoundaryTests
{
    [Theory]
    [InlineData("jti", false)]
    [InlineData("user", false)]
    [InlineData("version", false)]
    [InlineData("missing-user", false)]
    [InlineData("jti", true)]
    [InlineData("user", true)]
    [InlineData("version", true)]
    [InlineData("missing-user", true)]
    public async Task RevokedIdentityIsClearedAndOnlyExplicitAnonymousEndpointsContinue(string scenario, bool anonymous)
    {
        var user = Guid.NewGuid();
        var jti = Guid.NewGuid().ToString("N");
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", user.ToString()), new Claim("jti", jti), new Claim("token_version", "1"),
            new Claim("iat", DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture))], "Bearer"));
        if (anonymous)
        {
            context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AllowAnonymousAttribute()), "public"));
        }
        var revocation = new Mock<ITokenRevocationService>();
        revocation.Setup(value => value.IsRevokedAsync(jti, It.IsAny<CancellationToken>())).ReturnsAsync(scenario == "jti");
        revocation.Setup(value => value.IsUserTokenRevokedAsync(user, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(scenario == "user");
        var repository = new Mock<IUserRepository>();
        repository.Setup(value => value.GetTokenVersionAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario == "missing-user" ? (int?)null : 2);
        var nextCalled = false;
        var middleware = new TokenRevocationMiddleware(_ =>
        {
            nextCalled = true;
            Assert.False(context.User.Identity?.IsAuthenticated);
            Assert.Empty(context.User.Claims);
            return Task.CompletedTask;
        }, NullLogger<TokenRevocationMiddleware>.Instance);

        await middleware.InvokeAsync(context, revocation.Object, repository.Object, Mock.Of<IUserSessionRepository>());

        Assert.Equal(anonymous, nextCalled);
        Assert.False(context.User.Identity?.IsAuthenticated);
        Assert.Empty(context.User.Claims);
        if (anonymous)
        {
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.Equal(0, context.Response.Body.Length);
        }
        else
        {
            Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
            Assert.Equal("Bearer", context.Response.Headers.WWWAuthenticate.ToString());
            Assert.Contains("application/problem+json", context.Response.ContentType);
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            using var problem = JsonDocument.Parse(body);
            Assert.Equal(401, problem.RootElement.GetProperty("status").GetInt32());
            Assert.Equal("Invalid access token", problem.RootElement.GetProperty("detail").GetString());
            Assert.DoesNotContain(jti, body, StringComparison.Ordinal);
            Assert.DoesNotContain(user.ToString(), body, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("legacy-user")]
    [InlineData("service")]
    public async Task TokensWithoutUserVersionRetainExistingCompatibility(string kind)
    {
        var context = new DefaultHttpContext();
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()) };
        if (kind == "service")
        {
            claims.Add(new Claim("actor_kind", "Service"));
            claims.Add(new Claim("grant_type", "client_credentials"));
        }
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        var revocation = new Mock<ITokenRevocationService>(MockBehavior.Strict);
        var called = false;
        var middleware = new TokenRevocationMiddleware(_ =>
        {
            called = true;
            Assert.True(context.User.Identity?.IsAuthenticated);
            return Task.CompletedTask;
        }, NullLogger<TokenRevocationMiddleware>.Instance);

        var sessions = new Mock<IUserSessionRepository>(MockBehavior.Strict);
        await middleware.InvokeAsync(context, revocation.Object, repository.Object, sessions.Object);

        Assert.True(called);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        repository.VerifyNoOtherCalls();
        revocation.VerifyNoOtherCalls();
        sessions.VerifyNoOtherCalls();
    }
}
