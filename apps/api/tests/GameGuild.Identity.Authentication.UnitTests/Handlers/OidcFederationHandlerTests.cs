using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
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
}
