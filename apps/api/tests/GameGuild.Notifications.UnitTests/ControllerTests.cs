using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using GameGuild.Notifications;
using GameGuild.Notifications.Controllers;
using GameGuild.Notifications.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Notifications.UnitTests;

public class ControllerTests
{
    private readonly Mock<INotificationService> _svc = new();
    private readonly Mock<IActorContextAccessor> _actor = new();

    private NotificationsController CreateController(Guid? userId = null)
    {
        var uid = userId ?? Guid.NewGuid();
        _actor.Setup(a => a.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = uid.ToString(),
            TenantId = Guid.NewGuid(),
            IsAuthenticated = true,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        });
        return new NotificationsController(_svc.Object, new Mock<INotificationPreferenceService>().Object, _actor.Object,
            new HandlerSender(new NotificationMutationCommandHandler(_svc.Object)));
    }

    [Fact] public void Ctor_Creates() => CreateController().Should().NotBeNull();

    [Fact]
    public async Task GetNotifications_ReturnsOk()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.GetUserNotificationsAsync(uid, 0, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IEnumerable<Notification>>(new List<Notification>()));
        var r = await CreateController(uid).GetNotifications(0, 20, null, CancellationToken.None);
        r.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsOk()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.GetUnreadCountAsync(uid, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(5));
        var r = await CreateController(uid).GetUnreadCount(CancellationToken.None);
        r.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetNotification_Found_ReturnsOk()
    {
        var uid = Guid.NewGuid(); var nid = Guid.NewGuid();
        _svc.Setup(s => s.GetByIdAsync(nid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Notification.Create(uid, NotificationType.System, NotificationChannel.InApp, "T", "M")));
        var r = await CreateController(uid).GetNotification(nid, CancellationToken.None);
        r.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task MarkAsRead_Returns204()
    {
        var uid = Guid.NewGuid(); var nid = Guid.NewGuid();
        var notif = Notification.Create(uid, NotificationType.System, NotificationChannel.InApp, "T", "M");
        _svc.Setup(s => s.GetByIdAsync(nid, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(notif));
        _svc.Setup(s => s.MarkAsReadAsync(nid, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        var r = await CreateController(uid).MarkAsRead(nid, CancellationToken.None);
        r.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task MarkAllAsRead_Returns204()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.MarkAllAsReadAsync(uid, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        var r = await CreateController(uid).MarkAllAsRead(CancellationToken.None);
        r.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task MarkAsUnread_Returns204()
    {
        var uid = Guid.NewGuid(); var nid = Guid.NewGuid();
        var notif = Notification.Create(uid, NotificationType.System, NotificationChannel.InApp, "T", "M");
        _svc.Setup(s => s.GetByIdAsync(nid, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(notif));
        _svc.Setup(s => s.MarkAsUnreadAsync(nid, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        var r = await CreateController(uid).MarkAsUnread(nid, CancellationToken.None);
        r.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteNotification_Returns204()
    {
        var uid = Guid.NewGuid(); var nid = Guid.NewGuid();
        var notif = Notification.Create(uid, NotificationType.System, NotificationChannel.InApp, "T", "M");
        _svc.Setup(s => s.GetByIdAsync(nid, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(notif));
        _svc.Setup(s => s.DeleteAsync(nid, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        var r = await CreateController(uid).DeleteNotification(nid, CancellationToken.None);
        r.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteReadNotifications_ReturnsOk()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.DeleteReadNotificationsAsync(uid, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(3));
        var r = await CreateController(uid).DeleteReadNotifications(CancellationToken.None);
        r.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPreferences_ReturnsOk()
    {
        var uid = Guid.NewGuid();
        _svc.Setup(s => s.GetPreferencesAsync(uid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(NotificationPreference.CreateDefault(uid)));
        var r = await CreateController(uid).GetPreferences(CancellationToken.None);
        r.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdatePreferences_ReturnsOk()
    {
        var uid = Guid.NewGuid();
        var req = new UpdatePreferencesRequest(true, true, true, false, false, true, true, true);
        _svc.Setup(s => s.UpdatePreferencesAsync(uid,
            req.EmailEnabled, req.PushEnabled, req.InAppEnabled, req.SmsEnabled,
            req.MarketingEnabled, req.SocialEnabled, req.LearningEnabled, req.AchievementsEnabled,
            It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(NotificationPreference.CreateDefault(uid)));
        var r = await CreateController(uid).UpdatePreferences(req, CancellationToken.None);
        r.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SetQuietHours_Returns204()
    {
        var uid = Guid.NewGuid();
        var req = new SetQuietHoursRequest(new TimeOnly(22, 0), new TimeOnly(7, 0), "UTC");
        _svc.Setup(s => s.SetQuietHoursAsync(uid, req.Start, req.End, req.Timezone, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var r = await CreateController(uid).SetQuietHours(req, CancellationToken.None);
        r.Should().BeOfType<NoContentResult>();
    }
}
