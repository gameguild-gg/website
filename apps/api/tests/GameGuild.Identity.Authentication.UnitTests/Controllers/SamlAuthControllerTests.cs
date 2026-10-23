using FluentAssertions;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Controllers;

public class SamlAuthControllerTests
{
    private const string Slug = "corp-idp";

    [Fact]
    public async Task SamlSignIn_ShouldReturnOk_WithRedirectUrlAndRelayState()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<SamlSignInCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SamlSignInResponse
            {
                RedirectUrl = "https://sts.corp.example.test/sso?SAMLRequest=abc",
                RelayState = "relay-42"
            });

        var controller = new AuthController(sender.Object);

        var result = await controller.SamlSignIn(Slug, new SamlAuthorizeRequestDto { RelayState = "relay-42" }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SamlSignInResponse>().Subject;
        response.RelayState.Should().Be("relay-42");
        response.RedirectUrl.Should().Contain("sts.corp.example.test");
    }

    [Fact]
    public async Task SamlSignIn_ShouldReturn503ProblemDetails_WhenProviderNotConfigured()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<SamlSignInCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SAML federation provider 'corp-idp' is not configured."));

        var controller = new AuthController(sender.Object);

        var result = await controller.SamlSignIn(Slug, null, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(503);
        problem.Title.Should().Be("SAML federation provider is not configured");
    }

    [Fact]
    public async Task SamlCallback_ShouldReturnOk_WithSignInResponse()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<SamlCallbackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignInResponse { Success = true, AccessToken = "access-token", RefreshToken = "refresh-token" });

        var controller = new AuthController(sender.Object);

        var result = await controller.SamlCallback(
            Slug,
            new SamlCallbackRequestDto { SamlResponse = "c2FtbC1yZXNwb25zZQ==", RelayState = "relay-1" },
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SignInResponse>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SamlCallback_ShouldReturn401_WhenResponseRejected()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<SamlCallbackCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("SAML response for provider 'corp-idp' is unsigned and was refused."));

        var controller = new AuthController(sender.Object);

        var result = await controller.SamlCallback(
            Slug,
            new SamlCallbackRequestDto { SamlResponse = "c2FtbC1yZXNwb25zZQ==" },
            CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var problem = unauthorized.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(401);
        problem.Detail.Should().Contain("unsigned");
    }

    [Fact]
    public async Task SamlMetadata_ShouldReturnXmlContent()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GetSamlServiceProviderMetadataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SamlMetadataResponse { MetadataXml = "<md:EntityDescriptor entityID=\"https://gameguild.example.test/auth/saml\"/>" });

        var controller = new AuthController(sender.Object);

        var result = await controller.SamlMetadata(Slug, CancellationToken.None);

        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.ContentType.Should().Be("application/samlmetadata+xml");
        content.Content.Should().Contain("EntityDescriptor");
    }

    [Fact]
    public async Task SamlLogoutUrl_ShouldReturnOk_WithLogoutUrl()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GetSamlLogoutUrlQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SamlLogoutUrlResponse { LogoutUrl = "https://sts.corp.example.test/slo?SAMLRequest=xyz" });

        var controller = new AuthController(sender.Object);

        var result = await controller.SamlLogoutUrl(Slug, "https://gameguild.example.test/bye", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SamlLogoutUrlResponse>().Subject;
        response.LogoutUrl.Should().StartWith("https://sts.corp.example.test/slo");
    }
}
