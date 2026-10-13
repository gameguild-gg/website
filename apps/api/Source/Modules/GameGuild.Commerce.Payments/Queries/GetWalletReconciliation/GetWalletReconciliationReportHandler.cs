using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Computes the report-only wallet reconciliation (issue #406): per wallet, the persisted
///     <c>Balance</c> is compared against the latest transaction's <c>BalanceAfter</c> and
///     against the signed sum of completed transactions
///     (Credit + TransferIn − Debit − TransferOut). Never mutates state.
/// </summary>
public sealed class GetWalletReconciliationReportHandler(IApplicationDbContext context)
    : IQueryHandler<GetWalletReconciliationReportQuery, WalletReconciliationReportResponse>
{
    public async Task<WalletReconciliationReportResponse> Handle(GetWalletReconciliationReportQuery request, CancellationToken cancellationToken)
    {
        var wallets = await context.Set<UserWallet>()
            .AsNoTracking()
            .Where(wallet => wallet.DeletedAt == null && (request.WalletId == null || wallet.Id == request.WalletId))
            .Select(wallet => new { wallet.Id, wallet.UserId, wallet.Currency, wallet.Balance })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var walletIds = wallets.Select(wallet => wallet.Id).ToList();

        var signedSums = await context.Set<WalletTransaction>()
            .AsNoTracking()
            .Where(transaction => walletIds.Contains(transaction.WalletId)
                && transaction.DeletedAt == null
                && transaction.Status == TransactionStatus.Completed
                && (transaction.Type == WalletTransactionType.Credit
                    || transaction.Type == WalletTransactionType.TransferIn
                    || transaction.Type == WalletTransactionType.Debit
                    || transaction.Type == WalletTransactionType.TransferOut))
            .GroupBy(transaction => transaction.WalletId)
            .Select(group => new
            {
                WalletId = group.Key,
                Sum = group.Sum(transaction =>
                    transaction.Type == WalletTransactionType.Credit || transaction.Type == WalletTransactionType.TransferIn
                        ? transaction.Amount
                        : -transaction.Amount)
            })
            .ToDictionaryAsync(row => row.WalletId, row => row.Sum, cancellationToken)
            .ConfigureAwait(false);

        var latestBalanceAfters = await context.Set<WalletTransaction>()
            .AsNoTracking()
            .Where(transaction => walletIds.Contains(transaction.WalletId) && transaction.DeletedAt == null)
            .GroupBy(transaction => transaction.WalletId)
            .Select(group => new
            {
                WalletId = group.Key,
                BalanceAfter = group.OrderByDescending(transaction => transaction.CreatedAt).Select(transaction => (decimal?)transaction.BalanceAfter).FirstOrDefault()
            })
            .ToDictionaryAsync(row => row.WalletId, row => row.BalanceAfter, cancellationToken)
            .ConfigureAwait(false);

        var items = new List<WalletReconciliationEntry>(wallets.Count);
        foreach (var wallet in wallets)
        {
            var signedSum = signedSums.GetValueOrDefault(wallet.Id, 0m);
            var latestBalanceAfter = latestBalanceAfters.GetValueOrDefault(wallet.Id);
            var balanceMatchesLatest = latestBalanceAfter is null ? wallet.Balance == 0m : wallet.Balance == latestBalanceAfter.Value;
            var balanceMatchesSum = wallet.Balance == signedSum;

            items.Add(new WalletReconciliationEntry(
                wallet.Id,
                wallet.UserId,
                wallet.Currency,
                wallet.Balance,
                latestBalanceAfter,
                signedSum,
                balanceMatchesLatest,
                balanceMatchesSum));
        }

        items.Sort((left, right) => string.CompareOrdinal(left.Currency, right.Currency) != 0
            ? string.CompareOrdinal(left.Currency, right.Currency)
            : left.WalletId.CompareTo(right.WalletId));

        return new WalletReconciliationReportResponse(
            SystemClock.UtcNow,
            items.Count,
            items.Count(item => !item.BalanceMatchesLatest || !item.BalanceMatchesTransactionSum),
            items);
    }
}
