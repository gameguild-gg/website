using GameGuild.Configuration.PresentationLayer.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization.Caching;

/// <summary>
///     Unified cache invalidation service for coordinating cache coherence across services.
/// </summary>
public interface ICacheInvalidationService
{
    /// <summary>
    ///     Invalidates all permission caches for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Invalidates permission caches for a specific user in a tenant.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Invalidates ACL caches for a specific resource.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="resourceType">The resource type.</param>
    /// <param name="resourceId">The resource ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidateResourceAsync(Guid tenantId, string resourceType, string resourceId, CancellationToken cancellationToken = default);

    /// <summary>Invalidates multiple cache targets with one version update and one distributed event.</summary>
    Task InvalidateBatchAsync(Guid tenantId, IReadOnlyCollection<CacheInvalidationTarget> targets);

    /// <summary>Invalidates multiple cache targets with one version update and one distributed event.</summary>
    Task InvalidateBatchAsync(Guid tenantId, IReadOnlyCollection<CacheInvalidationTarget> targets, CancellationToken cancellationToken);

    /// <summary>Invalidates ACL cache entries across tenants after a global role or permission change.</summary>
    Task InvalidateGlobalAsync();

    /// <summary>Invalidates ACL cache entries across tenants after a global role or permission change.</summary>
    Task InvalidateGlobalAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Invalidates policy caches for a tenant.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="policyName">Optional specific policy name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InvalidatePolicyAsync(Guid tenantId, string? policyName = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Publishes an invalidation event for distributed cache coherence.
    /// </summary>
    /// <param name="invalidationEvent">The invalidation event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishInvalidationAsync(CacheInvalidationEvent invalidationEvent, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Handles an invalidation event received from another instance.
    /// </summary>
    /// <param name="invalidationEvent">The invalidation event.</param>
    void HandleInvalidationEvent(CacheInvalidationEvent invalidationEvent);
}

/// <summary>
///     Event representing a cache invalidation request.
/// </summary>
public sealed class CacheInvalidationEvent
{
    /// <summary>
    ///     The type of invalidation.
    /// </summary>
    public CacheInvalidationType Type { get; set; }

    /// <summary>
    ///     The tenant ID.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    ///     Optional user ID (for user-specific invalidation).
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    ///     Optional resource type (for resource-specific invalidation).
    /// </summary>
    public string? ResourceType { get; set; }

    /// <summary>
    ///     Optional resource ID (for resource-specific invalidation).
    /// </summary>
    public string? ResourceId { get; set; }

    /// <summary>
    ///     Optional policy name (for policy-specific invalidation).
    /// </summary>
    public string? PolicyName { get; set; }

    /// <summary>Targets carried by a batch invalidation event.</summary>
    public List<CacheInvalidationTarget> Targets { get; set; } = [];

    /// <summary>
    ///     Timestamp of the invalidation event.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    ///     Instance ID that originated the event (to avoid self-handling).
    /// </summary>
    public string OriginInstanceId { get; set; } = string.Empty;
}

/// <summary>One user, resource, policy, or role/group dependency in a batch invalidation.</summary>
public sealed record CacheInvalidationTarget(
    CacheInvalidationTargetType Type,
    Guid? UserId = null,
    string? ResourceType = null,
    string? ResourceId = null,
    string? PolicyName = null,
    string? DependencyKind = null,
    Guid? DependencyId = null);

/// <summary>Supported target kinds within a batch invalidation.</summary>
public enum CacheInvalidationTargetType
{
    User,
    Resource,
    Policy,
    Dependency
}

/// <summary>
///     Types of cache invalidation.
/// </summary>
public enum CacheInvalidationType
{
    /// <summary>
    ///     Invalidate all caches for a tenant.
    /// </summary>
    Tenant,

    /// <summary>
    ///     Invalidate caches for a specific user.
    /// </summary>
    User,

    /// <summary>
    ///     Invalidate caches for a specific resource.
    /// </summary>
    Resource,

    /// <summary>
    ///     Invalidate policy caches.
    /// </summary>
    Policy,

    /// <summary>Invalidate multiple dependent keys in one distributed event.</summary>
    Batch,

    /// <summary>Invalidate ACL cache entries across every tenant.</summary>
    Global
}

/// <summary>
///     Default implementation of <see cref="ICacheInvalidationService"/>.
/// </summary>
public sealed class CacheInvalidationService : ICacheInvalidationService
{
    private static readonly string InstanceId = Guid.NewGuid().ToString("N")[..8];
    private readonly string _instanceId = InstanceId;

    private readonly ITenantSecurityVersionStore _versionStore;
    private readonly IHybridPermissionCache _hybridCache;
    private readonly ICacheMetricsService _metrics;
    private readonly AuthorizationCacheOptions _options;
    private readonly ILogger<CacheInvalidationService> _logger;
    private readonly ICacheInvalidationPublisher? _invalidationPublisher;
    private readonly IPermissionCacheKeyTracker _keyTracker;

    /// <summary>
    ///     Initializes a new instance of <see cref="CacheInvalidationService"/>.
    /// </summary>
    public CacheInvalidationService(
        IMemoryCache memoryCache,
        ITenantSecurityVersionStore versionStore,
        IHybridPermissionCache hybridCache,
        ICacheMetricsService metrics,
        IOptions<AuthorizationCacheOptions> options,
        ILogger<CacheInvalidationService> logger)
        : this(memoryCache, versionStore, hybridCache, metrics, options, logger, null, null)
    {
    }

    public CacheInvalidationService(
        IMemoryCache memoryCache,
        ITenantSecurityVersionStore versionStore,
        IHybridPermissionCache hybridCache,
        ICacheMetricsService metrics,
        IOptions<AuthorizationCacheOptions> options,
        ILogger<CacheInvalidationService> logger,
        ICacheInvalidationPublisher? invalidationPublisher)
        : this(memoryCache, versionStore, hybridCache, metrics, options, logger, invalidationPublisher, null)
    {
    }

    public CacheInvalidationService(
        IMemoryCache memoryCache,
        ITenantSecurityVersionStore versionStore,
        IHybridPermissionCache hybridCache,
        ICacheMetricsService metrics,
        IOptions<AuthorizationCacheOptions> options,
        ILogger<CacheInvalidationService> logger,
        IPermissionCacheKeyTracker keyTracker)
        : this(memoryCache, versionStore, hybridCache, metrics, options, logger, null, keyTracker)
    {
    }

    public CacheInvalidationService(
        IMemoryCache memoryCache,
        ITenantSecurityVersionStore versionStore,
        IHybridPermissionCache hybridCache,
        ICacheMetricsService metrics,
        IOptions<AuthorizationCacheOptions> options,
        ILogger<CacheInvalidationService> logger,
        ICacheInvalidationPublisher? invalidationPublisher,
        IPermissionCacheKeyTracker? keyTracker)
    {
        _versionStore = versionStore;
        _hybridCache = hybridCache;
        _metrics = metrics;
        _options = options.Value;
        _logger = logger;
        _invalidationPublisher = invalidationPublisher;
        _keyTracker = keyTracker ?? new PermissionCacheKeyTracker(memoryCache, metrics);
    }

    /// <inheritdoc />
    public async Task InvalidateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Invalidating all caches for tenant {TenantId}", tenantId);

        // Increment version to invalidate version-keyed caches
        await _versionStore.IncrementVersionAsync(tenantId.ToString(), cancellationToken).ConfigureAwait(false);

        // Clear tracked keys for this tenant
        ClearTenantKeys(tenantId);

        // Publish event for distributed invalidation
        await PublishInvalidationAsync(new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.Tenant,
            TenantId = tenantId,
            OriginInstanceId = _instanceId
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task InvalidateUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Invalidating caches for user {UserId} in tenant {TenantId}", userId, tenantId);

        // Advance the shared tenant version before local or distributed best-effort cleanup.
        await _versionStore.IncrementVersionAsync(tenantId.ToString(), cancellationToken).ConfigureAwait(false);

        // Remove specific user cache entries
        var keyPattern = $"perm:{tenantId}:{userId}:";
        await _hybridCache.InvalidatePatternAsync(keyPattern, "permission", cancellationToken).ConfigureAwait(false);
        _keyTracker.InvalidatePattern($"acl:{tenantId}:{userId}:", "acl", "user_invalidation");
        _keyTracker.InvalidatePattern($"acl:subj:{tenantId}:{userId}:", "acl", "user_invalidation");

        await PublishInvalidationAsync(new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.User,
            TenantId = tenantId,
            UserId = userId,
            OriginInstanceId = _instanceId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task InvalidateResourceAsync(Guid tenantId, string resourceType, string resourceId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Invalidating ACL caches for resource {ResourceType}:{ResourceId} in tenant {TenantId}", 
            resourceType, resourceId, tenantId);

        // ACL cache keys include the tenant security version. Advancing it guarantees stale
        // entries are bypassed even when a Redis invalidation message cannot be delivered.
        await _versionStore.IncrementVersionAsync(tenantId.ToString(), cancellationToken).ConfigureAwait(false);

        // ACL keys length-prefix the free-form resource segments (see AclCacheKeys), so the
        // pattern must match the length-prefixed type/ID pair rather than bare raw values.
        var keyPattern = $"acl:{tenantId}:*:{AclCacheKeys.BuildResourceSegment(resourceType, resourceId)}:";
        await _hybridCache.InvalidatePatternAsync(keyPattern, "acl", cancellationToken).ConfigureAwait(false);

        await PublishInvalidationAsync(new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.Resource,
            TenantId = tenantId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            OriginInstanceId = _instanceId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task InvalidateBatchAsync(Guid tenantId, IReadOnlyCollection<CacheInvalidationTarget> targets)
    {
        return InvalidateBatchAsync(tenantId, targets, CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task InvalidateBatchAsync(
        Guid tenantId,
        IReadOnlyCollection<CacheInvalidationTarget> targets,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant ID is required for batch invalidation.", nameof(tenantId));
        }

        if (!IsValidBatchTargets(targets))
        {
            throw new ArgumentException("Batch invalidation requires 1 to 500 valid targets.", nameof(targets));
        }

        var targetSnapshot = targets.ToList();
        await _versionStore.IncrementVersionAsync(tenantId.ToString(), cancellationToken).ConfigureAwait(false);
        InvalidateTargets(tenantId, targetSnapshot);

        await PublishInvalidationAsync(new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.Batch,
            TenantId = tenantId,
            Targets = targetSnapshot,
            OriginInstanceId = _instanceId
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task InvalidateGlobalAsync() => InvalidateGlobalAsync(CancellationToken.None);

    /// <inheritdoc />
    public async Task InvalidateGlobalAsync(CancellationToken cancellationToken)
    {
        // Guid.Empty is reserved as the shared global ACL cache-version scope. It has no tenant FK.
        await _versionStore.IncrementVersionAsync(Guid.Empty.ToString(), cancellationToken).ConfigureAwait(false);
        ClearAllAclKeys();

        await PublishInvalidationAsync(new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.Global,
            TenantId = Guid.Empty,
            OriginInstanceId = _instanceId
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task InvalidatePolicyAsync(Guid tenantId, string? policyName = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Invalidating policy caches for tenant {TenantId}, policy {PolicyName}", tenantId, policyName ?? "all");

        // Policy cache keys include the tenant security version. Advance it before cleanup so
        // existing L1/L2 entries cannot be selected while invalidation is being propagated.
        await _versionStore.IncrementVersionAsync(tenantId.ToString(), cancellationToken).ConfigureAwait(false);

        var keyPattern = policyName != null 
            ? $"policy:{tenantId}:{policyName}:" 
            : $"policy:{tenantId}:";
        
        await _hybridCache.InvalidatePatternAsync(keyPattern, "policy", cancellationToken).ConfigureAwait(false);

        await PublishInvalidationAsync(new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.Policy,
            TenantId = tenantId,
            PolicyName = policyName,
            OriginInstanceId = _instanceId
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task PublishInvalidationAsync(
        CacheInvalidationEvent invalidationEvent,
        CancellationToken cancellationToken = default)
    {
        if (!_options.UseDistributedCache || !_options.UsePubSubInvalidation)
        {
            // No distributed cache or pub/sub disabled
            return;
        }

        if (_invalidationPublisher is null)
        {
            _logger.LogWarning(
                "Distributed permission cache invalidation is enabled but no Redis publisher is registered; relying on versioned cache keys and TTL");
            return;
        }

        await _invalidationPublisher.PublishAsync(invalidationEvent, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void HandleInvalidationEvent(CacheInvalidationEvent invalidationEvent)
    {
        // Skip if this is our own event
        if (invalidationEvent.OriginInstanceId == _instanceId)
        {
            return;
        }

        _logger.LogDebug(
            "Handling invalidation event from instance {Instance}: Type={Type}, TenantId={TenantId}",
            invalidationEvent.OriginInstanceId,
            invalidationEvent.Type,
            invalidationEvent.TenantId);

        switch (invalidationEvent.Type)
        {
            case CacheInvalidationType.Tenant:
                ClearTenantKeys(invalidationEvent.TenantId);
                break;

            case CacheInvalidationType.User:
                // Clear user-specific keys from L1
                if (invalidationEvent.UserId.HasValue)
                {
                    var userPattern = $"perm:{invalidationEvent.TenantId}:{invalidationEvent.UserId}:";
                    ClearKeysMatchingPattern(invalidationEvent.TenantId, userPattern);
                    ClearKeysMatchingPattern(
                        invalidationEvent.TenantId,
                        $"acl:{invalidationEvent.TenantId}:{invalidationEvent.UserId}:");
                    ClearKeysMatchingPattern(
                        invalidationEvent.TenantId,
                        $"acl:subj:{invalidationEvent.TenantId}:{invalidationEvent.UserId}:");
                }
                break;

            case CacheInvalidationType.Resource:
                if (!string.IsNullOrEmpty(invalidationEvent.ResourceType) && !string.IsNullOrEmpty(invalidationEvent.ResourceId))
                {
                    // ACL keys length-prefix the resource segments; match the encoded pair.
                    var resourcePattern = $":{AclCacheKeys.BuildResourceSegment(invalidationEvent.ResourceType, invalidationEvent.ResourceId)}:";
                    ClearKeysMatchingPattern(invalidationEvent.TenantId, resourcePattern);
                }
                break;

            case CacheInvalidationType.Policy:
                var policyPattern = invalidationEvent.PolicyName != null
                    ? $"policy:{invalidationEvent.TenantId}:{invalidationEvent.PolicyName}:"
                    : $"policy:{invalidationEvent.TenantId}:";
                ClearKeysMatchingPattern(invalidationEvent.TenantId, policyPattern);
                break;

            case CacheInvalidationType.Batch:
                if (!IsValidBatchTargets(invalidationEvent.Targets))
                {
                    _logger.LogWarning("Ignoring permission cache batch invalidation with invalid targets for tenant {TenantId}",
                        invalidationEvent.TenantId);
                    return;
                }

                InvalidateTargets(invalidationEvent.TenantId, invalidationEvent.Targets);
                break;

            case CacheInvalidationType.Global:
                ClearAllAclKeys();
                break;
        }
    }

    private void ClearAllAclKeys()
    {
        _keyTracker.Invalidate(
            key => key.StartsWith("acl:", StringComparison.OrdinalIgnoreCase),
            "acl",
            "global_invalidation");
    }

    internal static bool IsValidBatchTargets(IReadOnlyCollection<CacheInvalidationTarget>? targets)
    {
        return targets is { Count: > 0 and <= 500 } && targets.All(target => target is not null && (target.Type switch
        {
            CacheInvalidationTargetType.User => target.UserId is { } userId && userId != Guid.Empty,
            CacheInvalidationTargetType.Resource =>
                !string.IsNullOrWhiteSpace(target.ResourceType) && target.ResourceType.Length <= 128 &&
                !string.IsNullOrWhiteSpace(target.ResourceId) && target.ResourceId.Length <= 255,
            CacheInvalidationTargetType.Policy => target.PolicyName is null ||
                (!string.IsNullOrWhiteSpace(target.PolicyName) && target.PolicyName.Length <= 200),
            CacheInvalidationTargetType.Dependency =>
                (string.Equals(target.DependencyKind, "role", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(target.DependencyKind, "group", StringComparison.OrdinalIgnoreCase)) &&
                target.DependencyId is { } dependencyId && dependencyId != Guid.Empty,
            _ => false
        }));
    }

    private void InvalidateTargets(Guid tenantId, IReadOnlyCollection<CacheInvalidationTarget> targets)
    {
        _keyTracker.Invalidate(
            key => targets.Any(target => MatchesTarget(key, tenantId, target)),
            "batch",
            "batch_invalidation");
    }

    private static bool MatchesTarget(string cacheKey, Guid tenantId, CacheInvalidationTarget target)
    {
        var tenant = tenantId.ToString();
        var keySegments = cacheKey.Split(new[] { ':', '|' }, StringSplitOptions.RemoveEmptyEntries);
        if (!keySegments.Any(segment => segment.Equals(tenant, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return target.Type switch
        {
            CacheInvalidationTargetType.User when target.UserId.HasValue =>
                cacheKey.StartsWith($"perm:{tenant}:{target.UserId}:", StringComparison.OrdinalIgnoreCase) ||
                cacheKey.StartsWith($"acl:{tenant}:{target.UserId}:", StringComparison.OrdinalIgnoreCase) ||
                cacheKey.StartsWith($"acl:subj:{tenant}:{target.UserId}:", StringComparison.OrdinalIgnoreCase),
            CacheInvalidationTargetType.Resource when target.ResourceType is not null && target.ResourceId is not null =>
                cacheKey.Contains($":{AclCacheKeys.BuildResourceSegment(target.ResourceType, target.ResourceId)}:", StringComparison.OrdinalIgnoreCase),
            CacheInvalidationTargetType.Policy => MatchesPolicy(cacheKey, tenant, target.PolicyName),
            CacheInvalidationTargetType.Dependency => MatchesAclDependency(cacheKey, tenant, target.DependencyKind, target.DependencyId),
            _ => false
        };
    }

    private static bool MatchesPolicy(string cacheKey, string tenant, string? policyName)
    {
        if (!cacheKey.StartsWith("policy:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (policyName is null)
        {
            return true;
        }

        return cacheKey.Contains($"policy:{tenant}:{policyName}:", StringComparison.OrdinalIgnoreCase) ||
               cacheKey.Contains($"policy:{policyName}:{tenant}:", StringComparison.OrdinalIgnoreCase) ||
               cacheKey.Contains($"policy:{policyName}|{tenant}|", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesAclDependency(string cacheKey, string tenant, string? dependencyKind, Guid? dependencyId)
    {
        if (dependencyId is null || !cacheKey.StartsWith($"acl:subj:{tenant}:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var keySegments = cacheKey.Split(':');
        var dependencySegmentIndex = string.Equals(dependencyKind, "role", StringComparison.OrdinalIgnoreCase) ? 4
            : string.Equals(dependencyKind, "group", StringComparison.OrdinalIgnoreCase) ? 5
            : -1;
        return dependencySegmentIndex >= 0 && keySegments.Length > dependencySegmentIndex &&
               keySegments[dependencySegmentIndex].Split(',')
                   .Contains(dependencyId.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Registers a cache key for tracking (enables efficient invalidation).
    /// </summary>
    public void TrackKey(Guid tenantId, string cacheKey)
    {
        _keyTracker.Track(cacheKey, "all");
    }

    private void ClearTenantKeys(Guid tenantId)
    {
        var tenant = tenantId.ToString();
        _keyTracker.Invalidate(
            key => key.Split(new[] { ':', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment.Equals(tenant, StringComparison.OrdinalIgnoreCase)),
            "all",
            "tenant_invalidation");
    }

    private void ClearKeysMatchingPattern(Guid tenantId, string pattern)
    {
        var tenant = tenantId.ToString();
        _keyTracker.Invalidate(
            key => key.Contains(tenant, StringComparison.OrdinalIgnoreCase) &&
                   key.Contains(pattern, StringComparison.OrdinalIgnoreCase),
            "pattern",
            "pattern_invalidation");
    }
}
