using FluentAssertions;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class SendEmailCodeRequestedHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ShouldCreateEmailCodeRow_WithCodeMetadata()
    {
        var logger = new TestLogger<SendEmailCodeRequestedHandler>();
        var service = NotificationQueueStub.Success();
        var userRepo = new Mock<IUserRepository>();
        userRepo
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = UserId, Email = "user@example.com" });
        var handler = new SendEmailCodeRequestedHandler(logger, service.Object, userRepo.Object);
        var notification = new EmailCodeRequestedNotification
        {
            Email = "user@example.com",
            Code = "123456",
            UserName = "Alice",
            TenantId = Guid.NewGuid()
        };

        await handler.Handle(notification, CancellationToken.None);

        service.Verify(s => s.SendAsync(
            UserId,
            NotificationType.EmailCode,
            "Your GameGuild sign-in code",
            It.IsAny<string>(),
            NotificationChannel.Email,
            notification.TenantId,
            It.IsAny<string?>(),
            It.IsAny<NotificationPriority>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.Is<string>(m => m!.Contains("123456") && m.Contains("user@example.com")),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains($"Email-code email queued for {LogRedaction.MaskEmail(notification.Email)}"));
        logger.Entries.Should().OnlyContain(entry =>
            !entry.Message.Contains(notification.Email) && !entry.Message.Contains("u***@e***.com"));
    }

    [Fact]
    public async Task Handle_ShouldSkipDelivery_WhenUserIsUnknown()
    {
        var logger = new TestLogger<SendEmailCodeRequestedHandler>();
        var service = NotificationQueueStub.Success();
        var userRepo = new Mock<IUserRepository>();
        userRepo
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var handler = new SendEmailCodeRequestedHandler(logger, service.Object, userRepo.Object);

        await handler.Handle(new EmailCodeRequestedNotification { Email = "ghost@example.com", Code = "123456" }, CancellationToken.None);

        service.Verify(s => s.SendAsync(
            It.IsAny<Guid>(),
            It.IsAny<NotificationType>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<NotificationChannel>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<NotificationPriority>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenNotificationServiceFails()
    {
        var logger = new TestLogger<SendEmailCodeRequestedHandler>();
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
        var handler = new SendEmailCodeRequestedHandler(logger, service.Object, userRepo.Object);
        var notification = new EmailCodeRequestedNotification
        {
            Email = "user@example.com",
            Code = "123456",
            UserName = "Alice"
        };

        var act = () => handler.Handle(notification, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Error &&
            entry.Message.Contains(LogRedaction.MaskEmail(notification.Email)));
        logger.Entries.Should().OnlyContain(entry =>
            !entry.Message.Contains(notification.Email) && !entry.Message.Contains("u***@e***.com"));
    }
}
