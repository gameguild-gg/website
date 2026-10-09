using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Subscription plan configuration changes monetization settings (tiers, pricing,
///     limits, lifecycle) and requires the monetization configure permission (issue #346).
/// </summary>
[AuthorizeRequest(MonetizationPermission.Keys.Configure)]
public sealed record SetSubscriptionPlanFeaturedCommand(Guid Id, bool IsFeatured) : ICommand;
