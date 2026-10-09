using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Resources;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Command to create a new subscription plan.
///     Subscription plan configuration changes monetization settings (tiers, pricing,
///     limits, lifecycle) and requires the monetization configure permission (issue #346).
/// </summary>
[RequiresQuota(ResourceUsageType.SubscriptionPlans, Source = "CreateSubscriptionPlan")]
[AuthorizeRequest(MonetizationPermission.Keys.Configure)]
public sealed record CreateSubscriptionPlanCommand(string Name, string Slug, long MonthlyPriceInCents, string Currency = "USD", string? Description = null) : ICommand<Guid>;
