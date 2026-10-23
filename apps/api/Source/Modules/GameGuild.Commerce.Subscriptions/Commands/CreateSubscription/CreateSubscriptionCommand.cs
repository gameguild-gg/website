using GameGuild.CQRS;
using GameGuild.Resources;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Command to create a new subscription
/// </summary>
[RequiresQuota(ResourceUsageType.Subscriptions, 1, Source = "CreateSubscription")]
public sealed record CreateSubscriptionCommand(
    Guid TenantId,
    Guid PlanId,
    Guid CreatedByUserId,
    BillingCycle BillingCycle,
    decimal Amount,
    Guid? FulfilledOrderId = null,
    DateTime? StartDate = null,
    int? TrialDays = null,
    IReadOnlyList<string>? PromoCodes = null,
    string? PromoCodeIdempotencyKey = null,
    Guid? ProductId = null) : ICommand<Guid>;
