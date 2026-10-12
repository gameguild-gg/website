using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Computes wallet balance analytics and completed transaction volume (issue #406) from the
///     wallets and wallet transactions sets. Aggregation happens in memory over minimal
///     projections, mirroring the payment recovery metrics handler.
/// </summary>
public sealed class GetWalletMetricsHandler(IApplicationDbContext context)
    : IQueryHandler<GetWalletMetricsQuery, WalletMetricsResponse>
{
    private const int MaxWindowDays = 365;

    public async Task<WalletMetricsResponse> Handle(GetWalletMetricsQuery request, CancellationToken cancellationToken)
    {
        var generatedAt = SystemClock.UtcNow;
        var windowDays = Math.Clamp(request.Days, 1, MaxWindowDays);
        var volumeFrom = generatedAt.AddDays(-windowDays);

        var wallets = await context.Set<UserWallet>()
            .AsNoTracking()
            .Where(wallet => wallet.DeletedAt == null)
            .Select(wallet => new { wallet.Currency, wallet.Balance, wallet.IsActive, wallet.IsLocked })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var transactions = await context.Set<WalletTransaction>()
            .AsNoTracking()
            .Where(transaction => transaction.DeletedAt == null
                && transaction.Status == TransactionStatus.Completed
                && transaction.CreatedAt >= volumeFrom
                && transaction.CreatedAt <= generatedAt)
            .Select(transaction => new { transaction.Type, transaction.Amount, transaction.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var balancesByCurrency = wallets
            .GroupBy(wallet => wallet.Currency, StringComparer.Ordinal)
            .Select(group => new WalletCurrencyBalance(group.Key, group.Sum(wallet => wallet.Balance), group.Count()))
            .OrderBy(entry => entry.Currency, StringComparer.Ordinal)
            .ToList();

        var volumeByType = transactions
            .GroupBy(transaction => transaction.Type)
            .Select(group => new WalletTransactionTypeVolume(group.Key.ToString(), group.Count(), group.Sum(transaction => transaction.Amount)))
            .OrderBy(entry => entry.Type, StringComparer.Ordinal)
            .ToList();

        var dailyVolume = transactions
            .GroupBy(transaction => DateOnly.FromDateTime(transaction.CreatedAt))
            .Select(group => new WalletDailyVolume(
                group.Key,
                group.Sum(transaction => transaction.Type is WalletTransactionType.Credit or WalletTransactionType.TransferIn ? transaction.Amount : 0m),
                group.Sum(transaction => transaction.Type is WalletTransactionType.Debit or WalletTransactionType.TransferOut ? transaction.Amount : 0m),
                group.Count()))
            .OrderBy(entry => entry.Date)
            .ToList();

        return new WalletMetricsResponse(
            generatedAt,
            wallets.Count,
            wallets.Count(wallet => wallet.IsActive),
            wallets.Count(wallet => wallet.IsLocked),
            balancesByCurrency,
            volumeByType,
            dailyVolume,
            volumeFrom,
            generatedAt);
    }
}
