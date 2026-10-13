using FluentAssertions;
using GameGuild.Commerce.Payments;
using GameGuild.Notifications.Services;
using GameGuild.Notifications;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Services;

/// <summary>
///     Expiry monitor tests (issue #414): 30/14/7-day horizons, idempotent notices,
/// auto-expiry after the card's expiry month, and preference-suppressed deliveries.
/// </summary>
public class PaymentMethodExpiryMonitorTests : IDisposable
{
    private readonly PaymentsPersistenceTestDbContext _context;
    private readonly PaymentMethodRepository _repository;
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly PaymentMethodExpiryMonitor _monitor;
    private readonly DateTime _now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    public PaymentMethodExpiryMonitorTests()
    {
        var options = new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
            .UseInMemoryDatabase($"payment-methods-expiry-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new PaymentsPersistenceTestDbContext(options);
        _repository = new PaymentMethodRepository(_context, NullLogger<PaymentMethodRepository>.Instance);
        _monitor = new PaymentMethodExpiryMonitor(_repository, _notificationService.Object, NullLogger<PaymentMethodExpiryMonitor>.Instance);
    }

    public void Dispose() => _context.Dispose();

    private async Task<PaymentMethod> SeedCardAsync(int expMonth, int expYear, string gatewayId)
    {
        var paymentMethod = new PaymentMethod(Guid.NewGuid(), Guid.NewGuid(), "stripe", gatewayId, PaymentMethodType.Card, isDefault: false);
        paymentMethod.UpdateGatewayMetadata(PaymentMethodType.Card, "visa", "4242", expMonth, expYear, $"fp_{gatewayId}");
        paymentMethod.Version = 1;
        _context.Set<PaymentMethod>().Add(paymentMethod);
        await _context.SaveChangesAsync();
        return paymentMethod;
    }

    private void SetupNotificationSuccess()
    {
        _notificationService
            .Setup(service => service.SendAsync(
                It.IsAny<Guid?>(),
                NotificationType.PaymentMethodExpiring,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<NotificationChannel>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Notification.Create(
                Guid.NewGuid(),
                NotificationType.PaymentMethodExpiring,
                NotificationChannel.Email,
                "title",
                "message")));
    }

    private void SetupNotificationSuppressed()
    {
        _notificationService
            .Setup(service => service.SendAsync(
                It.IsAny<Guid?>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<NotificationChannel>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Notification>(Error.Failure("Notifications.Suppressed", "preference decision")));
    }

    private int SendCallCount() =>
        _notificationService.Invocations.Count(i => i.Method.Name == nameof(INotificationService.SendAsync));

    [Fact]
    public async Task RunPass_ShouldSkipCardsOutsideTheHorizonWindow()
    {
        // Expires end of Nov 2027 → more than 30 days away.
        await SeedCardAsync(11, 2027, "pm_far_future");
        SetupNotificationSuccess();

        var notices = await _monitor.RunPassAsync(_now, CancellationToken.None);

        notices.Should().Be(0);
        SendCallCount().Should().Be(0);
    }

    [Fact]
    public async Task RunPass_ShouldSendThe30DayNoticeOnly_When20DaysRemain()
    {
        // Expires 2026-10-31 → 21 days from _now (rounded up); inside 30, outside 14.
        var card = await SeedCardAsync(10, 2026, "pm_twenty_days");
        SetupNotificationSuccess();

        var notices = await _monitor.RunPassAsync(_now, CancellationToken.None);

        notices.Should().Be(1);
        card.HasExpiryNotice(30).Should().BeTrue();
        card.HasExpiryNotice(14).Should().BeFalse();
        card.HasExpiryNotice(7).Should().BeFalse();
    }

    [Fact]
    public async Task RunPass_ShouldSend30And14_When10DaysRemain()
    {
        var card = await SeedCardAsync(10, 2026, "pm_ten_days");
        SetupNotificationSuccess();
        // Move reference time to Oct 21 so the card expires within 14 days (Oct 31).
        var later = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

        var notices = await _monitor.RunPassAsync(later, CancellationToken.None);

        notices.Should().Be(2);
        card.HasExpiryNotice(30).Should().BeTrue();
        card.HasExpiryNotice(14).Should().BeTrue();
        card.HasExpiryNotice(7).Should().BeFalse();
    }

    [Fact]
    public async Task RunPass_ShouldSendAllThreeHorizons_When5DaysRemain()
    {
        var card = await SeedCardAsync(10, 2026, "pm_five_days");
        SetupNotificationSuccess();
        var later = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

        var notices = await _monitor.RunPassAsync(later, CancellationToken.None);

        notices.Should().Be(3);
        card.HasExpiryNotice(30).Should().BeTrue();
        card.HasExpiryNotice(14).Should().BeTrue();
        card.HasExpiryNotice(7).Should().BeTrue();
    }

    [Fact]
    public async Task RunPass_ShouldBeIdempotent_AcrossDailyPasses()
    {
        var card = await SeedCardAsync(10, 2026, "pm_daily");
        SetupNotificationSuccess();

        var dayOne = await _monitor.RunPassAsync(new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        var dayTwo = await _monitor.RunPassAsync(new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        dayOne.Should().Be(2, "30- and 14-day notices fire on the first pass");
        dayTwo.Should().Be(0, "already-notified horizons are not repeated");
        SendCallCount().Should().Be(2);
    }

    [Fact]
    public async Task RunPass_ShouldAutoExpireCards_PastTheirExpiryMonth()
    {
        var card = await SeedCardAsync(9, 2026, "pm_past_expiry");
        SetupNotificationSuccess();

        var notices = await _monitor.RunPassAsync(_now, CancellationToken.None);

        notices.Should().Be(0);
        card.Status.Should().Be(PaymentMethodStatus.Expired);
        SendCallCount().Should().Be(0);
    }

    [Fact]
    public async Task RunPass_ShouldNotReExpireAlreadyExpiredCards()
    {
        var card = await SeedCardAsync(9, 2026, "pm_already_expired");
        card.MarkExpired();
        await _repository.UpdateAsync(card, CancellationToken.None);

        var notices = await _monitor.RunPassAsync(_now, CancellationToken.None);

        notices.Should().Be(0);
        card.Status.Should().Be(PaymentMethodStatus.Expired);
    }

    [Fact]
    public async Task RunPass_ShouldCountSuppressedDeliveriesAsNotified()
    {
        var card = await SeedCardAsync(10, 2026, "pm_muted");
        SetupNotificationSuppressed();

        var firstPass = await _monitor.RunPassAsync(new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        var secondPass = await _monitor.RunPassAsync(new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        firstPass.Should().Be(0, "suppressed deliveries are not counted as issued notices");
        secondPass.Should().Be(0, "preference decisions are final; the horizon is not retried");
        card.HasExpiryNotice(30).Should().BeTrue();
        card.HasExpiryNotice(14).Should().BeTrue();
    }

    [Fact]
    public async Task RunPass_ShouldSendThroughTheNotificationsModule_WithBillingType()
    {
        var card = await SeedCardAsync(10, 2026, "pm_contract");
        SetupNotificationSuccess();

        await _monitor.RunPassAsync(_now, CancellationToken.None);

        _notificationService.Verify(
            service => service.SendAsync(
                card.UserId,
                NotificationType.PaymentMethodExpiring,
                It.Is<string>(title => title.Contains("visa") && title.Contains("4242")),
                It.Is<string>(message => message.Contains("10/2026")),
                NotificationChannel.Email,
                card.TenantId,
                It.IsAny<string>(),
                NotificationPriority.Normal,
                card.Id,
                nameof(PaymentMethod),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunPass_ShouldSkipNonCardMethods()
    {
        var wallet = new PaymentMethod(Guid.NewGuid(), Guid.NewGuid(), "stripe", "pm_wallet", PaymentMethodType.Wallet, isDefault: false);
        wallet.UpdateGatewayMetadata(PaymentMethodType.Wallet, null, null, null, null, null);
        wallet.Version = 1;
        _context.Set<PaymentMethod>().Add(wallet);
        await _context.SaveChangesAsync();
        SetupNotificationSuccess();

        var notices = await _monitor.RunPassAsync(_now, CancellationToken.None);

        notices.Should().Be(0);
        SendCallCount().Should().Be(0);
    }
}
