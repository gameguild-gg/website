using System.Text.Json;
using FluentAssertions;
using GameGuild.Notifications.Services.Email;
using GameGuild.Notifications.Services.Push;
using GameGuild.Notifications.UnitTests.Infrastructure;
using GameGuild.Push;
using GameGuild.Sms;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Notifications.UnitTests.Services.Push;

/// <summary>
///     Push dispatch pipeline tests (issue #259): pending push rows are claimed and delivered
///     through the IPushSender port with the protected-metadata device token and data payload;
///     declines and missing tokens retry then deadletter; stale challenges never send.
/// </summary>
public sealed class PushDispatcherServiceTests : IDisposable
{
    private readonly NotificationsTestDbContext context = CreateContext();
    private readonly Mock<IPushSender> pushSender = new();
    private readonly NotificationMetadataProtector protector = new(new EphemeralDataProtectionProvider());

    private static NotificationsTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotificationsTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new NotificationsTestDbContext(options);
    }

    private PushDispatcherService CreateService(PushDispatcherOptions? options = null)
    {
        return new PushDispatcherService(
            context,
            pushSender.Object,
            protector,
            Microsoft.Extensions.Options.Options.Create(options ?? new PushDispatcherOptions()),
            NullLogger<PushDispatcherService>.Instance);
    }

    private async Task<Notification> AddPendingPushAsync(string? metadataJson, NotificationType type = NotificationType.PushMfaChallenge)
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            type,
            NotificationChannel.Push,
            "GameGuild sign-in approval",
            "Approve this sign-in request",
            metadata: metadataJson);
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        return notification;
    }

    private static string MetadataJson(string? deviceToken) =>
        JsonSerializer.Serialize(new
        {
            deviceToken,
            data = new Dictionary<string, string> { ["challengeId"] = "abc123", ["kind"] = "mfa-challenge" }
        });

    public void Dispose() => context.Dispose();

    [Fact]
    public async Task PendingPushWithProtectedToken_IsDeliveredWithTitleBodyAndData()
    {
        var notification = await AddPendingPushAsync(MetadataJson("device-token-0123456789abcdef"));
        protector.ProtectForStorage(notification);
        await context.SaveChangesAsync();
        pushSender
            .Setup(s => s.SendAsync(It.Is<PushMessage>(m =>
                m.DeviceToken == "device-token-0123456789abcdef" &&
                m.Title.Contains("approval") &&
                m.Data!["challengeId"] == "abc123"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushDeliveryReceipt(Accepted: true, ProviderMessageId: "projects/p1/m1"));
        var service = CreateService();

        var processed = await service.SweepOnceAsync();

        processed.Should().Be(1);
        await context.Entry(notification).ReloadAsync();
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Sent);
        notification.ProviderMessageId.Should().Be("projects/p1/m1");
    }

    [Fact]
    public async Task MissingDeviceToken_DeadlettersWithoutSending()
    {
        var notification = await AddPendingPushAsync(null);
        var service = CreateService();

        await service.SweepOnceAsync();

        await context.Entry(notification).ReloadAsync();
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.DeadLettered);
        pushSender.Verify(s => s.SendAsync(It.IsAny<PushMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProviderDecline_ConsumesRetriesThenDeadletters()
    {
        var options = new PushDispatcherOptions { MaxAttempts = 2, BackoffSchedule = [TimeSpan.Zero] };
        var notification = await AddPendingPushAsync(MetadataJson("device-token-0123456789abcdef"));
        protector.ProtectForStorage(notification);
        await context.SaveChangesAsync();
        pushSender
            .Setup(s => s.SendAsync(It.IsAny<PushMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushDeliveryReceipt(Accepted: false, ProviderMessageId: null));
        var service = CreateService(options);

        await service.SweepOnceAsync();
        await context.Entry(notification).ReloadAsync();
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);
        notification.AttemptCount.Should().Be(1);

        notification.GetType().GetProperty("NextAttemptAt")!.SetValue(notification, DateTime.UtcNow.AddMinutes(-1));
        await context.SaveChangesAsync();
        await service.SweepOnceAsync();

        await context.Entry(notification).ReloadAsync();
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.DeadLettered);
    }

    [Fact]
    public async Task StaleTransactionalPush_DeadlettersWithoutSending()
    {
        var notification = await AddPendingPushAsync(MetadataJson("device-token-0123456789abcdef"));
        protector.ProtectForStorage(notification);
        notification.GetType().GetProperty("CreatedAt")!.SetValue(notification, DateTime.UtcNow.AddHours(-1));
        await context.SaveChangesAsync();
        var service = CreateService(new PushDispatcherOptions { TransactionalStalenessTtl = TimeSpan.FromMinutes(10) });

        await service.SweepOnceAsync();

        await context.Entry(notification).ReloadAsync();
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.DeadLettered);
        pushSender.Verify(s => s.SendAsync(It.IsAny<PushMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
