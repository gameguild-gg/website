using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class OidcDiscoveryLoggingTests
{
    [Theory]
    [InlineData("private-login@confidential.example.test", "confidential.example.test")]
    [InlineData("private-login-without-separator", "private-login-without-separator")]
    [InlineData("private-login\r\nforged-log-entry", "private-login\r\nforged-log-entry")]
    public async Task Discovery_PreservesLookupAndResponse_WithoutLoggingSubmittedIdentifier(string email, string expectedDomain)
    {
        var federation = new Mock<IOidcFederationService>();
        federation.Setup(service => service.FindProvidersForEmailDomain(expectedDomain))
            .Returns([new OidcDiscoveredProvider { Slug = "configured-idp", DisplayName = "Configured identity provider" }]);
        var logger = new Mock<ILogger<DiscoverOidcProvidersQueryHandler>>();
        var handler = new DiscoverOidcProvidersQueryHandler(federation.Object, logger.Object);

        var response = await handler.Handle(new DiscoverOidcProvidersQuery { Email = email }, CancellationToken.None);

        federation.Verify(service => service.FindProvidersForEmailDomain(expectedDomain), Times.Once);
        response.Providers.Should().ContainSingle().Which.Slug.Should().Be("configured-idp");
        var log = Assert.Single(logger.Invocations.Where(invocation => invocation.Method.Name == nameof(ILogger.Log)));
        log.Arguments[0].Should().Be(LogLevel.Information);
        log.Arguments[3].Should().BeNull();
        var state = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(log.Arguments[2]).ToArray();
        state.Select(property => property.Key).Should().Equal("Count", "{OriginalFormat}");
        state.Single(property => property.Key == "Count").Value.Should().Be(1);
        state.Single(property => property.Key == "{OriginalFormat}").Value.Should()
            .Be("OIDC domain discovery matched {Count} provider(s)");
        foreach (var property in state)
        {
            (property.Value?.ToString() ?? string.Empty).Should().NotContain(email).And.NotContain(expectedDomain);
        }
        log.Arguments[2].ToString().Should().Be("OIDC domain discovery matched 1 provider(s)");
    }
}
