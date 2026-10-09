using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Caching;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Backward-compatible three-layer permission facade used by legacy authentication surfaces.
///     New request handlers should prefer the focused Authorization module services.
/// </summary>
/// <remarks>
///     <para>
///         <b>SECURITY:</b> every mutation path bumps the tenant security version (cache
///         invalidation) and writes a permission audit entry. The invalidation/audit
///         collaborators are optional so legacy construction sites keep working, but the
///         composition root always supplies them; when they are absent the mutation still
///         throws on failure to keep the fail-loud behavior of the Authorization module.
///     </para>
/// </remarks>
public class PermissionService(
    IApplicationDbContext context,
    ITenantSecurityVersionStore? securityVersionStore = null,
    IPermissionAuditService? auditService = null,
    IActorContextAccessor? actorContextAccessor = null,
    IHybridPermissionCache? hybridPermissionCache = null) : IPermissionService
{
    private static readonly Meter BulkPermissionCheckMeter = new("GameGuild.Identity.Authentication.PermissionBulkCheck", "1.0.0");
    private static readonly Counter<long> BulkPermissionCheckBatchCounter = BulkPermissionCheckMeter.CreateCounter<long>(
        "permission_bulk_check_batches",
        "batches",
        "Successfully evaluated bulk permission check batches");
    private static readonly Counter<long> BulkPermissionCheckRequestCounter = BulkPermissionCheckMeter.CreateCounter<long>(
        "permission_bulk_check_requests",
        "requests",
        "Permission decisions evaluated in bulk batches");
    private static readonly Counter<long> BulkPermissionCheckUniqueRequestCounter = BulkPermissionCheckMeter.CreateCounter<long>(
        "permission_bulk_check_unique_requests",
        "requests",
        "Distinct permission decisions evaluated in bulk batches");
    private static readonly Histogram<double> BulkPermissionCheckDuration = BulkPermissionCheckMeter.CreateHistogram<double>(
        "permission_bulk_check_batch_duration",
        "ms",
        "Elapsed time for bulk permission batch reads and evaluation");

    private const int MaximumBulkPermissionCheckBatchSize = 256;
    private const int MinimumParallelBulkPermissionCheckBatchSize = 32;
    private const int MaximumParallelBulkPermissionChecks = 4;
    private const int MaximumBulkPermissionCacheVersionRetries = 3;
    private const string BulkPermissionCacheType = "permission";

    public async Task<TenantPermission> GrantTenantPermissionAsync(Guid? userId, Guid? tenantId, PermissionType[] permissions)
    {
        var grant = await GetTenantGrantAsync(userId, tenantId).ConfigureAwait(false);
        if (grant == null)
        {
            grant = new TenantPermission
            {
                UserId = userId,
                TenantId = tenantId,
                GrantedAt = SystemClock.UtcNow
            };
            context.Set<TenantPermission>().Add(grant);
        }

        grant.AddPermissions(ToPermissionNames(permissions));
        grant.IsActive = true;
        grant.ExpiresAt = null;

        await context.SaveChangesAsync().ConfigureAwait(false);

        await TrackMutationAsync(
            PermissionOperationType.Grant,
            userId,
            tenantId,
            ToPermissionNames(permissions)).ConfigureAwait(false);

        return grant;
    }

    public async Task<List<TenantPermission>> BulkGrantTenantPermissionAsync(Guid[] userIds, Guid tenantId, PermissionType[] permissions)
    {
        var results = new List<TenantPermission>();
        foreach (var userId in userIds.Where(id => id != Guid.Empty).Distinct())
        {
            results.Add(await GrantTenantPermissionAsync(userId, tenantId, permissions).ConfigureAwait(false));
        }

        return results;
    }

    public Task<bool> HasTenantPermissionAsync(Guid? userId, Guid? tenantId, PermissionType permission)
        => CheckPermissionAsync(userId, tenantId, permission);

    public async Task<IEnumerable<PermissionType>> GetTenantPermissionsAsync(Guid? userId, Guid? tenantId)
    {
        var grant = await GetTenantGrantAsync(userId, tenantId).ConfigureAwait(false);
        return grant == null || grant.IsExpired() || !grant.IsActive
            ? []
            : ToPermissionTypes(grant.Permissions);
    }

    public async Task<IEnumerable<PermissionType>> GetGlobalDefaultPermissionsAsync()
        => await GetTenantPermissionsAsync(null, null).ConfigureAwait(false);

    public async Task SetGlobalDefaultPermissionsAsync(PermissionType[] permissions)
    {
        await SetTenantGrantAsync(null, null, permissions, "Global default permissions").ConfigureAwait(false);
    }

    public async Task<IEnumerable<PermissionType>> GetTenantDefaultPermissionsAsync(Guid tenantId)
        => await GetTenantPermissionsAsync(null, tenantId).ConfigureAwait(false);

    public async Task SetTenantDefaultPermissionsAsync(Guid tenantId, PermissionType[] permissions)
    {
        await SetTenantGrantAsync(null, tenantId, permissions, "Tenant default permissions").ConfigureAwait(false);
    }

    public async Task RevokeTenantPermissionAsync(Guid? userId, Guid? tenantId, PermissionType[] permissions)
    {
        var grant = await GetTenantGrantAsync(userId, tenantId).ConfigureAwait(false);
        if (grant == null)
        {
            return;
        }

        grant.RemovePermissions(ToPermissionNames(permissions));
        if (grant.Permissions.Length == 0)
        {
            grant.SoftDelete();
            context.Set<TenantPermission>().Update(grant);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        await TrackMutationAsync(
            PermissionOperationType.Revoke,
            userId,
            tenantId,
            ToPermissionNames(permissions)).ConfigureAwait(false);
    }

    public async Task<TenantPermission> JoinTenantAsync(Guid userId, Guid tenantId)
    {
        var defaults = (await GetTenantDefaultPermissionsAsync(tenantId).ConfigureAwait(false)).ToArray();
        if (defaults.Length == 0)
        {
            defaults = [PermissionType.Read];
        }

        return await GrantTenantPermissionAsync(userId, tenantId, defaults).ConfigureAwait(false);
    }

    public async Task LeaveTenantAsync(Guid userId, Guid tenantId)
    {
        var directPermissions = (await GetTenantPermissionsAsync(userId, tenantId).ConfigureAwait(false)).ToArray();
        if (directPermissions.Length > 0)
        {
            await RevokeTenantPermissionAsync(userId, tenantId, directPermissions).ConfigureAwait(false);
        }
    }

    public async Task<bool> IsUserInTenantAsync(Guid userId, Guid tenantId)
    {
        var grant = await GetTenantGrantAsync(userId, tenantId).ConfigureAwait(false);
        return grant is { IsActive: true } && !grant.IsExpired();
    }

    public async Task<IEnumerable<TenantPermission>> GetUserTenantsAsync(Guid userId)
    {
        var grants = await context.Set<TenantPermission>()
            .Where(p => p.UserId == userId && p.TenantId != null && p.IsActive)
            .ToListAsync()
            .ConfigureAwait(false);

        return grants.Where(p => !p.IsExpired());
    }

    public async Task<IEnumerable<PermissionType>> GetEffectiveTenantPermissionsAsync(Guid? userId, Guid? tenantId)
    {
        var permissions = new HashSet<PermissionType>();
        AddRange(permissions, await GetGlobalDefaultPermissionsAsync().ConfigureAwait(false));

        if (tenantId.HasValue)
        {
            AddRange(permissions, await GetTenantDefaultPermissionsAsync(tenantId.Value).ConfigureAwait(false));
        }

        if (userId.HasValue)
        {
            AddRange(permissions, await GetTenantPermissionsAsync(userId, tenantId).ConfigureAwait(false));
        }

        return permissions;
    }

    public async Task<ContentTypePermission> GrantContentTypePermissionAsync(Guid? userId, Guid? tenantId, string contentTypeName, PermissionType[] permissions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentTypeName);

        var grant = await GetContentTypeGrantAsync(userId, tenantId, contentTypeName).ConfigureAwait(false);
        if (grant == null)
        {
            grant = new ContentTypePermission(userId, tenantId, contentTypeName);
            context.Set<ContentTypePermission>().Add(grant);
        }

        grant.AddPermissions(permissions);
        grant.IsActive = true;
        grant.ExpiresAt = null;

        await context.SaveChangesAsync().ConfigureAwait(false);

        await TrackMutationAsync(
            PermissionOperationType.Grant,
            userId,
            tenantId,
            permissions.Select(p => p.ToString()),
            contentTypeName).ConfigureAwait(false);

        return grant;
    }

    public Task<bool> HasContentTypePermissionAsync(Guid? userId, Guid? tenantId, string contentTypeName, PermissionType permission)
        => CheckPermissionAsync(userId, tenantId, permission, contentTypeName);

    public async Task<IEnumerable<PermissionType>> GetContentTypePermissionsAsync(Guid? userId, Guid? tenantId, string contentTypeName)
    {
        var grant = await GetContentTypeGrantAsync(userId, tenantId, contentTypeName).ConfigureAwait(false);
        return grant == null || !grant.IsEffective()
            ? []
            : grant.GetPermissionsAsEnum();
    }

    public async Task RevokeContentTypePermissionAsync(Guid? userId, Guid? tenantId, string contentTypeName, PermissionType[] permissions)
    {
        var grant = await GetContentTypeGrantAsync(userId, tenantId, contentTypeName).ConfigureAwait(false);
        if (grant == null)
        {
            return;
        }

        grant.RemovePermissions(permissions);
        if (!grant.GetPermissionsAsEnum().Any())
        {
            context.Set<ContentTypePermission>().Remove(grant);
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        await TrackMutationAsync(
            PermissionOperationType.Revoke,
            userId,
            tenantId,
            permissions.Select(p => p.ToString()),
            contentTypeName).ConfigureAwait(false);
    }

    public async Task<IEnumerable<PermissionType>> GetEffectiveContentTypePermissionsAsync(Guid? userId, Guid? tenantId, string contentTypeName)
    {
        var permissions = new HashSet<PermissionType>();
        AddRange(permissions, await GetEffectiveTenantPermissionsAsync(userId, tenantId).ConfigureAwait(false));
        AddRange(permissions, await GetContentTypePermissionsAsync(null, null, contentTypeName).ConfigureAwait(false));

        if (tenantId.HasValue)
        {
            AddRange(permissions, await GetContentTypePermissionsAsync(null, tenantId, contentTypeName).ConfigureAwait(false));
        }

        if (userId.HasValue)
        {
            AddRange(permissions, await GetContentTypePermissionsAsync(userId, tenantId, contentTypeName).ConfigureAwait(false));
        }

        return permissions;
    }

    public async Task<TPermission> GrantResourcePermissionAsync<TPermission, TResource>(Guid userId, Guid? tenantId, Guid resourceId, PermissionType[] permissions)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase
    {
        var grant = await GetResourceGrantAsync<TPermission, TResource>(userId, tenantId, resourceId).ConfigureAwait(false);
        if (grant == null)
        {
            grant = new TPermission
            {
                UserId = userId,
                TenantId = tenantId,
                ResourceId = resourceId,
                ResourceType = typeof(TResource).Name,
                GrantedAt = SystemClock.UtcNow
            };
            context.Set<TPermission>().Add(grant);
        }

        grant.AddPermissions(permissions);
        grant.IsActive = true;
        grant.ExpiresAt = null;

        await context.SaveChangesAsync().ConfigureAwait(false);

        await TrackMutationAsync(
            PermissionOperationType.Grant,
            userId,
            tenantId,
            permissions.Select(p => p.ToString()),
            resourceTypeName: typeof(TResource).Name,
            resourceId: resourceId).ConfigureAwait(false);

        return grant;
    }

    public Task<bool> HasResourcePermissionAsync<TPermission, TResource>(Guid userId, Guid? tenantId, Guid resourceId, PermissionType permission)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase
        => HasResourcePermissionWithTenantDenyAsync<TPermission, TResource>(userId, tenantId, resourceId, permission);

    public async Task<IEnumerable<PermissionType>> GetResourcePermissionsAsync<TPermission, TResource>(Guid? userId, Guid? tenantId, Guid resourceId)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase
    {
        if (!userId.HasValue)
        {
            return [];
        }

        var grant = await GetResourceGrantAsync<TPermission, TResource>(userId.Value, tenantId, resourceId).ConfigureAwait(false);
        return grant == null || !grant.IsEffective()
            ? []
            : grant.GetPermissionsAsEnum();
    }

    public async Task BulkGrantResourcePermissionAsync<TPermission, TResource>(Guid userId, Guid? tenantId, Guid[] resourceIds, PermissionType[] permissions)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase
    {
        foreach (var resourceId in resourceIds.Where(id => id != Guid.Empty).Distinct())
        {
            await GrantResourcePermissionAsync<TPermission, TResource>(userId, tenantId, resourceId, permissions).ConfigureAwait(false);
        }
    }

    public async Task<Dictionary<Guid, IEnumerable<PermissionType>>> GetBulkResourcePermissionsAsync<TPermission, TResource>(Guid? userId, Guid? tenantId, Guid[] resourceIds)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase
    {
        var results = new Dictionary<Guid, IEnumerable<PermissionType>>();
        foreach (var resourceId in resourceIds.Distinct())
        {
            results[resourceId] = await GetResourcePermissionsAsync<TPermission, TResource>(userId, tenantId, resourceId).ConfigureAwait(false);
        }

        return results;
    }

    public async Task ShareResourceAsync<TPermission, TResource>(Guid resourceId, Guid targetUserId, Guid? tenantId, PermissionType[] permissions, DateTime? expiresAt = null)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase
    {
        var grant = await GrantResourcePermissionAsync<TPermission, TResource>(targetUserId, tenantId, resourceId, permissions).ConfigureAwait(false);
        grant.ExpiresAt = expiresAt;
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task RevokeResourceAccessAsync<TPermission, TResource>(Guid userId, Guid? tenantId, Guid resourceId)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase
    {
        var grant = await GetResourceGrantAsync<TPermission, TResource>(userId, tenantId, resourceId).ConfigureAwait(false);
        if (grant == null)
        {
            return;
        }

        context.Set<TPermission>().Remove(grant);
        await context.SaveChangesAsync().ConfigureAwait(false);

        await TrackMutationAsync(
            PermissionOperationType.Revoke,
            userId,
            tenantId,
            [],
            resourceTypeName: typeof(TResource).Name,
            resourceId: resourceId).ConfigureAwait(false);
    }

    public async Task<IEnumerable<PermissionType>> GetEffectiveResourcePermissionsAsync<TPermission, TResource>(Guid? userId, Guid? tenantId, Guid resourceId, string? contentTypeName = null)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase
    {
        var permissions = new HashSet<PermissionType>();
        if (!string.IsNullOrWhiteSpace(contentTypeName))
        {
            AddRange(permissions, await GetEffectiveContentTypePermissionsAsync(userId, tenantId, contentTypeName).ConfigureAwait(false));
        }
        else
        {
            AddRange(permissions, await GetEffectiveTenantPermissionsAsync(userId, tenantId).ConfigureAwait(false));
        }

        AddRange(permissions, await GetResourcePermissionsAsync<TPermission, TResource>(userId, tenantId, resourceId).ConfigureAwait(false));
        return permissions;
    }

    public async Task<IEnumerable<PermissionType>> ResolveEffectivePermissionsAsync(Guid? userId, Guid? tenantId, string? contentTypeName = null, Guid? resourceId = null, string? resourceTypeName = null)
    {
        var permissions = new HashSet<PermissionType>();
        if (!string.IsNullOrWhiteSpace(contentTypeName))
        {
            AddRange(permissions, await GetEffectiveContentTypePermissionsAsync(userId, tenantId, contentTypeName).ConfigureAwait(false));
        }
        else
        {
            AddRange(permissions, await GetEffectiveTenantPermissionsAsync(userId, tenantId).ConfigureAwait(false));
        }

        if (userId.HasValue && resourceId.HasValue)
        {
            var query = context.Set<GenericResourcePermission>()
                .Where(p => p.UserId == userId && p.TenantId == tenantId && p.ResourceId == resourceId && p.IsActive);

            if (!string.IsNullOrWhiteSpace(resourceTypeName))
            {
                query = query.Where(p => p.ResourceType == resourceTypeName);
            }

            var resourceGrant = await query.FirstOrDefaultAsync().ConfigureAwait(false);
            if (resourceGrant is { } grant && grant.IsEffective())
            {
                AddRange(permissions, grant.GetPermissionsAsEnum());
            }
        }

        return permissions;
    }

    public async Task<bool> HasPermissionAsync(Guid? userId, Guid? tenantId, PermissionType permission, string? contentTypeName = null, Guid? resourceId = null, string? resourceTypeName = null)
        => await CheckPermissionAsync(userId, tenantId, permission, contentTypeName, resourceId, resourceTypeName).ConfigureAwait(false);

    public async Task<string> GetPermissionSourceAsync(Guid? userId, Guid? tenantId, PermissionType permission, string? contentTypeName = null, Guid? resourceId = null, string? resourceTypeName = null)
    {
        if (userId.HasValue && resourceId.HasValue)
        {
            var resourcePermissions = await GetResourcePermissionsAsync<GenericResourcePermission, EntityBase>(userId, tenantId, resourceId.Value).ConfigureAwait(false);
            if (resourcePermissions.Contains(permission))
            {
                return "Resource";
            }
        }

        if (!string.IsNullOrWhiteSpace(contentTypeName))
        {
            var contentPermissions = await GetContentTypePermissionsAsync(userId, tenantId, contentTypeName).ConfigureAwait(false);
            if (contentPermissions.Contains(permission))
            {
                return "ContentType";
            }
        }

        var tenantPermissions = await GetTenantPermissionsAsync(userId, tenantId).ConfigureAwait(false);
        if (tenantPermissions.Contains(permission))
        {
            return "Tenant";
        }

        if (tenantId.HasValue)
        {
            var tenantDefaults = await GetTenantDefaultPermissionsAsync(tenantId.Value).ConfigureAwait(false);
            if (tenantDefaults.Contains(permission))
            {
                return "TenantDefault";
            }
        }

        var globalDefaults = await GetGlobalDefaultPermissionsAsync().ConfigureAwait(false);
        return globalDefaults.Contains(permission) ? "GlobalDefault" : "None";
    }

    public async Task<IEnumerable<Guid>> GetUsersWithPermissionAsync(Guid tenantId, PermissionType permission)
    {
        var permissionName = permission.ToString();
        var grants = await context.Set<TenantPermission>()
            .Where(p => p.TenantId == tenantId && p.UserId != null && p.IsActive)
            .ToListAsync()
            .ConfigureAwait(false);

        return grants
            .Where(p => !p.IsExpired() && p.HasPermission(permissionName))
            .Select(p => p.UserId!.Value)
            .Distinct()
            .ToArray();
    }

    public async Task<IEnumerable<Guid>> GetResourcesWithPermissionAsync(Guid userId, Guid? tenantId, PermissionType permission, string? resourceTypeName = null)
    {
        var query = context.Set<GenericResourcePermission>()
            .Where(p => p.UserId == userId && p.TenantId == tenantId && p.IsActive);

        if (!string.IsNullOrWhiteSpace(resourceTypeName))
        {
            query = query.Where(p => p.ResourceType == resourceTypeName);
        }

        var grants = await query.ToListAsync().ConfigureAwait(false);
        return grants
            .Where(p => p.IsEffective() && p.HasPermission(permission))
            .Select(p => p.ResourceId)
            .Distinct()
            .ToArray();
    }

    public async Task<Dictionary<Guid, Dictionary<PermissionType, bool>>> BulkCheckPermissionsAsync(Guid[] userIds, Guid? tenantId, PermissionType[] permissions)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        ArgumentNullException.ThrowIfNull(permissions);

        var distinctUserIds = userIds.Distinct().ToArray();
        var distinctPermissions = permissions.Distinct().ToArray();
        var results = distinctUserIds.ToDictionary(
            userId => userId,
            _ => new Dictionary<PermissionType, bool>());

        if (distinctUserIds.Length == 0 || distinctPermissions.Length == 0)
        {
            return results;
        }

        if (securityVersionStore is not null && hybridPermissionCache is not null)
        {
            await foreach (var result in StreamBulkCheckPermissionsAsync(
                               EnumerateBulkPermissionRequests(distinctUserIds, tenantId, distinctPermissions, CancellationToken.None),
                               cancellationToken: CancellationToken.None).ConfigureAwait(false))
            {
                results[result.Request.UserId][result.Request.Permission] = result.IsGranted;
            }

            return results;
        }

        // Load global defaults, tenant defaults, and all requested users' direct grants
        // in one database round-trip instead of resolving every user/permission pair
        // independently (which previously produced O(users * permissions) queries).
        var grants = await context.Set<TenantPermission>()
            .AsNoTracking()
            .Where(grant =>
                grant.UserId == null && (grant.TenantId == null || grant.TenantId == tenantId) ||
                grant.UserId != null && grant.TenantId == tenantId && distinctUserIds.Contains(grant.UserId.Value))
            .ToListAsync()
            .ConfigureAwait(false);

        var activeGrants = grants.Where(grant => grant.IsActive && !grant.IsExpired()).ToArray();
        var defaultGrants = activeGrants.Where(grant => grant.UserId == null).ToArray();
        var defaultPermissions = new HashSet<PermissionType>(
            defaultGrants.SelectMany(grant => ToPermissionTypes(grant.Permissions)));
        var deniedDefaultPermissions = new HashSet<PermissionType>(
            defaultGrants.SelectMany(grant => ToPermissionTypes(grant.DenyPermissions)));
        var userPermissionsById = activeGrants
            .Where(grant => grant.UserId.HasValue)
            .GroupBy(grant => grant.UserId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (
                    Allowed: new HashSet<PermissionType>(
                        group.SelectMany(grant => ToPermissionTypes(grant.Permissions))),
                    Denied: new HashSet<PermissionType>(
                        group.SelectMany(grant => ToPermissionTypes(grant.DenyPermissions)))));

        foreach (var userId in distinctUserIds)
        {
            var effectivePermissions = new HashSet<PermissionType>(defaultPermissions);
            var deniedPermissions = new HashSet<PermissionType>(deniedDefaultPermissions);
            if (userPermissionsById.TryGetValue(userId, out var userPermissions))
            {
                effectivePermissions.UnionWith(userPermissions.Allowed);
                deniedPermissions.UnionWith(userPermissions.Denied);
            }

            // Explicit denies at any applicable layer override all matching allows.
            effectivePermissions.ExceptWith(deniedPermissions);

            foreach (var permission in distinctPermissions)
            {
                results[userId][permission] = effectivePermissions.Contains(permission);
            }
        }

        return results;
    }

    public Task<IReadOnlyList<BulkPermissionCheckResult>> BulkCheckPermissionsAsync(
        IReadOnlyCollection<BulkPermissionCheckRequest> requests)
    {
        return BulkCheckPermissionsAsync(requests, CancellationToken.None);
    }

    public async Task<IReadOnlyList<BulkPermissionCheckResult>> BulkCheckPermissionsAsync(
        IReadOnlyCollection<BulkPermissionCheckRequest> requests,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);

        var results = new List<BulkPermissionCheckResult>(requests.Count);
        await foreach (var result in StreamBulkCheckPermissionsAsync(
                           ToAsyncEnumerable(requests, cancellationToken),
                           MaximumBulkPermissionCheckBatchSize,
                           cancellationToken).ConfigureAwait(false))
        {
            results.Add(result);
        }

        return results;
    }

    public IAsyncEnumerable<BulkPermissionCheckResult> StreamBulkCheckPermissionsAsync(
        IAsyncEnumerable<BulkPermissionCheckRequest> requests)
    {
        return StreamBulkCheckPermissionsAsync(requests, 128, CancellationToken.None);
    }

    public IAsyncEnumerable<BulkPermissionCheckResult> StreamBulkCheckPermissionsAsync(
        IAsyncEnumerable<BulkPermissionCheckRequest> requests,
        int batchSize)
    {
        return StreamBulkCheckPermissionsAsync(requests, batchSize, CancellationToken.None);
    }

    public IAsyncEnumerable<BulkPermissionCheckResult> StreamBulkCheckPermissionsAsync(
        IAsyncEnumerable<BulkPermissionCheckRequest> requests,
        CancellationToken cancellationToken)
    {
        return StreamBulkCheckPermissionsAsync(requests, 128, cancellationToken);
    }

    public async IAsyncEnumerable<BulkPermissionCheckResult> StreamBulkCheckPermissionsAsync(
        IAsyncEnumerable<BulkPermissionCheckRequest> requests,
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (batchSize is < 1 or > MaximumBulkPermissionCheckBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                $"Batch size must be between 1 and {MaximumBulkPermissionCheckBatchSize}.");
        }

        var batch = new List<BulkPermissionCheckRequest>(batchSize);
        await foreach (var request in requests.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            ValidateBulkPermissionCheckRequest(request);
            batch.Add(request);
            if (batch.Count < batchSize)
            {
                continue;
            }

            var results = await EvaluateBulkPermissionCheckBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            foreach (var result in results)
            {
                yield return result;
            }

            batch.Clear();
        }

        if (batch.Count == 0)
        {
            yield break;
        }

        var finalResults = await EvaluateBulkPermissionCheckBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        foreach (var result in finalResults)
        {
            yield return result;
        }
    }

    private async Task<IReadOnlyList<BulkPermissionCheckResult>> EvaluateBulkPermissionCheckBatchAsync(
        IReadOnlyList<BulkPermissionCheckRequest> requests,
        CancellationToken cancellationToken,
        bool recordMetrics = true)
    {
        var startedAt = Stopwatch.GetTimestamp();

        var result = securityVersionStore is not null && hybridPermissionCache is not null
            ? await EvaluateBulkPermissionCheckBatchWithCacheAsync(requests, cancellationToken).ConfigureAwait(false)
            : await EvaluateBulkPermissionCheckBatchFromDatabaseAsync(requests, cancellationToken).ConfigureAwait(false);

        if (recordMetrics)
        {
            BulkPermissionCheckBatchCounter.Add(1);
            BulkPermissionCheckRequestCounter.Add(requests.Count);
            BulkPermissionCheckUniqueRequestCounter.Add(requests.Distinct().Count());
            BulkPermissionCheckDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }

        return result;
    }

    private async Task<IReadOnlyList<BulkPermissionCheckResult>> EvaluateBulkPermissionCheckBatchWithCacheAsync(
        IReadOnlyList<BulkPermissionCheckRequest> requests,
        CancellationToken cancellationToken)
    {
        var distinctRequests = requests.Distinct().ToArray();
        var tenantIds = distinctRequests
            .Select(request => request.TenantId ?? Guid.Empty)
            .Distinct()
            .ToArray();

        for (var attempt = 0; attempt < MaximumBulkPermissionCacheVersionRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var versionSnapshot = await securityVersionStore!
                .GetTenantAndGlobalVersionsAsync(tenantIds, cancellationToken)
                .ConfigureAwait(false);
            var cacheKeys = new Dictionary<BulkPermissionCheckRequest, string>(distinctRequests.Length);
            foreach (var request in distinctRequests)
            {
                var scopeId = request.TenantId ?? Guid.Empty;
                var versions = versionSnapshot.GetValueOrDefault(scopeId);
                cacheKeys.Add(request, BuildBulkPermissionCacheKey(request, versions));
            }

            var cacheResults = await hybridPermissionCache!
                .GetManyValuesAsync<bool>(cacheKeys.Values.ToArray(), BulkPermissionCacheType, cancellationToken)
                .ConfigureAwait(false);
            var decisions = new Dictionary<BulkPermissionCheckRequest, bool>(distinctRequests.Length);
            var cacheMisses = new List<BulkPermissionCheckRequest>();

            foreach (var request in distinctRequests)
            {
                var cacheKey = cacheKeys[request];
                if (cacheResults.TryGetValue(cacheKey, out var cached) && cached.Found)
                {
                    decisions[request] = cached.Value;
                }
                else
                {
                    cacheMisses.Add(request);
                }
            }

            IReadOnlyList<BulkPermissionCheckResult> databaseResults = [];
            if (cacheMisses.Count > 0)
            {
                databaseResults = await EvaluateBulkPermissionCheckBatchFromDatabaseAsync(cacheMisses, cancellationToken)
                    .ConfigureAwait(false);
                foreach (var result in databaseResults)
                {
                    decisions[result.Request] = result.IsGranted;
                }
            }

            var currentVersions = await securityVersionStore
                .GetTenantAndGlobalVersionsAsync(tenantIds, cancellationToken)
                .ConfigureAwait(false);
            if (!AreBulkPermissionVersionsCurrent(tenantIds, versionSnapshot, currentVersions))
            {
                continue;
            }

            if (databaseResults.Count > 0)
            {
                var valuesToCache = new Dictionary<string, bool>(databaseResults.Count, StringComparer.Ordinal);
                foreach (var result in databaseResults)
                {
                    valuesToCache[cacheKeys[result.Request]] = result.IsGranted;
                }

                await hybridPermissionCache.SetManyValuesAsync(valuesToCache, BulkPermissionCacheType, cancellationToken)
                    .ConfigureAwait(false);
            }

            return requests
                .Select(request => new BulkPermissionCheckResult(request, decisions[request]))
                .ToArray();
        }

        throw new InvalidOperationException(
            $"Could not obtain stable permission versions for a bulk authorization batch after {MaximumBulkPermissionCacheVersionRetries} attempts.");
    }

    private static bool AreBulkPermissionVersionsCurrent(
        IReadOnlyCollection<Guid> tenantIds,
        IReadOnlyDictionary<Guid, (long TenantVersion, long GlobalVersion)> expectedVersions,
        IReadOnlyDictionary<Guid, (long TenantVersion, long GlobalVersion)> currentVersions)
    {
        foreach (var tenantId in tenantIds)
        {
            if (expectedVersions.GetValueOrDefault(tenantId) != currentVersions.GetValueOrDefault(tenantId))
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildBulkPermissionCacheKey(
        BulkPermissionCheckRequest request,
        (long TenantVersion, long GlobalVersion) versions)
    {
        var tenantKey = request.TenantId?.ToString("D") ?? "global";
        var resourceKey = request.ResourceId?.ToString("D") ?? "none";
        var tenantVersion = versions.TenantVersion.ToString(CultureInfo.InvariantCulture);
        var globalVersion = versions.GlobalVersion.ToString(CultureInfo.InvariantCulture);
        var permission = ((int)request.Permission).ToString(CultureInfo.InvariantCulture);

        return $"perm:{tenantKey}:{request.UserId:D}:bulk:v1:{tenantVersion}:{globalVersion}:{permission}:{FingerprintCacheKeyPart(request.ContentTypeName)}:{resourceKey}:{FingerprintCacheKeyPart(request.ResourceTypeName)}";
    }

    private static string FingerprintCacheKeyPart(string? value) =>
        value is null ? "none" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static async IAsyncEnumerable<BulkPermissionCheckRequest> EnumerateBulkPermissionRequests(
        IReadOnlyList<Guid> userIds,
        Guid? tenantId,
        IReadOnlyList<PermissionType> permissions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var userId in userIds)
        {
            foreach (var permission in permissions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new BulkPermissionCheckRequest(userId, tenantId, permission);
                await Task.CompletedTask.ConfigureAwait(false);
            }
        }
    }

    private async Task<IReadOnlyList<BulkPermissionCheckResult>> EvaluateBulkPermissionCheckBatchFromDatabaseAsync(
        IReadOnlyList<BulkPermissionCheckRequest> requests,
        CancellationToken cancellationToken)
    {
        var tenantGrants = await context.Set<TenantPermission>()
            .AsNoTracking()
            .Where(BuildPermissionGrantScopePredicate<TenantPermission>(requests))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var contentTypeNames = requests
            .Where(request => !string.IsNullOrWhiteSpace(request.ContentTypeName))
            .Select(request => request.ContentTypeName!)
            .Distinct()
            .ToArray();
        var contentTypeGrants = contentTypeNames.Length == 0
            ? []
            : await context.Set<ContentTypePermission>()
                .AsNoTracking()
                .Where(BuildContentTypeGrantPredicate(requests))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var resourceRequests = requests.Where(request => request.ResourceId.HasValue).ToArray();
        var resourceGrants = resourceRequests.Length == 0
            ? []
            : await context.Set<GenericResourcePermission>()
                .AsNoTracking()
                .Where(BuildResourceGrantPredicate(resourceRequests))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        // Read through EF sequentially using the scoped DbContext, then evaluate distinct
        // decisions in parallel. Duplicate requests share one decision and are expanded
        // back into the original order after evaluation.
        var distinctRequestIndexes = new Dictionary<BulkPermissionCheckRequest, int>();
        var distinctRequests = new List<BulkPermissionCheckRequest>(requests.Count);
        var resultIndexes = new int[requests.Count];
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            if (!distinctRequestIndexes.TryGetValue(request, out var distinctIndex))
            {
                distinctIndex = distinctRequests.Count;
                distinctRequestIndexes.Add(request, distinctIndex);
                distinctRequests.Add(request);
            }

            resultIndexes[index] = distinctIndex;
        }

        var decisions = new bool[distinctRequests.Count];
        void EvaluateAt(int index)
        {
            decisions[index] = EvaluateBulkPermissionDecision(
                distinctRequests[index],
                tenantGrants,
                contentTypeGrants,
                resourceGrants);
        }

        if (distinctRequests.Count >= MinimumParallelBulkPermissionCheckBatchSize && Environment.ProcessorCount > 1)
        {
            Parallel.For(
                0,
                distinctRequests.Count,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, MaximumParallelBulkPermissionChecks)
                },
                EvaluateAt);
        }
        else
        {
            for (var index = 0; index < distinctRequests.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EvaluateAt(index);
            }
        }

        var result = new List<BulkPermissionCheckResult>(requests.Count);
        for (var index = 0; index < requests.Count; index++)
        {
            result.Add(new BulkPermissionCheckResult(requests[index], decisions[resultIndexes[index]]));
        }

        return result;
    }

    private async Task<bool> CheckPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        PermissionType permission,
        string? contentTypeName = null,
        Guid? resourceId = null,
        string? resourceTypeName = null)
    {
        // The bulk evaluator is the single source of truth for allow/deny precedence.
        // Guid.Empty represents a user-less check here; user grants are excluded by
        // TenantGrantApplies and ContentTypeGrantApplies for that sentinel.
        var request = new BulkPermissionCheckRequest(
            userId ?? Guid.Empty,
            tenantId,
            permission,
            contentTypeName,
            resourceId,
            resourceTypeName);
        var result = await EvaluateBulkPermissionCheckBatchAsync([request], CancellationToken.None, recordMetrics: false)
            .ConfigureAwait(false);

        return result[0].IsGranted;
    }

    private async Task<bool> HasResourcePermissionWithTenantDenyAsync<TPermission, TResource>(
        Guid userId,
        Guid? tenantId,
        Guid resourceId,
        PermissionType permission)
        where TPermission : ResourcePermission<TResource>, new()
        where TResource : EntityBase
    {
        var allowedPermissions = await GetEffectiveResourcePermissionsAsync<TPermission, TResource>(
                userId,
                tenantId,
                resourceId)
            .ConfigureAwait(false);

        return allowedPermissions.Contains(permission) &&
               !await IsDeniedByTenantPermissionAsync(userId, tenantId, permission).ConfigureAwait(false);
    }

    private async Task<bool> IsDeniedByTenantPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        PermissionType permission)
    {
        var grants = await context.Set<TenantPermission>()
            .AsNoTracking()
            .Where(grant =>
                (grant.UserId == null && grant.TenantId == null) ||
                (grant.TenantId == tenantId &&
                 (grant.UserId == null || (userId.HasValue && grant.UserId == userId.Value))))
            .ToListAsync()
            .ConfigureAwait(false);
        var request = new BulkPermissionCheckRequest(userId ?? Guid.Empty, tenantId, permission);

        return grants.Any(grant =>
            grant.IsActive &&
            !grant.IsExpired() &&
            TenantGrantApplies(grant, request) &&
            ToPermissionTypes(grant.DenyPermissions).Contains(permission));
    }

    private static Expression<Func<TGrant, bool>> BuildPermissionGrantScopePredicate<TGrant>(
        IReadOnlyList<BulkPermissionCheckRequest> requests)
    {
        var grant = Expression.Parameter(typeof(TGrant), "grant");
        var userId = Expression.Property(grant, nameof(WithPermissions.UserId));
        var tenantId = Expression.Property(grant, nameof(WithPermissions.TenantId));
        Expression? predicate = null;

        foreach (var request in requests.Distinct())
        {
            var applicableGrant = BuildPermissionGrantScopeExpression(userId, tenantId, request);

            predicate = predicate is null ? applicableGrant : Expression.OrElse(predicate, applicableGrant);
        }

        return Expression.Lambda<Func<TGrant, bool>>(predicate ?? Expression.Constant(false), grant);
    }

    private static Expression<Func<ContentTypePermission, bool>> BuildContentTypeGrantPredicate(
        IReadOnlyList<BulkPermissionCheckRequest> requests)
    {
        var grant = Expression.Parameter(typeof(ContentTypePermission), "grant");
        var userId = Expression.Property(grant, nameof(WithPermissions.UserId));
        var tenantId = Expression.Property(grant, nameof(WithPermissions.TenantId));
        var contentTypeName = Expression.Property(grant, nameof(ContentTypePermission.ContentTypeName));
        Expression? predicate = null;

        foreach (var request in requests
                     .Where(request => !string.IsNullOrWhiteSpace(request.ContentTypeName))
                     .Distinct())
        {
            var sameContentType = Expression.Equal(
                contentTypeName,
                Expression.Constant(request.ContentTypeName, typeof(string)));
            var applicableScope = BuildPermissionGrantScopeExpression(userId, tenantId, request);
            var applicableGrant = Expression.AndAlso(sameContentType, applicableScope);
            predicate = predicate is null ? applicableGrant : Expression.OrElse(predicate, applicableGrant);
        }

        return Expression.Lambda<Func<ContentTypePermission, bool>>(predicate ?? Expression.Constant(false), grant);
    }

    private static Expression BuildPermissionGrantScopeExpression(
        Expression userId,
        Expression tenantId,
        BulkPermissionCheckRequest request)
    {
        var userIsDefault = Expression.Equal(userId, Expression.Constant(null, typeof(Guid?)));
        var tenantIsGlobal = Expression.Equal(tenantId, Expression.Constant(null, typeof(Guid?)));
        var globalDefault = Expression.AndAlso(userIsDefault, tenantIsGlobal);

        var requestedTenant = Expression.Equal(
            tenantId,
            Expression.Constant(request.TenantId, typeof(Guid?)));
        var requestedUser = Expression.OrElse(
            userIsDefault,
            Expression.Equal(userId, Expression.Constant((Guid?)request.UserId, typeof(Guid?))));
        var scopedGrant = Expression.AndAlso(requestedTenant, requestedUser);

        return Expression.OrElse(globalDefault, scopedGrant);
    }

    private static Expression<Func<GenericResourcePermission, bool>> BuildResourceGrantPredicate(
        IReadOnlyList<BulkPermissionCheckRequest> requests)
    {
        var grant = Expression.Parameter(typeof(GenericResourcePermission), "grant");
        var userId = Expression.Property(grant, nameof(WithPermissions.UserId));
        var tenantId = Expression.Property(grant, nameof(WithPermissions.TenantId));
        var resourceId = Expression.Property(grant, nameof(ResourcePermission<EntityBase>.ResourceId));
        var resourceType = Expression.Property(grant, nameof(ResourcePermission<EntityBase>.ResourceType));
        Expression? predicate = null;

        foreach (var request in requests.Distinct())
        {
            if (!request.ResourceId.HasValue)
            {
                continue;
            }

            var matchesRequest = Expression.AndAlso(
                Expression.Equal(userId, Expression.Constant((Guid?)request.UserId, typeof(Guid?))),
                Expression.AndAlso(
                    Expression.Equal(tenantId, Expression.Constant(request.TenantId, typeof(Guid?))),
                    Expression.Equal(resourceId, Expression.Constant(request.ResourceId.Value))));
            var exactResourceType = request.ResourceTypeName is null
                ? matchesRequest
                : Expression.AndAlso(
                    matchesRequest,
                    Expression.Equal(resourceType, Expression.Constant(request.ResourceTypeName, typeof(string))));
            predicate = predicate is null ? exactResourceType : Expression.OrElse(predicate, exactResourceType);
        }

        return Expression.Lambda<Func<GenericResourcePermission, bool>>(predicate ?? Expression.Constant(false), grant);
    }

    private static bool EvaluateBulkPermissionDecision(
        BulkPermissionCheckRequest request,
        IReadOnlyList<TenantPermission> tenantGrants,
        IReadOnlyList<ContentTypePermission> contentTypeGrants,
        IReadOnlyList<GenericResourcePermission> resourceGrants)
    {
        var allowed = new HashSet<PermissionType>();
        var denied = new HashSet<PermissionType>();

        foreach (var grant in tenantGrants)
        {
            if (!grant.IsActive || grant.IsExpired() || !TenantGrantApplies(grant, request))
            {
                continue;
            }

            allowed.UnionWith(ToPermissionTypes(grant.Permissions));
            denied.UnionWith(ToPermissionTypes(grant.DenyPermissions));
        }

        if (!string.IsNullOrWhiteSpace(request.ContentTypeName))
        {
            foreach (var grant in contentTypeGrants)
            {
                if (!grant.IsEffective() ||
                    !string.Equals(grant.ContentTypeName, request.ContentTypeName, StringComparison.Ordinal) ||
                    !ContentTypeGrantApplies(grant, request))
                {
                    continue;
                }

                allowed.UnionWith(grant.GetPermissionsAsEnum());
            }
        }

        if (request.ResourceId.HasValue)
        {
            foreach (var grant in resourceGrants)
            {
                if (!grant.IsEffective() ||
                    request.UserId == Guid.Empty ||
                    grant.UserId != request.UserId ||
                    grant.TenantId != request.TenantId ||
                    grant.ResourceId != request.ResourceId.Value ||
                    (request.ResourceTypeName is not null &&
                     !string.Equals(grant.ResourceType, request.ResourceTypeName, StringComparison.Ordinal)))
                {
                    continue;
                }

                allowed.UnionWith(grant.GetPermissionsAsEnum());
            }
        }

        return allowed.Contains(request.Permission) && !denied.Contains(request.Permission);
    }

    private static bool TenantGrantApplies(TenantPermission grant, BulkPermissionCheckRequest request)
        => (grant.UserId == null && grant.TenantId == null) ||
           (grant.TenantId == request.TenantId &&
            (grant.UserId == null || (request.UserId != Guid.Empty && grant.UserId == request.UserId)));

    private static bool ContentTypeGrantApplies(ContentTypePermission grant, BulkPermissionCheckRequest request)
        => (grant.UserId == null && grant.TenantId == null) ||
           (grant.TenantId == request.TenantId &&
            (grant.UserId == null || (request.UserId != Guid.Empty && grant.UserId == request.UserId)));

    private static void ValidateBulkPermissionCheckRequest(BulkPermissionCheckRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ResourceId.HasValue && string.IsNullOrWhiteSpace(request.ResourceTypeName))
        {
            throw new ArgumentException("ResourceTypeName is required when ResourceId is provided.", nameof(request));
        }

        if (!request.ResourceId.HasValue && !string.IsNullOrWhiteSpace(request.ResourceTypeName))
        {
            throw new ArgumentException("ResourceTypeName cannot be provided without ResourceId.", nameof(request));
        }
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(
        IEnumerable<T> source,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }

    public async Task CleanupExpiredPermissionsAsync()
    {
        var tenantPermissions = await context.Set<TenantPermission>()
            .Where(p => p.IsActive && p.ExpiresAt != null && p.ExpiresAt <= SystemClock.UtcNow)
            .ToListAsync()
            .ConfigureAwait(false);
        foreach (var grant in tenantPermissions)
        {
            grant.IsActive = false;
        }

        var contentPermissions = await context.Set<ContentTypePermission>()
            .Where(p => p.IsActive && p.ExpiresAt != null && p.ExpiresAt <= SystemClock.UtcNow)
            .ToListAsync()
            .ConfigureAwait(false);
        foreach (var grant in contentPermissions)
        {
            grant.Expire();
        }

        var resourcePermissions = await context.Set<GenericResourcePermission>()
            .Where(p => p.IsActive && p.ExpiresAt != null && p.ExpiresAt <= SystemClock.UtcNow)
            .ToListAsync()
            .ConfigureAwait(false);
        foreach (var grant in resourcePermissions)
        {
            grant.Expire();
        }

        await context.SaveChangesAsync().ConfigureAwait(false);

        // Expiring grants changes effective permissions: bump every affected tenant's
        // security version so stale caches are discarded, then audit the cleanup.
        var affectedTenants = tenantPermissions
            .Select(p => p.TenantId)
            .Concat(contentPermissions.Select(p => (Guid?)p.TenantId))
            .Concat(resourcePermissions.Select(p => (Guid?)p.TenantId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        foreach (var tenantId in affectedTenants)
        {
            await TrackMutationAsync(
                PermissionOperationType.Update,
                userId: null,
                tenantId: tenantId,
                permissionNames: [],
                reason: "Expired permissions cleanup").ConfigureAwait(false);
        }
    }

    private async Task SetTenantGrantAsync(Guid? userId, Guid? tenantId, PermissionType[] permissions, string reason)
    {
        var grant = await GetTenantGrantAsync(userId, tenantId).ConfigureAwait(false);
        if (grant == null)
        {
            grant = new TenantPermission
            {
                UserId = userId,
                TenantId = tenantId,
                GrantedAt = SystemClock.UtcNow,
                Reason = reason
            };
            context.Set<TenantPermission>().Add(grant);
        }

        grant.Permissions = ToPermissionNames(permissions);
        grant.IsActive = true;
        grant.ExpiresAt = null;
        await context.SaveChangesAsync().ConfigureAwait(false);

        await TrackMutationAsync(
            PermissionOperationType.Update,
            userId,
            tenantId,
            permissions.Select(p => p.ToString()),
            reason: reason).ConfigureAwait(false);
    }

    private Task<TenantPermission?> GetTenantGrantAsync(Guid? userId, Guid? tenantId)
        => context.Set<TenantPermission>()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.TenantId == tenantId);

    private Task<ContentTypePermission?> GetContentTypeGrantAsync(Guid? userId, Guid? tenantId, string contentTypeName)
        => context.Set<ContentTypePermission>()
            .FirstOrDefaultAsync(p =>
                p.UserId == userId &&
                p.TenantId == tenantId &&
                p.ContentTypeName == contentTypeName);

    private Task<TPermission?> GetResourceGrantAsync<TPermission, TResource>(Guid userId, Guid? tenantId, Guid resourceId)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase
        => context.Set<TPermission>()
            .FirstOrDefaultAsync(p =>
                p.UserId == userId &&
                p.TenantId == tenantId &&
                p.ResourceId == resourceId);

    /// <summary>
    ///     SECURITY: records a permission mutation — bumps the tenant security version so
    ///     cached permission sets are invalidated, and writes a permission audit entry.
    ///     Failures propagate: a mutation whose invalidation failed must not be reported as
    ///     silently successful (stale caches would retain revoked privileges).
    /// </summary>
    private async Task TrackMutationAsync(
        PermissionOperationType operation,
        Guid? userId,
        Guid? tenantId,
        IEnumerable<string> permissionNames,
        string? contentTypeName = null,
        string? resourceTypeName = null,
        Guid? resourceId = null,
        string? reason = null)
    {
        var permissionList = permissionNames as string[] ?? permissionNames.ToArray();
        var performedBy = actorContextAccessor?.ActorContext.SubjectIdAsGuid ?? Guid.Empty;

        if (securityVersionStore is not null)
        {
            var tenantKey = tenantId?.ToString() ?? Guid.Empty.ToString();
            await securityVersionStore.IncrementVersionAsync(tenantKey).ConfigureAwait(false);
        }

        if (auditService is not null)
        {
            await auditService.LogPermissionChangeAsync(
                operation,
                userId,
                performedBy,
                tenantId,
                permissionType: permissionList.Length > 0 ? string.Join(",", permissionList) : contentTypeName ?? resourceTypeName,
                resourceId: resourceId,
                resourceType: resourceTypeName ?? contentTypeName,
                reason: reason ?? $"{operation} via legacy permission facade").ConfigureAwait(false);
        }
    }

    private static string[] ToPermissionNames(IEnumerable<PermissionType> permissions)
        => permissions.Distinct().Select(p => p.ToString()).ToArray();

    private static IEnumerable<PermissionType> ToPermissionTypes(IEnumerable<string> permissions)
    {
        foreach (var permission in permissions)
        {
            if (Enum.TryParse<PermissionType>(permission, ignoreCase: true, out var parsed))
            {
                yield return parsed;
            }
            else if (int.TryParse(permission, out var numeric) && Enum.IsDefined(typeof(PermissionType), numeric))
            {
                yield return (PermissionType)numeric;
            }
        }
    }

    private static void AddRange(HashSet<PermissionType> target, IEnumerable<PermissionType> permissions)
    {
        foreach (var permission in permissions)
        {
            target.Add(permission);
        }
    }
}
