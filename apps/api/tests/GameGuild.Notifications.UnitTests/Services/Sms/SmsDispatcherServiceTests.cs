using System.Text.Json;
using FluentAssertions;
using GameGuild.Notifications.Services.Email;
using GameGuild.Notifications.Services.Sms;
using GameGuild.Sms;
using GameGuild.Notifications.UnitTests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Notifications.UnitTests.Services.Sms;

/// <summary>
///     SMS dispatch pipeline tests (issue #244): pending SMS rows are claimed and delivered
///     through the ISmsSender port with the protected-metadata phone; declines and missing
///     recipients retry then deadletter; stale transactional SMS never sends.
/// </summary>
public sealed class SmsDispatcherServiceTests : IDisposable
{
    private readonly NotificationsTestDbContext context = CreateContext();
    private readonly Mock<ISmsSender> smsSender = new();
    private readonly NotificationMetadataProtector protector = new(new EphemeralDataProtectionProvider());

    private static NotificationsTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotificationsTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new NotificationsTestDbContext(options);
    }

    private SmsDispatcherService CreateService(SmsDispatcherOptions? options = null)
    {
        return new SmsDispatcherService(
            context,
            smsSender.Object,
            protector,
            Microsoft.Extensions.Options.Options.Create(options ?? new SmsDispatcherOptions()),
            NullLogger<SmsDispatcherService>.Instance);
    }

    private async Task<Notification> AddPendingSmsAsync(string? metadataJson, NotificationType type = NotificationType.SmsMfaCode)
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            type,
            NotificationChannel.Sms,
            "GameGuild verification code",
            "Your code is 123456",
            metadata: metadataJson);
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        return notification;
    }

    private static string MetadataJson(string? phone) =>
        JsonSerializer.Serialize(new { code = "123456", phoneNumber = phone });

    public void Dispose() => context.Dispose();

    [Fact]
    public async Task PendingSmsWithProtectedPhone_IsDeliveredAndMarkedSent()
    {
        var notification = await AddPendingSmsAsync(MetadataJson("+15551234567"));
        protector.ProtectForStorage(notification);
        await context.SaveChangesAsync();
        smsSender
            .Setup(s => s.SendAsync(It.Is<SmsMessage>(m => m.To == "+15551234567" && m.Body.Contains("123456")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmsDeliveryReceipt(Accepted: true, ProviderMessageId: "SM123"));
        var service = CreateService();

        var processed = await service.SweepOnceAsync();

        processed.Should().Be(1);
        await context.Entry(notification).ReloadAsync();
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Sent);
        notification.ProviderMessageId.Should().Be("SM123");
    }

    [Fact]
    public async Task ProviderDecline_ConsumesRetriesThenDeadletters()
    {
        var options = new SmsDispatcherOptions { MaxAttempts = 2, BackoffSchedule = [TimeSpan.Zero] };
        var notification = await AddPendingSmsAsync(MetadataJson("+15551234567"));
        protector.ProtectForStorage(notification);
        await context.SaveChangesAsync();
        smsSender
            .Setup(s => s.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmsDeliveryReceipt(Accepted: false, ProviderMessageId: null));
        var service = CreateService(options);

        await service.SweepOnceAsync();
        await context.Entry(notification).ReloadAsync();
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);
        notification.AttemptCount.Should().Be(1);
        notification.NextAttemptAt.Should().NotBeNull();

        // Make the retry due immediately and exhaust the attempt budget.
        notification.GetType().GetProperty("NextAttemptAt")!.SetValue(notification, DateTime.UtcNow.AddMinutes(-1));
        await context.SaveChangesAsync();
        await service.SweepOnceAsync();

        await context.Entry(notification).ReloadAsync();
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.DeadLettered);
    }

    [Fact]
    public async Task MissingPhone_DeadlettersWithoutSending()
    {
        var notification = await AddPendingSmsAsync(null);
        var service = CreateService();

        await service.SweepOnceAsync();

        await context.Entry(notification).ReloadAsync();
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.DeadLettered);
        smsSender.Verify(s => s.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StaleTransactionalSms_DeadlettersWithoutSending()
    {
        var notification = await AddPendingSmsAsync(MetadataJson("+15551234567"));
        protector.ProtectForStorage(notification);
        notification.GetType().GetProperty("CreatedAt")!.SetValue(notification, DateTime.UtcNow.AddHours(-1));
        await context.SaveChangesAsync();
        var service = CreateService(new SmsDispatcherOptions { TransactionalStalenessTtl = TimeSpan.FromMinutes(15) });

        await service.SweepOnceAsync();

        await context.Entry(notification).ReloadAsync();
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.DeadLettered);
        smsSender.Verify(s => s.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmailChannelRows_AreNotSwept()
    {
        context.Notifications.Add(Notification.Create(
            Guid.NewGuid(),
            NotificationType.System,
            NotificationChannel.Email,
            "t",
            "m"));
        await context.SaveChangesAsync();
        var service = CreateService();

        var processed = await service.SweepOnceAsync();

        processed.Should().Be(0);
        smsSender.Verify(s => s.SendAsync(It.IsAny<SmsMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
