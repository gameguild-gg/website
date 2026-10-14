using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Handles subscription lifecycle operations: create, activate, cancel, suspend, upgrade, downgrade.
/// </summary>
public class SubscriptionLifecycleService(
    ISubscriptionRepository repository,
    ISubscriptionPlanService planService,
    ISubscriptionIntervalPricingService intervalPricingService,
    IBillingCalculator billingCalculator,
    ILogger<SubscriptionLifecycleService> logger) : ISubscriptionLifecycleService
{
    private readonly ISubscriptionRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    private readonly ISubscriptionPlanService _planService = planService ?? throw new ArgumentNullException(nameof(planService));
    private readonly ISubscriptionIntervalPricingService _intervalPricingService = intervalPricingService ?? throw new ArgumentNullException(nameof(intervalPricingService));
    private readonly IBillingCalculator _billingCalculator = billingCalculator ?? throw new ArgumentNullException(nameof(billingCalculator));
    private readonly ILogger<SubscriptionLifecycleService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private async Task<Subscription> GetRequiredAsync(Guid subscriptionId, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(subscriptionId, ct).ConfigureAwait(false);
        return subscription ?? throw new SubscriptionNotFoundException(subscriptionId);
    }

    private async Task<SubscriptionPlan> GetRequiredPlanAsync(Guid planId, CancellationToken ct)
    {
        var plan = await _planService.GetByIdAsync(planId, ct).ConfigureAwait(false);
        return plan ?? throw new InvalidOperationException($"Subscription plan {planId} not found");
    }

    /// <summary>
    ///     Resolves the interval-aware price for a cycle (issue #392): an explicit
    ///     <see cref="SubscriptionPlanIntervalPrice" /> wins over the derived fallback.
    /// </summary>
    private Task<Money> GetPriceForCycleAsync(SubscriptionPlan plan, BillingCycle cycle, CancellationToken ct) =>
        _intervalPricingService.ResolvePriceForCycleAsync(plan, cycle, ct);

    private static Money GetPriceForCycle(SubscriptionPlan plan, BillingCycle cycle)
    {
        var monthlyPrice = plan.GetMonthlyPrice();
        return cycle switch
        {
            BillingCycle.Monthly => monthlyPrice,
            BillingCycle.Annually => plan.GetAnnualPrice() ?? monthlyPrice * 12,
            BillingCycle.Quarterly => monthlyPrice * 3,
            BillingCycle.SemiAnnually => monthlyPrice * 6,
            BillingCycle.Biannually => monthlyPrice * 24,
            _ => monthlyPrice
        };
    }

    public async Task<Subscription> CreateAsync(
        Guid tenantId,
        Guid planId,
        Guid createdByUserId,
        BillingCycle billingCycle,
        Money amount,
        DateTime? startDate = null,
        int? trialDays = null,
        CancellationToken cancellationToken = default)
    {
        // Issue #416: fail closed — a custom-quote-pending enterprise plan cannot start a subscription.
        var newPlan = await GetRequiredPlanAsync(planId, cancellationToken).ConfigureAwait(false);
        if (newPlan.RequiresCustomQuote())
        {
            throw new SubscriptionPlanCustomQuotePendingException(planId);
        }

        var effectiveStartDate = startDate ?? SystemClock.UtcNow;
        DateTime? trialEndDate = trialDays.HasValue ? effectiveStartDate.AddDays(trialDays.Value) : null;

        var subscription = new Subscription(
            tenantId,
            planId,
            createdByUserId,
            billingCycle,
            amount,
            effectiveStartDate,
            trialEndDate
        );

        return await _repository.AddAsync(subscription, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Subscription> ActivateAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        subscription.Activate();
        return await _repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Subscription> StartTrialAsync(Guid subscriptionId, int trialDays, CancellationToken cancellationToken = default)
    {
        if (trialDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(trialDays), "Trial days must be positive");

        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        subscription.StartTrial(SystemClock.UtcNow.AddDays(trialDays));
        return await _repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Subscription> EndTrialAsync(Guid subscriptionId, bool convertToPaid, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        subscription.EndTrial(convertToPaid);
        return await _repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Subscription> CancelAsync(Guid subscriptionId, CancellationReason reason, string? note = null, DateTime? effectiveDate = null, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        subscription.Cancel(reason, note, effectiveDate);
        return await _repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Subscription> SuspendAsync(Guid subscriptionId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        subscription.Suspend(reason);
        return await _repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Subscription> ReactivateAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        subscription.Reactivate();
        return await _repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SubscriptionUpgradeResult> UpgradePlanAsync(Guid subscriptionId, Guid newPlanId, DateTime? effectiveDate = null, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        var newPlan = await GetRequiredPlanAsync(newPlanId, cancellationToken).ConfigureAwait(false);
        var oldPlan = await GetRequiredPlanAsync(subscription.PlanId, cancellationToken).ConfigureAwait(false);

        if (newPlan.GetMonthlyPrice().Amount <= oldPlan.GetMonthlyPrice().Amount)
            return SubscriptionUpgradeResult.Failed("New plan is not an upgrade. Use DowngradePlanAsync instead.");

        var newAmount = await GetPriceForCycleAsync(newPlan, subscription.BillingCycle, cancellationToken).ConfigureAwait(false);
        var proration = subscription.ChangePlan(newPlanId, newAmount, effectiveDate);
        await _repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);

        var currency = subscription.Amount.Currency;
        var proratedAmount = new Money(proration.NetAdjustment, currency);
        var creditApplied = new Money(proration.CreditForUnused, currency);

        return SubscriptionUpgradeResult.CreateSuccess(subscription, proratedAmount, creditApplied);
    }

    public async Task<SubscriptionDowngradeResult> DowngradePlanAsync(Guid subscriptionId, Guid newPlanId, DateTime? effectiveDate = null, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        var newPlan = await GetRequiredPlanAsync(newPlanId, cancellationToken).ConfigureAwait(false);

        var newAmount = await GetPriceForCycleAsync(newPlan, subscription.BillingCycle, cancellationToken).ConfigureAwait(false);
        var proration = subscription.ChangePlan(newPlanId, newAmount, effectiveDate);

        var actualEffectiveDate = effectiveDate ?? subscription.CurrentPeriodEnd;
        await _repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);

        var currency = subscription.Amount.Currency;
        var creditIssued = new Money(proration.CreditForUnused, currency);

        return SubscriptionDowngradeResult.CreateSuccess(subscription, actualEffectiveDate, creditIssued);
    }

    public async Task<ChangeBillingCycleResult> ChangeBillingCycleAsync(Guid subscriptionId, BillingCycle newBillingCycle, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        var plan = await GetRequiredPlanAsync(subscription.PlanId, cancellationToken).ConfigureAwait(false);

        if (subscription.Status != SubscriptionStatus.Active)
            return ChangeBillingCycleResult.Failed("Can only change billing cycle for active subscriptions");

        var oldCycle = subscription.BillingCycle;
        var oldAmount = subscription.Amount;

        if (oldCycle == newBillingCycle)
        {
            // Same-cycle change is a no-op: no state change, no proration.
            return ChangeBillingCycleResult.CreateSuccess(subscription, oldCycle, oldAmount, null);
        }

        var newAmount = await GetPriceForCycleAsync(plan, newBillingCycle, cancellationToken).ConfigureAwait(false);

        // Issue #392: prorate the remainder of the current period for the interval switch
        // using the shared billing calculator, before the period dates move to the new cycle.
        var effectiveDate = SystemClock.UtcNow;
        var proration = _billingCalculator.CalculateProration(
            oldAmount,
            newAmount,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            effectiveDate);

        subscription.ChangeBillingCycle(newBillingCycle, newAmount);
        await _repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);

        return ChangeBillingCycleResult.CreateSuccess(subscription, oldCycle, oldAmount, proration);
    }

    public async Task<Subscription> SetAutoRenewAsync(Guid subscriptionId, bool autoRenew, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        subscription.SetAutoRenew(autoRenew);
        return await _repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Subscription> UpdateMetadataAsync(Guid subscriptionId, string metadata, CancellationToken cancellationToken = default)
    {
        var subscription = await GetRequiredAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        subscription.UpdateMetadata(metadata);
        return await _repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
    }
}
