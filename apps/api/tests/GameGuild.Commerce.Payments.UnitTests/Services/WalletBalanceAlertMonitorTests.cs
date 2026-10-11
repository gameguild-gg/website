using FluentAssertions;
using GameGuild.Commerce.Payments;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Services;

/// <summary>
///     Low-balance threshold alerting tests (issue #406): notice below threshold, one notice
///     per dip (idempotent via LowBalanceAlertedAt), re-arm after recovery, and
///     preference-suppressed deliveries.
/// </summary>
public class WalletBalanceAlertMonitorTests : IDisposable
{
    private readonly PaymentsPersistenceTestDbContext _context;
    private readonly WalletRepository _repository;
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly WalletBalanceAlertMonitor _monitor;
    private readonly DateTime _now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    public WalletBalanceAlertMonitorTests()
    {
        var options = new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
            .UseInMemoryDatabase($"wallet-low-balance-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new PaymentsPersistenceTestDbContext(options);
        _repository = new WalletRepository(_context, NullLogger<WalletRepository>.Instance);
        _monitor = new WalletBalanceAlertMonitor(
            _repository,
            _notificationService.Object,
            Options.Create(new WalletsOptions { LowBalanceThreshold = 10m }),
            NullLogger<WalletBalanceAlertMonitor>.Instance);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task RunPass_BelowThreshold_ShouldNotifyTheOwnerOnceAndRecordTheMarker()
    {
        var wallet = await SeedWalletAsync(balance: 4m);
        SetupNotificationSuccess();

        var notices = await _monitor.RunPassAsync(_now);

        notices.Should().Be(1);
        _notificationService.Verify(service => service.SendAsync(
            wallet.UserId,
            NotificationType.WalletBalanceBelowThreshold,
            It.IsAny<string>(),
            It.Is<string>(message => message.Contains("4.00") && message.Contains("10.00")),
            NotificationChannel.InApp,
            wallet.TenantId,
            It.IsAny<string?>(),
            NotificationPriority.Normal,
            wallet.Id,
            nameof(UserWallet),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        (await _repository.GetByIdAsync(wallet.Id))!.LowBalanceAlertedAt.Should().Be(_now);
    }

    [Fact]
    public async Task RunPass_AlreadyAlertedForThisDip_ShouldStayQuiet()
    {
        var wallet = await SeedWalletAsync(balance: 4m, alertedAt: _now.AddDays(-1));
        SetupNotificationSuccess();

        var notices = await _monitor.RunPassAsync(_now);

        notices.Should().Be(0);
        _notificationService.VerifyNoOtherCalls();
        (await _repository.GetByIdAsync(wallet.Id))!.LowBalanceAlertedAt.Should().Be(_now.AddDays(-1));
    }

    [Fact]
    public async Task RunPass_AfterRecoveryAboveThreshold_ShouldClearTheMarker()
    {
        var wallet = await SeedWalletAsync(balance: 25m, alertedAt: _now.AddDays(-3));

        var notices = await _monitor.RunPassAsync(_now);

        notices.Should().Be(0);
        (await _repository.GetByIdAsync(wallet.Id))!.LowBalanceAlertedAt.Should().BeNull();
    }

    [Fact]
    public async Task RunPass_AfterRecoveryAndSecondDip_ShouldNotifyAgain()
    {
        var wallet = await SeedWalletAsync(balance: 25m, alertedAt: _now.AddDays(-3));
        SetupNotificationSuccess();
        await _monitor.RunPassAsync(_now); // recovery pass clears the marker

        wallet.Balance = 2m;
        _repository.Update(wallet);
        await _repository.SaveChangesAsync();

        var notices = await _monitor.RunPassAsync(_now.AddHours(1));

        notices.Should().Be(1, "a new dip after recovery must re-alert");
    }

    [Fact]
    public async Task RunPass_AtOrAboveThresholdWithoutMarker_ShouldStayQuiet()
    {
        await SeedWalletAsync(balance: 10m);
        await SeedWalletAsync(balance: 500m);

        var notices = await _monitor.RunPassAsync(_now);

        notices.Should().Be(0);
        _notificationService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunPass_SuppressedDelivery_ShouldStillRecordTheMarkerToAvoidRetrying()
    {
        var wallet = await SeedWalletAsync(balance: 1m);
        SetupNotificationSuppressed();

        var notices = await _monitor.RunPassAsync(_now);

        notices.Should().Be(0);
        (await _repository.GetByIdAsync(wallet.Id))!.LowBalanceAlertedAt.Should().Be(_now);
    }

    private async Task<UserWallet> SeedWalletAsync(decimal balance, DateTime? alertedAt = null)
    {
        var wallet = new UserWallet
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Currency = "USD",
            Balance = balance,
            IsActive = true,
            LowBalanceAlertedAt = alertedAt
        };
        _context.Set<UserWallet>().Add(wallet);
        await _context.SaveChangesAsync();
        return wallet;
    }

    private void SetupNotificationSuccess()
    {
        _notificationService
            .Setup(service => service.SendAsync(
                It.IsAny<Guid?>(),
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
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Notification.Create(
                Guid.NewGuid(),
                NotificationType.WalletBalanceBelowThreshold,
                NotificationChannel.InApp,
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
                It.IsAny<string?>(),
                It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Notification>(Error.Failure("Notifications.Suppressed", "preference decision")));
    }
}
