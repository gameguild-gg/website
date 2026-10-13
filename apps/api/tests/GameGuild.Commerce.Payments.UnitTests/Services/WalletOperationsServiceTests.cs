using FluentAssertions;
using GameGuild.Commerce.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Services;

/// <summary>
///     Multi-currency and idempotency tests for the billing wallet operations (issue #406):
///     one wallet per user per currency, currency-aware credit/debit/transfer resolution and
///     ReferenceId replay protection.
/// </summary>
public class WalletOperationsServiceTests : IDisposable
{
    private readonly PaymentsPersistenceTestDbContext _context;
    private readonly WalletRepository _repository;
    private readonly WalletService _service;

    public WalletOperationsServiceTests()
    {
        var options = new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
            .UseInMemoryDatabase($"wallet-operations-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new PaymentsPersistenceTestDbContext(options);
        _repository = new WalletRepository(_context, NullLogger<WalletRepository>.Instance);
        _service = new WalletService(_repository, NullLogger<WalletService>.Instance);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task CreateWallet_ShouldAllowDistinctCurrenciesPerUser()
    {
        var userId = Guid.NewGuid();

        var usd = await _service.CreateWalletAsync(userId, "USD");
        var eur = await _service.CreateWalletAsync(userId, "EUR");

        usd.Id.Should().NotBe(eur.Id);
        (await _repository.GetAllByUserIdAsync(userId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateWallet_ForAnExistingUserCurrency_ShouldThrow()
    {
        var userId = Guid.NewGuid();
        await _service.CreateWalletAsync(userId, "USD");

        var act = () => _service.CreateWalletAsync(userId, "USD");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"*{userId}*USD*");
    }

    [Fact]
    public async Task AddFunds_WithCurrency_ShouldTargetTheMatchingWallet()
    {
        var userId = Guid.NewGuid();
        await _service.CreateWalletAsync(userId, "USD");
        var eurWallet = await _service.CreateWalletAsync(userId, "EUR");

        var transaction = await _service.AddFundsAsync(userId, 30m, "EUR top-up", currency: "EUR");

        transaction.WalletId.Should().Be(eurWallet.Id);
        eurWallet.Balance.Should().Be(30m);
        var usdWallet = await _repository.GetByUserIdAndCurrencyAsync(userId, "USD");
        usdWallet!.Balance.Should().Be(0m);
    }

    [Fact]
    public async Task AddFunds_WithoutWallet_ShouldCreateThePrimaryWalletOnDemand()
    {
        var userId = Guid.NewGuid();

        var transaction = await _service.AddFundsAsync(userId, 12m, "initial credit");

        transaction.BalanceAfter.Should().Be(12m);
        var wallet = await _service.GetWalletByUserIdAsync(userId);
        wallet!.Currency.Should().Be("USD");
        wallet!.Balance.Should().Be(12m);
    }

    [Fact]
    public async Task AddFunds_ReplayedReference_ShouldReturnTheOriginalTransactionWithoutDoubleCredit()
    {
        var userId = Guid.NewGuid();

        var first = await _service.AddFundsAsync(userId, 15m, "webhook credit", referenceId: "evt_charge_1");
        var replay = await _service.AddFundsAsync(userId, 15m, "webhook credit", referenceId: "evt_charge_1");

        replay.Id.Should().Be(first.Id);
        (await _service.GetWalletByUserIdAsync(userId))!.Balance.Should().Be(15m);
    }

    [Fact]
    public async Task DeductFunds_WithInsufficientBalance_ShouldThrow()
    {
        var userId = Guid.NewGuid();
        await _service.AddFundsAsync(userId, 5m, "seed");

        var act = () => _service.DeductFundsAsync(userId, 10m, "overdraft attempt");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Insufficient balance*");
    }

    [Fact]
    public async Task DeductFunds_ForAMissingCurrencyWallet_ShouldThrow()
    {
        var userId = Guid.NewGuid();
        await _service.CreateWalletAsync(userId, "USD");

        var act = () => _service.DeductFundsAsync(userId, 5m, "missing wallet", currency: "EUR");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*EUR*");
    }

    [Fact]
    public async Task DeductFunds_ReplayedReference_ShouldReturnTheOriginalTransactionWithoutDoubleDebit()
    {
        var userId = Guid.NewGuid();
        await _service.AddFundsAsync(userId, 20m, "seed");

        var first = await _service.DeductFundsAsync(userId, 8m, "charge", referenceId: "charge_1");
        var replay = await _service.DeductFundsAsync(userId, 8m, "charge", referenceId: "charge_1");

        replay.Id.Should().Be(first.Id);
        (await _service.GetWalletByUserIdAsync(userId))!.Balance.Should().Be(12m);
    }

    [Fact]
    public async Task TransferFunds_ShouldMoveFundsAndTypeTheTransactions()
    {
        var fromUserId = Guid.NewGuid();
        var toUserId = Guid.NewGuid();
        await _service.AddFundsAsync(fromUserId, 100m, "seed");
        await _service.CreateWalletAsync(toUserId, "USD");

        var (debit, credit) = await _service.TransferFundsAsync(fromUserId, toUserId, 40m, "payout", currency: "USD");

        debit.Type.Should().Be(WalletTransactionType.TransferOut);
        credit.Type.Should().Be(WalletTransactionType.TransferIn);
        debit.BalanceAfter.Should().Be(60m);
        credit.BalanceAfter.Should().Be(40m);
    }

    [Fact]
    public async Task TransferFunds_ReplayedReference_ShouldReturnTheOriginalTransactionsWithoutDoubleMove()
    {
        var fromUserId = Guid.NewGuid();
        var toUserId = Guid.NewGuid();
        await _service.AddFundsAsync(fromUserId, 100m, "seed");
        await _service.CreateWalletAsync(toUserId, "USD");

        var (firstDebit, firstCredit) = await _service.TransferFundsAsync(fromUserId, toUserId, 10m, "payout", referenceId: "batch_1");
        var (replayDebit, replayCredit) = await _service.TransferFundsAsync(fromUserId, toUserId, 10m, "payout", referenceId: "batch_1");

        replayDebit.Id.Should().Be(firstDebit.Id);
        replayCredit.Id.Should().Be(firstCredit.Id);
        (await _service.GetWalletByUserIdAsync(fromUserId))!.Balance.Should().Be(90m);
        (await _service.GetWalletByUserIdAsync(toUserId))!.Balance.Should().Be(10m);
    }

    [Fact]
    public async Task TransferFunds_WithCurrencyMismatch_ShouldThrow()
    {
        var fromUserId = Guid.NewGuid();
        var toUserId = Guid.NewGuid();
        await _service.AddFundsAsync(fromUserId, 100m, "seed", currency: "USD");
        await _service.CreateWalletAsync(toUserId, "EUR");

        var act = () => _service.TransferFundsAsync(fromUserId, toUserId, 10m, "cross currency", currency: "USD");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Currency mismatch*");
        (await _service.GetWalletByUserIdAsync(fromUserId))!.Balance.Should().Be(100m, "a failed transfer must not move funds");
    }
}
