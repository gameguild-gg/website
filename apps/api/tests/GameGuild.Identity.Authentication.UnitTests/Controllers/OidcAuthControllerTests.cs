using FluentAssertions;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Controllers;

public class OidcAuthControllerTests
{
    private const string Slug = "corp-idp";

    [Fact]
    public async Task OidcSignIn_ShouldReturnOk_WithAuthUrlAndState()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<OidcSignInCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OidcSignInResponse
            {
                AuthUrl = "https://login.corp.example.test/authorize?client_id=x",
                State = "state-abc"
            });

        var controller = new AuthController(sender.Object);

        var result = await controller.OidcSignIn(
            Slug,
            new OidcAuthorizeRequestDto { RedirectUri = "https://web.example.test/api/auth/callback/oidc" },
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<OidcSignInResponse>().Subject;
        response.State.Should().Be("state-abc");
    }

    [Fact]
    public async Task OidcSignIn_ShouldReturn503ProblemDetails_WhenProviderNotConfigured()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<OidcSignInCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("OIDC federation provider 'corp-idp' is not configured."));

        var controller = new AuthController(sender.Object);

        var result = await controller.OidcSignIn(
            Slug,
            new OidcAuthorizeRequestDto { RedirectUri = "https://web.example.test/api/auth/callback/oidc" },
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(503);
        problem.Title.Should().Be("OIDC federation provider is not configured");
    }

    [Fact]
    public async Task OidcCallback_ShouldReturnOk_WithSignInResponse()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<OidcCallbackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignInResponse { Success = true, AccessToken = "access-token", RefreshToken = "refresh-token" });

        var controller = new AuthController(sender.Object);

        var result = await controller.OidcCallback(
            Slug,
            new OidcCallbackRequestDto
            {
                Code = "auth-code",
                State = "state-abc",
                RedirectUri = "https://web.example.test/api/auth/callback/oidc"
            },
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SignInResponse>().Subject;
        response.Success.Should().BeTrue();
    }

    [Fact]
    public async Task OidcCallback_ShouldReturn401ProblemDetails_WhenSenderThrowsUnauthorizedAccessException()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<OidcCallbackCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("OIDC ID token for provider 'corp-idp' failed validation."));

        var controller = new AuthController(sender.Object);

        var result = await controller.OidcCallback(
            Slug,
            new OidcCallbackRequestDto
            {
                Code = "auth-code",
                State = "state-abc",
                RedirectUri = "https://web.example.test/api/auth/callback/oidc"
            },
            CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var problem = unauthorized.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(401);
    }

    [Fact]
    public async Task OidcCallback_ShouldReturn503ProblemDetails_WhenProviderDisabled()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<OidcCallbackCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("OIDC federation provider 'corp-idp' is disabled."));

        var controller = new AuthController(sender.Object);

        var result = await controller.OidcCallback(
            Slug,
            new OidcCallbackRequestDto
            {
                Code = "auth-code",
                State = "state-abc",
                RedirectUri = "https://web.example.test/api/auth/callback/oidc"
            },
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task OidcDiscoverProvider_ShouldReturnOk_WithMatchedProviders()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<DiscoverOidcProvidersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OidcDiscoverProviderResponse
            {
                Providers = [new OidcDiscoveredProvider { Slug = Slug, DisplayName = "Corp SSO" }]
            });

        var controller = new AuthController(sender.Object);

        var result = await controller.OidcDiscoverProvider("jane@corp.example.test", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<OidcDiscoverProviderResponse>().Subject;
        response.Providers.Should().ContainSingle().Which.Slug.Should().Be(Slug);
    }

    [Fact]
    public async Task OidcEndSessionUrl_ShouldReturnOk_WithForwardedUrl()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GetOidcEndSessionUrlQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OidcEndSessionUrlResponse
            {
                EndSessionUrl = "https://login.corp.example.test/logout?post_logout_redirect_uri=https%3A%2F%2Fweb.example.test"
            });

        var controller = new AuthController(sender.Object);

        var result = await controller.OidcEndSessionUrl(Slug, "https://web.example.test", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<OidcEndSessionUrlResponse>().Subject;
        response.EndSessionUrl.Should().StartWith("https://login.corp.example.test/logout");
    }

    [Fact]
    public async Task OidcEndSessionUrl_ShouldReturn503_WhenProviderNotConfigured()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GetOidcEndSessionUrlQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("OIDC federation provider 'corp-idp' is not configured."));

        var controller = new AuthController(sender.Object);

        var result = await controller.OidcEndSessionUrl(Slug, null, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }
}
