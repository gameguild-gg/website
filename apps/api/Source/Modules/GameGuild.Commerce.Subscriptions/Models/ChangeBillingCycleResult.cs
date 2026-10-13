namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Result of a billing-cycle change on a subscription (issue #392): the new cycle,
///     its interval-aware price and the proration adjustment for the remainder of the
///     current period, computed with the shared billing calculator.
/// </summary>
public class ChangeBillingCycleResult
{
    public bool Success { get; init; }

    public Subscription? UpdatedSubscription { get; init; }

    public BillingCycle? PreviousBillingCycle { get; init; }

    public BillingCycle? NewBillingCycle { get; init; }

    public Money? PreviousAmount { get; init; }

    public Money? NewAmount { get; init; }

    /// <summary>
    ///     Proration for the remainder of the current period, when one applied.
    ///     Null when the cycle change did not move the amount (same price) or the
    ///     subscription was outside a billable period.
    /// </summary>
    public PlanChangeProration? Proration { get; init; }

    public string? FailureReason { get; init; }

    /// <summary>
    ///     Creates a successful cycle-change result.
    /// </summary>
    public static ChangeBillingCycleResult CreateSuccess(
        Subscription updatedSubscription,
        BillingCycle previousBillingCycle,
        Money previousAmount,
        PlanChangeProration? proration)
    {
        return new ChangeBillingCycleResult
        {
            Success = true,
            UpdatedSubscription = updatedSubscription,
            PreviousBillingCycle = previousBillingCycle,
            NewBillingCycle = updatedSubscription.BillingCycle,
            PreviousAmount = previousAmount,
            NewAmount = updatedSubscription.Amount,
            Proration = proration
        };
    }

    /// <summary>
    ///     Creates a failed cycle-change result.
    /// </summary>
    public static ChangeBillingCycleResult Failed(string failureReason)
    {
        return new ChangeBillingCycleResult { Success = false, FailureReason = failureReason };
    }
}
