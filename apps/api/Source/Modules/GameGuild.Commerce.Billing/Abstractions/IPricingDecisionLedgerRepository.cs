using GameGuild.Commerce;

namespace GameGuild.Commerce.Billing;

/// <summary>Search criteria for the pricing-decision ledger read paths (issue #400).</summary>
/// <param name="TenantId">Optional tenant filter.</param>
/// <param name="OrderId">Optional order correlation filter.</param>
/// <param name="SubscriptionId">Optional subscription correlation filter.</param>
/// <param name="PromoCode">Optional exact (normalized, case-insensitive) applied-code filter.</param>
/// <param name="DecisionType">Optional decision type filter.</param>
/// <param name="DriftStatus">Optional drift status filter.</param>
/// <param name="FromUtc">Inclusive lower bound on the decision timestamp.</param>
/// <param name="ToUtc">Inclusive upper bound on the decision timestamp.</param>
public sealed record PricingDecisionLedgerSearchCriteria(
    Guid? TenantId = null,
    Guid? OrderId = null,
    Guid? SubscriptionId = null,
    string? PromoCode = null,
    string? DecisionType = null,
    string? DriftStatus = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);

/// <summary>
/// Read and aggregation surface over <see cref="PricingDecisionLedgerEntry"/> rows.
/// The ledger is append-only; this repository never mutates rows except the
/// drift-validation stamps applied by the verification command.
/// </summary>
public interface IPricingDecisionLedgerRepository
{
    /// <summary>Pages through ledger entries, newest first.</summary>
    Task<(IReadOnlyList<PricingDecisionLedgerEntry> Items, int TotalCount)> SearchAsync(
        PricingDecisionLedgerSearchCriteria criteria,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches one ledger entry by id.</summary>
    Task<PricingDecisionLedgerEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregates discount effectiveness over a window: overall totals, per-promo-code
    /// effectiveness (redemptions, discount given, baseline vs influenced revenue), per-rule
    /// impact and stacking outcome counts.
    /// </summary>
    Task<PricingLedgerEffectivenessAggregate> GetEffectivenessAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Samples up to <paramref name="sampleSize"/> entries in the window for drift
    /// validation, preferring entries that have never been validated (oldest first).
    /// </summary>
    Task<IReadOnlyList<PricingDecisionLedgerEntry>> SampleForValidationAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        int sampleSize,
        CancellationToken cancellationToken = default);

    /// <summary>Persists drift-validation stamps applied by the verification command.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Overall totals of the effectiveness aggregation.</summary>
/// <param name="EntryCount">Decisions in the window.</param>
/// <param name="BaselineRevenue">Sum of base amounts (pre-discount).</param>
/// <param name="FinalRevenue">Sum of final amounts (post-discount).</param>
/// <param name="DiscountGiven">Baseline minus final revenue.</param>
/// <param name="EntriesWithPromo">Decisions that applied at least one promo code.</param>
/// <param name="EntriesWithRule">Decisions that applied a pricing rule.</param>
/// <param name="PromoRedemptions">Total promo applications (an entry may apply several codes).</param>
public sealed record PricingLedgerTotals(
    int EntryCount,
    decimal BaselineRevenue,
    decimal FinalRevenue,
    decimal DiscountGiven,
    int EntriesWithPromo,
    int EntriesWithRule,
    int PromoRedemptions);

/// <summary>Per-promo-code effectiveness row (discount ROI view).</summary>
/// <param name="Code">Normalized promo code.</param>
/// <param name="Redemptions">Ledger entries that applied this code.</param>
/// <param name="DiscountGiven">Discount total attributed to this code.</param>
/// <param name="BaselineRevenue">Baseline (pre-discount) revenue of those entries.</param>
/// <param name="RevenueInfluenced">Final revenue collected on those entries.</param>
public sealed record PromoCodeEffectivenessRow(
    string Code,
    int Redemptions,
    decimal DiscountGiven,
    decimal BaselineRevenue,
    decimal RevenueInfluenced);

/// <summary>Per-pricing-rule impact row.</summary>
/// <param name="RuleId">Applied rule identifier.</param>
/// <param name="Decisions">Ledger entries that applied this rule.</param>
/// <param name="AdjustmentTotal">Rule adjustment total across those entries.</param>
/// <param name="BaselineRevenue">Baseline revenue of those entries.</param>
/// <param name="FinalRevenue">Final revenue of those entries.</param>
public sealed record PricingRuleImpactRow(
    Guid RuleId,
    int Decisions,
    decimal AdjustmentTotal,
    decimal BaselineRevenue,
    decimal FinalRevenue);

/// <summary>Stacking outcome count row.</summary>
/// <param name="StackingRuleName">Rule name, or "(no stacking rule)" for single/default resolution.</param>
/// <param name="Decisions">Ledger entries resolved under this outcome.</param>
public sealed record StackingOutcomeRow(
    string StackingRuleName,
    int Decisions);

/// <summary>Drift outcome count row.</summary>
/// <param name="DriftStatus">Drift status.</param>
/// <param name="Entries">Ledger entries in this status.</param>
public sealed record DriftOutcomeRow(
    string DriftStatus,
    int Entries);

/// <summary>Full effectiveness aggregation result.</summary>
/// <param name="FromUtc">Window start (null = unbounded).</param>
/// <param name="ToUtc">Window end (null = unbounded).</param>
/// <param name="TenantId">Tenant scope, when filtered.</param>
/// <param name="Totals">Overall totals.</param>
/// <param name="TopPromoCodes">Promo codes ranked by discount given (top 20).</param>
/// <param name="TopRules">Pricing rules ranked by adjustment total (top 20).</param>
/// <param name="StackingOutcomes">Decisions per stacking outcome.</param>
/// <param name="DriftOutcomes">Entries per drift status.</param>
public sealed record PricingLedgerEffectivenessAggregate(
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? TenantId,
    PricingLedgerTotals Totals,
    IReadOnlyList<PromoCodeEffectivenessRow> TopPromoCodes,
    IReadOnlyList<PricingRuleImpactRow> TopRules,
    IReadOnlyList<StackingOutcomeRow> StackingOutcomes,
    IReadOnlyList<DriftOutcomeRow> DriftOutcomes);
