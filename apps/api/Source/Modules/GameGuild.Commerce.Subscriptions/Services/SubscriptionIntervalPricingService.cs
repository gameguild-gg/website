namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     One billable interval of a plan with its effective pricing (issue #392).
/// </summary>
/// <param name="Interval">The billing interval (monthly, quarterly, annually, ...).</param>
/// <param name="IntervalMonths">Months covered by one interval period.</param>
/// <param name="PriceInCents">Effective price for the full interval in the plan currency's smallest unit.</param>
/// <param name="Currency">Plan currency (ISO 4217).</param>
/// <param name="IsConfiguredPricePoint">
///     True when an explicit <see cref="SubscriptionPlanIntervalPrice" /> row backs this
///     interval; false when the price is derived (monthly x months, or the plan's
///     annual price for the annual interval).
/// </param>
/// <param name="MonthlyEquivalentInCents">Price of the interval divided by its month count.</param>
/// <param name="DiscountPercentVersusMonthly">
///     Savings versus paying monthly for the same period, in percent (0 when none, positive for a discount).
/// </param>
public sealed record PlanIntervalPricing(
    BillingCycle Interval,
    int IntervalMonths,
    long PriceInCents,
    string Currency,
    bool IsConfiguredPricePoint,
    long MonthlyEquivalentInCents,
    decimal? DiscountPercentVersusMonthly);

/// <summary>
///     Resolves interval-aware pricing for subscription plans (issue #392). Explicit
///     interval price points win over derived prices; the derived fallback keeps the
///     historical <c>monthly x months</c> (and plan annual price) behavior so
///     existing plans keep their prices until an override is configured.
/// </summary>
public interface ISubscriptionIntervalPricingService
{
    /// <summary>
    ///     Resolves the effective price of one plan on one billing cycle.
    /// </summary>
    Task<Money> ResolvePriceForCycleAsync(SubscriptionPlan plan, BillingCycle cycle, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists every supported billing interval for a plan with its effective pricing,
    ///     explicit-price-point flag and discount versus monthly billing.
    /// </summary>
    Task<IReadOnlyList<PlanIntervalPricing>> GetIntervalPricingAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class SubscriptionIntervalPricingService(ISubscriptionPlanRepository planRepository)
    : ISubscriptionIntervalPricingService
{
    private static readonly BillingCycle[] SupportedIntervals =
    [
        BillingCycle.Monthly,
        BillingCycle.Quarterly,
        BillingCycle.SemiAnnually,
        BillingCycle.Annually
    ];

    /// <inheritdoc />
    public async Task<Money> ResolvePriceForCycleAsync(SubscriptionPlan plan, BillingCycle cycle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var configured = await planRepository
            .GetIntervalPriceAsync(plan.Id, cycle, cancellationToken)
            .ConfigureAwait(false);
        if (configured is not null)
        {
            return configured.GetPrice(plan.Currency);
        }

        return DeriveFallbackPrice(plan, cycle);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlanIntervalPricing>> GetIntervalPricingAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var configuredPrices = await planRepository
            .GetIntervalPricesAsync(plan.Id, cancellationToken)
            .ConfigureAwait(false);
        var byInterval = configuredPrices.ToDictionary(price => price.Interval, price => price);

        var result = new List<PlanIntervalPricing>(SupportedIntervals.Length);
        foreach (var interval in SupportedIntervals)
        {
            byInterval.TryGetValue(interval, out var configured);
            var priceInCents = configured?.PriceInCents ?? DeriveFallbackPriceInCents(plan, interval);
            var months = interval.GetMonths();

            var monthlyEquivalent = months > 0 ? priceInCents / months : priceInCents;
            decimal? discount = null;
            if (interval != BillingCycle.Monthly && plan.MonthlyPriceInCents > 0)
            {
                var monthlyTotal = plan.MonthlyPriceInCents * months;
                discount = Math.Round((monthlyTotal - priceInCents) * 100m / monthlyTotal, 2, MidpointRounding.AwayFromZero);
            }

            result.Add(new PlanIntervalPricing(
                interval,
                months,
                priceInCents,
                plan.Currency,
                configured is not null,
                monthlyEquivalent,
                discount));
        }

        return result;
    }

    internal static long DeriveFallbackPriceInCents(SubscriptionPlan plan, BillingCycle cycle) => cycle switch
    {
        BillingCycle.Monthly => plan.MonthlyPriceInCents,
        // Issue #416: the derived annual price applies the plan-level annual discount
        // (default 0 keeps the historical monthly x 12 behavior). The enterprise -1
        // custom-quote sentinel propagates unchanged until a concrete price is set.
        BillingCycle.Annually => SubscriptionPlanPricingRules.DeriveAnnualPriceInCents(
            plan.MonthlyPriceInCents, plan.AnnualPriceInCents, plan.AnnualDiscountPercent),
        BillingCycle.Quarterly => plan.MonthlyPriceInCents * 3,
        BillingCycle.SemiAnnually => plan.MonthlyPriceInCents * 6,
        BillingCycle.Biannually => plan.MonthlyPriceInCents * 24,
        BillingCycle.Weekly => plan.MonthlyPriceInCents / 4,
        _ => plan.MonthlyPriceInCents
    };

    private static Money DeriveFallbackPrice(SubscriptionPlan plan, BillingCycle cycle)
    {
        var cents = DeriveFallbackPriceInCents(plan, cycle);

        // The enterprise custom-quote sentinel is never a billable amount; clamp it to
        // zero so the Money value object's non-negative invariant holds (the resolver
        // layer surfaces the sentinel as a "no billable price" signal upstream).
        if (cents < 0)
        {
            cents = 0;
        }

        return new Money(cents / 100m, plan.Currency);
    }
}
