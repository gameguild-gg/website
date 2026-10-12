using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Caches the public subscription plan lists (active + featured) for the anonymous
///     pricing surface (issue #416: "Plan list queries cached for 5 minutes").
/// </summary>
/// <remarks>
///     Entries are sized (<see cref="MemoryCacheEntryOptions.Size" />) because the platform
///     registers <c>IMemoryCache</c> with a <c>SizeLimit</c> (see CachingExtensions) — an
///     unsized entry throws at runtime (the #779 lesson). Every plan mutation command
///     invalidates through <see cref="Invalidate" />, which is the whole-surface reset: plan
///     mutations are rare compared to pricing-page reads.
/// </remarks>
public sealed class SubscriptionPlanListCache(IMemoryCache cache, ILogger<SubscriptionPlanListCache> logger) : ISubscriptionPlanListCache
{
    private const string ActivePlansCacheKey = "SubscriptionPlans:Query:Active";
    private const string FeaturedPlansCacheKey = "SubscriptionPlans:Query:Featured";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyList<SubscriptionPlan>> GetOrCreateActivePlansAsync(
        Func<CancellationToken, Task<IEnumerable<SubscriptionPlan>>> queryFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryFactory);

        if (cache.TryGetValue(ActivePlansCacheKey, out IReadOnlyList<SubscriptionPlan>? cached) && cached is not null)
            return cached;

        var plans = (await queryFactory(cancellationToken).ConfigureAwait(false)).ToList();

        cache.Set(ActivePlansCacheKey, plans, BuildEntryOptions(plans.Count));
        logger.LogDebug("Cached {PlanCount} active subscription plans for {Duration}", plans.Count, CacheDuration);

        return plans;
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> GetOrCreateFeaturedPlansAsync(
        Func<CancellationToken, Task<IEnumerable<SubscriptionPlan>>> queryFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryFactory);

        if (cache.TryGetValue(FeaturedPlansCacheKey, out IReadOnlyList<SubscriptionPlan>? cached) && cached is not null)
            return cached;

        var plans = (await queryFactory(cancellationToken).ConfigureAwait(false)).ToList();

        cache.Set(FeaturedPlansCacheKey, plans, BuildEntryOptions(plans.Count));
        logger.LogDebug("Cached {PlanCount} featured subscription plans for {Duration}", plans.Count, CacheDuration);

        return plans;
    }

    public void Invalidate()
    {
        cache.Remove(ActivePlansCacheKey);
        cache.Remove(FeaturedPlansCacheKey);
        logger.LogDebug("Invalidated subscription plan list cache entries");
    }

    private static MemoryCacheEntryOptions BuildEntryOptions(int planCount) => new()
    {
        AbsoluteExpirationRelativeToNow = CacheDuration,
        Size = Math.Max(1, planCount) // cache is size-limited: every entry must carry a size
    };
}

/// <summary>
///     Cache contract for the public subscription plan lists (issue #416).
/// </summary>
public interface ISubscriptionPlanListCache
{
    /// <summary>
    ///     Returns the active-plan list, populating the 5-minute cache entry on miss.
    /// </summary>
    Task<IReadOnlyList<SubscriptionPlan>> GetOrCreateActivePlansAsync(
        Func<CancellationToken, Task<IEnumerable<SubscriptionPlan>>> queryFactory,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the featured-plan list, populating the 5-minute cache entry on miss.
    /// </summary>
    Task<IReadOnlyList<SubscriptionPlan>> GetOrCreateFeaturedPlansAsync(
        Func<CancellationToken, Task<IEnumerable<SubscriptionPlan>>> queryFactory,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Invalidates all cached plan lists. Called by every plan mutation command.
    /// </summary>
    void Invalidate();
}
