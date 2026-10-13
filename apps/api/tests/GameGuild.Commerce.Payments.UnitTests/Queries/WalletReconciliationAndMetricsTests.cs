using FluentAssertions;
using GameGuild.Commerce.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.WalletReporting;

/// <summary>
///     Report-only reconciliation and metrics queries (issue #406): balance vs latest
///     BalanceAfter vs Σ completed transactions, and wallet analytics aggregation.
/// </summary>
public class WalletReconciliationAndMetricsTests : IDisposable
{
    private readonly PaymentsPersistenceTestDbContext _context;
    private readonly GetWalletReconciliationReportHandler _reconciliationHandler;
    private readonly GetWalletMetricsHandler _metricsHandler;

    public WalletReconciliationAndMetricsTests()
    {
        var options = new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
            .UseInMemoryDatabase($"wallet-reconciliation-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new PaymentsPersistenceTestDbContext(options);
        _reconciliationHandler = new GetWalletReconciliationReportHandler(_context);
        _metricsHandler = new GetWalletMetricsHandler(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Reconciliation_ForAConsistentWallet_ShouldReportBothInvariantsIntact()
    {
        var wallet = SeedWallet("USD", 60m);
        var t0 = SystemClock.UtcNow.AddMinutes(-10);
        SeedTransaction(wallet.Id, WalletTransactionType.Credit, 100m, 100m, createdAtUtc: t0);
        SeedTransaction(wallet.Id, WalletTransactionType.Debit, 40m, 60m, createdAtUtc: t0.AddMinutes(1));

        var report = await _reconciliationHandler.Handle(new GetWalletReconciliationReportQuery(), CancellationToken.None);

        report.WalletsChecked.Should().Be(1);
        report.DiscrepanciesFound.Should().Be(0);
        var entry = report.Items.Single();
        entry.Balance.Should().Be(60m);
        entry.LatestBalanceAfter.Should().Be(60m);
        entry.CompletedTransactionSum.Should().Be(60m);
        entry.BalanceMatchesLatest.Should().BeTrue();
        entry.BalanceMatchesTransactionSum.Should().BeTrue();
    }

    [Fact]
    public async Task Reconciliation_ForDriftedBalances_ShouldFlagDiscrepancies()
    {
        var drifted = SeedWallet("USD", 150m); // transactions only sum to 100
        SeedTransaction(drifted.Id, WalletTransactionType.Credit, 100m, 100m, createdAtUtc: SystemClock.UtcNow.AddMinutes(-10));

        var stale = SeedWallet("EUR", 20m); // latest transaction says 40
        SeedTransaction(stale.Id, WalletTransactionType.Credit, 40m, 40m, createdAtUtc: SystemClock.UtcNow.AddMinutes(-9));

        var report = await _reconciliationHandler.Handle(new GetWalletReconciliationReportQuery(), CancellationToken.None);

        report.WalletsChecked.Should().Be(2);
        report.DiscrepanciesFound.Should().Be(2);
        report.Items.Single(item => item.WalletId == drifted.Id).BalanceMatchesTransactionSum.Should().BeFalse();
        report.Items.Single(item => item.WalletId == stale.Id).BalanceMatchesLatest.Should().BeFalse();
        report.Items.Single(item => item.WalletId == stale.Id).CompletedTransactionSum.Should().Be(40m);
    }

    [Fact]
    public async Task Reconciliation_ForATransactionlessWallet_ShouldOnlyRequireZeroBalance()
    {
        var empty = SeedWallet("USD", 0m);
        var funded = SeedWallet("EUR", 5m);

        var report = await _reconciliationHandler.Handle(new GetWalletReconciliationReportQuery(), CancellationToken.None);

        var emptyEntry = report.Items.Single(item => item.WalletId == empty.Id);
        emptyEntry.LatestBalanceAfter.Should().BeNull();
        emptyEntry.BalanceMatchesLatest.Should().BeTrue();
        emptyEntry.CompletedTransactionSum.Should().Be(0m);
        report.Items.Single(item => item.WalletId == funded.Id).BalanceMatchesTransactionSum.Should().BeFalse();
    }

    [Fact]
    public async Task Reconciliation_ShouldIgnoreFailedTransactionsInTheCompletedSum()
    {
        var wallet = SeedWallet("USD", 100m);
        var t0 = SystemClock.UtcNow.AddMinutes(-10);
        SeedTransaction(wallet.Id, WalletTransactionType.Credit, 100m, 100m, createdAtUtc: t0);
        SeedTransaction(wallet.Id, WalletTransactionType.Debit, 70m, 30m, TransactionStatus.Failed, t0.AddMinutes(1));

        var report = await _reconciliationHandler.Handle(new GetWalletReconciliationReportQuery(), CancellationToken.None);

        var entry = report.Items.Single();
        entry.CompletedTransactionSum.Should().Be(100m, "failed transactions must not enter the completed sum");
        entry.BalanceMatchesTransactionSum.Should().BeTrue();
        entry.BalanceMatchesLatest.Should().BeFalse("the newest transaction is the failed debit with BalanceAfter 30");
    }

    [Fact]
    public async Task Metrics_ShouldAggregateBalancesCountsAndVolume()
    {
        var usdWallet = SeedWallet("USD", 50m);
        var eurWallet = SeedWallet("EUR", 20m, isActive: false, isLocked: true);
        var t0 = SystemClock.UtcNow.AddMinutes(-10);
        SeedTransaction(usdWallet.Id, WalletTransactionType.Credit, 50m, 50m, createdAtUtc: t0);
        SeedTransaction(usdWallet.Id, WalletTransactionType.Debit, 10m, 40m, createdAtUtc: t0.AddMinutes(1));
        SeedTransaction(eurWallet.Id, WalletTransactionType.TransferIn, 20m, 20m, createdAtUtc: t0.AddMinutes(2));

        var metrics = await _metricsHandler.Handle(new GetWalletMetricsQuery(30), CancellationToken.None);

        metrics.TotalWallets.Should().Be(2);
        metrics.ActiveWallets.Should().Be(1);
        metrics.FrozenWallets.Should().Be(1);
        metrics.BalancesByCurrency.Should().BeEquivalentTo(
        [
            new WalletCurrencyBalance("EUR", 20m, 1),
            new WalletCurrencyBalance("USD", 50m, 1)
        ]);
        metrics.TransactionVolumeByType.Should().BeEquivalentTo(
        [
            new WalletTransactionTypeVolume(nameof(WalletTransactionType.Credit), 1, 50m),
            new WalletTransactionTypeVolume(nameof(WalletTransactionType.Debit), 1, 10m),
            new WalletTransactionTypeVolume(nameof(WalletTransactionType.TransferIn), 1, 20m)
        ]);
        metrics.DailyTransactionVolume.Should().HaveCount(1);
        var day = metrics.DailyTransactionVolume.Single();
        day.CreditTotal.Should().Be(70m);
        day.DebitTotal.Should().Be(10m);
        day.Count.Should().Be(3);
    }

    private UserWallet SeedWallet(string currency, decimal balance, bool isActive = true, bool isLocked = false)
    {
        var wallet = new UserWallet
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Currency = currency,
            Balance = balance,
            IsActive = isActive,
            IsLocked = isLocked,
            CreatedAt = SystemClock.UtcNow.AddMinutes(-5)
        };
        _context.Set<UserWallet>().Add(wallet);
        _context.SaveChanges();
        return wallet;
    }

    private void SeedTransaction(
        Guid walletId,
        WalletTransactionType type,
        decimal amount,
        decimal balanceAfter,
        TransactionStatus status = TransactionStatus.Completed,
        DateTime? createdAtUtc = null)
    {
        _context.Set<WalletTransaction>().Add(new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            Type = type,
            Amount = amount,
            BalanceAfter = balanceAfter,
            Description = "seed",
            Status = status,
            CreatedAt = createdAtUtc ?? SystemClock.UtcNow
        });
        _context.SaveChanges();
    }
}
