using GameGuild.Commerce;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Billing;

/// <summary>
/// EF Core implementation of <see cref="IPricingDecisionLedgerRepository"/> over the
/// shared <see cref="IApplicationDbContext"/>. All filters are built by closing over
/// pre-computed local values so every lambda stays a clean, translatable expression tree.
/// </summary>
public sealed class PricingDecisionLedgerRepository(IApplicationDbContext dbContext)
    : IPricingDecisionLedgerRepository
{
    private const int TopRankingSize = 20;

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PricingDecisionLedgerEntry> Items, int TotalCount)> SearchAsync(
        PricingDecisionLedgerSearchCriteria criteria,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var query = BuildFilteredQuery(criteria);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public Task<PricingDecisionLedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Set<PricingDecisionLedgerEntry>()
            .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<PricingLedgerEffectivenessAggregate> GetEffectivenessAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var criteria = new PricingDecisionLedgerSearchCriteria(TenantId: tenantId, FromUtc: fromUtc, ToUtc: toUtc);
        var query = BuildFilteredQuery(criteria);

        var projection = query.Select(entry => new
        {
            entry.BaseAmount,
            entry.FinalAmount,
            entry.RuleAdjustmentTotal,
            entry.PromoDiscountTotal,
            entry.AppliedPricingRuleId,
            entry.AppliedPromoCodes,
            entry.AppliedStackingRuleName,
            entry.DriftStatus
        });

        var rows = await projection.ToListAsync(cancellationToken).ConfigureAwait(false);

        var totals = new PricingLedgerTotals(
            EntryCount: rows.Count,
            BaselineRevenue: Math.Round(rows.Sum(row => row.BaseAmount), 2, MidpointRounding.AwayFromZero),
            FinalRevenue: Math.Round(rows.Sum(row => row.FinalAmount), 2, MidpointRounding.AwayFromZero),
            DiscountGiven: Math.Round(rows.Sum(row => row.BaseAmount - row.FinalAmount), 2, MidpointRounding.AwayFromZero),
            EntriesWithPromo: rows.Count(row => !string.IsNullOrEmpty(row.AppliedPromoCodes)),
            EntriesWithRule: rows.Count(row => row.AppliedPricingRuleId.HasValue),
            PromoRedemptions: rows.Sum(row => CodeList(row.AppliedPromoCodes).Count));

        var perCode = rows
            .SelectMany(row => CodeList(row.AppliedPromoCodes)
                .Select(code => new { Code = code, row.BaseAmount, row.FinalAmount, Discount = row.BaseAmount - row.FinalAmount }))
            .GroupBy(item => item.Code, StringComparer.Ordinal)
            .Select(group => new PromoCodeEffectivenessRow(
                Code: group.Key,
                Redemptions: group.Count(),
                DiscountGiven: Math.Round(group.Sum(item => item.Discount), 2, MidpointRounding.AwayFromZero),
                BaselineRevenue: Math.Round(group.Sum(item => item.BaseAmount), 2, MidpointRounding.AwayFromZero),
                RevenueInfluenced: Math.Round(group.Sum(item => item.FinalAmount), 2, MidpointRounding.AwayFromZero)))
            .OrderByDescending(row => row.DiscountGiven)
            .ThenBy(row => row.Code, StringComparer.Ordinal)
            .Take(TopRankingSize)
            .ToList();

        var perRule = rows
            .Where(row => row.AppliedPricingRuleId.HasValue)
            .GroupBy(row => row.AppliedPricingRuleId!.Value)
            .Select(group => new PricingRuleImpactRow(
                RuleId: group.Key,
                Decisions: group.Count(),
                AdjustmentTotal: Math.Round(group.Sum(row => row.RuleAdjustmentTotal), 2, MidpointRounding.AwayFromZero),
                BaselineRevenue: Math.Round(group.Sum(row => row.BaseAmount), 2, MidpointRounding.AwayFromZero),
                FinalRevenue: Math.Round(group.Sum(row => row.FinalAmount), 2, MidpointRounding.AwayFromZero)))
            .OrderByDescending(row => row.AdjustmentTotal)
            .ThenBy(row => row.RuleId)
            .Take(TopRankingSize)
            .ToList();

        var stackingOutcomes = rows
            .GroupBy(row => string.IsNullOrWhiteSpace(row.AppliedStackingRuleName)
                ? "(no stacking rule)"
                : row.AppliedStackingRuleName!.Trim())
            .Select(group => new StackingOutcomeRow(group.Key, group.Count()))
            .OrderByDescending(row => row.Decisions)
            .ThenBy(row => row.StackingRuleName, StringComparer.Ordinal)
            .ToList();

        var driftOutcomes = rows
            .GroupBy(row => row.DriftStatus)
            .Select(group => new DriftOutcomeRow(group.Key, group.Count()))
            .OrderByDescending(row => row.Entries)
            .ToList();

        return new PricingLedgerEffectivenessAggregate(
            FromUtc: fromUtc,
            ToUtc: toUtc,
            TenantId: tenantId,
            Totals: totals,
            TopPromoCodes: perCode,
            TopRules: perRule,
            StackingOutcomes: stackingOutcomes,
            DriftOutcomes: driftOutcomes);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PricingDecisionLedgerEntry>> SampleForValidationAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        int sampleSize,
        CancellationToken cancellationToken = default)
    {
        var effectiveSize = Math.Clamp(sampleSize, 1, 200);

        var criteria = new PricingDecisionLedgerSearchCriteria(FromUtc: fromUtc, ToUtc: toUtc);
        var query = BuildFilteredQuery(criteria);

        var notValidatedStatus = PricingDecisionDriftStatus.NotValidated;

        // Prefer never-validated entries (oldest first); top up with previously verified
        // entries when the window has fewer unvalidated rows than the sample budget.
        var unvalidated = await query
            .Where(entry => entry.DriftStatus == notValidatedStatus)
            .OrderBy(entry => entry.CreatedAt)
            .ThenBy(entry => entry.Id)
            .Take(effectiveSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (unvalidated.Count >= effectiveSize)
        {
            return unvalidated;
        }

        var remaining = effectiveSize - unvalidated.Count;
        var validatedIds = unvalidated.Select(entry => entry.Id).ToHashSet();

        var previouslyValidated = await query
            .Where(entry => entry.DriftStatus != notValidatedStatus)
            .OrderByDescending(entry => entry.ValidatedAtUtc)
            .ThenBy(entry => entry.Id)
            .Take(remaining + validatedIds.Count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return unvalidated
            .Concat(previouslyValidated.Where(entry => !validatedIds.Contains(entry.Id)).Take(remaining))
            .ToList();
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<PricingDecisionLedgerEntry> BuildFilteredQuery(PricingDecisionLedgerSearchCriteria criteria)
    {
        var query = dbContext.Set<PricingDecisionLedgerEntry>().AsQueryable();

        if (criteria.TenantId.HasValue)
        {
            var tenantId = criteria.TenantId.Value;
            query = query.Where(entry => entry.TenantId == tenantId);
        }

        if (criteria.OrderId.HasValue)
        {
            var orderId = criteria.OrderId.Value;
            query = query.Where(entry => entry.OrderId == orderId);
        }

        if (criteria.SubscriptionId.HasValue)
        {
            var subscriptionId = criteria.SubscriptionId.Value;
            query = query.Where(entry => entry.SubscriptionId == subscriptionId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.PromoCode))
        {
            // AppliedPromoCodes is stored as ",CODE1,CODE2," so this is an exact-code match.
            var needle = $",{criteria.PromoCode.Trim().ToUpperInvariant()},";
            query = query.Where(entry => entry.AppliedPromoCodes != null && entry.AppliedPromoCodes.Contains(needle));
        }

        if (!string.IsNullOrWhiteSpace(criteria.DecisionType))
        {
            var decisionType = criteria.DecisionType.Trim();
            query = query.Where(entry => entry.DecisionType == decisionType);
        }

        if (!string.IsNullOrWhiteSpace(criteria.DriftStatus))
        {
            var driftStatus = criteria.DriftStatus.Trim();
            query = query.Where(entry => entry.DriftStatus == driftStatus);
        }

        if (criteria.FromUtc.HasValue)
        {
            var fromUtc = criteria.FromUtc.Value;
            query = query.Where(entry => entry.CreatedAt >= fromUtc);
        }

        if (criteria.ToUtc.HasValue)
        {
            var toUtc = criteria.ToUtc.Value;
            query = query.Where(entry => entry.CreatedAt <= toUtc);
        }

        return query;
    }

    private static IReadOnlyList<string> CodeList(string? appliedPromoCodes)
    {
        if (string.IsNullOrWhiteSpace(appliedPromoCodes))
        {
            return [];
        }

        return appliedPromoCodes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
