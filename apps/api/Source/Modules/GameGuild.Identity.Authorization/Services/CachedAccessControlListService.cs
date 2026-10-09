using System.Collections.Concurrent;
using System.Diagnostics;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Cached wrapper for IAccessControlListService that adds hybrid (L1 + L2) caching for Access Control List lookups.
///     This wraps a database-backed service and provides fast reads via cache.
///     Write operations go through to the database and invalidate cache.
/// </summary>
/// <remarks>
///     <para>
///         <b>Cache Levels:</b>
///         <list type="bullet">
///             <item>L1 (IMemoryCache): Fast, per-instance cache with short TTL</item>
///             <item>L2 (IDistributedCache via IHybridPermissionCache): Shared cache for multi-instance deployments</item>
///         </list>
///     </para>
///     <para>
///         <b>Cache Invalidation:</b>
///         Uses version-based cache keys. When permissions change, the tenant security version is incremented,
///         causing old cache entries to become stale. Explicit invalidation is also performed for immediate consistency.
///     </para>
/// </remarks>
public sealed class CachedAccessControlListService : IAccessControlListService, IAuthorizationCacheWarmupBatchPath
{
    private const string CacheType = "acl";
    private const int MaxSecurityVersionRetries = 3;
    
    private readonly IAccessControlListService _innerService;
    private readonly IMemoryCache _l1Cache;
    private readonly IHybridPermissionCache? _hybridCache;
    private readonly ITenantSecurityVersionStore _tenantVersionStore;
    private readonly IUserSecurityVersionStore _userVersionStore;
    private readonly ICacheMetricsService? _metrics;
    private readonly IPermissionCacheKeyTracker? _keyTracker;
    private readonly ICacheInvalidationService? _invalidationService;
    private readonly IPermissionCachePopularityTracker? _popularityTracker;
    private readonly AuthorizationCacheOptions _options;
    private readonly ConcurrentDictionary<string, HashSet<string>> _tenantCacheKeys = new();

    /// <summary>
    ///     Initializes a new instance of <see cref="CachedAccessControlListService"/>.
    /// </summary>
    public CachedAccessControlListService(
        IAccessControlListService innerService,
        IMemoryCache cache,
        ITenantSecurityVersionStore tenantVersionStore,
        IUserSecurityVersionStore userVersionStore,
        IOptions<AuthorizationCacheOptions> options)
        : this(innerService, cache, tenantVersionStore, userVersionStore, options, null, null, null, null)
    {
    }

    public CachedAccessControlListService(
        IAccessControlListService innerService,
        IMemoryCache cache,
        ITenantSecurityVersionStore tenantVersionStore,
        IUserSecurityVersionStore userVersionStore,
        IOptions<AuthorizationCacheOptions> options,
        IHybridPermissionCache? hybridCache)
        : this(innerService, cache, tenantVersionStore, userVersionStore, options, hybridCache, null, null, null)
    {
    }

    public CachedAccessControlListService(
        IAccessControlListService innerService,
        IMemoryCache cache,
        ITenantSecurityVersionStore tenantVersionStore,
        IUserSecurityVersionStore userVersionStore,
        IOptions<AuthorizationCacheOptions> options,
        ICacheInvalidationService? invalidationService)
        : this(innerService, cache, tenantVersionStore, userVersionStore, options, null, null, null, invalidationService)
    {
    }

    public CachedAccessControlListService(
        IAccessControlListService innerService,
        IMemoryCache cache,
        ITenantSecurityVersionStore tenantVersionStore,
        IUserSecurityVersionStore userVersionStore,
        IOptions<AuthorizationCacheOptions> options,
        IHybridPermissionCache? hybridCache,
        ICacheMetricsService? metrics)
        : this(innerService, cache, tenantVersionStore, userVersionStore, options, hybridCache, metrics, null, null)
    {
    }

    public CachedAccessControlListService(
        IAccessControlListService innerService,
        IMemoryCache cache,
        ITenantSecurityVersionStore tenantVersionStore,
        IUserSecurityVersionStore userVersionStore,
        IOptions<AuthorizationCacheOptions> options,
        IHybridPermissionCache? hybridCache,
        ICacheMetricsService? metrics,
        IPermissionCacheKeyTracker? keyTracker,
        ICacheInvalidationService? invalidationService)
        : this(innerService, cache, tenantVersionStore, userVersionStore, options, hybridCache, metrics, keyTracker, invalidationService, null)
    {
    }

    public CachedAccessControlListService(
        IAccessControlListService innerService,
        IMemoryCache cache,
        ITenantSecurityVersionStore tenantVersionStore,
        IUserSecurityVersionStore userVersionStore,
        IOptions<AuthorizationCacheOptions> options,
        IHybridPermissionCache? hybridCache,
        ICacheMetricsService? metrics,
        IPermissionCacheKeyTracker? keyTracker,
        ICacheInvalidationService? invalidationService,
        IPermissionCachePopularityTracker? popularityTracker)
    {
        _innerService = innerService;
        _l1Cache = cache;
        _tenantVersionStore = tenantVersionStore;
        _userVersionStore = userVersionStore;
        _options = options.Value;
        _hybridCache = hybridCache;
        _metrics = metrics;
        _keyTracker = keyTracker;
        _invalidationService = invalidationService;
        _popularityTracker = popularityTracker;
    }

    #region Subject-based operations (preferred)

    async Task IAuthorizationCacheWarmupBatchPath.WarmCacheBatchAsync(
        IReadOnlyCollection<PermissionCacheWarmupRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var tenantVersions = new Dictionary<Guid, (long TenantVersion, long GlobalVersion)>();
        var userVersions = new Dictionary<Guid, long>();
        var normalizedRequests = requests.ToArray();
        var cacheKeys = new string[normalizedRequests.Length];

        for (var index = 0; index < normalizedRequests.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = normalizedRequests[index];
            _popularityTracker?.Record(request);

            if (!tenantVersions.TryGetValue(request.TenantId, out var versions))
            {
                versions = await _tenantVersionStore
                    .GetTenantAndGlobalVersionsAsync(request.TenantId, cancellationToken)
                    .ConfigureAwait(false);
                tenantVersions.Add(request.TenantId, versions);
            }

            var userVersion = 0L;
            if (request.Subject.UserId is { } userId)
            {
                if (!userVersions.TryGetValue(userId, out userVersion))
                {
                    userVersion = await _userVersionStore.GetVersionAsync(userId, cancellationToken).ConfigureAwait(false);
                    userVersions.Add(userId, userVersion);
                }
            }

            cacheKeys[index] = BuildSubjectCacheKey(
                request.Subject,
                request.TenantId,
                request.ResourceType,
                request.ResourceId,
                versions.TenantVersion,
                userVersion,
                versions.GlobalVersion);
        }

        IReadOnlyDictionary<string, CacheResult<AccessLevel>>? cachedResults = null;
        if (_hybridCache is not null)
        {
            cachedResults = await _hybridCache
                .GetManyValuesAsync<AccessLevel>(cacheKeys, CacheType, cancellationToken)
                .ConfigureAwait(false);
        }

        var valuesToWrite = new Dictionary<string, AccessLevel>(StringComparer.Ordinal);
        for (var index = 0; index < normalizedRequests.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = normalizedRequests[index];
            var cacheKey = cacheKeys[index];

            if (cachedResults is not null && cachedResults.TryGetValue(cacheKey, out var cached) && cached.Found)
            {
                // Register the entry with this wrapper's tenant index as well as promoting it to L1.
                CacheAccessLevel(cacheKey, request.TenantId.ToString(), cached.Value, l1Only: true);
                continue;
            }

            var l1LookupStartedAt = cachedResults is null ? Stopwatch.GetTimestamp() : 0;
            if (cachedResults is null && _l1Cache.TryGetValue(cacheKey, out AccessLevel l1Value))
            {
                _metrics?.RecordLookupDuration(Stopwatch.GetElapsedTime(l1LookupStartedAt), CacheType);
                _metrics?.RecordHit(CacheLevel.L1, CacheType);
                CacheAccessLevel(cacheKey, request.TenantId.ToString(), l1Value, l1Only: true);
                continue;
            }

            if (cachedResults is null)
            {
                _metrics?.RecordLookupDuration(Stopwatch.GetElapsedTime(l1LookupStartedAt), CacheType);
                _metrics?.RecordMiss(CacheType);
            }

            // Database-backed ACL services can share a scoped DbContext, so evaluate misses sequentially.
            var accessLevel = await _innerService
                .EvaluateAccessAsync(
                    request.Subject,
                    request.TenantId,
                    request.ResourceType,
                    request.ResourceId,
                    cancellationToken)
                .ConfigureAwait(false);

            CacheAccessLevel(cacheKey, request.TenantId.ToString(), accessLevel, l1Only: true);
            valuesToWrite[cacheKey] = accessLevel;
        }

        if (_hybridCache is not null && valuesToWrite.Count > 0)
        {
            await _hybridCache.SetManyValuesAsync(valuesToWrite, CacheType, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<AccessLevel> EvaluateAccessAsync(
        AclSubject subject,
        Guid tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        _popularityTracker?.Record(new PermissionCacheWarmupRequest(tenantId, subject, resourceType, resourceId));

        for (var attempt = 0; attempt < MaxSecurityVersionRetries; attempt++)
        {
            // Include the shared global version so global role changes invalidate every tenant's ACL keys.
            var (tenantVersion, globalVersion) = await _tenantVersionStore
                .GetTenantAndGlobalVersionsAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var userVersion = subject.UserId.HasValue
                ? await _userVersionStore.GetVersionAsync(subject.UserId.Value, cancellationToken).ConfigureAwait(false)
                : 0;
            var cacheKey = BuildSubjectCacheKey(
                subject,
                tenantId,
                resourceType,
                resourceId,
                tenantVersion,
                userVersion,
                globalVersion);

            // The cache key is scoped to the current tenant/global/user version snapshot.
            var l1LookupStartedAt = Stopwatch.GetTimestamp();
            if (_l1Cache.TryGetValue(cacheKey, out AccessLevel cachedLevel))
            {
                _metrics?.RecordLookupDuration(Stopwatch.GetElapsedTime(l1LookupStartedAt), CacheType);
                _metrics?.RecordHit(CacheLevel.L1, CacheType);
                return cachedLevel;
            }
            if (_hybridCache is null)
            {
                _metrics?.RecordLookupDuration(Stopwatch.GetElapsedTime(l1LookupStartedAt), CacheType);
            }

            // Try L2 (hybrid) cache if available
            if (_hybridCache != null)
            {
                var hybridResult = await _hybridCache
                    .GetValueAsync<AccessLevel>(cacheKey, CacheType, cancellationToken)
                    .ConfigureAwait(false);
                if (hybridResult.Found)
                {
                    // The version was read before this key lookup, so a permission mutation
                    // completed before that read cannot reuse an entry from its prior version.
                    CacheAccessLevel(cacheKey, tenantId.ToString(), hybridResult.Value, l1Only: true);
                    return hybridResult.Value;
                }
            }

            // Cache miss - fetch from underlying service.
            _metrics?.RecordMiss(CacheType);
            var level = await _innerService
                .EvaluateAccessAsync(subject, tenantId, resourceType, resourceId, cancellationToken)
                .ConfigureAwait(false);

            if (!await IsSecurityVersionCurrentAsync(
                    tenantId,
                    subject.UserId,
                    tenantVersion,
                    userVersion,
                    globalVersion,
                    cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            await CacheAccessLevelAsync(cacheKey, tenantId.ToString(), level, cancellationToken).ConfigureAwait(false);
            return level;
        }

        throw new InvalidOperationException(
            $"Could not obtain a stable ACL decision for tenant '{tenantId}' after {MaxSecurityVersionRetries} attempts because its security version kept changing.");
    }

    private async Task<bool> IsSecurityVersionCurrentAsync(
        Guid tenantId,
        Guid? userId,
        long expectedTenantVersion,
        long expectedUserVersion,
        long expectedGlobalVersion,
        CancellationToken cancellationToken)
    {
        var currentTenantVersions = await _tenantVersionStore
            .GetTenantAndGlobalVersionsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (currentTenantVersions.TenantVersion != expectedTenantVersion ||
            currentTenantVersions.GlobalVersion != expectedGlobalVersion)
        {
            return false;
        }

        if (userId is not { } currentUserId)
        {
            return true;
        }

        var currentUserVersion = await _userVersionStore
            .GetVersionAsync(currentUserId, cancellationToken).ConfigureAwait(false);
        return currentUserVersion == expectedUserVersion;
    }

    /// <inheritdoc />
    public async Task<bool> HasAccessAsync(
        AclSubject subject,
        Guid tenantId,
        string resourceType,
        string resourceId,
        AccessLevel requiredLevel,
        CancellationToken cancellationToken = default)
    {
        var actualLevel = await EvaluateAccessAsync(subject, tenantId, resourceType, resourceId, cancellationToken).ConfigureAwait(false);
        return actualLevel >= requiredLevel;
    }

    /// <inheritdoc />
    public async Task GrantAccessAsync(
        Guid grantorId,
        AclPrincipalType principalType,
        Guid? principalId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        AccessLevel accessLevel,
        CancellationToken cancellationToken = default)
    {
        // Write through to underlying service
        await _innerService.GrantAccessAsync(grantorId, principalType, principalId, tenantId, resourceType, resourceId, accessLevel, cancellationToken).ConfigureAwait(false);

        // Invalidate cache for this principal/resource combination
        await InvalidateAfterAclMutationAsync(principalType, principalId, tenantId, resourceType, resourceId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DenyAccessAsync(
        Guid grantorId,
        AclPrincipalType principalType,
        Guid? principalId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        AccessLevel accessLevel,
        CancellationToken cancellationToken = default)
    {
        // Write through to underlying service
        await _innerService.DenyAccessAsync(grantorId, principalType, principalId, tenantId, resourceType, resourceId, accessLevel, cancellationToken).ConfigureAwait(false);

        // Invalidate cache for this principal/resource combination
        await InvalidateAfterAclMutationAsync(principalType, principalId, tenantId, resourceType, resourceId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RevokeAccessAsync(
        Guid revokerId,
        AclPrincipalType principalType,
        Guid? principalId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        // Write through to underlying service
        await _innerService.RevokeAccessAsync(revokerId, principalType, principalId, tenantId, resourceType, resourceId, cancellationToken).ConfigureAwait(false);

        // Invalidate cache for this principal/resource combination
        await InvalidateAfterAclMutationAsync(principalType, principalId, tenantId, resourceType, resourceId, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Legacy user-based operations (backward compatibility)

    /// <inheritdoc />
    public async Task<AccessLevel> GetAccessLevelAsync(
        Guid userId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        // Include the shared global version so global role changes invalidate every tenant's ACL keys.
        var (tenantVersion, globalVersion) = await _tenantVersionStore
            .GetTenantAndGlobalVersionsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var userVersion = await _userVersionStore.GetVersionAsync(userId, cancellationToken).ConfigureAwait(false);
        var cacheKey = BuildCacheKey(userId, tenantId, resourceType, resourceId, tenantVersion, userVersion, globalVersion);

        // Try L1 cache first
        var l1LookupStartedAt = Stopwatch.GetTimestamp();
        if (_l1Cache.TryGetValue(cacheKey, out AccessLevel cachedLevel))
        {
            _metrics?.RecordLookupDuration(Stopwatch.GetElapsedTime(l1LookupStartedAt), CacheType);
            _metrics?.RecordHit(CacheLevel.L1, CacheType);
            return cachedLevel;
        }
        if (_hybridCache is null)
        {
            _metrics?.RecordLookupDuration(Stopwatch.GetElapsedTime(l1LookupStartedAt), CacheType);
        }

        // Try L2 (hybrid) cache if available
        if (_hybridCache != null)
        {
            var hybridResult = await _hybridCache.GetValueAsync<AccessLevel>(cacheKey, CacheType, cancellationToken).ConfigureAwait(false);
            if (hybridResult.Found)
            {
                // Promote to L1
                CacheAccessLevel(cacheKey, tenantId.ToString(), hybridResult.Value, l1Only: true);
                return hybridResult.Value;
            }
        }

        // Cache miss - fetch from underlying service
        _metrics?.RecordMiss(CacheType);
        var level = await _innerService.GetAccessLevelAsync(userId, tenantId, resourceType, resourceId, cancellationToken).ConfigureAwait(false);

        await CacheAccessLevelAsync(cacheKey, tenantId.ToString(), level, cancellationToken).ConfigureAwait(false);

        return level;
    }

    /// <inheritdoc />
    public async Task GrantAccessAsync(
        Guid grantorId,
        Guid granteeId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        AccessLevel accessLevel,
        CancellationToken cancellationToken = default)
    {
        // Write through to underlying service
        await _innerService.GrantAccessAsync(grantorId, granteeId, tenantId, resourceType, resourceId, accessLevel, cancellationToken).ConfigureAwait(false);

        // Invalidate cache for this user/resource combination
        await InvalidateAfterAclMutationAsync(AclPrincipalType.User, granteeId, tenantId, resourceType, resourceId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RevokeAccessAsync(
        Guid revokerId,
        Guid userId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        // Write through to underlying service
        await _innerService.RevokeAccessAsync(revokerId, userId, tenantId, resourceType, resourceId, cancellationToken).ConfigureAwait(false);

        // Invalidate cache for this user/resource combination
        await InvalidateAfterAclMutationAsync(AclPrincipalType.User, userId, tenantId, resourceType, resourceId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> HasAccessAsync(
        Guid userId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        AccessLevel requiredLevel,
        CancellationToken cancellationToken = default)
    {
        var actualLevel = await GetAccessLevelAsync(userId, tenantId, resourceType, resourceId, cancellationToken).ConfigureAwait(false);
        return actualLevel >= requiredLevel;
    }

    #endregion

    /// <summary>
    ///     Invalidates all cached Access Control List entries for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    public void InvalidateTenant(string tenantId)
    {
        if (_tenantCacheKeys.TryRemove(tenantId, out var keys))
        {
            string[] keySnapshot;
            lock (keys)
            {
                keySnapshot = keys.ToArray();
            }

            foreach (var key in keySnapshot)
            {
                _l1Cache.Remove(key);
                _metrics?.RecordEviction(CacheLevel.L1, CacheType);
            }
        }
    }

    /// <summary>
    ///     Invalidates all cached Access Control List entries for a tenant asynchronously,
    ///     including distributed cache if enabled.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InvalidateTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (!_tenantCacheKeys.TryRemove(tenantId, out var keys))
        {
            return;
        }

        string[] keySnapshot;
        lock (keys)
        {
            keySnapshot = keys.ToArray();
            keys.Clear();
        }

        foreach (var key in keySnapshot)
        {
            _l1Cache.Remove(key);
            _metrics?.RecordEviction(CacheLevel.L1, CacheType);
            if (_hybridCache is not null)
            {
                await _hybridCache.RemoveAsync(key, CacheType, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static string BuildCacheKey(
        Guid userId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        long tenantVersion,
        long userVersion,
        long globalVersion)
    {
        return $"acl:{tenantId}:{userId}:{resourceType}:{resourceId}:tv{tenantVersion}:uv{userVersion}:gv{globalVersion}";
    }

    private static string BuildSubjectCacheKey(
        AclSubject subject,
        Guid tenantId,
        string resourceType,
        string resourceId,
        long tenantVersion,
        long userVersion,
        long globalVersion)
    {
        // Build a stable cache key from subject principals
        // Includes both tenant version (for tenant-wide changes) and user version (for user-specific changes)
        var userPart = subject.UserId?.ToString() ?? "anon";
        var rolesPart = subject.RoleIds.Count > 0 ? string.Join(",", subject.RoleIds.OrderBy(r => r)) : "nr";
        var groupsPart = subject.GroupIds.Count > 0 ? string.Join(",", subject.GroupIds.OrderBy(g => g)) : "ng";
        return $"acl:subj:{tenantId}:{userPart}:{rolesPart}:{groupsPart}:{resourceType}:{resourceId}:tv{tenantVersion}:uv{userVersion}:gv{globalVersion}";
    }

    /// <summary>
    ///     Caches an access level in L1 cache only (used for L2 → L1 promotion).
    /// </summary>
    private void CacheAccessLevel(string cacheKey, string tenantId, AccessLevel level, bool l1Only)
    {
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(_options.AccessControlListTtlSeconds))
            .SetSlidingExpiration(TimeSpan.FromSeconds(_options.AccessControlListTtlSeconds / 2))
            .SetSize(1);

        _keyTracker?.Track(cacheKey, CacheType, cacheOptions);
        _l1Cache.Set(cacheKey, level, cacheOptions);

        // Track cache key for tenant invalidation
        TrackCacheKey(tenantId, cacheKey);
    }

    /// <summary>
    ///     Caches an access level in both L1 and L2 caches asynchronously.
    /// </summary>
    private async Task CacheAccessLevelAsync(string cacheKey, string tenantId, AccessLevel level, CancellationToken cancellationToken)
    {
        // Cache in L1
        CacheAccessLevel(cacheKey, tenantId, level, l1Only: true);

        // Cache in L2 if available
        if (_hybridCache != null)
        {
            await _hybridCache.SetValueAsync(cacheKey, level, CacheType, cancellationToken).ConfigureAwait(false);
        }
    }

    private void TrackCacheKey(string tenantId, string cacheKey)
    {
        _tenantCacheKeys.AddOrUpdate(
            tenantId,
            _ => new HashSet<string> { cacheKey },
            (_, existingKeys) =>
            {
                lock (existingKeys)
                {
                    existingKeys.Add(cacheKey);
                }
                return existingKeys;
            });
    }

    private async Task InvalidateAfterAclMutationAsync(
        AclPrincipalType principalType,
        Guid? principalId,
        Guid tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken)
    {
        if (_invalidationService is not null)
        {
            var targets = new List<CacheInvalidationTarget>
            {
                new(CacheInvalidationTargetType.Resource, ResourceType: resourceType, ResourceId: resourceId)
            };

            if (principalType == AclPrincipalType.User && principalId.HasValue)
            {
                targets.Add(new CacheInvalidationTarget(CacheInvalidationTargetType.User, UserId: principalId));
            }
            else if ((principalType is AclPrincipalType.Role or AclPrincipalType.Group) && principalId.HasValue)
            {
                targets.Add(new CacheInvalidationTarget(
                    CacheInvalidationTargetType.Dependency,
                    DependencyKind: principalType == AclPrincipalType.Role ? "role" : "group",
                    DependencyId: principalId));
            }

            await _invalidationService.InvalidateBatchAsync(tenantId, targets, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (principalType == AclPrincipalType.User && principalId.HasValue)
        {
            InvalidateUserResourceCache(principalId.Value, tenantId, resourceType, resourceId);
        }
        else
        {
            InvalidatePrincipalResourceCache(tenantId, resourceType, resourceId);
        }
    }

    private void InvalidatePrincipalResourceCache(Guid tenantId, string resourceType, string resourceId)
    {
        // When a principal's access changes, we need to invalidate any subject cache that might include this principal.
        // Since subject cache keys include multiple principals, we use a more aggressive invalidation strategy.
        var tenantIdString = tenantId.ToString();
        if (_tenantCacheKeys.TryGetValue(tenantIdString, out var keys))
        {
            // Look for any cache key containing this resource and potentially this principal
            var resourcePattern = $":{resourceType}:{resourceId}:";
            var keysToRemove = keys.Where(k => k.Contains(resourcePattern, StringComparison.OrdinalIgnoreCase)).ToList();

            lock (keys)
            {
                foreach (var key in keysToRemove)
                {
                    _l1Cache.Remove(key);
                    _metrics?.RecordEviction(CacheLevel.L1, CacheType);
                    keys.Remove(key);
                }
            }
        }
    }

    private void InvalidateUserResourceCache(Guid userId, Guid tenantId, string resourceType, string resourceId)
    {
        // Since we include version in the cache key, the cache will naturally become invalid
        // when the version is incremented by the underlying service.
        // However, we can also proactively remove known keys.
        var tenantIdString = tenantId.ToString();
        if (_tenantCacheKeys.TryGetValue(tenantIdString, out var keys))
        {
            var pattern = $"acl:{tenantId}:{userId}:{resourceType}:{resourceId}:";
            var keysToRemove = keys.Where(k => k.StartsWith(pattern, StringComparison.OrdinalIgnoreCase)).ToList();

            lock (keys)
            {
                foreach (var key in keysToRemove)
                {
                    _l1Cache.Remove(key);
                    _metrics?.RecordEviction(CacheLevel.L1, CacheType);
                    keys.Remove(key);
                }
            }
        }
    }
}
