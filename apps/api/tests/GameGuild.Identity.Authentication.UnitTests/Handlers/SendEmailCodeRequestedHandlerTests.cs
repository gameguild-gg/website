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
            entry.Message.Contains($"Email-code email queued for user {LogRedaction.RedactId(UserId, "uid")}"));
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
            entry.Message.Contains(typeof(InvalidOperationException).FullName!));
        logger.Entries.Should().OnlyContain(entry =>
            !entry.Message.Contains(notification.Email) && !entry.Message.Contains("u***@e***.com"));
    }
    [Fact]
    public async Task Handle_LogPrivacy_UnknownAccountSkipsDeliveryWithoutEmailOrCode()
    {
        var logger = new TestLogger<SendEmailCodeRequestedHandler>();
        var service = NotificationQueueStub.Success();
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var notification = new EmailCodeRequestedNotification
        {
            Email = $"unknown-{Guid.NewGuid():N}@privacy.example.test",
            Code = "942761",
            TenantId = Guid.NewGuid()
        };
        var subject = new SendEmailCodeRequestedHandler(logger, service.Object, userRepository.Object);

        await subject.Handle(notification, CancellationToken.None);

        service.Invocations.Should().BeEmpty();
        logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning);
        logger.Entries.Single().Properties.Should().Contain(pair => pair.Key == "TenantId" && Equals(pair.Value, notification.TenantId));
        EmailLogPrivacyAssertions.RequirePrivateValuesAbsent(logger, notification.Email, notification.Code, LogRedaction.MaskEmail(notification.Email));
    }

    [Fact]
    public async Task Handle_LogPrivacy_QueuedCodeKeepsExactDeliveryMetadata()
    {
        var logger = new TestLogger<SendEmailCodeRequestedHandler>();
        var service = NotificationQueueStub.Success();
        var userRepository = new Mock<IUserRepository>();
        var userId = Guid.NewGuid();
        var notification = new EmailCodeRequestedNotification
        {
            Email = $"recipient-{Guid.NewGuid():N}@privacy.example.test",
            Code = "942761",
            UserName = $"private-name-{Guid.NewGuid():N}",
            TenantId = Guid.NewGuid()
        };
        userRepository.Setup(r => r.GetByEmailAsync(notification.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, Email = notification.Email });
        var subject = new SendEmailCodeRequestedHandler(logger, service.Object, userRepository.Object);

        await subject.Handle(notification, CancellationToken.None);

        var sent = service.Invocations.Should().ContainSingle().Subject;
        sent.Arguments[0].Should().Be(userId);
        sent.Arguments[5].Should().Be(notification.TenantId);
        using var metadata = System.Text.Json.JsonDocument.Parse(sent.Arguments[10].Should().BeOfType<string>().Subject);
        metadata.RootElement.GetProperty("email").GetString().Should().Be(notification.Email);
        metadata.RootElement.GetProperty("code").GetString().Should().Be(notification.Code);
        metadata.RootElement.GetProperty("userName").GetString().Should().Be(notification.UserName);
        logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Information);
        logger.Entries.Single().Properties.Should().Contain(pair => pair.Key == "UserId" && Equals(pair.Value, LogRedaction.RedactId(userId, "uid")));
        EmailLogPrivacyAssertions.RequirePrivateValuesAbsent(logger, notification.Email, notification.Code, notification.UserName, userId.ToString(), LogRedaction.MaskEmail(notification.Email));
    }

    [Theory]
    [InlineData(false, "message")]
    [InlineData(false, "inner")]
    [InlineData(false, "data")]
    [InlineData(true, "message")]
    [InlineData(true, "inner")]
    [InlineData(true, "data")]
    public async Task Handle_LogPrivacy_PrivateFailureIsRethrown(bool repositoryFailure, string carrier)
    {
        var logger = new TestLogger<SendEmailCodeRequestedHandler>();
        var service = NotificationQueueStub.Success();
        var userRepository = new Mock<IUserRepository>();
        var privateValue = $"private-payload-{Guid.NewGuid():N}";
        var failure = EmailLogPrivacyAssertions.PrivateFailure(carrier, privateValue);
        var notification = new EmailCodeRequestedNotification
        {
            Email = $"recipient-{Guid.NewGuid():N}@privacy.example.test",
            Code = "942761",
            TenantId = Guid.NewGuid()
        };
        if (repositoryFailure)
        {
            userRepository.Setup(r => r.GetByEmailAsync(notification.Email, It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        }
        else
        {
            userRepository.Setup(r => r.GetByEmailAsync(notification.Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new User { Id = Guid.NewGuid(), Email = notification.Email });
            service.SetReturnsDefault(Task.FromException<Result<GameGuild.Notifications.Notification>>(failure));
        }
        var subject = new SendEmailCodeRequestedHandler(logger, service.Object, userRepository.Object);

        var act = () => subject.Handle(notification, CancellationToken.None);
        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);

        service.Invocations.Count.Should().Be(repositoryFailure ? 0 : 1);
        logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Error);
        logger.Entries.Single().Properties.Should().Contain(pair => pair.Key == "ErrorType" && Equals(pair.Value, typeof(InvalidOperationException).FullName));
        EmailLogPrivacyAssertions.RequirePrivateValuesAbsent(logger, notification.Email, notification.Code, privateValue, LogRedaction.MaskEmail(notification.Email));
    }

}
