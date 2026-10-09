using System.Security.Claims;
using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests.Middleware;

public sealed class ActorContextMiddlewareLoggingSecurityTests
{
    [Fact]
    public async Task InvokeAsync_OnPermissionFetchFailure_SanitizesForgedRequestPathInLog()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Bearer"));
        var claimsAccessor = new StaticClaimsPrincipalAccessor(principal);
        var tenantResolver = new Mock<IAuthorizationTenantResolver>();
        tenantResolver
            .Setup(resolver => resolver.ResolveTenantIdAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());
        var permissionService = new Mock<IAuthorizationPermissionService>();
        permissionService
            .Setup(service => service.GetPermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database offline"));
        var logger = new CapturingLogger();
        var http = new DefaultHttpContext();
        http.Request.Path = "/api/audits\r\n2026-10-06 WARNING forged log line";
        var middleware = new ActorContextMiddleware(_ => Task.CompletedTask, logger);

        await middleware.InvokeAsync(http, new ActorContextAccessor(), tenantResolver.Object, claimsAccessor, permissionService.Object);

        logger.Error.Should().ContainSingle();
        var loggedPath = logger.Error[0].Properties["Path"].Should().BeOfType<string>().Subject;
        loggedPath.Should().Be(LogRedaction.Sanitize(http.Request.Path.Value));
        loggedPath.Should().NotContain("\r").And.NotContain("\n");
        http.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    private sealed class CapturingLogger : ILogger<ActorContextMiddleware>
    {
        public List<(string Rendered, Dictionary<string, object?> Properties)> Error { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                    ? values.ToDictionary(value => value.Key, value => value.Value)
                    : [];
                Error.Add((formatter(state, exception), properties));
            }
        }
    }
}
