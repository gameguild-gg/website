using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Wallet service implementation using repository abstraction
/// </summary>
public class WalletService(IWalletRepository walletRepository, ILogger<WalletService> logger) : IWalletService
{

    public async Task<UserWallet> CreateWalletAsync(Guid userId, string currency = "USD", CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating wallet for user {UserId} with currency {Currency}", userId, currency);

        // Issue #406: uniqueness is per (user, currency) — a user may hold one wallet in each
        // currency concurrently, but never two in the same currency.
        var existing = await walletRepository.GetByUserIdAndCurrencyAsync(userId, currency, cancellationToken).ConfigureAwait(false);

        if (existing != null)
        {
            logger.LogWarning("Wallet already exists for user {UserId} in currency {Currency}", userId, currency);

            throw new InvalidOperationException($"Wallet already exists for user {userId} in currency {currency}");
        }

        var wallet = new UserWallet { UserId = userId, Currency = currency, Balance = 0, IsActive = true, IsLocked = false };

        walletRepository.Add(wallet);
        await walletRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Wallet created for user {UserId} with ID {WalletId}", userId, wallet.Id);

        return wallet;
    }

    public async Task<UserWallet?> GetWalletByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await walletRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserWallet?> GetWalletByIdAsync(Guid walletId, CancellationToken cancellationToken = default)
    {
        return await walletRepository.GetByIdAsync(walletId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<decimal> GetBalanceAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var wallet = await GetWalletByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        return wallet?.Balance ?? 0;
    }

    public async Task<WalletTransaction> AddFundsAsync(Guid userId, decimal amount, string description, string? referenceId = null, string? currency = null, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Adding {Amount} funds to wallet for user {UserId}", amount, userId);

        var wallet = await ResolveWalletForCreditAsync(userId, currency, cancellationToken).ConfigureAwait(false);

        // Issue #406: ReferenceId idempotency — replays of an already-applied reference (e.g. a
        // duplicated provider webhook credit or an admin retried adjustment) return the original
        // transaction instead of double-crediting the balance.
        if (!string.IsNullOrEmpty(referenceId))
        {
            var existing = await walletRepository.GetCompletedTransactionByReferenceAsync(wallet.Id, referenceId, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                logger.LogInformation("Funds reference {ReferenceId} already applied to wallet {WalletId}; returning the existing transaction", referenceId, wallet.Id);
                return existing;
            }
        }

        // Issue #406: the ledger transaction must be registered explicitly — EntityBase
        // pre-generates its key, so leaving it to change-tracking graph discovery makes EF
        // infer the new row as an update to an existing one and the save fails with a
        // concurrency exception (the exact 500s behind adjust/transfer/reconciliation).
        var transaction = wallet.AddFunds(amount, description, referenceId);
        walletRepository.AddTransaction(transaction);
        await walletRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Funds added: Transaction {TransactionId}, Balance {Balance}", transaction.Id, wallet.Balance);

        return transaction;
    }

    public async Task<WalletTransaction> DeductFundsAsync(Guid userId, decimal amount, string description, string? referenceId = null, string? currency = null, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deducting {Amount} funds from wallet for user {UserId}", amount, userId);

        var wallet = await ResolveExistingWalletAsync(userId, currency, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(referenceId))
        {
            var existing = await walletRepository.GetCompletedTransactionByReferenceAsync(wallet.Id, referenceId, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                logger.LogInformation("Funds reference {ReferenceId} already applied to wallet {WalletId}; returning the existing transaction", referenceId, wallet.Id);
                return existing;
            }
        }

        var transaction = wallet.DeductFunds(amount, description, referenceId);
        walletRepository.AddTransaction(transaction);
        await walletRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Funds deducted: Transaction {TransactionId}, Balance {Balance}", transaction.Id, wallet.Balance);

        return transaction;
    }

    public async Task<(WalletTransaction debitTransaction, WalletTransaction creditTransaction)> TransferFundsAsync(
        Guid fromUserId,
        Guid toUserId,
        decimal amount,
        string description,
        string? referenceId = null,
        string? currency = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Transferring {Amount} from user {FromUserId} to user {ToUserId}", amount, fromUserId, toUserId);

        // Get both wallets (issue #406: currency-aware resolution keeps a multi-currency user's
        // funds in the intended wallet).
        var fromWallet = await ResolveExistingWalletAsync(fromUserId, currency, cancellationToken).ConfigureAwait(false);
        if (fromWallet == null) throw new InvalidOperationException($"Source wallet not found for user {fromUserId}");

        // Issue #406: when the recipient holds no wallet in the transfer currency, fall back to
        // their primary wallet so a cross-currency transfer SURFACES as a mismatch error instead
        // of silently minting a new wallet in the source currency; a wallet is created on demand
        // only when the recipient has no wallet at all.
        var toWallet = await walletRepository.GetByUserIdAndCurrencyAsync(toUserId, fromWallet.Currency, cancellationToken).ConfigureAwait(false)
            ?? await GetWalletByUserIdAsync(toUserId, cancellationToken).ConfigureAwait(false);

        if (toWallet == null)
        {
            logger.LogWarning("Destination wallet not found for user {ToUserId}, creating new wallet", toUserId);
            toWallet = await CreateWalletAsync(toUserId, fromWallet.Currency, cancellationToken).ConfigureAwait(false);
        }

        // Currency check
        if (fromWallet.Currency != toWallet.Currency) throw new InvalidOperationException($"Currency mismatch: {fromWallet.Currency} != {toWallet.Currency}");

        // Issue #406: ReferenceId idempotency on the source wallet.
        if (!string.IsNullOrEmpty(referenceId) && await walletRepository.HasCompletedTransactionAsync(fromWallet.Id, referenceId, cancellationToken).ConfigureAwait(false))
        {
            logger.LogInformation("Transfer reference {ReferenceId} already applied from wallet {WalletId}; returning the existing transactions", referenceId, fromWallet.Id);
            var existingDebit = await walletRepository.GetCompletedTransactionByReferenceAsync(fromWallet.Id, referenceId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Transfer reference {referenceId} already applied but the debit transaction could not be resolved.");
            var existingCredit = await walletRepository.GetCompletedTransactionByReferenceAsync(toWallet.Id, referenceId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Transfer reference {referenceId} already applied but the credit transaction could not be resolved.");
            return (existingDebit, existingCredit);
        }

        // Perform transfer. Both ledger rows are registered explicitly (issue #406): keys are
        // pre-generated by EntityBase, so graph discovery would infer them as row updates.
        var debitDescription = $"Transfer out: {description}";
        var creditDescription = $"Transfer in: {description}";

        var debitTransaction = fromWallet.DeductFunds(amount, debitDescription, referenceId);
        var creditTransaction = toWallet.AddFunds(amount, creditDescription, referenceId);

        // Update transaction types
        debitTransaction.Type = WalletTransactionType.TransferOut;
        creditTransaction.Type = WalletTransactionType.TransferIn;

        walletRepository.AddTransaction(debitTransaction);
        walletRepository.AddTransaction(creditTransaction);
        await walletRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Transfer completed: Debit {DebitId}, Credit {CreditId}", debitTransaction.Id, creditTransaction.Id);

        return (debitTransaction, creditTransaction);
    }

    /// <summary>
    ///     Resolves the wallet a credit applies to (issue #406): the user's wallet in the
    ///     requested currency when specified (created on demand), otherwise the primary
    ///     (earliest-created) wallet, created on demand in USD.
    /// </summary>
    private async Task<UserWallet> ResolveWalletForCreditAsync(Guid userId, string? currency, CancellationToken cancellationToken)
    {
        if (currency is null)
        {
            return await GetWalletByUserIdAsync(userId, cancellationToken).ConfigureAwait(false)
                ?? await CreateWalletAsync(userId, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return await walletRepository.GetByUserIdAndCurrencyAsync(userId, currency, cancellationToken).ConfigureAwait(false)
            ?? await CreateWalletAsync(userId, currency, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Resolves an existing wallet for debits (issue #406): currency-specific when
    ///     specified, otherwise the user's primary wallet. Unlike credits, debits never create a
    ///     wallet implicitly — deducting from a wallet that does not exist is an error.
    /// </summary>
    private async Task<UserWallet> ResolveExistingWalletAsync(Guid userId, string? currency, CancellationToken cancellationToken)
    {
        var wallet = currency is null
            ? await GetWalletByUserIdAsync(userId, cancellationToken).ConfigureAwait(false)
            : await walletRepository.GetByUserIdAndCurrencyAsync(userId, currency, cancellationToken).ConfigureAwait(false);

        if (wallet == null)
        {
            logger.LogError("Wallet not found for user {UserId} (currency {Currency})", userId, currency ?? "primary");

            throw new InvalidOperationException($"Wallet not found for user {userId}" + (currency is null ? string.Empty : $" in currency {currency}"));
        }

        return wallet;
    }

    public async Task LockWalletAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Locking wallet for user {UserId}: {Reason}", userId, reason);

        var wallet = await GetWalletByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (wallet == null) throw new InvalidOperationException($"Wallet not found for user {userId}");

        wallet.Lock(reason);
        await walletRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Wallet locked for user {UserId}", userId);
    }

    public async Task UnlockWalletAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Unlocking wallet for user {UserId}", userId);

        var wallet = await GetWalletByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (wallet == null) throw new InvalidOperationException($"Wallet not found for user {userId}");

        wallet.Unlock();
        await walletRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Wallet unlocked for user {UserId}", userId);
    }

    public async Task<List<WalletTransaction>> GetTransactionHistoryAsync(
        Guid userId,
        int skip = 0,
        int take = 50,
        WalletTransactionType? typeFilter = null,
        TransactionStatus? statusFilter = null,
        CancellationToken cancellationToken = default
    )
    {
        var wallet = await GetWalletByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (wallet == null) return new List<WalletTransaction>();

        return await walletRepository.GetTransactionsAsync(wallet.Id, skip, take, typeFilter, statusFilter, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(List<UserWallet> Wallets, int TotalCount)> ListWalletsAsync(
        int page,
        int pageSize,
        string? currency = null,
        bool? isFrozen = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Listing wallets - Page: {Page}, PageSize: {PageSize}, Currency: {Currency}, IsFrozen: {IsFrozen}",
            page, pageSize, currency, isFrozen);

        return await walletRepository.ListWalletsAsync(page, pageSize, currency, isFrozen, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateWalletSettingsAsync(
        Guid walletId,
        string? currency = null,
        decimal? dailyLimit = null,
        decimal? monthlyLimit = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Updating wallet settings for {WalletId}", walletId);

        var wallet = await GetWalletByIdAsync(walletId, cancellationToken).ConfigureAwait(false);

        if (wallet == null) throw new InvalidOperationException($"Wallet not found: {walletId}");

        if (currency != null) wallet.Currency = currency;
        if (dailyLimit.HasValue) wallet.DailyLimit = dailyLimit.Value;
        if (monthlyLimit.HasValue) wallet.MonthlyLimit = monthlyLimit.Value;

        await walletRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Wallet settings updated for {WalletId}", walletId);
    }

    public async Task CloseWalletAsync(Guid walletId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Closing wallet {WalletId}", walletId);

        var wallet = await GetWalletByIdAsync(walletId, cancellationToken).ConfigureAwait(false);

        if (wallet == null) throw new InvalidOperationException($"Wallet not found: {walletId}");

        if (wallet.Balance != 0)
            throw new InvalidOperationException($"Cannot close wallet with non-zero balance: {wallet.Balance}");

        wallet.IsActive = false;
        await walletRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Wallet closed: {WalletId}", walletId);
    }

    public async Task FreezeWalletAsync(Guid walletId, string reason, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Freezing wallet {WalletId}: {Reason}", walletId, reason);

        var wallet = await GetWalletByIdAsync(walletId, cancellationToken).ConfigureAwait(false);

        if (wallet == null) throw new InvalidOperationException($"Wallet not found: {walletId}");

        wallet.Lock(reason);
        await walletRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Wallet frozen: {WalletId}", walletId);
    }

    public async Task UnfreezeWalletAsync(Guid walletId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Unfreezing wallet {WalletId}", walletId);

        var wallet = await GetWalletByIdAsync(walletId, cancellationToken).ConfigureAwait(false);

        if (wallet == null) throw new InvalidOperationException($"Wallet not found: {walletId}");

        wallet.Unlock();
        await walletRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Wallet unfrozen: {WalletId}", walletId);
    }

    public async Task<(List<WalletTransaction> Transactions, int TotalCount)> GetWalletAuditLogAsync(
        Guid walletId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting audit log for wallet {WalletId}", walletId);

        var wallet = await GetWalletByIdAsync(walletId, cancellationToken).ConfigureAwait(false);

        if (wallet == null) throw new InvalidOperationException($"Wallet not found: {walletId}");

        var skip = (page - 1) * pageSize;
        var transactions = await walletRepository.GetTransactionsAsync(walletId, skip, pageSize, null, null, cancellationToken).ConfigureAwait(false);
        var totalCount = await walletRepository.GetTransactionCountAsync(walletId, cancellationToken).ConfigureAwait(false);

        return (transactions, totalCount);
    }
}
