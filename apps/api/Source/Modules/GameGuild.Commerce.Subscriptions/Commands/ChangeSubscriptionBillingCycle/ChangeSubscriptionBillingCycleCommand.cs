using GameGuild.CQRS;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Command to change the billing cycle of a subscription (issue #392). The result
///     carries the interval-aware price and the proration adjustment for the remainder
///     of the current period.
/// </summary>
public sealed record ChangeSubscriptionBillingCycleCommand(Guid SubscriptionId, BillingCycle NewBillingCycle)
    : ICommand<ChangeBillingCycleResult>;
