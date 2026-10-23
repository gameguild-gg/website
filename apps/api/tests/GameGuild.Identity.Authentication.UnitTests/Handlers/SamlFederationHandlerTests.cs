using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public class SamlCallbackCommandValidatorTests
{
    [Fact]
    public void Validate_FullCommand_Passes()
    {
        var validator = new SamlCallbackCommandValidator();
        var command = new SamlCallbackCommand
        {
            Slug = "corp-idp",
            SamlResponse = "c2FtbC1yZXNwb25zZQ==",
            RelayState = "relay-1"
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
        var validator = new SamlCallbackCommandValidator();
        var command = new SamlCallbackCommand { Slug = slug, SamlResponse = "c2FtbC1yZXNwb25zZQ==" };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SamlCallbackCommand.Slug));
    }

    [Fact]
    public void Validate_MissingSamlResponse_Fails()
    {
        var validator = new SamlCallbackCommandValidator();
        var command = new SamlCallbackCommand { Slug = "corp-idp" };

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SamlCallbackCommand.SamlResponse));
    }
}

public class SamlSignInCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsProviderRedirectAndRelayState()
    {
        var federation = new Mock<ISamlFederationService>();
        federation
            .Setup(f => f.BuildSignInRedirectAsync("corp-idp", "relay-42", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SamlSignInChallenge
            {
                RedirectUrl = "https://sts.corp.example.test/sso?SAMLRequest=abc",
                RequestId = "_req1",
                RelayState = "relay-42"
            });

        var handler = new SamlSignInCommandHandler(federation.Object, NullLogger<SamlSignInCommandHandler>.Instance);

        var response = await handler.Handle(new SamlSignInCommand { Slug = "corp-idp", RelayState = "relay-42" }, CancellationToken.None);

        response.RedirectUrl.Should().Contain("sts.corp.example.test");
        response.RelayState.Should().Be("relay-42");
        federation.Verify(f => f.BuildSignInRedirectAsync("corp-idp", "relay-42", It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class SamlCallbackCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidatedResponse_CompletesSignInThroughOAuthService()
    {
        var oAuthAuthService = new Mock<IOAuthAuthService>();
        oAuthAuthService
            .Setup(s => s.SamlSignInAsync(It.IsAny<SamlSignInRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignInResponse { Success = true, AccessToken = "access", RefreshToken = "refresh", UserId = Guid.NewGuid() });
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Email = "jane@corp.example.test" });

        var handler = new SamlCallbackCommandHandler(
            oAuthAuthService.Object,
            userRepository.Object,
            NullLogger<SamlCallbackCommandHandler>.Instance,
            new SamlCallbackCommandValidator());

        var result = await handler.Handle(new SamlCallbackCommand
        {
            Slug = "corp-idp",
            SamlResponse = "c2FtbC1yZXNwb25zZQ==",
            RelayState = "relay-1"
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        oAuthAuthService.Verify(
            s => s.SamlSignInAsync(It.Is<SamlSignInRequest>(r => r.Slug == "corp-idp" && r.SamlResponse == "c2FtbC1yZXNwb25zZQ==" && r.RelayState == "relay-1"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidCommand_ThrowsRequestValidation()
    {
        var handler = new SamlCallbackCommandHandler(
            Mock.Of<IOAuthAuthService>(),
            Mock.Of<IUserRepository>(),
            NullLogger<SamlCallbackCommandHandler>.Instance,
            new SamlCallbackCommandValidator());

        var act = () => handler.Handle(new SamlCallbackCommand { Slug = "Not_A_Slug" }, CancellationToken.None);

        await act.Should().ThrowAsync<RequestValidationException>();
    }
}

public class SamlFederationQueryHandlerTests
{
    [Fact]
    public async Task GetSamlServiceProviderMetadata_ReturnsMetadataXml()
    {
        var federation = new Mock<ISamlFederationService>();
        federation
            .Setup(f => f.BuildServiceProviderMetadata("corp-idp"))
            .Returns("<md:EntityDescriptor entityID=\"https://gameguild.example.test/auth/saml\"/>");

        var handler = new GetSamlServiceProviderMetadataQueryHandler(federation.Object, NullLogger<GetSamlServiceProviderMetadataQueryHandler>.Instance);

        var result = await handler.Handle(new GetSamlServiceProviderMetadataQuery { Slug = "corp-idp" }, CancellationToken.None);

        result.MetadataXml.Should().Contain("EntityDescriptor");
    }

    [Fact]
    public async Task GetSamlLogoutUrl_WhenConfigured_ReturnsUrl()
    {
        var federation = new Mock<ISamlFederationService>();
        federation
            .Setup(f => f.BuildLogoutRedirectUrl("corp-idp", "https://gameguild.example.test/bye"))
            .Returns("https://sts.corp.example.test/slo?SAMLRequest=xyz");

        var handler = new GetSamlLogoutUrlQueryHandler(federation.Object, NullLogger<GetSamlLogoutUrlQueryHandler>.Instance);

        var result = await handler.Handle(new GetSamlLogoutUrlQuery { Slug = "corp-idp", PostLogoutRedirectUri = "https://gameguild.example.test/bye" }, CancellationToken.None);

        result.LogoutUrl.Should().StartWith("https://sts.corp.example.test/slo");
    }

    [Fact]
    public async Task GetSamlLogoutUrl_WhenUnconfigured_ReturnsNull()
    {
        var federation = new Mock<ISamlFederationService>();
        federation
            .Setup(f => f.BuildLogoutRedirectUrl("corp-idp", It.IsAny<string?>()))
            .Returns((string?)null);

        var handler = new GetSamlLogoutUrlQueryHandler(federation.Object, NullLogger<GetSamlLogoutUrlQueryHandler>.Instance);

        var result = await handler.Handle(new GetSamlLogoutUrlQuery { Slug = "corp-idp" }, CancellationToken.None);

        result.LogoutUrl.Should().BeNull();
    }
}

public class SamlDiscoveryHandlerTests
{
    [Fact]
    public async Task DiscoverProviders_MergesOidcAndSamlProvidersBySlug()
    {
        var oidc = new Mock<IOidcFederationService>();
        oidc
            .Setup(s => s.FindProvidersForEmailDomain("corp.example.test"))
            .Returns([new OidcDiscoveredProvider { Slug = "zen-oauth", DisplayName = "Zen OAuth" }]);
        var saml = new Mock<ISamlFederationService>();
        saml
            .Setup(s => s.FindProvidersForEmailDomain("corp.example.test"))
            .Returns([new OidcDiscoveredProvider { Slug = "alpha-saml", DisplayName = "Alpha SAML", Protocol = "saml" }]);

        var handler = new DiscoverOidcProvidersQueryHandler(
            oidc.Object,
            NullLogger<DiscoverOidcProvidersQueryHandler>.Instance,
            saml.Object);

        var response = await handler.Handle(new DiscoverOidcProvidersQuery { Email = "jane@corp.example.test" }, CancellationToken.None);

        response.Providers.Select(provider => provider.Slug).Should().Equal("alpha-saml", "zen-oauth");
        response.Providers.Should().OnlyContain(provider => provider.Protocol == "oidc" || provider.Protocol == "saml");
    }

    [Fact]
    public async Task DiscoverProviders_WithoutSamlService_KeepsOidcOnlyResults()
    {
        var oidc = new Mock<IOidcFederationService>();
        oidc
            .Setup(s => s.FindProvidersForEmailDomain("corp.example.test"))
            .Returns([new OidcDiscoveredProvider { Slug = "corp-idp", DisplayName = "Corp SSO" }]);

        var handler = new DiscoverOidcProvidersQueryHandler(
            oidc.Object,
            NullLogger<DiscoverOidcProvidersQueryHandler>.Instance);

        var response = await handler.Handle(new DiscoverOidcProvidersQuery { Email = "jane@corp.example.test" }, CancellationToken.None);

        response.Providers.Should().ContainSingle().Which.Protocol.Should().Be("oidc");
    }
}
