namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Thin facade that delegates to focused sub-services for backward compatibility.
///     New code should inject the focused interfaces directly.
/// </summary>
/// <remarks>
///     Delegates to:
///     - <see cref="SubscriptionLifecycleService"/> for lifecycle operations
///     - <see cref="SubscriptionBillingService"/> for billing operations
///     - <see cref="SubscriptionQueryAndExternalIdService"/> for queries and external IDs
/// </remarks>
public class SubscriptionService(
    ISubscriptionLifecycleService lifecycleService,
    ISubscriptionBillingService billingService,
    ISubscriptionQueryService queryService,
    ISubscriptionExternalIdService externalIdService)
    : ISubscriptionLifecycleService,
      ISubscriptionBillingService,
      ISubscriptionQueryService,
      ISubscriptionExternalIdService
{
    private readonly ISubscriptionLifecycleService _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
    private readonly ISubscriptionBillingService _billingService = billingService ?? throw new ArgumentNullException(nameof(billingService));
    private readonly ISubscriptionQueryService _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
    private readonly ISubscriptionExternalIdService _externalIdService = externalIdService ?? throw new ArgumentNullException(nameof(externalIdService));

    #region ISubscriptionLifecycleService

    public Task<Subscription> CreateAsync(
        Guid tenantId, Guid planId, Guid createdByUserId, BillingCycle billingCycle,
        Money amount, DateTime? startDate = null, int? trialDays = null,
        CancellationToken cancellationToken = default)
        => _lifecycleService.CreateAsync(tenantId, planId, createdByUserId, billingCycle, amount, startDate, trialDays, cancellationToken);

    public Task<Subscription> ActivateAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
        => _lifecycleService.ActivateAsync(subscriptionId, cancellationToken);

    public Task<Subscription> StartTrialAsync(Guid subscriptionId, int trialDays, CancellationToken cancellationToken = default)
        => _lifecycleService.StartTrialAsync(subscriptionId, trialDays, cancellationToken);

    public Task<Subscription> EndTrialAsync(Guid subscriptionId, bool convertToPaid, CancellationToken cancellationToken = default)
        => _lifecycleService.EndTrialAsync(subscriptionId, convertToPaid, cancellationToken);

    public Task<Subscription> CancelAsync(Guid subscriptionId, CancellationReason reason, string? note = null, DateTime? effectiveDate = null, CancellationToken cancellationToken = default)
        => _lifecycleService.CancelAsync(subscriptionId, reason, note, effectiveDate, cancellationToken);

    public Task<Subscription> SuspendAsync(Guid subscriptionId, string? reason = null, CancellationToken cancellationToken = default)
        => _lifecycleService.SuspendAsync(subscriptionId, reason, cancellationToken);

    public Task<Subscription> ReactivateAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
        => _lifecycleService.ReactivateAsync(subscriptionId, cancellationToken);

    public Task<SubscriptionUpgradeResult> UpgradePlanAsync(Guid subscriptionId, Guid newPlanId, DateTime? effectiveDate = null, CancellationToken cancellationToken = default)
        => _lifecycleService.UpgradePlanAsync(subscriptionId, newPlanId, effectiveDate, cancellationToken);

    public Task<SubscriptionDowngradeResult> DowngradePlanAsync(Guid subscriptionId, Guid newPlanId, DateTime? effectiveDate = null, CancellationToken cancellationToken = default)
        => _lifecycleService.DowngradePlanAsync(subscriptionId, newPlanId, effectiveDate, cancellationToken);

    public Task<ChangeBillingCycleResult> ChangeBillingCycleAsync(Guid subscriptionId, BillingCycle newBillingCycle, CancellationToken cancellationToken = default)
        => _lifecycleService.ChangeBillingCycleAsync(subscriptionId, newBillingCycle, cancellationToken);

    public Task<Subscription> SetAutoRenewAsync(Guid subscriptionId, bool autoRenew, CancellationToken cancellationToken = default)
        => _lifecycleService.SetAutoRenewAsync(subscriptionId, autoRenew, cancellationToken);

    public Task<Subscription> UpdateMetadataAsync(Guid subscriptionId, string metadata, CancellationToken cancellationToken = default)
        => _lifecycleService.UpdateMetadataAsync(subscriptionId, metadata, cancellationToken);

    #endregion

    #region ISubscriptionBillingService

    public Task<SubscriptionRenewalResult> ProcessRenewalAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
        => _billingService.ProcessRenewalAsync(subscriptionId, cancellationToken);

    public Task<Subscription> RecordPaymentAsync(Guid subscriptionId, decimal amount, string currency, DateTime paymentDate, CancellationToken cancellationToken = default)
        => _billingService.RecordPaymentAsync(subscriptionId, amount, currency, paymentDate, cancellationToken);

    public Task<Subscription> RecordPaymentFailureAsync(Guid subscriptionId, string reason, DateTime failureDate, CancellationToken cancellationToken = default)
        => _billingService.RecordPaymentFailureAsync(subscriptionId, reason, failureDate, cancellationToken);

    public Task<BulkRenewalResult> ProcessBulkRenewalsAsync(IEnumerable<Guid> subscriptionIds, CancellationToken cancellationToken = default)
        => _billingService.ProcessBulkRenewalsAsync(subscriptionIds, cancellationToken);

    public Task SendRenewalRemindersAsync(int daysBeforeRenewal, CancellationToken cancellationToken = default)
        => _billingService.SendRenewalRemindersAsync(daysBeforeRenewal, cancellationToken);

    public Task SendTrialExpirationRemindersAsync(int daysBeforeExpiration, CancellationToken cancellationToken = default)
        => _billingService.SendTrialExpirationRemindersAsync(daysBeforeExpiration, cancellationToken);

    #endregion

    #region ISubscriptionQueryService

    public Task<Subscription?> GetByIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
        => _queryService.GetByIdAsync(subscriptionId, cancellationToken);

    public Task<Subscription?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
        => _queryService.GetByExternalIdAsync(externalId, cancellationToken);

    public Task<bool> IsSubscriptionActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _queryService.IsSubscriptionActiveAsync(tenantId, cancellationToken);

    public Task<Subscription?> GetActiveTenantSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _queryService.GetActiveTenantSubscriptionAsync(tenantId, cancellationToken);

    public Task<IEnumerable<Subscription>> GetTenantSubscriptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _queryService.GetTenantSubscriptionsAsync(tenantId, cancellationToken);

    public Task<IEnumerable<Subscription>> GetTenantSubscriptionHistoryAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _queryService.GetTenantSubscriptionHistoryAsync(tenantId, cancellationToken);

    public Task<IEnumerable<Subscription>> GetExpiringSoonAsync(int days, CancellationToken cancellationToken = default)
        => _queryService.GetExpiringSoonAsync(days, cancellationToken);

    public Task<IEnumerable<Subscription>> GetDueForRenewalAsync(int days, CancellationToken cancellationToken = default)
        => _queryService.GetDueForRenewalAsync(days, cancellationToken);

    public Task<IEnumerable<Subscription>> GetTrialsExpiringSoonAsync(int days, CancellationToken cancellationToken = default)
        => _queryService.GetTrialsExpiringSoonAsync(days, cancellationToken);

    public Task<SubscriptionLimitValidationResult> ValidateSubscriptionLimitsAsync(Guid tenantId, int userCount, long storageMb, long apiCallsPerMonth, CancellationToken cancellationToken = default)
        => _queryService.ValidateSubscriptionLimitsAsync(tenantId, userCount, storageMb, apiCallsPerMonth, cancellationToken);

    public Task<SubscriptionUsageStatistics> GetUsageStatisticsAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
        => _queryService.GetUsageStatisticsAsync(subscriptionId, cancellationToken);

    public Task<RevenueAnalytics> GetRevenueAnalyticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        => _queryService.GetRevenueAnalyticsAsync(startDate, endDate, cancellationToken);

    public Task<SubscriptionAnalytics> GetSubscriptionAnalyticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        => _queryService.GetSubscriptionAnalyticsAsync(startDate, endDate, cancellationToken);

    #endregion

    #region ISubscriptionExternalIdService

    public Task<Subscription> SetExternalIdsAsync(Guid subscriptionId, string? externalSubscriptionId, string? externalCustomerId, CancellationToken cancellationToken = default)
        => _externalIdService.SetExternalIdsAsync(subscriptionId, externalSubscriptionId, externalCustomerId, cancellationToken);

    Task<Subscription?> ISubscriptionExternalIdService.GetByExternalIdAsync(string externalId, CancellationToken cancellationToken)
        => _externalIdService.GetByExternalIdAsync(externalId, cancellationToken);

    #endregion
}

#region Concrete implementations for abstract result types (required for instantiation)

/// <summary>
///     Concrete implementation of BulkRenewalResult for instantiation.
/// </summary>
internal sealed class ConcreteBulkRenewalResult : BulkRenewalResult;

/// <summary>
///     Concrete implementation of SubscriptionUsageStatistics for instantiation.
/// </summary>
internal sealed class ConcreteSubscriptionUsageStatistics : SubscriptionUsageStatistics;

/// <summary>
///     Concrete implementation of RevenueAnalytics for instantiation.
/// </summary>
internal sealed class ConcreteRevenueAnalytics : RevenueAnalytics;

/// <summary>
///     Concrete implementation of SubscriptionAnalytics for instantiation.
/// </summary>
internal sealed class ConcreteSubscriptionAnalytics : SubscriptionAnalytics;

/// <summary>
///     Concrete implementation of RenewalAttempt for instantiation.
/// </summary>
internal sealed class ConcreteRenewalAttempt : RenewalAttempt;

/// <summary>
///     Concrete implementation of LimitCheckResult for instantiation.
/// </summary>
internal sealed class ConcreteLimitCheckResult : LimitCheckResult;

#endregion
