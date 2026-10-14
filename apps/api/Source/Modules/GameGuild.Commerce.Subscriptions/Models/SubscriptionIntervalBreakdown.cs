namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Aggregated interval breakdown of active subscriptions (issue #392): how many
/// active subscriptions bill on each interval and the recurring billing amount they
/// represent — the revenue-impact view of billing-interval preferences.
/// </summary>
/// <param name="Interval">Billing interval (monthly, quarterly, annually, ...).</param>
/// <param name="IntervalMonths">Months covered by one interval period.</param>
/// <param name="IntervalName">Display name of the interval.</param>
/// <param name="ActiveSubscriptions">Count of active subscriptions on this interval.</param>
/// <param name="ActiveBillingAmount">
/// Sum of the recurring amounts of those active subscriptions. Amounts are per-cycle
/// (an annual subscription contributes its annual amount), so the per-interval numbers
/// are comparable as committed recurring revenue per cycle, not per month.
/// </param>
/// <param name="MonthlyRecurringRevenueEquivalent">
/// Interval amount normalized to a monthly equivalent (amount / months), summed —
/// comparable across intervals.
/// </param>
public sealed record SubscriptionIntervalBreakdown(
    BillingCycle Interval,
    int IntervalMonths,
    string IntervalName,
    int ActiveSubscriptions,
    decimal ActiveBillingAmount,
    decimal MonthlyRecurringRevenueEquivalent);
