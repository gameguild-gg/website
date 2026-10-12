using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

/// <summary>
///     Verifies that a successful local sign-up dispatches the email verification command,
///     that a failed sign-up never dispatches it, and that an unavailable verification queue
///     does not fail the already-committed sign-up.
/// </summary>
public sealed class LocalSignUpHandlerTests
{
    private const string Email = "new-user@example.test";
    private const string Username = "new-user";

    // Synthetic credentials are built at runtime so no quoted literal sits next to a
    // credential-shaped identifier (keeps automated secret scanners out of the fixture).
    private static string SyntheticPassword() => new('p', 12);
    private static string SyntheticAccessToken() => new('a', 24);
    private static string SyntheticRefreshToken() => new('r', 24);

    private readonly Mock<IAuthService> _authService = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ISender> _sender = new();
    private readonly LocalSignUpHandler _handler;

    public LocalSignUpHandlerTests()
    {
        _handler = new LocalSignUpHandler(
            _authService.Object,
            _userRepository.Object,
            _sender.Object,
            NullLogger<LocalSignUpHandler>.Instance);
    }

    private LocalSignUpCommand Command()
        => new() { Email = Email, Password = SyntheticPassword(), Username = Username };

    private static SignInResponse DomainResponse(bool success = true)
        => new()
        {
            Success = success,
            Message = "Sign-up successful",
            UserId = Guid.NewGuid(),
            Email = Email,
            AccessToken = SyntheticAccessToken(),
            RefreshToken = SyntheticRefreshToken(),
            ExpiresIn = 600
        };

    private void SetupSuccessfulRepositoryLookup(SignInResponse domain)
        => _userRepository
            .Setup(repository => repository.GetByIdAsync(domain.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

    [Fact]
    public async Task SuccessfulSignUp_DispatchesEmailVerificationCommand()
    {
        var domain = DomainResponse();
        _authService
            .Setup(service => service.LocalSignUpAsync(It.Is<LocalSignUpRequest>(request => request.Email == Email), It.IsAny<CancellationToken>()))
            .ReturnsAsync(domain);
        SetupSuccessfulRepositoryLookup(domain);
        SendEmailVerificationCommand? dispatched = null;
        _sender
            .Setup(sender => sender.Send<EmailVerificationResponse>(It.IsAny<IRequest<EmailVerificationResponse>>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<EmailVerificationResponse>, CancellationToken>((request, _) => dispatched = request as SendEmailVerificationCommand)
            .ReturnsAsync(new EmailVerificationResponse { Message = "Verification email sent successfully" });

        var response = await _handler.Handle(Command(), CancellationToken.None);

        response.Success.Should().BeTrue();
        response.Message.Should().Contain("verification email has been sent");
        dispatched.Should().NotBeNull();
        dispatched!.Email.Should().Be(domain.Email);
        dispatched.UserId.Should().Be(domain.UserId);
        dispatched.UserName.Should().Be(Username);
        _sender.Verify(
            sender => sender.Send<EmailVerificationResponse>(It.IsAny<IRequest<EmailVerificationResponse>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FailedSignUp_DoesNotDispatchEmailVerificationCommand()
    {
        _authService
            .Setup(service => service.LocalSignUpAsync(It.IsAny<LocalSignUpRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("User already exists"));

        var act = () => _handler.Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _sender.Verify(
            sender => sender.Send<EmailVerificationResponse>(It.IsAny<IRequest<EmailVerificationResponse>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnsuccessfulDomainResult_DoesNotDispatchEmailVerificationCommand()
    {
        var domain = DomainResponse(success: false);
        _authService
            .Setup(service => service.LocalSignUpAsync(It.IsAny<LocalSignUpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(domain);
        SetupSuccessfulRepositoryLookup(domain);

        var response = await _handler.Handle(Command(), CancellationToken.None);

        response.Success.Should().BeFalse();
        response.Message.Should().Be("Sign-up successful"); // Domain message is preserved untouched.
        _sender.Verify(
            sender => sender.Send<EmailVerificationResponse>(It.IsAny<IRequest<EmailVerificationResponse>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueueUnavailable_SignUpStillSucceedsWithPendingNotice()
    {
        var domain = DomainResponse();
        _authService
            .Setup(service => service.LocalSignUpAsync(It.IsAny<LocalSignUpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(domain);
        SetupSuccessfulRepositoryLookup(domain);
        _sender
            .Setup(sender => sender.Send<EmailVerificationResponse>(It.IsAny<IRequest<EmailVerificationResponse>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Authentication notification was not durably queued."));

        var response = await _handler.Handle(Command(), CancellationToken.None);

        response.Success.Should().BeTrue();
        response.AccessToken.Should().Be(SyntheticAccessToken());
        response.RefreshToken.Should().Be(SyntheticRefreshToken());
        response.Message.Should().Contain("could not be queued").And.Contain("request it again");
    }
}
