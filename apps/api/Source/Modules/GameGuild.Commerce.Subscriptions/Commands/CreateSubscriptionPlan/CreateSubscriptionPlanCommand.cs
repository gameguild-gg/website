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
public sealed record CreateSubscriptionPlanCommand(
    string Name,
    string? Slug = null,
    long MonthlyPriceInCents = 0,
    string Currency = "USD",
    string? Description = null,
    SubscriptionPlanType Type = SubscriptionPlanType.Paid,
    decimal AnnualDiscountPercent = 0m,
    string? Features = null,
    int? MaxUsers = null,
    long? MaxStorageMb = null,
    long? MaxApiCallsPerMonth = null,
    bool? HasPrioritySupport = null,
    bool? HasAdvancedAnalytics = null,
    bool? HasCustomBranding = null) : ICommand<Guid>;
