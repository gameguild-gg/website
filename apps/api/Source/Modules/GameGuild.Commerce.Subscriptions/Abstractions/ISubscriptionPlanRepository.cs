

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Repository interface for subscription plan data access
/// </summary>
public interface ISubscriptionPlanRepository
{
    /// <summary>
    ///     Gets a subscription plan by ID
    /// </summary>
    Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a subscription plan by slug
    /// </summary>
    Task<SubscriptionPlan?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a subscription plan by external ID
    /// </summary>
    Task<SubscriptionPlan?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all active subscription plans
    /// </summary>
    Task<IEnumerable<SubscriptionPlan>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all subscription plans (active and inactive)
    /// </summary>
    Task<IEnumerable<SubscriptionPlan>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets featured subscription plans
    /// </summary>
    Task<IEnumerable<SubscriptionPlan>> GetFeaturedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Searches subscription plans by name
    /// </summary>
    Task<IEnumerable<SubscriptionPlan>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets subscription plans within a price range
    /// </summary>
    Task<IEnumerable<SubscriptionPlan>> GetByPriceRangeAsync(long minPriceInCents, long maxPriceInCents, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a new subscription plan
    /// </summary>
    Task<SubscriptionPlan> AddAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an existing subscription plan
    /// </summary>
    Task<SubscriptionPlan> UpdateAsync(SubscriptionPlan plan, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a subscription plan (soft delete)
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a plan name is unique
    /// </summary>
    Task<bool> IsNameUniqueAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a plan slug is unique
    /// </summary>
    Task<bool> IsSlugUniqueAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the count of active subscriptions for a plan
    /// </summary>
    Task<int> GetActiveSubscriptionCountAsync(Guid planId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets paginated subscription plans with search and filtering
    /// </summary>
    Task<PagedResult<SubscriptionPlan>> GetPagedAsync(int skip, int pageSize, string? searchTerm = null, bool includeDeleted = false, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists all interval price points configured for a plan (issue #392).
    /// </summary>
    Task<IReadOnlyList<SubscriptionPlanIntervalPrice>> GetIntervalPricesAsync(Guid planId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the interval price point for one plan interval, when explicitly configured (issue #392).
    /// </summary>
    Task<SubscriptionPlanIntervalPrice?> GetIntervalPriceAsync(Guid planId, BillingCycle interval, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds or updates the interval price point for a plan interval (issue #392).
    /// </summary>
    Task<SubscriptionPlanIntervalPrice> UpsertIntervalPriceAsync(SubscriptionPlanIntervalPrice intervalPrice, CancellationToken cancellationToken = default);
}
