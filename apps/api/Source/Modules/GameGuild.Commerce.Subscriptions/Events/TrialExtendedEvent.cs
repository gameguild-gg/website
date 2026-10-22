using GameGuild.CQRS;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Event raised when a subscription's trial period is extended (issue #418:
///     consumption of promo codes granting <c>FreeTrialExtensionDays</c>).
/// </summary>
public sealed class TrialExtendedEvent(Guid subscriptionId, Guid tenantId, int additionalDays, DateTime newTrialEndDate) : DomainEvent
{
    public Guid SubscriptionId { get; } = subscriptionId;

    public Guid TenantId { get; } = tenantId;

    public int AdditionalDays { get; } = additionalDays;

    public DateTime NewTrialEndDate { get; } = newTrialEndDate;
}

/// <summary>
///     Event raised when a subscription skips billing periods (issue #418: consumption of
///     promo codes granting <c>FreeMonths</c>).
/// </summary>
public sealed class BillingPeriodsSkippedEvent(Guid subscriptionId, Guid tenantId, int skippedPeriods, DateTime newNextBillingDate) : DomainEvent
{
    public Guid SubscriptionId { get; } = subscriptionId;

    public Guid TenantId { get; } = tenantId;

    public int SkippedPeriods { get; } = skippedPeriods;

    public DateTime NewNextBillingDate { get; } = newNextBillingDate;
}
