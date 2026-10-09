using GameGuild.Notifications.Services.Email;
using Microsoft.AspNetCore.DataProtection;
using GameGuild.Notifications.UnitTests.Infrastructure;

namespace GameGuild.Notifications.UnitTests.Services;

public class NotificationDeliveryServiceTests
{
    [Fact]
    public async Task GetByIdAsync_Should_Return_NotFound_When_Notification_Does_Not_Exist()
    {
        using var context = CreateContext();
        var subject = CreateSubject(context, shouldSend: true);

        var result = await subject.GetByIdAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Notification.NotFound");
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Notification_When_It_Exists()
    {
        using var context = CreateContext();
        var notification = Notification.Create(Guid.NewGuid(), NotificationType.System, NotificationChannel.InApp, "Title", "Message");
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        var subject = CreateSubject(context, shouldSend: true);

        var result = await subject.GetByIdAsync(notification.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(notification.Id);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_Should_Filter_And_Page_Notifications()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var first = Notification.Create(userId, NotificationType.System, NotificationChannel.InApp, "1", "1");
        first.MarkAsRead();
        first.CreatedAt = SystemClock.UtcNow.AddMinutes(-10);
        var second = Notification.Create(userId, NotificationType.System, NotificationChannel.InApp, "2", "2");
        second.CreatedAt = SystemClock.UtcNow;
        var other = Notification.Create(Guid.NewGuid(), NotificationType.System, NotificationChannel.InApp, "3", "3");
        context.Notifications.AddRange(first, second, other);
        await context.SaveChangesAsync();
        var subject = CreateSubject(context, shouldSend: true);

        var unread = await subject.GetUserNotificationsAsync(userId, 0, 10, false);
        var paged = await subject.GetUserNotificationsAsync(userId, 0, 1, null);

        unread.Value.Should().ContainSingle(notification => notification.Id == second.Id);
        paged.Value.Should().ContainSingle(notification => notification.Id == second.Id);
    }

    [Fact]
    public async Task GetUnreadCountAsync_Should_Count_Only_Unread_Notifications()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var read = Notification.Create(userId, NotificationType.System, NotificationChannel.InApp, "1", "1");
        read.MarkAsRead();
        var unread = Notification.Create(userId, NotificationType.System, NotificationChannel.InApp, "2", "2");
        context.Notifications.AddRange(read, unread);
        await context.SaveChangesAsync();
        var subject = CreateSubject(context, shouldSend: true);

        var result = await subject.GetUnreadCountAsync(userId);

        result.Value.Should().Be(1);
    }

    [Fact]
    public async Task SendAsync_Should_Return_Failure_When_User_Preferences_Block_Delivery()
    {
        using var context = CreateContext();
        var subject = CreateSubject(context, NotificationDeliveryDecision.Drop("muted"));

        var result = await subject.SendAsync(Guid.NewGuid(), NotificationType.System, "Title", "Message");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Notification.Skipped");
        result.Error.Description.Should().Contain("muted");
        context.Notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_Should_Persist_And_Mark_InApp_Notifications_As_Sent()
    {
        using var context = CreateContext();
        var subject = CreateSubject(context, shouldSend: true);

        var result = await subject.SendAsync(Guid.NewGuid(), NotificationType.System, "Title", "Message", NotificationChannel.InApp);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsSent.Should().BeTrue();
        result.Value.SentAt.Should().NotBeNull();
        context.Notifications.Should().ContainSingle();
    }

    [Fact]
    public async Task SendAsync_Should_Persist_Non_InApp_Notifications_Without_Marking_Sent()
    {
        using var context = CreateContext();
        var subject = CreateSubject(context, shouldSend: true);

        var result = await subject.SendAsync(Guid.NewGuid(), NotificationType.System, "Title", "Message", NotificationChannel.Email);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsSent.Should().BeFalse();
        result.Value.SentAt.Should().BeNull();
        result.Value.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);
        result.Value.ScheduledAt.Should().BeNull();
    }

    [Fact]
    public async Task SendAsync_With_Null_Recipient_And_RecipientEmail_Sets_Email_On_Row()
    {
        using var context = CreateContext();
        var subject = CreateSubject(context, shouldSend: true);

        var result = await subject.SendAsync(
            null,
            NotificationType.TenantInvite,
            "Title",
            "Message",
            NotificationChannel.Email,
            recipientEmail: "learner@example.com");

        result.IsSuccess.Should().BeTrue();
        var row = context.Notifications.Should().ContainSingle().Subject;
        row.RecipientId.Should().BeNull();
        row.RecipientEmail.Should().Be("learner@example.com");
        row.Type.Should().Be(NotificationType.TenantInvite);
        row.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);
    }

    [Fact]
    public async Task SendAsync_Should_Schedule_Email_Held_By_Quiet_Hours()
    {
        using var context = CreateContext();
        var heldUntil = new DateTime(2026, 1, 2, 6, 0, 0, DateTimeKind.Utc);
        var subject = CreateSubject(context, NotificationDeliveryDecision.HoldUntil(heldUntil));

        var result = await subject.SendAsync(Guid.NewGuid(), NotificationType.System, "Title", "Message", NotificationChannel.Email);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);
        result.Value.ScheduledAt.Should().Be(heldUntil);
        result.Value.IsSent.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_Should_Queue_Digest_Emails_As_HeldForDigest()
    {
        using var context = CreateContext();
        var subject = CreateSubject(context, NotificationDeliveryDecision.Digest());

        var result = await subject.SendAsync(Guid.NewGuid(), NotificationType.System, "Title", "Message", NotificationChannel.Email);

        result.IsSuccess.Should().BeTrue();
        result.Value.DeliveryStatus.Should().Be(NotificationDeliveryStatus.HeldForDigest);
        result.Value.ScheduledAt.Should().BeNull();
        result.Value.IsSent.Should().BeFalse();
    }

    [Fact]
    public async Task SendFromTemplateAsync_Should_Return_NotFound_When_Template_Does_Not_Exist()
    {
        using var context = CreateContext();
        var subject = CreateSubject(context, shouldSend: true);

        var result = await subject.SendFromTemplateAsync(Guid.NewGuid(), "missing", new Dictionary<string, string>());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Template.NotFound");
    }

    [Fact]
    public async Task SendFromTemplateAsync_Should_Render_And_Send_Notification()
    {
        using var context = CreateContext();
        var template = NotificationTemplate.Create(
            "welcome",
            "Welcome",
            NotificationType.Onboarding,
            NotificationChannel.InApp,
            "Welcome {{name}}",
            "Hello {{name}}",
            actionUrlTemplate: "https://example.test/{{slug}}");
        context.NotificationTemplates.Add(template);
        await context.SaveChangesAsync();
        var subject = CreateSubject(context, shouldSend: true);

        var result = await subject.SendFromTemplateAsync(
            Guid.NewGuid(),
            "welcome",
            new Dictionary<string, string>
            {
                ["name"] = "Ada",
                ["slug"] = "welcome"
            });

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Welcome Ada");
        result.Value.Message.Should().Be("Hello Ada");
        result.Value.ActionUrl.Should().Be("https://example.test/welcome");
    }

    [Fact]
    public async Task SendBulkAsync_Should_Only_Send_To_Allowed_Recipients()
    {
        using var context = CreateContext();
        var allowed = Guid.NewGuid();
        var blocked = Guid.NewGuid();
        var preferenceService = new Mock<INotificationPreferenceService>();
        preferenceService
            .Setup(service => service.DecideDeliveryAsync(allowed, NotificationType.System, NotificationChannel.InApp, NotificationPriority.Normal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationDeliveryDecision.Send());
        preferenceService
            .Setup(service => service.DecideDeliveryAsync(blocked, NotificationType.System, NotificationChannel.InApp, NotificationPriority.Normal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationDeliveryDecision.Drop("muted"));
        var templateService = CreateTemplateServiceMock();
        var subject = new NotificationDeliveryService(
            new ApplicationDbContextAdapter(context),
            preferenceService.Object,
            templateService.Object,
            NullLogger<NotificationDeliveryService>.Instance,
            new NotificationMetadataProtector(new EphemeralDataProtectionProvider()));

        var result = await subject.SendBulkAsync([allowed, blocked], NotificationType.System, "Bulk", "Message");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(notification => notification.RecipientId == allowed && notification.IsSent);
    }

    [Fact]
    public async Task SendBulkAsync_Should_Map_Hold_And_Digest_Decisions_To_Row_State()
    {
        using var context = CreateContext();
        var immediate = Guid.NewGuid();
        var held = Guid.NewGuid();
        var digested = Guid.NewGuid();
        var dropped = Guid.NewGuid();
        var heldUntil = new DateTime(2026, 1, 2, 6, 0, 0, DateTimeKind.Utc);
        var preferenceService = new Mock<INotificationPreferenceService>();
        preferenceService
            .Setup(service => service.DecideDeliveryAsync(immediate, NotificationType.System, NotificationChannel.Email, NotificationPriority.Normal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationDeliveryDecision.Send());
        preferenceService
            .Setup(service => service.DecideDeliveryAsync(held, NotificationType.System, NotificationChannel.Email, NotificationPriority.Normal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationDeliveryDecision.HoldUntil(heldUntil));
        preferenceService
            .Setup(service => service.DecideDeliveryAsync(digested, NotificationType.System, NotificationChannel.Email, NotificationPriority.Normal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationDeliveryDecision.Digest());
        preferenceService
            .Setup(service => service.DecideDeliveryAsync(dropped, NotificationType.System, NotificationChannel.Email, NotificationPriority.Normal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationDeliveryDecision.Drop("muted"));
        var subject = new NotificationDeliveryService(
            new ApplicationDbContextAdapter(context),
            preferenceService.Object,
            CreateTemplateServiceMock().Object,
            NullLogger<NotificationDeliveryService>.Instance,
            new NotificationMetadataProtector(new EphemeralDataProtectionProvider()));

        var result = await subject.SendBulkAsync([immediate, held, digested, dropped], NotificationType.System, "Bulk", "Message", NotificationChannel.Email);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Single(n => n.RecipientId == immediate).DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);
        result.Value.Single(n => n.RecipientId == immediate).ScheduledAt.Should().BeNull();
        result.Value.Single(n => n.RecipientId == held).ScheduledAt.Should().Be(heldUntil);
        result.Value.Single(n => n.RecipientId == digested).DeliveryStatus.Should().Be(NotificationDeliveryStatus.HeldForDigest);
        result.Value.Should().NotContain(n => n.RecipientId == dropped);
    }

    [Fact]
    public async Task ScheduleAsync_Should_Validate_Past_And_Create_Future_Schedules()
    {
        using var context = CreateContext();
        var subject = CreateSubject(context, shouldSend: true);

        var invalid = await subject.ScheduleAsync(Guid.NewGuid(), NotificationType.System, "Past", "Message", SystemClock.UtcNow.AddMinutes(-1));
        var valid = await subject.ScheduleAsync(Guid.NewGuid(), NotificationType.System, "Future", "Message", SystemClock.UtcNow.AddMinutes(10));

        invalid.IsSuccess.Should().BeFalse();
        invalid.Error.Code.Should().Be("Notification.InvalidSchedule");
        valid.IsSuccess.Should().BeTrue();
        valid.Value.ScheduledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkAsReadAsync_And_MarkAsUnreadAsync_Should_Handle_Missing_And_Existing_Notifications()
    {
        using var context = CreateContext();
        var notification = Notification.Create(Guid.NewGuid(), NotificationType.System, NotificationChannel.InApp, "Title", "Message");
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        var subject = CreateSubject(context, shouldSend: true);

        var missingRead = await subject.MarkAsReadAsync(Guid.NewGuid());
        var read = await subject.MarkAsReadAsync(notification.Id);
        var missingUnread = await subject.MarkAsUnreadAsync(Guid.NewGuid());
        var unread = await subject.MarkAsUnreadAsync(notification.Id);

        missingRead.IsSuccess.Should().BeFalse();
        read.IsSuccess.Should().BeTrue();
        missingUnread.IsSuccess.Should().BeFalse();
        unread.IsSuccess.Should().BeTrue();
        notification.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAllAsReadAsync_Should_Mark_All_Unread_Notifications()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        context.Notifications.AddRange(
            Notification.Create(userId, NotificationType.System, NotificationChannel.InApp, "1", "1"),
            Notification.Create(userId, NotificationType.System, NotificationChannel.InApp, "2", "2"));
        await context.SaveChangesAsync();
        var subject = CreateSubject(context, shouldSend: true);

        var result = await subject.MarkAllAsReadAsync(userId);

        result.IsSuccess.Should().BeTrue();
        context.Notifications.Should().OnlyContain(notification => notification.IsRead);
    }

    [Fact]
    public async Task DeleteAsync_And_DeleteReadNotificationsAsync_Should_Soft_Delete_Persisted_Notifications()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var one = Notification.Create(userId, NotificationType.System, NotificationChannel.InApp, "1", "1");
        one.Version = 1;
        one.MarkAsRead();
        var two = Notification.Create(userId, NotificationType.System, NotificationChannel.InApp, "2", "2");
        two.Version = 1;
        two.MarkAsRead();
        context.Notifications.AddRange(one, two);
        await context.SaveChangesAsync();
        var subject = CreateSubject(context, shouldSend: true);

        var missingDelete = await subject.DeleteAsync(Guid.NewGuid());
        var deleteOne = await subject.DeleteAsync(one.Id);
        var deleteRead = await subject.DeleteReadNotificationsAsync(userId);

        missingDelete.IsSuccess.Should().BeFalse();
        deleteOne.IsSuccess.Should().BeTrue();
        deleteRead.Value.Should().Be(1);
        context.Notifications.IgnoreQueryFilters().Count(notification => notification.DeletedAt != null).Should().Be(2);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_Should_Exclude_Email_Channel_Rows()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var inApp = Notification.Create(userId, NotificationType.System, NotificationChannel.InApp, "InApp", "Message");
        var email = Notification.Create(userId, NotificationType.MonthlyStatement, NotificationChannel.Email, "Email", "Message");
        context.Notifications.AddRange(inApp, email);
        await context.SaveChangesAsync();
        var subject = CreateSubject(context, shouldSend: true);

        var result = await subject.GetUserNotificationsAsync(userId, 0, 20, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(n => n.Id).Should().Equal(inApp.Id);
    }

    [Fact]
    public async Task GetUnreadCountAsync_Should_Exclude_Email_Channel_Rows()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        context.Notifications.AddRange(
            Notification.Create(userId, NotificationType.System, NotificationChannel.InApp, "InApp unread", "Message"),
            Notification.Create(userId, NotificationType.MonthlyStatement, NotificationChannel.Email, "Email unread", "Message"));
        await context.SaveChangesAsync();
        var subject = CreateSubject(context, shouldSend: true);

        var result = await subject.GetUnreadCountAsync(userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    private static NotificationsTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotificationsTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new NotificationsTestDbContext(options);
    }

    private static NotificationDeliveryService CreateSubject(NotificationsTestDbContext context, bool shouldSend)
        => CreateSubject(context, shouldSend ? NotificationDeliveryDecision.Send() : NotificationDeliveryDecision.Drop("muted"));

    private static NotificationDeliveryService CreateSubject(NotificationsTestDbContext context, NotificationDeliveryDecision decision)
    {
        var preferenceService = new Mock<INotificationPreferenceService>();
        preferenceService
            .Setup(service => service.DecideDeliveryAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<NotificationChannel>(), It.IsAny<NotificationPriority>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(decision);

        return new NotificationDeliveryService(
            new ApplicationDbContextAdapter(context),
            preferenceService.Object,
            CreateTemplateServiceMock().Object,
            NullLogger<NotificationDeliveryService>.Instance,
            new NotificationMetadataProtector(new EphemeralDataProtectionProvider()));
    }

    private static Mock<INotificationTemplateService> CreateTemplateServiceMock()
    {
        var templateService = new Mock<INotificationTemplateService>();
        templateService
            .Setup(service => service.ReplacePlaceholders(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns((string template, Dictionary<string, string> placeholders) =>
            {
                var result = template;
                foreach (var placeholder in placeholders)
                {
                    result = result.Replace($"{{{{{placeholder.Key}}}}}", placeholder.Value);
                }

                return result;
            });

        return templateService;
    }

    private sealed class ApplicationDbContextAdapter(NotificationsTestDbContext inner) : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => inner.Set<T>();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => inner.SaveChangesAsync(cancellationToken);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Mock.Of<IDbContextTransaction>());
    }
}
