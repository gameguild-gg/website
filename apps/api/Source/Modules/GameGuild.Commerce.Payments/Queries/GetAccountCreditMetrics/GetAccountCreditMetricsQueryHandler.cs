using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>Credit totals per currency for the metrics report.</summary>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="Issued">Sum of Grant ledger amounts.</param>
/// <param name="Applied">Sum of Consume ledger amounts (usage).</param>
/// <param name="ExpiredUnused">Sum of Expire ledger amounts (expiration waste).</param>
/// <param name="Revoked">Sum of Revoke ledger amounts.</param>
/// <param name="ActiveBalance">Sum of remaining balances of Active grants.</param>
/// <param name="ActiveGrantCount">Number of Active grants.</param>
public sealed record CreditCurrencyMetrics(
    string Currency,
    decimal Issued,
    decimal Applied,
    decimal ExpiredUnused,
    decimal Revoked,
    decimal ActiveBalance,
    int ActiveGrantCount);

/// <summary>Aggregate credit metrics across currencies (rates are global in v1).</summary>
/// <param name="PerCurrency">Totals per currency.</param>
/// <param name="UsageRate">Applied / Issued across currencies, when Issued &gt; 0.</param>
/// <param name="ExpirationWasteRate">ExpiredUnused / Issued across currencies, when Issued &gt; 0.</param>
public sealed record CreditMetrics(
    IReadOnlyList<CreditCurrencyMetrics> PerCurrency,
    decimal? UsageRate,
    decimal? ExpirationWasteRate);

/// <summary>Referral funnel counters (clicks → signups → conversions → rewards).</summary>
/// <param name="CodesShared">Tracking rows created.</param>
/// <param name="Clicks">Rows with a tracked click.</param>
/// <param name="Signups">Rows with a signed-up referee.</param>
/// <param name="Conversions">Rows converted by a first paid invoice.</param>
/// <param name="RewardsIssued">Rows with rewards issued.</param>
/// <param name="Fraudulent">Rows blocked by the fraud rules.</param>
public sealed record ReferralFunnelMetrics(
    int CodesShared,
    int Clicks,
    int Signups,
    int Conversions,
    int RewardsIssued,
    int Fraudulent);

/// <summary>One leaderboard row: referrer by rewarded referrals.</summary>
/// <param name="ReferrerUserId">Referrer.</param>
/// <param name="RewardedReferrals">Referrals in status RewardIssued.</param>
/// <param name="FraudulentReferrals">Referrals blocked as fraudulent.</param>
public sealed record ReferrerLeaderboardEntry(Guid ReferrerUserId, int RewardedReferrals, int FraudulentReferrals);

/// <summary>Growth metrics report (issue #410): credit issuance/usage/expiry plus referral funnel and top referrers.</summary>
/// <param name="Credit">Credit ledger aggregates.</param>
/// <param name="ReferralFunnel">Referral conversion funnel.</param>
/// <param name="TopReferrers">Top referrers by rewarded referrals (max 10).</param>
public sealed record AccountCreditMetricsReport(
    CreditMetrics Credit,
    ReferralFunnelMetrics ReferralFunnel,
    IReadOnlyList<ReferrerLeaderboardEntry> TopReferrers);

/// <summary>
///     Computes the growth metrics report (issue #410, #404 reporting pattern): credit
///     issuance/usage/expiry rates per currency, the referral conversion funnel and the
///     top-referrers leaderboard. Tenant-scoped for the calling admin tenant.
/// </summary>
public sealed class GetAccountCreditMetricsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAccountCreditMetricsQuery, AccountCreditMetricsReport>
{
    private const int LeaderboardSize = 10;

    /// <inheritdoc />
    public async Task<AccountCreditMetricsReport> Handle(GetAccountCreditMetricsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId;

        var ledgerTotals = await context.Set<AccountCreditTransaction>()
            .AsNoTracking()
            .Where(transaction => tenantId == null || transaction.TenantId == tenantId)
            .GroupBy(transaction => new { transaction.Currency, transaction.Type })
            .Select(grouping => new { grouping.Key.Currency, grouping.Key.Type, Total = grouping.Sum(transaction => transaction.Amount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activeGrants = await context.Set<AccountCreditGrant>()
            .AsNoTracking()
            .Where(grant => grant.Status == AccountCreditGrantStatus.Active
                            && (tenantId == null || grant.TenantId == tenantId))
            .GroupBy(grant => grant.Currency)
            .Select(grouping => new { Currency = grouping.Key, Balance = grouping.Sum(grant => grant.RemainingAmount), Count = grouping.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var currencies = ledgerTotals.Select(row => row.Currency)
            .Concat(activeGrants.Select(row => row.Currency))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(currency => currency, StringComparer.Ordinal)
            .ToList();

        var perCurrency = currencies.Select(currency =>
        {
            decimal TotalOf(AccountCreditTransactionType type)
                => ledgerTotals.FirstOrDefault(row => row.Currency == currency && row.Type == type)?.Total ?? 0m;
            var active = activeGrants.FirstOrDefault(row => row.Currency == currency);
            return new CreditCurrencyMetrics(
                currency,
                TotalOf(AccountCreditTransactionType.Grant),
                TotalOf(AccountCreditTransactionType.Consume),
                TotalOf(AccountCreditTransactionType.Expire),
                TotalOf(AccountCreditTransactionType.Revoke),
                active?.Balance ?? 0m,
                active?.Count ?? 0);
        }).ToList();

        decimal? Rate(decimal numerator, decimal denominator)
            => denominator > 0 ? decimal.Round(numerator / denominator, 4) : null;

        var issuedTotal = perCurrency.Sum(row => row.Issued);
        var credit = new CreditMetrics(
            perCurrency,
            Rate(perCurrency.Sum(row => row.Applied), issuedTotal),
            Rate(perCurrency.Sum(row => row.ExpiredUnused), issuedTotal));

        var referralStatuses = await context.Set<Referral>()
            .AsNoTracking()
            .Where(referral => tenantId == null || referral.TenantId == tenantId)
            .GroupBy(referral => referral.Status)
            .Select(grouping => new { Status = grouping.Key, Count = grouping.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var clicks = await context.Set<Referral>()
            .AsNoTracking()
            .Where(referral => (tenantId == null || referral.TenantId == tenantId) && referral.ClickedAt != null)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var byStatus = referralStatuses.ToDictionary(row => row.Status, row => row.Count);
        var funnel = new ReferralFunnelMetrics(
            referralStatuses.Sum(row => row.Count),
            clicks,
            byStatus.GetValueOrDefault(ReferralStatus.SignedUp)
            + byStatus.GetValueOrDefault(ReferralStatus.Converted)
            + byStatus.GetValueOrDefault(ReferralStatus.RewardIssued),
            byStatus.GetValueOrDefault(ReferralStatus.Converted)
            + byStatus.GetValueOrDefault(ReferralStatus.RewardIssued),
            byStatus.GetValueOrDefault(ReferralStatus.RewardIssued),
            byStatus.GetValueOrDefault(ReferralStatus.Fraudulent));

        var leaderboard = await context.Set<Referral>()
            .AsNoTracking()
            .Where(referral => tenantId == null || referral.TenantId == tenantId)
            .GroupBy(referral => referral.ReferrerUserId)
            // Conditional sums translate (CASE WHEN) where Count(predicate) over a
            // grouping does not on this provider.
            .Select(grouping => new ReferrerLeaderboardEntry(
                grouping.Key,
                grouping.Sum(referral => referral.Status == ReferralStatus.RewardIssued ? 1 : 0),
                grouping.Sum(referral => referral.Status == ReferralStatus.Fraudulent ? 1 : 0)))
            .OrderByDescending(entry => entry.RewardedReferrals)
            .ThenBy(entry => entry.ReferrerUserId)
            .Take(LeaderboardSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AccountCreditMetricsReport(credit, funnel, leaderboard);
    }
}
