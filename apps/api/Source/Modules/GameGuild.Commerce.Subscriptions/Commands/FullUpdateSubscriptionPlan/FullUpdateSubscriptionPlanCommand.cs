using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Command to perform a full update on a subscription plan. Subscription plan
///     configuration changes monetization settings (tiers, pricing, limits, lifecycle)
///     and requires the monetization configure permission (issue #346).
/// </summary>
[AuthorizeRequest(MonetizationPermission.Keys.Configure)]
public sealed record FullUpdateSubscriptionPlanCommand(
    Guid PlanId,
    string Name,
    string Slug,
    string? Description,
    long MonthlyPriceInCents,
    long? AnnualPriceInCents,
    int? MaxUsers,
    long? MaxStorageMb,
    long? MaxApiCallsPerMonth,
    bool? HasPrioritySupport,
    bool? HasAdvancedAnalytics,
    bool? HasCustomBranding,
    string? Features,
    int? SortOrder) : ICommand;
