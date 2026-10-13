

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Repository interface for subscription data access
/// </summary>
public interface ISubscriptionRepository
{
    /// <summary>
    ///     Gets a subscription by ID
    /// </summary>
    Task<Subscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a subscription by external ID
    /// </summary>
    Task<Subscription?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all subscriptions for a tenant
    /// </summary>
    Task<IEnumerable<Subscription>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the active subscription for a tenant
    /// </summary>
    Task<Subscription?> GetActiveTenantSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all subscriptions for a plan
    /// </summary>
    Task<IEnumerable<Subscription>> GetByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets subscriptions by status
    /// </summary>
    Task<IEnumerable<Subscription>> GetByStatusAsync(SubscriptionStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets subscriptions created by a specific user
    /// </summary>
    Task<IEnumerable<Subscription>> GetByCreatedUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets subscriptions expiring soon (within specified days)
    /// </summary>
    Task<IEnumerable<Subscription>> GetExpiringSoonAsync(int days, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets subscriptions due for renewal (within specified days)
    /// </summary>
    Task<IEnumerable<Subscription>> GetDueForRenewalAsync(int days, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets trial subscriptions expiring soon
    /// </summary>
    Task<IEnumerable<Subscription>> GetTrialsExpiringSoonAsync(int days, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets suspended subscriptions
    /// </summary>
    Task<IEnumerable<Subscription>> GetSuspendedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets subscriptions by billing cycle
    /// </summary>
    Task<IEnumerable<Subscription>> GetByBillingCycleAsync(BillingCycle billingCycle, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets subscriptions within a date range
    /// </summary>
    Task<IEnumerable<Subscription>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets subscription count by status
    /// </summary>
    Task<Dictionary<SubscriptionStatus, int>> GetCountByStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the interval breakdown of active subscriptions: counts and recurring
    ///     billing amounts per billing cycle (issue #392).
    /// </summary>
    Task<IReadOnlyList<SubscriptionIntervalBreakdown>> GetIntervalBreakdownAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets revenue statistics for a date range
    /// </summary>
    Task<decimal> GetRevenueForPeriodAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a new subscription
    /// </summary>
    Task<Subscription> AddAsync(Subscription subscription, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an existing subscription
    /// </summary>
    Task<Subscription> UpdateAsync(Subscription subscription, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a subscription (soft delete)
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a tenant has an active subscription
    /// </summary>
    Task<bool> HasActiveSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets paginated subscriptions with filtering
    /// </summary>
    Task<PagedResult<Subscription>> GetPagedAsync(int page, int pageSize, SubscriptionStatus? status = null, Guid? tenantId = null, Guid? planId = null, CancellationToken cancellationToken = default);
}
