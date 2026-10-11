namespace GameGuild.Commerce.Payments;

/// <summary>
///     Repository interface for UserWallet entity data access
/// </summary>
public interface IWalletRepository
{
    /// <summary>
    ///     Gets a wallet by ID
    /// </summary>
    Task<UserWallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a wallet by user ID
    /// </summary>
    Task<UserWallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all wallets for a user (supports multiple currencies)
    /// </summary>
    Task<IEnumerable<UserWallet>> GetAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a wallet by user ID and currency
    /// </summary>
    Task<UserWallet?> GetByUserIdAndCurrencyAsync(Guid userId, string currency, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a new wallet
    /// </summary>
    void Add(UserWallet wallet);

    /// <summary>
    ///     Updates a wallet
    /// </summary>
    void Update(UserWallet wallet);

    /// <summary>
    ///     Gets wallet transactions by wallet ID with filtering
    /// </summary>
    Task<List<WalletTransaction>> GetTransactionsAsync(
        Guid walletId,
        int skip = 0,
        int take = 50,
        WalletTransactionType? typeFilter = null,
        TransactionStatus? statusFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves changes to the database
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists wallets with pagination and optional filtering
    /// </summary>
    Task<(List<UserWallet> Wallets, int TotalCount)> ListWalletsAsync(
        int page,
        int pageSize,
        string? currency = null,
        bool? isFrozen = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the total count of transactions for a wallet
    /// </summary>
    Task<int> GetTransactionCountAsync(Guid walletId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks whether a wallet already has a completed transaction with the given reference
    ///     identifier (issue #406 ReferenceId idempotency for credits, debits and transfers).
    /// </summary>
    Task<bool> HasCompletedTransactionAsync(Guid walletId, string referenceId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the most recent completed transaction with the given reference identifier for a
    ///     wallet, or null when the reference has not been applied (issue #406 idempotent replay).
    /// </summary>
    Task<WalletTransaction?> GetCompletedTransactionByReferenceAsync(Guid walletId, string referenceId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists active wallets below the configured low-balance threshold or previously alerted
    ///     (issue #406), so the monitor can issue notices and re-arm them after recovery.
    /// </summary>
    Task<List<UserWallet>> ListBalanceAlertCandidatesAsync(decimal threshold, CancellationToken cancellationToken = default);
}
