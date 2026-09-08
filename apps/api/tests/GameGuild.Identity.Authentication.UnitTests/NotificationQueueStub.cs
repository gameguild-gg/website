using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Moq;

namespace GameGuild.Identity.Authentication.UnitTests;

internal static class NotificationQueueStub
{
    public static Mock<INotificationService> Success()
    {
        var service = new Mock<INotificationService>();
        service.SetReturnsDefault(Task.FromResult(Result.Success(Notification.Create(
            null, NotificationType.Onboarding, NotificationChannel.Email, "Queued", "Stored"))));
        return service;
    }
}
