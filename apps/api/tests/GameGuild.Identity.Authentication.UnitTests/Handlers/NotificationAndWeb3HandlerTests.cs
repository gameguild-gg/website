using FluentAssertions;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public class VerifyWeb3SignatureHandlerTests
{
    [Fact]
    public async Task Handle_ShouldMapCommandToVerificationRequest_AndReturnAuthServiceResponse()
    {
        var authService = new Mock<IAuthService>();
        Web3VerificationRequest? capturedRequest = null;
        var expectedResponse = new SignInResponse
        {
            Success = true,
            Message = "verified",
            Email = "wallet@example.com"
        };
        using var cancellationTokenSource = new CancellationTokenSource();

        authService
            .Setup(service => service.VerifyWeb3SignatureAsync(It.IsAny<Web3VerificationRequest>(), cancellationTokenSource.Token))
            .Callback<Web3VerificationRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(expectedResponse);

        var handler = new VerifyWeb3SignatureHandler(authService.Object);
        var command = new VerifyWeb3SignatureCommand
        {
            WalletAddress = "0xabc123",
            Signature = "signed-payload",
            Nonce = "nonce-value",
            ChainId = "1",
            DeviceFingerprint = "device-1",
            TenantId = Guid.NewGuid()
        };

        var result = await handler.Handle(command, cancellationTokenSource.Token);

        result.Should().BeSameAs(expectedResponse);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.WalletAddress.Should().Be(command.WalletAddress);
        capturedRequest.Signature.Should().Be(command.Signature);
        capturedRequest.Challenge.Should().Be(command.Nonce);

        authService.Verify(
            service => service.VerifyWeb3SignatureAsync(It.IsAny<Web3VerificationRequest>(), cancellationTokenSource.Token),
            Times.Once);
    }
}

public class UserSignedInEventHandlerTests
{
    [Fact]
    public async Task Handle_ShouldLogUnknownIp_WhenNotificationDoesNotProvideOne()
    {
        var logger = new TestLogger<UserSignedInEventHandler>();
        var handler = new UserSignedInEventHandler(logger);
        var notification = new TestUserSignedInEvent(
            Guid.NewGuid(),
            "user@example.com",
            "WebAuthn",
            null,
            "Firefox",
            new DateTime(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc));

        await handler.Handle(notification, CancellationToken.None);

        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Information);
        logger.Entries[0].Message.Should().Contain("Unknown");
        logger.Entries[0].Message.Should().Contain(notification.Email);
        logger.Entries[0].Message.Should().Contain(notification.AuthMethod);
    }

    private sealed record TestUserSignedInEvent(
        Guid UserId,
        string Email,
        string AuthMethod,
        string? IpAddress,
        string? UserAgent,
        DateTime Timestamp)
        : UserSignedInEvent(UserId, Email, AuthMethod, IpAddress, UserAgent, Timestamp);
}

public class SendEmailVerificationRequestedHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static (SendEmailVerificationRequestedHandler Handler, Mock<INotificationService> Service) CreateSubject(
        TestLogger<SendEmailVerificationRequestedHandler> logger,
        User? user = null)
    {
        var service = NotificationQueueStub.Success();
        var userRepo = new Mock<IUserRepository>();
        userRepo
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var handler = new SendEmailVerificationRequestedHandler(logger, service.Object, userRepo.Object);
        return (handler, service);
    }

    [Fact]
    public async Task Handle_ShouldCreateNotificationRow_WithVerificationMetadata()
    {
        var logger = new TestLogger<SendEmailVerificationRequestedHandler>();
        var (handler, service) = CreateSubject(logger, new User { Id = UserId, Email = "user@example.com" });
        var notification = new EmailVerificationRequestedNotification
        {
            Email = "user@example.com",
            Token = "token-abc",
            UserName = "Alice"
        };

        await handler.Handle(notification, CancellationToken.None);

        service.Verify(s => s.SendAsync(
            UserId,
            NotificationType.EmailVerification,
            It.IsAny<string>(),
            It.IsAny<string>(),
            NotificationChannel.Email,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<NotificationPriority>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.Is<string>(m => m!.Contains("token-abc") && m.Contains("user@example.com") && m.Contains("Alice")),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains("Verification email queued for user@example.com"));
    }

    [Fact]
    public async Task Handle_ShouldLogWarning_WhenUserNotFound()
    {
        var logger = new TestLogger<SendEmailVerificationRequestedHandler>();
        var (handler, _) = CreateSubject(logger, user: null);
        var notification = new EmailVerificationRequestedNotification
        {
            Email = "unknown@example.com",
            Token = "token-abc",
            UserName = null
        };

        await handler.Handle(notification, CancellationToken.None);

        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("unknown@example.com"));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenNotificationServiceFails()
    {
        var logger = new TestLogger<SendEmailVerificationRequestedHandler>();
        var (handler, service) = CreateSubject(logger, new User { Id = UserId, Email = "user@example.com" });
        service
            .Setup(s => s.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("notifications offline"));
        var notification = new EmailVerificationRequestedNotification
        {
            Email = "user@example.com",
            Token = "token-error",
            UserName = "User"
        };

        var act = () => handler.Handle(notification, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Error &&
            entry.Message.Contains("user@example.com"));
    }
}

public class SendWelcomeEmailHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ShouldCreateOnboardingRow_WithDisplayNameMetadata()
    {
        var logger = new TestLogger<SendWelcomeEmailHandler>();
        var service = NotificationQueueStub.Success();
        var handler = new SendWelcomeEmailHandler(logger, service.Object, Mock.Of<IUserRepository>());
        var notification = new UserSignedUpNotification
        {
            UserId = UserId,
            Email = "user@example.com",
            Username = "alice"
        };

        await handler.Handle(notification, CancellationToken.None);

        service.Verify(s => s.SendAsync(
            UserId,
            NotificationType.Onboarding,
            It.IsAny<string>(),
            It.IsAny<string>(),
            NotificationChannel.Email,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<NotificationPriority>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.Is<string>(m => m!.Contains("alice") && m.Contains("user@example.com")),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains("Welcome email queued for user@example.com"));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenNotificationServiceFails()
    {
        var logger = new TestLogger<SendWelcomeEmailHandler>();
        var service = NotificationQueueStub.Success();
        service
            .Setup(s => s.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("notifications offline"));
        var handler = new SendWelcomeEmailHandler(logger, service.Object, Mock.Of<IUserRepository>());
        var notification = new UserSignedUpNotification
        {
            UserId = UserId,
            Email = "user@example.com",
            Username = "alice"
        };

        var act = () => handler.Handle(notification, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("user@example.com"));
    }
}

public class SendPasswordResetRequestedHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ShouldCreatePasswordResetRow_WithTokenMetadata()
    {
        var logger = new TestLogger<SendPasswordResetRequestedHandler>();
        var service = NotificationQueueStub.Success();
        var userRepo = new Mock<IUserRepository>();
        userRepo
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, Email = "user@example.com" });
        var handler = new SendPasswordResetRequestedHandler(logger, service.Object, userRepo.Object);
        var notification = new PasswordResetRequestedNotification
        {
            Email = "user@example.com",
            Token = "reset-token",
            UserName = "Alice"
        };

        await handler.Handle(notification, CancellationToken.None);

        service.Verify(s => s.SendAsync(
            UserId,
            NotificationType.PasswordReset,
            It.IsAny<string>(),
            It.IsAny<string>(),
            NotificationChannel.Email,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<NotificationPriority>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.Is<string>(m => m!.Contains("reset-token") && m.Contains("user@example.com")),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains("Password reset email queued for user@example.com"));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenNotificationServiceFails()
    {
        var logger = new TestLogger<SendPasswordResetRequestedHandler>();
        var service = NotificationQueueStub.Success();
        var userRepo = new Mock<IUserRepository>();
        userRepo
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, Email = "user@example.com" });
        service
            .Setup(s => s.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("notifications offline"));
        var handler = new SendPasswordResetRequestedHandler(logger, service.Object, userRepo.Object);
        var notification = new PasswordResetRequestedNotification
        {
            Email = "user@example.com",
            Token = "reset-token",
            UserName = "Alice"
        };

        var act = () => handler.Handle(notification, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Error &&
            entry.Message.Contains("user@example.com"));
    }
}

public class SendMagicLinkRequestedHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ShouldCreateMagicLinkRow_WithTokenMetadata()
    {
        var logger = new TestLogger<SendMagicLinkRequestedHandler>();
        var service = NotificationQueueStub.Success();
        var userRepo = new Mock<IUserRepository>();
        userRepo
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, Email = "user@example.com" });
        var handler = new SendMagicLinkRequestedHandler(logger, service.Object, userRepo.Object);
        var notification = new MagicLinkRequestedNotification
        {
            Email = "user@example.com",
            Token = "magic-token",
            UserName = "Alice",
            TenantId = Guid.NewGuid()
        };

        await handler.Handle(notification, CancellationToken.None);

        service.Verify(s => s.SendAsync(
            UserId,
            NotificationType.MagicLink,
            It.IsAny<string>(),
            It.IsAny<string>(),
            NotificationChannel.Email,
            notification.TenantId,
            It.IsAny<string?>(),
            It.IsAny<NotificationPriority>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.Is<string>(m => m!.Contains("magic-token") && m.Contains("user@example.com")),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains("Magic-link email queued for user@example.com"));
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenNotificationServiceFails()
    {
        var logger = new TestLogger<SendMagicLinkRequestedHandler>();
        var service = NotificationQueueStub.Success();
        var userRepo = new Mock<IUserRepository>();
        userRepo
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, Email = "user@example.com" });
        service
            .Setup(s => s.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("notifications offline"));
        var handler = new SendMagicLinkRequestedHandler(logger, service.Object, userRepo.Object);
        var notification = new MagicLinkRequestedNotification
        {
            Email = "user@example.com",
            Token = "magic-token",
            UserName = "Alice"
        };

        var act = () => handler.Handle(notification, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Error &&
            entry.Message.Contains("user@example.com"));
    }
}

sealed class TestLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = new();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
