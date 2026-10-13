using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Sets (or replaces) the interval-specific price point of a subscription plan
///     (issue #392). Plan pricing configuration requires the monetization configure
///     permission, mirroring sibling plan pricing commands.
/// </summary>
[AuthorizeRequest(MonetizationPermission.Keys.Configure)]
public sealed record SetSubscriptionPlanIntervalPriceCommand(Guid PlanId, BillingCycle Interval, long PriceInCents)
    : ICommand<PlanIntervalPricing>;
