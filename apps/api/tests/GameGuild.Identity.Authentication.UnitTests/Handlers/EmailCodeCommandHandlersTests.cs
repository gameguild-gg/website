using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class EmailCodeCommandHandlersTests
{
    private static User CreateUser(string email) =>
        new() { Id = Guid.NewGuid(), Email = email, Username = "code-user", TokenVersion = 3 };

    [Fact]
    public async Task RequestEmailCodeCommandHandler_UserFoundGeneratesCodeAndPublishesNotification()
    {
        var user = CreateUser("code@test.com");
        var userRepository = new Mock<IUserRepository>();
        var emailCodeService = new Mock<IEmailCodeService>();
        var publisher = new Mock<IPublisher>();
        userRepository.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        emailCodeService.Setup(s => s.GenerateEmailCodeAsync(user.Id, user.Email))
            .ReturnsAsync("123456");
        publisher.Setup(p => p.Publish(It.IsAny<EmailCodeRequestedNotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RequestEmailCodeCommandHandler(
            userRepository.Object,
            emailCodeService.Object,
            publisher.Object,
            NullLogger<RequestEmailCodeCommandHandler>.Instance);

        var result = await handler.Handle(new RequestEmailCodeCommand { Email = user.Email }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ExpiresInMinutes.Should().Be(10);
        result.Message.Should().Contain("one-time sign-in code");
        publisher.Verify(
            p => p.Publish(
                It.Is<EmailCodeRequestedNotification>(n => n.Email == user.Email && n.Code == "123456"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RequestEmailCodeCommandHandler_UnknownEmailReturnsEnumerationSafeSuccessWithoutGenerating()
    {
        var userRepository = new Mock<IUserRepository>();
        var emailCodeService = new Mock<IEmailCodeService>();
        var publisher = new Mock<IPublisher>();
        userRepository.Setup(r => r.GetByEmailAsync("ghost@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new RequestEmailCodeCommandHandler(
            userRepository.Object,
            emailCodeService.Object,
            publisher.Object,
            NullLogger<RequestEmailCodeCommandHandler>.Instance);

        var result = await handler.Handle(new RequestEmailCodeCommand { Email = "ghost@test.com" }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ExpiresInMinutes.Should().Be(10);
        emailCodeService.Verify(s => s.GenerateEmailCodeAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        publisher.Verify(p => p.Publish(It.IsAny<EmailCodeRequestedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestEmailCodeCommandHandler_ThrottledUserStillGetsGenericSuccess()
    {
        var user = CreateUser("throttled@test.com");
        var userRepository = new Mock<IUserRepository>();
        var emailCodeService = new Mock<IEmailCodeService>();
        var publisher = new Mock<IPublisher>();
        userRepository.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        emailCodeService.Setup(s => s.GenerateEmailCodeAsync(user.Id, user.Email))
            .ReturnsAsync((string?)null);

        var handler = new RequestEmailCodeCommandHandler(
            userRepository.Object,
            emailCodeService.Object,
            publisher.Object,
            NullLogger<RequestEmailCodeCommandHandler>.Instance);

        var result = await handler.Handle(new RequestEmailCodeCommand { Email = user.Email }, CancellationToken.None);

        result.Success.Should().BeTrue();
        publisher.Verify(p => p.Publish(It.IsAny<EmailCodeRequestedNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestEmailCodeCommandHandler_NotificationDispatchFailsStillReturnsGenericSuccess()
    {
        var user = CreateUser("queue-down@test.com");
        var userRepository = new Mock<IUserRepository>();
        var emailCodeService = new Mock<IEmailCodeService>();
        var publisher = new Mock<IPublisher>();
        userRepository.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        emailCodeService.Setup(s => s.GenerateEmailCodeAsync(user.Id, user.Email))
            .ReturnsAsync("654321");
        publisher.Setup(p => p.Publish(It.IsAny<EmailCodeRequestedNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Notification queue unavailable"));

        var handler = new RequestEmailCodeCommandHandler(
            userRepository.Object,
            emailCodeService.Object,
            publisher.Object,
            NullLogger<RequestEmailCodeCommandHandler>.Instance);

        var result = await handler.Handle(new RequestEmailCodeCommand { Email = user.Email }, CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RequestEmailCodeCommandHandler_CancellationDuringNotificationDispatchPropagates()
    {
        var user = CreateUser("cancel@test.com");
        var userRepository = new Mock<IUserRepository>();
        var emailCodeService = new Mock<IEmailCodeService>();
        var publisher = new Mock<IPublisher>();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        userRepository.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        emailCodeService.Setup(s => s.GenerateEmailCodeAsync(user.Id, user.Email))
            .ReturnsAsync("111222");
        publisher.Setup(p => p.Publish(It.IsAny<EmailCodeRequestedNotification>(), cancellationSource.Token))
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token));

        var handler = new RequestEmailCodeCommandHandler(
            userRepository.Object,
            emailCodeService.Object,
            publisher.Object,
            NullLogger<RequestEmailCodeCommandHandler>.Instance);

        var action = () => handler.Handle(
            new RequestEmailCodeCommand { Email = user.Email },
            cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ConsumeEmailCodeCommandHandler_ValidCodeIssuesTokensThroughSessionIssuer()
    {
        var user = CreateUser("consume@test.com");
        var userRepository = new Mock<IUserRepository>();
        var emailCodeService = new Mock<IEmailCodeService>();
        var sessionIssuer = new Mock<IAuthenticatedSessionIssuer>();
        emailCodeService.Setup(s => s.VerifyEmailCodeAsync(user.Email, "123456"))
            .ReturnsAsync(new TokenValidationResult(true, user.Id, user.Email));
        userRepository.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        sessionIssuer.Setup(s => s.IssueAsync(user, null, It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignInResponse
            {
                Success = true,
                UserId = user.Id,
                Email = user.Email,
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                ExpiresIn = 900,
                SessionId = Guid.NewGuid()
            });

        var handler = new ConsumeEmailCodeCommandHandler(
            userRepository.Object,
            emailCodeService.Object,
            sessionIssuer.Object,
            NullLogger<ConsumeEmailCodeCommandHandler>.Instance);

        var result = await handler.Handle(
            new ConsumeEmailCodeCommand { Email = user.Email, Code = "123456" },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.UserId.Should().Be(user.Id);
        result.Message.Should().Be("Email-code sign-in successful");
        sessionIssuer.Verify(
            s => s.IssueAsync(
                user,
                null,
                It.Is<DeviceInfo>(device => device.DeviceName == "Email Code" && device.DeviceType == "Web"),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task ConsumeEmailCodeCommandHandler_InvalidCodeThrowsAuthenticationRequired()
    {
        var userRepository = new Mock<IUserRepository>();
        var emailCodeService = new Mock<IEmailCodeService>();
        var sessionIssuer = new Mock<IAuthenticatedSessionIssuer>();
        emailCodeService.Setup(s => s.VerifyEmailCodeAsync("consume@test.com", "000000"))
            .ReturnsAsync(TokenValidationResult.Failed("Invalid code"));

        var handler = new ConsumeEmailCodeCommandHandler(
            userRepository.Object,
            emailCodeService.Object,
            sessionIssuer.Object,
            NullLogger<ConsumeEmailCodeCommandHandler>.Instance);

        var action = () => handler.Handle(
            new ConsumeEmailCodeCommand { Email = "consume@test.com", Code = "000000" },
            CancellationToken.None);

        await action.Should().ThrowAsync<AuthenticationRequiredException>();
        sessionIssuer.Verify(s => s.IssueAsync(It.IsAny<User>(), It.IsAny<Guid?>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumeEmailCodeCommandHandler_UserFailingAuthenticationValidationThrows()
    {
        var user = CreateUser("inactive@test.com");
        user.IsActive = false;
        var userRepository = new Mock<IUserRepository>();
        var emailCodeService = new Mock<IEmailCodeService>();
        var sessionIssuer = new Mock<IAuthenticatedSessionIssuer>();
        emailCodeService.Setup(s => s.VerifyEmailCodeAsync(user.Email, "123456"))
            .ReturnsAsync(new TokenValidationResult(true, user.Id, user.Email));
        userRepository.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new ConsumeEmailCodeCommandHandler(
            userRepository.Object,
            emailCodeService.Object,
            sessionIssuer.Object,
            NullLogger<ConsumeEmailCodeCommandHandler>.Instance);

        var action = () => handler.Handle(
            new ConsumeEmailCodeCommand { Email = user.Email, Code = "123456" },
            CancellationToken.None);

        await action.Should().ThrowAsync<AuthenticationRequiredException>();
        sessionIssuer.Verify(s => s.IssueAsync(It.IsAny<User>(), It.IsAny<Guid?>(), It.IsAny<DeviceInfo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumeEmailCodeCommandHandler_MissingUserThrowsAuthenticationRequired()
    {
        var userId = Guid.NewGuid();
        var userRepository = new Mock<IUserRepository>();
        var emailCodeService = new Mock<IEmailCodeService>();
        var sessionIssuer = new Mock<IAuthenticatedSessionIssuer>();
        emailCodeService.Setup(s => s.VerifyEmailCodeAsync("gone@test.com", "123456"))
            .ReturnsAsync(new TokenValidationResult(true, userId, "gone@test.com"));
        userRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new ConsumeEmailCodeCommandHandler(
            userRepository.Object,
            emailCodeService.Object,
            sessionIssuer.Object,
            NullLogger<ConsumeEmailCodeCommandHandler>.Instance);

        var action = () => handler.Handle(
            new ConsumeEmailCodeCommand { Email = "gone@test.com", Code = "123456" },
            CancellationToken.None);

        await action.Should().ThrowAsync<AuthenticationRequiredException>();
    }
}
