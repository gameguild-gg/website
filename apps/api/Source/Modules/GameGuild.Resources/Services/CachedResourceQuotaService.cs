using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GameGuild.Resources;

/// <summary>
///     Caching decorator for <see cref="IResourceQuotaService"/>.
///     Provides read-through caching for quota queries with automatic invalidation on mutations.
/// </summary>
/// <remarks>
///     Cache keys are tenant-scoped to prevent cross-tenant leakage.
///     Write operations (SetQuota, TryAtomicConsume, Decrement) invalidate the cache immediately.
///     Recommended cache duration: 30-60 seconds for quotas (balances performance vs staleness).
/// </remarks>
public class CachedResourceQuotaService : IResourceQuotaService
{
    private readonly IResourceQuotaService _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedResourceQuotaService> _logger;
    private readonly ICostTelemetryRecorder? _costTelemetryRecorder;

    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromSeconds(30);
    private const string CacheKeyPrefix = "quota:";

    public CachedResourceQuotaService(
        IResourceQuotaService inner,
        IMemoryCache cache,
        ILogger<CachedResourceQuotaService> logger,
        ICostTelemetryRecorder? costTelemetryRecorder = null)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
        _costTelemetryRecorder = costTelemetryRecorder;
    }

    private static string GetQuotaCacheKey(Guid tenantId, ResourceUsageType type) =>
        $"{CacheKeyPrefix}{tenantId}:{type}";

    private static string GetTenantQuotasCacheKey(Guid tenantId) =>
        $"{CacheKeyPrefix}{tenantId}:all";

    private void InvalidateQuotaCache(Guid tenantId, ResourceUsageType type)
    {
        var specificKey = GetQuotaCacheKey(tenantId, type);
        var allKey = GetTenantQuotasCacheKey(tenantId);

        _cache.Remove(specificKey);
        _costTelemetryRecorder?.RecordCacheOperation();
        _cache.Remove(allKey);
        _costTelemetryRecorder?.RecordCacheOperation();

        _logger.LogDebug("Invalidated quota cache for tenant {TenantId}, type {Type}", tenantId, type);
    }

    [ExcludeFromCodeCoverage]
    private void InvalidateTenantCache(Guid tenantId)
    {
        // Invalidate all quotas cache for tenant
        var allKey = GetTenantQuotasCacheKey(tenantId);
        _cache.Remove(allKey);
        _costTelemetryRecorder?.RecordCacheOperation();

        _logger.LogDebug("Invalidated all quotas cache for tenant {TenantId}", tenantId);
    }

    // ========== Quota Management (writes invalidate cache) ==========

    public async Task<ResourceQuota> SetQuotaAsync(
        Guid tenantId,
        ResourceUsageType type,
        long? softLimit,
        long? hardLimit,
        ResourceQuotaPeriod period = ResourceQuotaPeriod.Monthly,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.SetQuotaAsync(tenantId, type, softLimit, hardLimit, period, cancellationToken).ConfigureAwait(false);
        InvalidateQuotaCache(tenantId, type);
        return result;
    }

    public async Task<ResourceQuota?> GetQuotaAsync(
        Guid tenantId,
        ResourceUsageType type,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetQuotaCacheKey(tenantId, type);

        _costTelemetryRecorder?.RecordCacheOperation();
        if (_cache.TryGetValue<ResourceQuota>(cacheKey, out var cached) && cached != null)
        {
            _logger.LogDebug("Cache hit for quota {TenantId}:{Type}", tenantId, type);
            return cached;
        }

        var quota = await _inner.GetQuotaAsync(tenantId, type, cancellationToken).ConfigureAwait(false);

        if (quota != null)
        {
            _cache.Set(cacheKey, quota, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = DefaultCacheDuration,
                Size = 1
            });
            _costTelemetryRecorder?.RecordCacheOperation();
            _logger.LogDebug("Cached quota for {TenantId}:{Type}", tenantId, type);
        }

        return quota;
    }

    public async Task<IEnumerable<ResourceQuota>> GetTenantQuotasAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetTenantQuotasCacheKey(tenantId);

        _costTelemetryRecorder?.RecordCacheOperation();
        if (_cache.TryGetValue<IEnumerable<ResourceQuota>>(cacheKey, out var cached) && cached != null)
        {
            _logger.LogDebug("Cache hit for all quotas of tenant {TenantId}", tenantId);
            return cached;
        }

        var quotas = await _inner.GetTenantQuotasAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var quotaList = quotas.ToList();

        _cache.Set(cacheKey, quotaList, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DefaultCacheDuration,
            Size = quotaList.Count
        });
        _costTelemetryRecorder?.RecordCacheOperation();

        return quotaList;
    }

    public async Task<bool> DeleteQuotaAsync(
        Guid tenantId,
        ResourceUsageType type,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.DeleteQuotaAsync(tenantId, type, cancellationToken).ConfigureAwait(false);
        InvalidateQuotaCache(tenantId, type);
        return result;
    }

    // ========== Usage Tracking (writes invalidate cache) ==========

    public async Task<long> GetCurrentUsageAsync(
        Guid tenantId,
        ResourceUsageType type,
        CancellationToken cancellationToken = default)
    {
        // Use cached quota if available
        var quota = await GetQuotaAsync(tenantId, type, cancellationToken).ConfigureAwait(false);
        return quota?.CurrentUsage ?? 0;
    }

    public Task<IEnumerable<UsageRecord>> GetUsageHistoryAsync(
        Guid tenantId,
        ResourceUsageType type,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        // History is not cached (potentially large dataset)
        return _inner.GetUsageHistoryAsync(tenantId, type, fromDate, toDate, cancellationToken);
    }

    // ========== Limit Checking (read-only, uses cached quota) ==========

    public async Task<ResourceLimitCheckResponse> CheckLimitsAsync(
        Guid tenantId,
        ResourceUsageType type,
        long requestedAmount = 1,
        CancellationToken cancellationToken = default)
    {
        // CheckLimitsAsync can use cached data (advisory only)
        return await _inner.CheckLimitsAsync(tenantId, type, requestedAmount, cancellationToken).ConfigureAwait(false);
    }

    public Task<Dictionary<ResourceUsageType, ResourceLimitCheckResponse>> CheckMultipleLimitsAsync(
        Guid tenantId,
        Dictionary<ResourceUsageType, long> requestedAmounts,
        CancellationToken cancellationToken = default)
    {
        return _inner.CheckMultipleLimitsAsync(tenantId, requestedAmounts, cancellationToken);
    }

    // ========== Atomic Operations (bypass cache for accuracy, invalidate on success) ==========

    public async Task<ResourceLimitCheckResponse> TryConsumeResourceAsync(
        Guid tenantId,
        ResourceUsageType type,
        long amount = 1,
        Guid? userId = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        // Always go to inner service for atomic operations
        var result = await _inner.TryConsumeResourceAsync(tenantId, type, amount, userId, source, cancellationToken).ConfigureAwait(false);

        // Invalidate cache after mutation
        InvalidateQuotaCache(tenantId, type);

        return result;
    }

    public async Task<(bool Success, long CurrentUsage, long? HardLimit)> TryAtomicConsumeAsync(
        Guid tenantId,
        ResourceUsageType type,
        long amount = 1,
        CancellationToken cancellationToken = default)
    {
        // Atomic operations always bypass cache for accuracy
        var result = await _inner.TryAtomicConsumeAsync(tenantId, type, amount, cancellationToken).ConfigureAwait(false);

        // Always invalidate after attempt (whether success or failure)
        InvalidateQuotaCache(tenantId, type);

        return result;
    }

    public async Task<bool> DecrementUsageAsync(
        Guid tenantId,
        ResourceUsageType type,
        long amount = 1,
        Guid? userId = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.DecrementUsageAsync(tenantId, type, amount, userId, source, cancellationToken).ConfigureAwait(false);

        if (result)
        {
            InvalidateQuotaCache(tenantId, type);
        }

        return result;
    }

    // ========== Analytics & Maintenance (not cached) ==========

    public Task<ResourceUsageResponse> GetResourceUsageDetailsAsync(
        Guid tenantId,
        ResourceUsageType type,
        int historyDays = 30,
        CancellationToken cancellationToken = default)
    {
        return _inner.GetResourceUsageDetailsAsync(tenantId, type, historyDays, cancellationToken);
    }

    public Task<IEnumerable<Guid>> GetTenantsExceedingLimitsAsync(
        ResourceUsageType? type = null,
        bool hardLimitOnly = false,
        CancellationToken cancellationToken = default)
    {
        return _inner.GetTenantsExceedingLimitsAsync(type, hardLimitOnly, cancellationToken);
    }

    public async Task<int> ResetExpiredQuotasAsync(CancellationToken cancellationToken = default)
    {
        var result = await _inner.ResetExpiredQuotasAsync(cancellationToken).ConfigureAwait(false);

        // Note: We can't know which tenants were reset, so we don't invalidate specific caches
        // The cache will naturally expire based on TTL
        _logger.LogDebug("Reset {Count} expired quotas. Cache will expire naturally.", result);

        return result;
    }

    public Task<int> CleanupOldUsageRecordsAsync(DateTime olderThan, CancellationToken cancellationToken = default)
    {
        return _inner.CleanupOldUsageRecordsAsync(olderThan, cancellationToken);
    }

    public async Task<bool> RecalculateUsageAsync(
        Guid tenantId,
        ResourceUsageType type,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.RecalculateUsageAsync(tenantId, type, cancellationToken).ConfigureAwait(false);

        if (result)
        {
            InvalidateQuotaCache(tenantId, type);
        }

        return result;
    }
}
