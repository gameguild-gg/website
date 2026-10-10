using FluentAssertions;
using GameGuild.API.Core.Security;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;

namespace GameGuild.API.UnitTests.Security;

public sealed class PermissionExpirationAlertLoggingTests
{
    [Theory]
    [InlineData(false, "failure")]
    [InlineData(true, "no result")]
    public async Task FailedDelivery_PreservesBothRecipients_WithoutLoggingPrivateFailureDetails(bool nullResult, string expectedOutcome)
    {
        const string recipientEmail = "private-recipient@confidential.example.test";
        const string privateFailureCode = "Provider.private-recipient@confidential.example.test";
        const string privateFailureDescription = "Rejected private-recipient@confidential.example.test\r\ncredential=private-value";
        var userId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();
        var users = new Mock<IUserRepository>();
        users.Setup(repository => repository.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, Email = recipientEmail, Name = "Recipient" });
        var notifications = new Mock<INotificationService>();
        notifications.Setup(service => service.SendAsync(It.IsAny<Guid?>(), It.IsAny<NotificationType>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationChannel>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<NotificationPriority>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(nullResult ? null! : Result.Failure<Notification>(Error.Failure(privateFailureCode, privateFailureDescription)));
        var logger = new Mock<ILogger<PermissionExpirationAlertHandler>>();
        var handler = new PermissionExpirationAlertHandler(users.Object, notifications.Object,
            Options.Create(new PermissionExpirationOptions { Enabled = true }), logger.Object);
        var notification = new PermissionExpirationNotification(permissionId, userId, null, ["permissions:read"],
            DateTime.UtcNow.AddDays(3), PermissionExpirationKind.Upcoming);

        await handler.Handle(notification, CancellationToken.None);

        var sends = notifications.Invocations.Where(invocation => invocation.Method.Name == nameof(INotificationService.SendAsync)).ToArray();
        sends.Should().HaveCount(2);
        sends[0].Arguments[0].Should().Be(userId);
        sends[0].Arguments[4].Should().Be(NotificationChannel.InApp);
        sends[0].Arguments[8].Should().Be(permissionId);
        sends[0].Arguments[11].Should().BeNull();
        sends[1].Arguments[0].Should().Be(userId);
        sends[1].Arguments[4].Should().Be(NotificationChannel.Email);
        sends[1].Arguments[8].Should().Be(permissionId);
        sends[1].Arguments[11].Should().Be(recipientEmail);

        var logs = logger.Invocations.Where(invocation => invocation.Method.Name == nameof(ILogger.Log)).ToArray();
        logs.Should().HaveCount(2);
        foreach (var log in logs)
        {
            log.Arguments[0].Should().Be(LogLevel.Warning);
            log.Arguments[3].Should().BeNull();
            var state = Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(log.Arguments[2]).ToArray();
            state.Select(property => property.Key).Should().Equal("Kind", "Channel", "Outcome", "{OriginalFormat}");
            state.Single(property => property.Key == "Kind").Value.Should().Be(PermissionExpirationKind.Upcoming);
            state.Single(property => property.Key == "Outcome").Value.Should().Be(expectedOutcome);
            foreach (var property in state)
            {
                (property.Value?.ToString() ?? string.Empty).Should().NotContain(recipientEmail)
                    .And.NotContain(privateFailureCode).And.NotContain(privateFailureDescription)
                    .And.NotContain(userId.ToString()).And.NotContain("private-value");
            }
        }
        logs.Select(log => Assert.IsAssignableFrom<IEnumerable<KeyValuePair<string, object?>>>(log.Arguments[2])
                .Single(property => property.Key == "Channel").Value)
            .Should().Equal(NotificationChannel.InApp, NotificationChannel.Email);
    }
}
