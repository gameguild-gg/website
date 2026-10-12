using FluentAssertions;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Controllers;

public class GitHubAuthControllerTests
{
    private static GitHubCallbackRequestDto CallbackBody() => new()
    {
        Code = "github-code",
        State = "state-123",
        RedirectUri = "https://web.example.com/api/auth/callback/github"
    };

    [Fact]
    public async Task GitHubCallback_ShouldReturnOk_WithSignInResponse()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GitHubCallbackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignInResponse
            {
                Success = true,
                AccessToken = "access-token",
                RefreshToken = "refresh-token"
            });

        var controller = new AuthController(sender.Object);

        var result = await controller.GitHubCallback(CallbackBody(), CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<SignInResponse>().Subject;
        response.Success.Should().BeTrue();
        response.AccessToken.Should().Be("access-token");
    }

    [Fact]
    public async Task GitHubCallback_ShouldMapBodyToCommand_WithCodeStateRedirectAndTenant()
    {
        var tenantId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GitHubCallbackCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignInResponse { Success = true });

        var controller = new AuthController(sender.Object);
        var body = CallbackBody();
        body.TenantId = tenantId;
        body.RememberMe = true;

        await controller.GitHubCallback(body, CancellationToken.None);

        sender.Verify(
            s => s.Send(
                It.Is<GitHubCallbackCommand>(command =>
                    command.Code == "github-code"
                    && command.State == "state-123"
                    && command.RedirectUri == "https://web.example.com/api/auth/callback/github"
                    && command.TenantId == tenantId
                    && command.RememberMe == true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GitHubCallback_ShouldReturn401ProblemDetails_WhenSenderThrowsUnauthorizedAccessException()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GitHubCallbackCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Email not available from GitHub profile"));

        var controller = new AuthController(sender.Object);

        var result = await controller.GitHubCallback(CallbackBody(), CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var problem = unauthorized.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(401);
        problem.Detail.Should().Be("Email not available from GitHub profile");
    }

    [Fact]
    public async Task GitHubCallback_ShouldReturn503ProblemDetails_WhenGitHubNotConfigured()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GitHubCallbackCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("OAuth client credentials are not configured for provider: github"));

        var controller = new AuthController(sender.Object);

        var result = await controller.GitHubCallback(CallbackBody(), CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(503);
        problem.Title.Should().Be("GitHub OAuth is not configured");
    }
}
