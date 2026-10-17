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
    string? Slug,
    string? Description,
    long MonthlyPriceInCents,
    long? AnnualPriceInCents,
    SubscriptionPlanType? Type = null,
    decimal? AnnualDiscountPercent = null,
    int? MaxUsers = null,
    long? MaxStorageMb = null,
    long? MaxApiCallsPerMonth = null,
    bool? HasPrioritySupport = null,
    bool? HasAdvancedAnalytics = null,
    bool? HasCustomBranding = null,
    string? Features = null,
    int? SortOrder = null,
    int? TrialPeriodDays = null) : ICommand;
