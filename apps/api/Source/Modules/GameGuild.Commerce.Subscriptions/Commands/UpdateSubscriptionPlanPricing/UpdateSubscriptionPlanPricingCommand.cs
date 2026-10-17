using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Subscription plan configuration changes monetization settings (tiers, pricing,
///     limits, lifecycle) and requires the monetization configure permission (issue #346).
///     AnnualDiscountPercent (issue #416) is optional: when omitted the stored plan-level
///     discount is preserved; 0 keeps the derived annual price at <c>monthly x 12</c>.
/// </summary>
[AuthorizeRequest(MonetizationPermission.Keys.Configure)]
public sealed record UpdateSubscriptionPlanPricingCommand(Guid Id, long MonthlyPriceInCents, long? AnnualPriceInCents = null, decimal? AnnualDiscountPercent = null) : ICommand;
