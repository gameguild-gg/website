
namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Service interface for subscription lifecycle operations (create, activate, cancel, suspend, etc.)
/// </summary>
public interface ISubscriptionLifecycleService
{
    /// <summary>
    ///     Creates a new subscription
    /// </summary>
    Task<Subscription> CreateAsync(
        Guid tenantId,
        Guid planId,
        Guid createdByUserId,
        BillingCycle billingCycle,
        Money amount,
        DateTime? startDate = null,
        int? trialDays = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Activates a subscription
    /// </summary>
    Task<Subscription> ActivateAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Starts a trial period
    /// </summary>
    Task<Subscription> StartTrialAsync(Guid subscriptionId, int trialDays, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Ends a trial period
    /// </summary>
    Task<Subscription> EndTrialAsync(Guid subscriptionId, bool convertToPaid, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Cancels a subscription
    /// </summary>
    Task<Subscription> CancelAsync(Guid subscriptionId, CancellationReason reason, string? note = null, DateTime? effectiveDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Suspends a subscription
    /// </summary>
    Task<Subscription> SuspendAsync(Guid subscriptionId, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reactivates a suspended subscription
    /// </summary>
    Task<Subscription> ReactivateAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Changes subscription plan (upgrade)
    /// </summary>
    Task<SubscriptionUpgradeResult> UpgradePlanAsync(Guid subscriptionId, Guid newPlanId, DateTime? effectiveDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Changes subscription plan (downgrade)
    /// </summary>
    Task<SubscriptionDowngradeResult> DowngradePlanAsync(Guid subscriptionId, Guid newPlanId, DateTime? effectiveDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Changes billing cycle with interval-aware pricing and proration of the
    ///     current period (issue #392).
    /// </summary>
    Task<ChangeBillingCycleResult> ChangeBillingCycleAsync(Guid subscriptionId, BillingCycle newBillingCycle, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sets auto-renewal preference
    /// </summary>
    Task<Subscription> SetAutoRenewAsync(Guid subscriptionId, bool autoRenew, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates subscription metadata
    /// </summary>
    Task<Subscription> UpdateMetadataAsync(Guid subscriptionId, string metadata, CancellationToken cancellationToken = default);
}

/// <summary>
///     Service interface for subscription billing operations (payments, renewals)
/// </summary>
public interface ISubscriptionBillingService
{
    /// <summary>
    ///     Processes subscription renewal
    /// </summary>
    Task<SubscriptionRenewalResult> ProcessRenewalAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Records a successful payment
    /// </summary>
    Task<Subscription> RecordPaymentAsync(Guid subscriptionId, decimal amount, string currency, DateTime paymentDate, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Records a payment failure
    /// </summary>
    Task<Subscription> RecordPaymentFailureAsync(Guid subscriptionId, string reason, DateTime failureDate, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Processes bulk renewals
    /// </summary>
    Task<BulkRenewalResult> ProcessBulkRenewalsAsync(IEnumerable<Guid> subscriptionIds, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sends renewal reminders
    /// </summary>
    Task SendRenewalRemindersAsync(int daysBeforeRenewal, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Sends trial expiration reminders
    /// </summary>
    Task SendTrialExpirationRemindersAsync(int daysBeforeExpiration, CancellationToken cancellationToken = default);
}

/// <summary>
///     Service interface for subscription query operations (read-only)
/// </summary>
public interface ISubscriptionQueryService
{
    /// <summary>
    ///     Gets subscription by ID
    /// </summary>
    Task<Subscription?> GetByIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets subscription by external ID (for payment provider webhooks)
    /// </summary>
    Task<Subscription?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a tenant has an active subscription
    /// </summary>
    Task<bool> IsSubscriptionActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets active subscription for tenant
    /// </summary>
    Task<Subscription?> GetActiveTenantSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all subscriptions for tenant
    /// </summary>
    Task<IEnumerable<Subscription>> GetTenantSubscriptionsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets subscription history for tenant
    /// </summary>
    Task<IEnumerable<Subscription>> GetTenantSubscriptionHistoryAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets subscriptions expiring soon
    /// </summary>
    Task<IEnumerable<Subscription>> GetExpiringSoonAsync(int days, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets subscriptions due for renewal
    /// </summary>
    Task<IEnumerable<Subscription>> GetDueForRenewalAsync(int days, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets trial subscriptions expiring soon
    /// </summary>
    Task<IEnumerable<Subscription>> GetTrialsExpiringSoonAsync(int days, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Validates subscription limits for tenant
    /// </summary>
    Task<SubscriptionLimitValidationResult> ValidateSubscriptionLimitsAsync(Guid tenantId, int userCount, long storageMb, long apiCallsPerMonth, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets subscription usage statistics
    /// </summary>
    Task<SubscriptionUsageStatistics> GetUsageStatisticsAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets revenue analytics
    /// </summary>
    Task<RevenueAnalytics> GetRevenueAnalyticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets subscription analytics
    /// </summary>
    Task<SubscriptionAnalytics> GetSubscriptionAnalyticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}

/// <summary>
///     Service interface for subscription external ID management (payment provider integration)
/// </summary>
public interface ISubscriptionExternalIdService
{
    /// <summary>
    ///     Sets external IDs for payment provider integration
    /// </summary>
    Task<Subscription> SetExternalIdsAsync(Guid subscriptionId, string? externalSubscriptionId, string? externalCustomerId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets subscription by external ID
    /// </summary>
    Task<Subscription?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
}
