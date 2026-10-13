using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public class OidcCallbackCommandValidatorTests
{
    [Fact]
    public void Validate_FullCommand_Passes()
    {
        var validator = new OidcCallbackCommandValidator();
        var command = new OidcCallbackCommand
        {
            Slug = "corp-idp",
            Code = "auth-code",
            State = "state-1",
            RedirectUri = "https://web.example.test/cb"
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Corp-Idp")]
    [InlineData("corp_idp")]
    public void Validate_InvalidSlug_Fails(string slug)
    {
        var validator = new OidcCallbackCommandValidator();
        var command = new OidcCallbackCommand
        {
            Slug = slug,
            Code = "auth-code",
            State = "state-1",
            RedirectUri = "https://web.example.test/cb"
        };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(OidcCallbackCommand.Slug));
    }

    [Fact]
    public void Validate_MissingCodeAndState_Fails()
    {
        var validator = new OidcCallbackCommandValidator();
        var command = new OidcCallbackCommand { Slug = "corp-idp", RedirectUri = "https://web.example.test/cb" };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(OidcCallbackCommand.Code));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(OidcCallbackCommand.State));
    }
}

public class OidcSignInCommandHandlerTests
{
    [Fact]
    public async Task Handle_BuildsChallengeFromFederationService()
    {
        var federation = new Mock<IOidcFederationService>();
        federation
            .Setup(f => f.BuildAuthorizationUrlAsync("corp-idp", "https://web.example.test/cb", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OidcSignInChallenge { AuthUrl = "https://login.corp.example.test/authorize?client_id=x", State = "incoming-state" });

        var handler = new OidcSignInCommandHandler(federation.Object, NullLogger<OidcSignInCommandHandler>.Instance);

        var response = await handler.Handle(new OidcSignInCommand { Slug = "corp-idp", RedirectUri = "https://web.example.test/cb" }, CancellationToken.None);

        response.AuthUrl.Should().Contain("login.corp.example.test");
        // The handler owns CSRF state generation (Guid "N"), not the federation challenge.
        response.State.Should().MatchRegex("^[0-9a-f]{32}$");
    }
}

public class OidcCallbackCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_DelegatesToAuthService()
    {
        var authService = new Mock<IOAuthAuthService>();
        authService
            .Setup(a => a.OidcSignInAsync(It.IsAny<OidcSignInRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignInResponse { Success = true, UserId = Guid.NewGuid() });
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new OidcCallbackCommandHandler(
            authService.Object,
            userRepository.Object,
            NullLogger<OidcCallbackCommandHandler>.Instance,
            new OidcCallbackCommandValidator());

        var response = await handler.Handle(new OidcCallbackCommand
        {
            Slug = "corp-idp",
            Code = "auth-code",
            State = "state-1",
            RedirectUri = "https://web.example.test/cb"
        }, CancellationToken.None);

        response.Success.Should().BeTrue();
        authService.Verify(a => a.OidcSignInAsync(
            It.Is<OidcSignInRequest>(r => r.Slug == "corp-idp" && r.Code == "auth-code"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidCommand_ThrowsRequestValidation()
    {
        var handler = new OidcCallbackCommandHandler(
            Mock.Of<IOAuthAuthService>(),
            Mock.Of<IUserRepository>(),
            NullLogger<OidcCallbackCommandHandler>.Instance,
            new OidcCallbackCommandValidator());

        var act = () => handler.Handle(new OidcCallbackCommand { Slug = "corp-idp" }, CancellationToken.None);

        await act.Should().ThrowAsync<RequestValidationException>();
    }
}

public class OidcFederationQueryHandlerTests
{
    [Fact]
    public async Task DiscoverProviders_ExtractsDomainAndDelegates()
    {
        var federation = new Mock<IOidcFederationService>();
        federation
            .Setup(f => f.FindProvidersForEmailDomain("corp.example.test"))
            .Returns([new OidcDiscoveredProvider { Slug = "corp-idp", DisplayName = "Corp SSO" }]);

        var handler = new DiscoverOidcProvidersQueryHandler(federation.Object, NullLogger<DiscoverOidcProvidersQueryHandler>.Instance);

        var response = await handler.Handle(new DiscoverOidcProvidersQuery { Email = "jane@corp.example.test" }, CancellationToken.None);

        response.Providers.Should().ContainSingle().Which.DisplayName.Should().Be("Corp SSO");
    }

    [Theory]
    [InlineData("private-person@corp.example.test", "corp.example.test", 0)]
    [InlineData("private-person@corp.example.test", "corp.example.test", 2)]
    [InlineData("  private-person@corp.example.test  ", "corp.example.test", 0)]
    [InlineData("  private-person@corp.example.test  ", "corp.example.test", 2)]
    [InlineData("corp.example.test", "corp.example.test", 0)]
    [InlineData("corp.example.test", "corp.example.test", 2)]
    [InlineData("private-person", "private-person", 0)]
    [InlineData("private-person", "private-person", 2)]
    [InlineData("private-person@", "private-person@", 0)]
    [InlineData("private-person@", "private-person@", 2)]
    [InlineData("private-person@corp.example.test\r\nforged-entry", "corp.example.test\r\nforged-entry", 0)]
    [InlineData("private-person@corp.example.test\r\nforged-entry", "corp.example.test\r\nforged-entry", 2)]
    [InlineData("private-person@corp.example.test\u2028forged-entry", "corp.example.test\u2028forged-entry", 0)]
    [InlineData("private-person@corp.example.test\u2028forged-entry", "corp.example.test\u2028forged-entry", 2)]
    public async Task DiscoverProviders_LogsOnlyCountAndPreservesRouting(string email, string expectedDomain, int providerCount)
    {
        IReadOnlyList<OidcDiscoveredProvider> providers = Enumerable.Range(0, providerCount)
            .Select(index => new OidcDiscoveredProvider { Slug = $"idp-{index}", DisplayName = $"Provider {index}" })
            .ToArray();
        var federation = new Mock<IOidcFederationService>(MockBehavior.Strict);
        federation.Setup(service => service.FindProvidersForEmailDomain(expectedDomain)).Returns(providers);
        var logger = new TestLogger<DiscoverOidcProvidersQueryHandler>();
        var handler = new DiscoverOidcProvidersQueryHandler(federation.Object, logger);

        var response = await handler.Handle(new DiscoverOidcProvidersQuery { Email = email }, CancellationToken.None);

        response.Providers.Should().BeSameAs(providers);
        federation.Verify(service => service.FindProvidersForEmailDomain(expectedDomain), Times.Once);
        federation.VerifyNoOtherCalls();
        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Information);
        entry.Exception.Should().BeNull();
        entry.Message.Should().Be($"OIDC provider discovery matched {providerCount} provider(s)");
        entry.Properties.Select(property => property.Key).Should().BeEquivalentTo(new[] { "Count", "{OriginalFormat}" });
        entry.Properties.Single(property => property.Key == "Count").Value.Should().Be(providerCount);
        entry.Properties.Single(property => property.Key == "{OriginalFormat}").Value.Should()
            .Be("OIDC provider discovery matched {Count} provider(s)");
        entry.Message.Should().NotContain(email.Trim()).And.NotContain(expectedDomain)
            .And.NotContain("\r").And.NotContain("\n").And.NotContain("\u2028");
        string.Join("|", entry.Properties.Select(property => property.Value)).Should()
            .NotContain(email.Trim()).And.NotContain(expectedDomain)
            .And.NotContain("\r").And.NotContain("\n").And.NotContain("\u2028");
    }

    [Fact]
    public async Task EndSessionUrl_WithRedirect_AppendsPostLogoutRedirect()
    {
        var federation = new Mock<IOidcFederationService>();
        federation
            .Setup(f => f.GetEndSessionEndpointAsync("corp-idp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://login.corp.example.test/logout");

        var handler = new GetOidcEndSessionUrlQueryHandler(federation.Object, NullLogger<GetOidcEndSessionUrlQueryHandler>.Instance);

        var response = await handler.Handle(
            new GetOidcEndSessionUrlQuery { Slug = "corp-idp", PostLogoutRedirectUri = "https://web.example.test/logged-out" },
            CancellationToken.None);

        response.EndSessionUrl.Should().Be(
            $"https://login.corp.example.test/logout?post_logout_redirect_uri={Uri.EscapeDataString("https://web.example.test/logged-out")}");
    }

    [Fact]
    public async Task EndSessionUrl_WithoutRedirect_ReturnsBareEndpoint()
    {
        var federation = new Mock<IOidcFederationService>();
        federation
            .Setup(f => f.GetEndSessionEndpointAsync("corp-idp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://login.corp.example.test/logout");

        var handler = new GetOidcEndSessionUrlQueryHandler(federation.Object, NullLogger<GetOidcEndSessionUrlQueryHandler>.Instance);

        var response = await handler.Handle(new GetOidcEndSessionUrlQuery { Slug = "corp-idp" }, CancellationToken.None);

        response.EndSessionUrl.Should().Be("https://login.corp.example.test/logout");
    }

    [Fact]
    public async Task EndSessionUrl_NotAdvertised_ReturnsNull()
    {
        var federation = new Mock<IOidcFederationService>();
        federation
            .Setup(f => f.GetEndSessionEndpointAsync("corp-idp", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var handler = new GetOidcEndSessionUrlQueryHandler(federation.Object, NullLogger<GetOidcEndSessionUrlQueryHandler>.Instance);

        var response = await handler.Handle(new GetOidcEndSessionUrlQuery { Slug = "corp-idp" }, CancellationToken.None);

        response.EndSessionUrl.Should().BeNull();
    }

    [Theory]
    [InlineData(" member@private-domain.example.test ", "private-domain.example.test", true)]
    [InlineData(" member@private-domain.example.test ", "private-domain.example.test", false)]
    [InlineData("private-member-id", "private-member-id", true)]
    [InlineData("private-member-id", "private-member-id", false)]
    [InlineData("member@private-domain.example.test?token=private-value", "private-domain.example.test?token=private-value", true)]
    [InlineData("member@private-domain.example.test?token=private-value", "private-domain.example.test?token=private-value", false)]
    [InlineData("member@first@private-domain.example.test", "private-domain.example.test", true)]
    [InlineData("member@first@private-domain.example.test", "private-domain.example.test", false)]
    [InlineData(" member@PRIVATE-DOMAIN.example.test ", "PRIVATE-DOMAIN.example.test", true)]
    [InlineData(" member@PRIVATE-DOMAIN.example.test ", "PRIVATE-DOMAIN.example.test", false)]
    public async Task Discover_LogPrivacy_PreservesProviderMatchingWithoutEmailOrDomain(string email, string expectedDomain, bool matched)
    {
        var federation = new Mock<IOidcFederationService>();
        IReadOnlyList<OidcDiscoveredProvider> providers = matched
            ? [new() { Slug = "corp-idp", DisplayName = "Corp SSO" }]
            : [];
        federation.Setup(service => service.FindProvidersForEmailDomain(expectedDomain)).Returns(providers);
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<DiscoverOidcProvidersQueryHandler>>();
        var handler = new DiscoverOidcProvidersQueryHandler(federation.Object, logger.Object);

        var response = await handler.Handle(new DiscoverOidcProvidersQuery { Email = email }, CancellationToken.None);

        response.Providers.Should().BeSameAs(providers);
        federation.Verify(service => service.FindProvidersForEmailDomain(expectedDomain), Times.Once);
        federation.VerifyNoOtherCalls();
        AssertOidcLogsPrivate(logger, email.Trim(), expectedDomain, "private-value");
        var log = logger.Invocations.Single(invocation => invocation.Method.Name == "Log");
        var properties = (IEnumerable<KeyValuePair<string, object?>>)log.Arguments[2];
        properties.Single(pair => pair.Key == "Count").Value.Should().Be(providers.Count);
    }

    [Theory]
    [InlineData("corp-private-provider")]
    [InlineData("private-alias-idp")]
    public async Task EndSessionUrl_LogPrivacy_UnadvertisedEndpointKeepsLocalLogoutWithoutSlug(string slug)
    {
        const string redirect = "https://web.example.test/logged-out?code=private-redirect-value";
        var federation = new Mock<IOidcFederationService>();
        federation.Setup(service => service.GetEndSessionEndpointAsync(slug, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<GetOidcEndSessionUrlQueryHandler>>();
        var handler = new GetOidcEndSessionUrlQueryHandler(federation.Object, logger.Object);

        var response = await handler.Handle(new GetOidcEndSessionUrlQuery { Slug = slug, PostLogoutRedirectUri = redirect }, CancellationToken.None);

        response.EndSessionUrl.Should().BeNull();
        federation.Verify(service => service.GetEndSessionEndpointAsync(slug, CancellationToken.None), Times.Once);
        federation.VerifyNoOtherCalls();
        AssertOidcLogsPrivate(logger, slug, redirect, "private-redirect-value");
        logger.Invocations.Single(invocation => invocation.Method.Name == "Log").Arguments[2].ToString()
            .Should().Contain("logout stays local");
    }

    [Fact]
    public async Task EndSessionUrl_LogPrivacy_AdvertisedEndpointKeepsEscapedRedirectWithoutLogs()
    {
        const string slug = "private-idp";
        const string endpoint = "https://login.example.test/logout";
        const string redirect = "https://web.example.test/logged-out?code=private-redirect-value&next=%2Faccount";
        var federation = new Mock<IOidcFederationService>();
        federation.Setup(service => service.GetEndSessionEndpointAsync(slug, It.IsAny<CancellationToken>()))
            .ReturnsAsync(endpoint);
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<GetOidcEndSessionUrlQueryHandler>>();
        var handler = new GetOidcEndSessionUrlQueryHandler(federation.Object, logger.Object);

        var response = await handler.Handle(new GetOidcEndSessionUrlQuery { Slug = slug, PostLogoutRedirectUri = redirect }, CancellationToken.None);

        response.EndSessionUrl.Should().Be(endpoint + "?post_logout_redirect_uri=" + Uri.EscapeDataString(redirect));
        federation.Verify(service => service.GetEndSessionEndpointAsync(slug, CancellationToken.None), Times.Once);
        federation.VerifyNoOtherCalls();
        logger.Invocations.Should().NotContain(invocation => invocation.Method.Name == "Log");
    }

    private static void AssertOidcLogsPrivate<T>(Mock<Microsoft.Extensions.Logging.ILogger<T>> logger, params string[] privateValues)
    {
        var logs = logger.Invocations.Where(invocation => invocation.Method.Name == "Log").ToArray();
        logs.Should().NotBeEmpty();
        foreach (var log in logs)
        {
            log.Arguments[0].Should().Be(Microsoft.Extensions.Logging.LogLevel.Information);
            log.Arguments[3].Should().BeNull();
            var properties = ((IEnumerable<KeyValuePair<string, object?>>)log.Arguments[2]).ToArray();
            properties.Should().NotContain(pair => pair.Key == "Email" || pair.Key == "Domain" || pair.Key == "Slug" || pair.Key == "Token");
            foreach (var privateValue in privateValues)
            {
                log.Arguments[2].ToString().Should().NotContain(privateValue);
                properties.Should().OnlyContain(pair => pair.Value == null || !pair.Value.ToString()!.Contains(privateValue, StringComparison.Ordinal));
            }
        }
    }
}
