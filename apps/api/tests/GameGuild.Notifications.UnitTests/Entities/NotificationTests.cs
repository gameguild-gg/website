using GameGuild.Notifications.UnitTests.Infrastructure;

namespace GameGuild.Notifications.UnitTests.Entities;

public class NotificationTests
{
    [Fact]
    public void Create_Should_Initialize_All_Fields()
    {
        var tenantId = Guid.NewGuid();
        var scheduledAt = SystemClock.UtcNow.AddHours(2);
        var referenceEntityId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        var notification = Notification.Create(
            Guid.NewGuid(),
            NotificationType.Billing,
            NotificationChannel.Email,
            "Invoice ready",
            "Your invoice is ready",
            tenantId,
            "https://example.test/invoices/1",
            "https://example.test/icon.svg",
            NotificationPriority.High,
            scheduledAt,
            referenceEntityId,
            "Invoice",
            "{\"amount\":42}",
            templateId);

        notification.Id.Should().NotBe(Guid.Empty);
        notification.NotificationTenantId.Should().NotBeNull();
        notification.NotificationTenantId!.Value.Value.Should().Be(tenantId);
        notification.Type.Should().Be(NotificationType.Billing);
        notification.Channel.Should().Be(NotificationChannel.Email);
        notification.Title.Should().Be("Invoice ready");
        notification.Message.Should().Be("Your invoice is ready");
        notification.ActionUrl.Should().Be("https://example.test/invoices/1");
        notification.IconUrl.Should().Be("https://example.test/icon.svg");
        notification.Priority.Should().Be(NotificationPriority.High);
        notification.ScheduledAt.Should().Be(scheduledAt);
        notification.ReferenceEntityId.Should().Be(referenceEntityId);
        notification.ReferenceEntityType.Should().Be("Invoice");
        notification.Metadata.Should().Be("{\"amount\":42}");
        notification.TemplateId.Should().Be(templateId);
        notification.IsRead.Should().BeFalse();
        notification.IsSent.Should().BeFalse();
    }

    [Fact]
    public void MarkAsRead_And_MarkAsUnread_Should_Update_Read_State()
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            NotificationType.System,
            NotificationChannel.InApp,
            "Welcome",
            "Hello");

        notification.MarkAsRead();
        var readAt = notification.ReadAt;

        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();

        notification.MarkAsRead();

        notification.ReadAt.Should().Be(readAt);

        notification.MarkAsUnread();

        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();
    }

    [Fact]
    public void MarkAsSent_Should_Set_Sent_State_Only_Once()
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            NotificationType.System,
            NotificationChannel.InApp,
            "Welcome",
            "Hello");

        notification.MarkAsSent();
        var sentAt = notification.SentAt;

        notification.IsSent.Should().BeTrue();
        notification.SentAt.Should().NotBeNull();

        notification.MarkAsSent();

        notification.SentAt.Should().Be(sentAt);
    }

    [Fact]
    public void Delete_Should_SoftDelete_Persisted_Notification()
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            NotificationType.System,
            NotificationChannel.InApp,
            "Welcome",
            "Hello");

        notification.Version = 1;

        notification.Delete();

        notification.IsDeleted.Should().BeTrue();
        notification.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Create_With_Null_Recipient_Should_Store_RecipientEmail()
    {
        var notification = Notification.Create(
            null,
            NotificationType.TenantInvite,
            NotificationChannel.Email,
            "You're invited",
            "Join our workspace",
            recipientEmail: "invitee@example.test");

        notification.RecipientId.Should().BeNull();
        notification.RecipientEmail.Should().Be("invitee@example.test");
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);
        notification.AttemptCount.Should().Be(0);
        notification.LastError.Should().BeNull();
        notification.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public void MarkDeliverySent_Should_Be_Idempotent_And_Set_Sent_State()
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            NotificationType.MonthlyStatement,
            NotificationChannel.Email,
            "Statement",
            "Your statement is ready");
        notification.MarkDeliveryAttemptFailed("smtp down", SystemClock.UtcNow.AddMinutes(1));

        notification.MarkDeliverySent();
        var sentAt = notification.SentAt;

        notification.IsSent.Should().BeTrue();
        notification.SentAt.Should().NotBeNull();
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Sent);
        notification.NextAttemptAt.Should().BeNull();
        notification.LastError.Should().BeNull();

        notification.MarkDeliverySent();

        notification.SentAt.Should().Be(sentAt);
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Sent);
    }

    [Fact]
    public void MarkDeliveryAttemptFailed_Should_Increment_Attempts_And_Schedule_Retry()
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            NotificationType.EmailVerification,
            NotificationChannel.Email,
            "Verify",
            "Verify your email");
        notification.ClaimForSending();
        var nextAttemptAt = SystemClock.UtcNow.AddMinutes(5);

        notification.MarkDeliveryAttemptFailed("transient failure", nextAttemptAt);

        notification.AttemptCount.Should().Be(1);
        notification.LastError.Should().Be("transient failure");
        notification.NextAttemptAt.Should().Be(nextAttemptAt);
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);

        notification.MarkDeliveryAttemptFailed("still failing", nextAttemptAt.AddMinutes(25));

        notification.AttemptCount.Should().Be(2);
        notification.LastError.Should().Be("still failing");
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);
    }

    [Fact]
    public void MarkDeadLettered_Should_Set_Terminal_State()
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            NotificationType.PasswordReset,
            NotificationChannel.Email,
            "Reset",
            "Reset your password");
        notification.MarkDeliveryAttemptFailed("boom", SystemClock.UtcNow.AddHours(8));

        notification.MarkDeadLettered("max attempts exceeded");

        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.DeadLettered);
        notification.LastError.Should().Be("max attempts exceeded");
        notification.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public void MarkRequeued_Should_Return_DeadLettered_To_Pending_Keeping_Audit_And_Counting()
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            NotificationType.PasswordReset,
            NotificationChannel.Email,
            "Reset",
            "Reset your password");
        notification.MarkDeliveryAttemptFailed("boom", SystemClock.UtcNow.AddHours(8));
        notification.MarkDeliveryAttemptFailed("boom again", SystemClock.UtcNow.AddHours(16));
        notification.MarkDeadLettered("max attempts exceeded");

        notification.MarkRequeued();

        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);
        notification.NextAttemptAt.Should().BeNull();
        notification.RequeueCount.Should().Be(1);
        notification.AttemptCount.Should().Be(2); // audit preserved
        notification.LastError.Should().Be("max attempts exceeded"); // audit preserved
    }

    [Fact]
    public void MarkRequeued_Should_Be_NoOp_For_Non_DeadLettered_Rows()
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            NotificationType.System,
            NotificationChannel.InApp,
            "Welcome",
            "Hello");

        notification.MarkRequeued();

        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);
        notification.RequeueCount.Should().Be(0);
    }

    [Fact]
    public void ClaimForSending_Should_Transition_Pending_To_Sending_Only()
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            NotificationType.MagicLink,
            NotificationChannel.Email,
            "Sign in",
            "Use your magic link");

        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);

        notification.ClaimForSending();

        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Sending);

        notification.ClaimForSending();

        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Sending);
    }

    [Fact]
    public void DeliveryStatus_Enum_Values_Should_Be_Distinct()
    {
        Enum.GetValues<NotificationDeliveryStatus>()
            .Select(v => (int)v)
            .Should().OnlyHaveUniqueItems();
        ((int)NotificationDeliveryStatus.Pending).Should().Be(0);
        ((int)NotificationDeliveryStatus.Sending).Should().Be(1);
        ((int)NotificationDeliveryStatus.Sent).Should().Be(2);
        ((int)NotificationDeliveryStatus.Failed).Should().Be(3);
        ((int)NotificationDeliveryStatus.DeadLettered).Should().Be(4);
        ((int)NotificationDeliveryStatus.HeldForDigest).Should().Be(5);
    }

    [Theory]
    [InlineData(NotificationType.EmailVerification, 19)]
    [InlineData(NotificationType.PasswordReset, 20)]
    [InlineData(NotificationType.MagicLink, 21)]
    [InlineData(NotificationType.TenantInvite, 22)]
    [InlineData(NotificationType.MonthlyStatement, 23)]
    [InlineData(NotificationType.EmailCode, 24)]
    [InlineData(NotificationType.InvoiceIssued, 25)]
    public void New_Email_Types_Should_Append_Before_Custom(NotificationType type, int value)
    {
        ((int)type).Should().Be(value);
        value.Should().BeLessThan((int)NotificationType.Custom);
    }

    [Fact]
    public void NotificationType_Values_Should_Have_No_Collisions()
    {
        Enum.GetValues<NotificationType>()
            .Select(v => (int)v)
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Email_Channel_Row_Should_Persist_Delivery_Fields_Roundtrip()
    {
        using var context = new NotificationsTestDbContext(
            new DbContextOptionsBuilder<NotificationsTestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var notification = Notification.Create(
            null,
            NotificationType.TenantInvite,
            NotificationChannel.Email,
            "Invite",
            "You've been invited",
            recipientEmail: "invitee@example.test");
        notification.ClaimForSending();
        notification.MarkDeliveryAttemptFailed("smtp unavailable", SystemClock.UtcNow.AddMinutes(1));
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var loaded = await context.Notifications.SingleAsync(n => n.Id == notification.Id);

        loaded.RecipientId.Should().BeNull();
        loaded.RecipientEmail.Should().Be("invitee@example.test");
        loaded.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);
        loaded.AttemptCount.Should().Be(1);
        loaded.LastError.Should().Be("smtp unavailable");
        loaded.NextAttemptAt.Should().NotBeNull();
    }
}
