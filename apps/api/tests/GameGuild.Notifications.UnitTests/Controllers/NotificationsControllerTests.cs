namespace GameGuild.Notifications.UnitTests.Controllers;

public class NotificationsControllerTests
{
    [Fact]
    public async Task GetNotifications_Should_Return_Ok_With_Dtos()
    {
        var userId = Guid.NewGuid();
        var notification = Notification.Create(userId, NotificationType.Billing, NotificationChannel.Email, "Invoice", "Ready", actionUrl: "https://example.test");
        var service = new Mock<INotificationService>();
        service.Setup(x => x.GetUserNotificationsAsync(userId, 1, 2, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IEnumerable<Notification>>([notification]));
        var controller = CreateController(service, userId);

        var actionResult = await controller.GetNotifications(1, 2, false);

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeAssignableTo<IEnumerable<NotificationDto>>().Subject.Single();
        dto.Id.Should().Be(notification.Id);
        dto.Type.Should().Be(NotificationType.Billing.ToString());
        dto.Channel.Should().Be(NotificationChannel.Email.ToString());
    }

    [Fact]
    public async Task GetNotifications_Should_Throw_When_Actor_Is_Anonymous()
    {
        var service = new Mock<INotificationService>();
        var controller = CreateController(service, null);

        var act = () => controller.GetNotifications();

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetNotifications_Should_Return_BadRequest_On_Service_Failure()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<INotificationService>();
        service.Setup(x => x.GetUserNotificationsAsync(userId, 0, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<IEnumerable<Notification>>(Error.Failure("Notifications.Failed", "failed")));
        var controller = CreateController(service, userId);

        var actionResult = await controller.GetNotifications();

        actionResult.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetUnreadCount_Should_Return_Ok_Or_BadRequest()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<INotificationService>();
        service.SetupSequence(x => x.GetUnreadCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(3))
            .ReturnsAsync(Result.Failure<int>(Error.Failure("Notifications.Failed", "failed")));
        var controller = CreateController(service, userId);

        var okResult = await controller.GetUnreadCount();
        var badRequest = await controller.GetUnreadCount();

        okResult.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(new UnreadCountResponse(3));
        badRequest.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetNotification_Should_Return_NotFound_Forbid_Or_Ok()
    {
        var userId = Guid.NewGuid();
        var foreign = Notification.Create(Guid.NewGuid(), NotificationType.System, NotificationChannel.InApp, "Foreign", "Message");
        var own = Notification.Create(userId, NotificationType.System, NotificationChannel.InApp, "Own", "Message");
        var service = new Mock<INotificationService>();
        service.SetupSequence(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Notification>(Error.NotFound("Notification.NotFound", "missing")))
            .ReturnsAsync(Result.Success(foreign))
            .ReturnsAsync(Result.Success(own));
        var controller = CreateController(service, userId);

        var notFound = await controller.GetNotification(Guid.NewGuid());
        var forbid = await controller.GetNotification(Guid.NewGuid());
        var ok = await controller.GetNotification(Guid.NewGuid());

        notFound.Should().BeOfType<NotFoundObjectResult>();
        forbid.Should().BeOfType<ForbidResult>();
        ok.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<NotificationDto>();
    }

    [Fact]
    public async Task MarkAsRead_Should_Return_NotFound_Forbid_BadRequest_Or_NoContent()
    {
        var userId = Guid.NewGuid();
        var foreign = Notification.Create(Guid.NewGuid(), NotificationType.System, NotificationChannel.InApp, "Foreign", "Message");
        var own = Notification.Create(userId, NotificationType.System, NotificationChannel.InApp, "Own", "Message");
        var service = new Mock<INotificationService>();
        service.SetupSequence(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Notification>(Error.NotFound("Notification.NotFound", "missing")))
            .ReturnsAsync(Result.Success(foreign))
            .ReturnsAsync(Result.Success(own))
            .ReturnsAsync(Result.Success(own));
        service.SetupSequence(x => x.MarkAsReadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Failure("Notifications.Failed", "failed")))
            .ReturnsAsync(Result.Success());
        var controller = CreateController(service, userId);

        var notFound = await controller.MarkAsRead(Guid.NewGuid());
        var forbid = await controller.MarkAsRead(Guid.NewGuid());
        var badRequest = await controller.MarkAsRead(Guid.NewGuid());
        var noContent = await controller.MarkAsRead(Guid.NewGuid());

        notFound.Should().BeOfType<NotFoundObjectResult>();
        forbid.Should().BeOfType<ForbidResult>();
        badRequest.Should().BeOfType<BadRequestObjectResult>();
        noContent.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task MarkAllAsRead_Should_Return_BadRequest_Or_NoContent()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<INotificationService>();
        service.SetupSequence(x => x.MarkAllAsReadAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Failure("Notifications.Failed", "failed")))
            .ReturnsAsync(Result.Success());
        var controller = CreateController(service, userId);

        var badRequest = await controller.MarkAllAsRead();
        var noContent = await controller.MarkAllAsRead();

        badRequest.Should().BeOfType<BadRequestObjectResult>();
        noContent.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task MarkAsUnread_Should_Return_NotFound_Forbid_BadRequest_Or_NoContent()
    {
        var userId = Guid.NewGuid();
        var foreign = Notification.Create(Guid.NewGuid(), NotificationType.System, NotificationChannel.InApp, "Foreign", "Message");
        var own = Notification.Create(userId, NotificationType.System, NotificationChannel.InApp, "Own", "Message");
        var service = new Mock<INotificationService>();
        service.SetupSequence(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Notification>(Error.NotFound("Notification.NotFound", "missing")))
            .ReturnsAsync(Result.Success(foreign))
            .ReturnsAsync(Result.Success(own))
            .ReturnsAsync(Result.Success(own));
        service.SetupSequence(x => x.MarkAsUnreadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Failure("Notifications.Failed", "failed")))
            .ReturnsAsync(Result.Success());
        var controller = CreateController(service, userId);

        var notFound = await controller.MarkAsUnread(Guid.NewGuid());
        var forbid = await controller.MarkAsUnread(Guid.NewGuid());
        var badRequest = await controller.MarkAsUnread(Guid.NewGuid());
        var noContent = await controller.MarkAsUnread(Guid.NewGuid());

        notFound.Should().BeOfType<NotFoundObjectResult>();
        forbid.Should().BeOfType<ForbidResult>();
        badRequest.Should().BeOfType<BadRequestObjectResult>();
        noContent.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteNotification_Should_Return_NotFound_Forbid_BadRequest_Or_NoContent()
    {
        var userId = Guid.NewGuid();
        var foreign = Notification.Create(Guid.NewGuid(), NotificationType.System, NotificationChannel.InApp, "Foreign", "Message");
        var own = Notification.Create(userId, NotificationType.System, NotificationChannel.InApp, "Own", "Message");
        var service = new Mock<INotificationService>();
        service.SetupSequence(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Notification>(Error.NotFound("Notification.NotFound", "missing")))
            .ReturnsAsync(Result.Success(foreign))
            .ReturnsAsync(Result.Success(own))
            .ReturnsAsync(Result.Success(own));
        service.SetupSequence(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(Error.Failure("Notifications.Failed", "failed")))
            .ReturnsAsync(Result.Success());
        var controller = CreateController(service, userId);

        var notFound = await controller.DeleteNotification(Guid.NewGuid());
        var forbid = await controller.DeleteNotification(Guid.NewGuid());
        var badRequest = await controller.DeleteNotification(Guid.NewGuid());
        var noContent = await controller.DeleteNotification(Guid.NewGuid());

        notFound.Should().BeOfType<NotFoundObjectResult>();
        forbid.Should().BeOfType<ForbidResult>();
        badRequest.Should().BeOfType<BadRequestObjectResult>();
        noContent.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteReadNotifications_Should_Return_Ok_Or_BadRequest()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<INotificationService>();
        service.SetupSequence(x => x.DeleteReadNotificationsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(4))
            .ReturnsAsync(Result.Failure<int>(Error.Failure("Notifications.Failed", "failed")));
        var controller = CreateController(service, userId);

        var okResult = await controller.DeleteReadNotifications();
        var badRequest = await controller.DeleteReadNotifications();

        okResult.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeEquivalentTo(new DeletedCountResponse(4));
        badRequest.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Preference_Actions_Should_Return_Ok_BadRequest_And_NoContent()
    {
        var userId = Guid.NewGuid();
        var preference = NotificationPreference.CreateDefault(userId);
        preference.SetQuietHours(new TimeOnly(22, 0), new TimeOnly(7, 0), "UTC");
        preference.SetEmailDigestFrequency(DigestFrequency.Daily);
        var service = new Mock<INotificationService>();
        service.SetupSequence(x => x.GetPreferencesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(preference))
            .ReturnsAsync(Result.Failure<NotificationPreference>(Error.Failure("Notifications.Failed", "failed")));
        service.SetupSequence(x => x.UpdatePreferencesAsync(userId, false, true, false, true, false, true, false, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(preference))
            .ReturnsAsync(Result.Failure<NotificationPreference>(Error.Failure("Notifications.Failed", "failed")));
        service.SetupSequence(x => x.SetQuietHoursAsync(userId, new TimeOnly(22, 0), new TimeOnly(7, 0), "UTC", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success())
            .ReturnsAsync(Result.Failure(Error.Failure("Notifications.Failed", "failed")));
        var controller = CreateController(service, userId);

        var getOk = await controller.GetPreferences();
        var getBadRequest = await controller.GetPreferences();
        var updateOk = await controller.UpdatePreferences(new UpdatePreferencesRequest(false, true, false, true, false, true, false, true));
        var updateBadRequest = await controller.UpdatePreferences(new UpdatePreferencesRequest(false, true, false, true, false, true, false, true));
        var quietHoursNoContent = await controller.SetQuietHours(new SetQuietHoursRequest(new TimeOnly(22, 0), new TimeOnly(7, 0), "UTC"));
        var quietHoursBadRequest = await controller.SetQuietHours(new SetQuietHoursRequest(new TimeOnly(22, 0), new TimeOnly(7, 0), "UTC"));

        getOk.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<NotificationPreferenceDto>();
        getBadRequest.Should().BeOfType<BadRequestObjectResult>();
        updateOk.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<NotificationPreferenceDto>();
        updateBadRequest.Should().BeOfType<BadRequestObjectResult>();
        quietHoursNoContent.Should().BeOfType<NoContentResult>();
        quietHoursBadRequest.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetPreferences_Should_Map_Null_EmailDigestFrequency()
    {
        var userId = Guid.NewGuid();
        var preference = NotificationPreference.CreateDefault(userId);
        var service = new Mock<INotificationService>();
        service.Setup(x => x.GetPreferencesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(preference));
        var controller = CreateController(service, userId);

        var actionResult = await controller.GetPreferences();

        var dto = actionResult.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<NotificationPreferenceDto>().Subject;
        dto.EmailDigestFrequency.Should().BeNull();
    }

    [Fact]
    public async Task GetPreferences_Should_Map_MutedTypes()
    {
        var userId = Guid.NewGuid();
        var preference = NotificationPreference.CreateDefault(userId);
        preference.MuteType(nameof(NotificationType.MonthlyStatement));
        preference.MuteType(nameof(NotificationType.FeatureAnnouncement));
        var service = new Mock<INotificationService>();
        service.Setup(x => x.GetPreferencesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(preference));
        var controller = CreateController(service, userId);

        var actionResult = await controller.GetPreferences();

        var dto = actionResult.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeOfType<NotificationPreferenceDto>().Subject;
        dto.MutedTypes.Should().BeEquivalentTo(["MonthlyStatement", "FeatureAnnouncement"]);
    }

    private static NotificationsController CreateController(
        Mock<INotificationService> service,
        Guid? userId,
        Mock<INotificationPreferenceService>? preferences = null)
    {
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(CreateActorContext(userId));
        var preferenceService = (preferences ?? new Mock<INotificationPreferenceService>()).Object;
        var handler = new NotificationMutationCommandHandler(service.Object, preferenceService);
        return new NotificationsController(service.Object, actorAccessor.Object, new NotificationCommandTestSender(handler));
    }

    private static ActorContext CreateActorContext(Guid? userId)
    {
        if (!userId.HasValue)
        {
            return ActorContext.Anonymous;
        }

        return new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.Value.ToString(),
            TenantId = null,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Bearer",
            IsAuthenticated = true
        };
    }
}
