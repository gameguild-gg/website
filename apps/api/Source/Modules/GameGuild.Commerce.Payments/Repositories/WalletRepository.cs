using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Repository implementation for UserWallet entity
/// </summary>
public class WalletRepository(
    IApplicationDbContext context,
    ILogger<WalletRepository> logger) 
    : CommerceRepositoryBase<UserWallet>(context), IWalletRepository
{
    /// <summary>
    ///     Gets the WalletTransactions DbSet
    /// </summary>
    protected DbSet<WalletTransaction> WalletTransactions => Context.Set<WalletTransaction>();

    public new async Task<UserWallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting wallet by ID: {WalletId}", id);
        return await Query
            .Include(w => w.Transactions)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<UserWallet?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting wallet by user ID: {UserId}", userId);
        // Issue #406: a user may hold one wallet per currency; the "primary" wallet used by
        // currency-agnostic call sites is the earliest-created one, deterministically.
        return await Query
            .Include(w => w.Transactions)
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<UserWallet>> GetAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting all wallets for user: {UserId}", userId);
        return await Query
            .Where(w => w.UserId == userId)
            .Include(w => w.Transactions)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<UserWallet?> GetByUserIdAndCurrencyAsync(Guid userId, string currency, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting wallet by user ID {UserId} and currency {Currency}", userId, currency);
        return await Query
            .Include(w => w.Transactions)
            .FirstOrDefaultAsync(w => w.UserId == userId && w.Currency == currency, cancellationToken)
            .ConfigureAwait(false);
    }

    public void Add(UserWallet wallet)
    {
        logger.LogDebug("Adding wallet for user: {UserId}", wallet.UserId);
        Entities.Add(wallet);
    }

    public void Update(UserWallet wallet)
    {
        logger.LogDebug("Updating wallet: {WalletId}", wallet.Id);
        Entities.Update(wallet);
    }

    public async Task<List<WalletTransaction>> GetTransactionsAsync(
        Guid walletId,
        int skip = 0,
        int take = 50,
        WalletTransactionType? typeFilter = null,
        TransactionStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting transactions for wallet: {WalletId}, skip: {Skip}, take: {Take}", walletId, skip, take);

        var query = WalletTransactions
            .Where(t => t.WalletId == walletId && t.DeletedAt == null)
            .OrderByDescending(t => t.CreatedAt)
            .AsQueryable();

        if (typeFilter.HasValue)
            query = query.Where(t => t.Type == typeFilter.Value);

        if (statusFilter.HasValue)
            query = query.Where(t => t.Status == statusFilter.Value);

        return await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(List<UserWallet> Wallets, int TotalCount)> ListWalletsAsync(
        int page,
        int pageSize,
        string? currency = null,
        bool? isFrozen = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Listing wallets - Page: {Page}, PageSize: {PageSize}", page, pageSize);

        var query = Query.Where(w => w.IsActive);

        if (!string.IsNullOrEmpty(currency))
            query = query.Where(w => w.Currency == currency);

        if (isFrozen.HasValue)
            query = query.Where(w => w.IsLocked == isFrozen.Value);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var wallets = await query
            .OrderByDescending(w => w.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (wallets, totalCount);
    }

    public async Task<int> GetTransactionCountAsync(Guid walletId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting transaction count for wallet: {WalletId}", walletId);

        return await WalletTransactions
            .Where(t => t.WalletId == walletId && t.DeletedAt == null)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasCompletedTransactionAsync(Guid walletId, string referenceId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Checking reference idempotency for wallet: {WalletId}, reference: {ReferenceId}", walletId, referenceId);

        return await WalletTransactions
            .Where(t => t.WalletId == walletId && t.DeletedAt == null && t.ReferenceId == referenceId && t.Status == TransactionStatus.Completed)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WalletTransaction?> GetCompletedTransactionByReferenceAsync(Guid walletId, string referenceId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Resolving completed transaction for wallet: {WalletId}, reference: {ReferenceId}", walletId, referenceId);

        return await WalletTransactions
            .Where(t => t.WalletId == walletId && t.DeletedAt == null && t.ReferenceId == referenceId && t.Status == TransactionStatus.Completed)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<UserWallet>> ListBalanceAlertCandidatesAsync(decimal threshold, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Listing low-balance monitoring candidates (threshold {Threshold})", threshold);

        // Issue #406: candidates are wallets currently below the configured threshold plus
        // wallets that were previously alerted (so a recovery back above the threshold can
        // clear LowBalanceAlertedAt and re-arm the notice).
        return await Query
            .Where(w => w.IsActive && (w.Balance < threshold || w.LowBalanceAlertedAt != null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
