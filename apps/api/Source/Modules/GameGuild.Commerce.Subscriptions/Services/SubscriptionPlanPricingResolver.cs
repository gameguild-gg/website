using GameGuild.Commerce.Payments;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Adapter implementation of <see cref="IPlanPricingResolver"/> that wraps the Subscriptions module's
///     plan service to provide pricing information to the Payments module.
/// </summary>
/// <remarks>
///     This implementation follows the Adapter pattern, translating between the Subscriptions
///     module's domain model and the Payments module's pricing abstraction. Interval prices
///     resolve through <see cref="ISubscriptionIntervalPricingService"/> so every pricing surface
///     (preview, checkout, renewal) derives the same numbers, including the plan-level annual
///     discount (issue #416).
/// </remarks>
public sealed class SubscriptionPlanPricingResolver : IPlanPricingResolver
{
    private readonly ISubscriptionPlanService _planService;
    private readonly ISubscriptionIntervalPricingService _intervalPricingService;

    public SubscriptionPlanPricingResolver(
        ISubscriptionPlanService planService,
        ISubscriptionIntervalPricingService intervalPricingService)
    {
        _planService = planService ?? throw new ArgumentNullException(nameof(planService));
        _intervalPricingService = intervalPricingService ?? throw new ArgumentNullException(nameof(intervalPricingService));
    }

    /// <inheritdoc />
    public async Task<Money?> GetPlanMonthlyPriceAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var plan = await _planService.GetByIdAsync(planId, cancellationToken).ConfigureAwait(false);

        if (plan is null)
            return null;

        // Issue #416: an enterprise plan still carrying the -1 custom-quote sentinel has no
        // billable monthly price; fail closed with a clear domain error instead of a negative amount.
        if (plan.RequiresCustomQuote())
        {
            throw new SubscriptionPlanCustomQuotePendingException(planId);
        }

        return CentsToMoney(plan.MonthlyPriceInCents, plan.Currency);
    }

    /// <inheritdoc />
    public async Task<Money?> GetPlanPriceAsync(Guid planId, BillingCycle billingCycle, CancellationToken cancellationToken = default)
    {
        var plan = await _planService.GetByIdAsync(planId, cancellationToken).ConfigureAwait(false);

        if (plan is null)
            return null;

        if (plan.RequiresCustomQuote())
        {
            throw new SubscriptionPlanCustomQuotePendingException(planId);
        }

        return await _intervalPricingService.ResolvePriceForCycleAsync(plan, billingCycle, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> PlanExistsAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var plan = await _planService.GetByIdAsync(planId, cancellationToken).ConfigureAwait(false);
        return plan is not null;
    }

    /// <summary>
    ///     Converts cents to Money value object.
    /// </summary>
    private static Money CentsToMoney(long cents, string currency)
        => new(cents / 100m, currency);
}
