using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Command to clone a subscription plan. Subscription plan configuration changes
///     monetization settings (tiers, pricing, limits, lifecycle) and requires the
///     monetization configure permission (issue #346).
/// </summary>
[AuthorizeRequest(MonetizationPermission.Keys.Configure)]
public sealed record CloneSubscriptionPlanCommand(Guid SourcePlanId, string NewName, string NewSlug) : ICommand<Guid>;
