using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Implementation of <see cref="IPermissionGrantService"/> containing grant/revoke logic.
///     This is the primary implementation - <see cref="PermissionService"/> is a backward-compatible facade.
/// </summary>
/// <remarks>
///     <para>
///         <b>Security - Cache Invalidation:</b> All mutations increment the tenant security version to ensure
///         cache invalidation and prevent stale cache privilege retention (Attack 3).
///     </para>
///     <para>
///         <b>Security - Authorization Guards:</b> Global default operations (tenantId=null) require
///         system-level ManageGlobalDefaults permission. This is defense-in-depth beyond command handler checks.
///     </para>
/// </remarks>
public sealed class PermissionGrantService(
    ITenantPermissionRepository repository,
    IPermissionAuditService auditService,
    ITenantSecurityVersionStore securityVersionStore,
    IActorContextAccessor actorContextAccessor,
    ILogger<PermissionGrantService> logger
) : IPermissionGrantService
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    public async Task<TenantPermission> GrantTenantPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        string[] permissions,
        Guid? grantedBy = null,
        DateTime? expiresAt = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        // SECURITY (Attack 6): Defense-in-depth - global defaults require system permission
        ValidateGlobalDefaultAuthorization(tenantId, "grant global default permissions");

        logger.LogInformation(
            "Granting permissions {Permissions} to user {UserId} in tenant {TenantId}",
            string.Join(", ", permissions),
            userId,
            tenantId);

        var performedBy = Actor.SubjectIdAsGuid ?? Guid.Empty;
        var existing = await repository.GetByUserAndTenantAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
        var previousPermissions = existing?.Permissions.ToArray();
        TenantPermission result;

        if (existing is not null)
        {
            existing.AddPermissions(permissions);
            existing.RemoveDenyPermissions(permissions);
            existing.IsActive = true;
            existing.ExpiresAt = existing.ExpiresAt.HasValue && expiresAt.HasValue
                ? (existing.ExpiresAt.Value <= expiresAt.Value ? existing.ExpiresAt : expiresAt)
                : existing.ExpiresAt ?? expiresAt;
            existing.GrantedAt = SystemClock.UtcNow;
            existing.GrantedBy = performedBy;
            existing.Reason = reason;
            result = await repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var permission = new TenantPermission
            {
                UserId = userId,
                TenantId = tenantId,
                Permissions = Array.Empty<string>(),
                GrantedAt = SystemClock.UtcNow,
                GrantedBy = performedBy,
                ExpiresAt = expiresAt,
                Reason = reason
            };
            permission.AddPermissions(permissions);
            result = await repository.CreateAsync(permission, cancellationToken).ConfigureAwait(false);
        }

        // SECURITY: Increment tenant version to invalidate all cached permissions
        await InvalidateTenantCacheAsync(tenantId, cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Grant,
            userId,
            performedBy,
            tenantId,
            permissionType: "Tenant",
            resourceType: "TenantPermission",
            oldValue: previousPermissions is null ? null : string.Join(",", previousPermissions),
            newValue: string.Join(",", result.Permissions),
            reason: reason,
            cancellationToken: cancellationToken);

        return result;
    }

    public async Task<bool> RevokeTenantPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        string[] permissions,
        CancellationToken cancellationToken = default)
    {
        // SECURITY (Attack 6): Defense-in-depth - global defaults require system permission
        ValidateGlobalDefaultAuthorization(tenantId, "revoke global default permissions");

        var existing = await repository.GetByUserAndTenantAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);

        if (existing == null) return false;

        var previousPermissions = existing.Permissions.ToArray();
        existing.RemovePermissions(permissions);

        if (existing.Permissions.Length == 0)
        {
            await repository.DeleteAsync(existing.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        }

        // SECURITY: Increment tenant version to invalidate all cached permissions
        await InvalidateTenantCacheAsync(tenantId, cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Revoke,
            userId,
            Actor.SubjectIdAsGuid ?? Guid.Empty,
            tenantId,
            permissionType: "Tenant",
            resourceType: "TenantPermission",
            oldValue: string.Join(",", previousPermissions),
            newValue: existing.Permissions.Length == 0 ? null : string.Join(",", existing.Permissions),
            reason: "Permissions revoked",
            cancellationToken: cancellationToken);

        return true;
    }

    public async Task SetGlobalDefaultPermissionsAsync(
        string[] permissions,
        Guid? setBy = null,
        CancellationToken cancellationToken = default)
    {
        // SECURITY (Attack 6): CRITICAL - Global defaults affect ALL users across ALL tenants
        // This requires ManageGlobalDefaults permission - enforced at both service and command level
        ValidateGlobalDefaultAuthorization(null, "set global default permissions");

        logger.LogInformation("Setting global default permissions: {Permissions}", string.Join(", ", permissions));

        var existing = await repository.GetByUserAndTenantAsync(null, null, cancellationToken).ConfigureAwait(false);
        var previousPermissions = existing?.Permissions.ToArray();

        if (existing != null)
        {
            existing.Permissions = permissions;
            existing.GrantedBy = Actor.SubjectIdAsGuid;
            await repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var permission = new TenantPermission
            {
                UserId = null,
                TenantId = null,
                Permissions = permissions,
                GrantedBy = Actor.SubjectIdAsGuid,
                GrantedAt = SystemClock.UtcNow,
                Reason = "Global default permissions"
            };
            await repository.CreateAsync(permission, cancellationToken).ConfigureAwait(false);
        }

        await InvalidateTenantCacheAsync(null, cancellationToken).ConfigureAwait(false);
        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Update,
            null,
            Actor.SubjectIdAsGuid ?? Guid.Empty,
            null,
            permissionType: "GlobalDefault",
            resourceType: "TenantPermission",
            oldValue: previousPermissions is null ? null : string.Join(",", previousPermissions),
            newValue: string.Join(",", permissions),
            reason: "Global default permissions updated",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task SetTenantDefaultPermissionsAsync(
        Guid tenantId,
        string[] permissions,
        Guid? setBy = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Setting tenant {TenantId} default permissions: {Permissions}",
            tenantId,
            string.Join(", ", permissions));

        var existing = await repository.GetByUserAndTenantAsync(null, tenantId, cancellationToken).ConfigureAwait(false);
        var previousPermissions = existing?.Permissions.ToArray();

        if (existing != null)
        {
            existing.Permissions = permissions;
            existing.GrantedBy = Actor.SubjectIdAsGuid;
            await repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var permission = new TenantPermission
            {
                UserId = null,
                TenantId = tenantId,
                Permissions = permissions,
                GrantedBy = Actor.SubjectIdAsGuid,
                GrantedAt = SystemClock.UtcNow,
                Reason = "Tenant default permissions"
            };
            await repository.CreateAsync(permission, cancellationToken).ConfigureAwait(false);
        }

        await InvalidateTenantCacheAsync(tenantId, cancellationToken).ConfigureAwait(false);
        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Update,
            null,
            Actor.SubjectIdAsGuid ?? Guid.Empty,
            tenantId,
            permissionType: "TenantDefault",
            resourceType: "TenantPermission",
            oldValue: previousPermissions is null ? null : string.Join(",", previousPermissions),
            newValue: string.Join(",", permissions),
            reason: "Tenant default permissions updated",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<TenantPermission> DenyTenantPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        string[] permissions,
        Guid? deniedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Adding deny permissions {Permissions} for user {UserId} in tenant {TenantId}",
            string.Join(", ", permissions),
            userId,
            tenantId);

        var existing = await repository.GetByUserAndTenantAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);

        if (existing != null)
        {
            var previousDenyPermissions = existing.DenyPermissions.ToArray();
            existing.AddDenyPermissions(permissions);
            await repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);

            // SECURITY: Increment tenant version to invalidate all cached permissions
            await InvalidateTenantCacheAsync(tenantId, cancellationToken).ConfigureAwait(false);

            await auditService.LogPermissionChangeAsync(
                PermissionOperationType.Deny,
                userId,
                Actor.SubjectIdAsGuid ?? Guid.Empty,
                tenantId,
                permissionType: "Tenant",
                resourceType: "TenantPermission",
                oldValue: string.Join(",", previousDenyPermissions),
                newValue: string.Join(",", existing.DenyPermissions),
                reason: reason,
                cancellationToken: cancellationToken);

            return existing;
        }

        // Create new entry with deny permissions only
        var permission = new TenantPermission
        {
            UserId = userId,
            TenantId = tenantId,
            Permissions = Array.Empty<string>(),
            DenyPermissions = permissions,
            GrantedBy = Actor.SubjectIdAsGuid,
            GrantedAt = SystemClock.UtcNow,
            Reason = reason ?? "Deny permissions added"
        };

        var result = await repository.CreateAsync(permission, cancellationToken).ConfigureAwait(false);

        // SECURITY: Increment tenant version to invalidate all cached permissions
        await InvalidateTenantCacheAsync(tenantId, cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Deny,
            userId,
            Actor.SubjectIdAsGuid ?? Guid.Empty,
            tenantId,
            permissionType: "Tenant",
            resourceType: "TenantPermission",
            oldValue: null,
            newValue: string.Join(",", result.DenyPermissions),
            reason: reason,
            cancellationToken: cancellationToken);

        return result;
    }

    public async Task<bool> RemoveDenyPermissionsAsync(
        Guid? userId,
        Guid? tenantId,
        string[] permissions,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Removing deny permissions {Permissions} from user {UserId} in tenant {TenantId}",
            string.Join(", ", permissions),
            userId,
            tenantId);

        var existing = await repository.GetByUserAndTenantAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);

        if (existing == null) return false;

        var previousDenyPermissions = existing.DenyPermissions.ToArray();
        existing.RemoveDenyPermissions(permissions);
        await repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);

        // SECURITY: Increment tenant version to invalidate all cached permissions
        await InvalidateTenantCacheAsync(tenantId, cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Revoke,
            userId,
            Actor.SubjectIdAsGuid ?? Guid.Empty,
            tenantId,
            permissionType: "Tenant",
            resourceType: "TenantPermission",
            oldValue: string.Join(",", previousDenyPermissions),
            newValue: string.Join(",", existing.DenyPermissions),
            reason: "Deny permissions removed",
            cancellationToken: cancellationToken);

        return true;
    }

    private async Task InvalidateTenantCacheAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        var tenantKey = tenantId?.ToString() ?? Guid.Empty.ToString();

        try
        {
            var newVersion = await securityVersionStore.IncrementVersionAsync(tenantKey, cancellationToken).ConfigureAwait(false);
            logger.LogDebug(
                "Incremented security version for tenant {TenantId} to {Version}",
                tenantKey,
                newVersion);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to increment security version for tenant {TenantId}. Cache may be stale.",
                tenantKey);
            throw;
        }
    }

    /// <summary>
    ///     SECURITY (Attack 6): Validates that the current actor has permission to modify global defaults.
    /// </summary>
    /// <remarks>
    ///     Global defaults (tenantId=null) affect ALL users across ALL tenants.
    ///     Only system administrators or users with ManageGlobalDefaults permission can modify them.
    ///     This is defense-in-depth - command handlers also enforce this check.
    /// </remarks>
    private void ValidateGlobalDefaultAuthorization(Guid? tenantId, string operation)
    {
        // Only check for global operations (tenantId=null)
        if (tenantId.HasValue) return;

        // Skip if no actor context available (e.g., during system initialization)
        if (!Actor.IsAuthenticated) return;

        // System admins can always modify global defaults
        if (Actor.IsSystemAdmin) return;

        // Check for ManageGlobalDefaults permission
        if (Actor.HasPermission(SystemPermission.Keys.ManageGlobalDefaults)) return;

        // SECURITY: Fail-closed - deny access if no authorization
        logger.LogWarning(
            "User {UserId} attempted to {Operation} without ManageGlobalDefaults permission",
            Actor.SubjectId,
            operation);

        throw new UnauthorizedAccessException(
            $"Modifying global default permissions requires '{SystemPermission.Keys.ManageGlobalDefaults}' permission. " +
            $"Attempted operation: {operation}");
    }
}

/// <summary>
///     Implementation of <see cref="IPermissionQueryService"/> containing query/check logic.
///     This is the primary implementation - <see cref="PermissionService"/> is a backward-compatible facade.
/// </summary>
public sealed class PermissionQueryService(
    ITenantPermissionRepository repository,
    ITenantMembershipChecker membershipChecker,
    ILogger<PermissionQueryService> logger,
    IEnumerable<IAuthorizationRolePermissionProvider>? rolePermissionProviders = null,
    IJitElevationRequestRepository? jitElevationRepository = null
) : IPermissionQueryService
{
    public async Task<bool> HasTenantPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var grants = new[]
        {
            await repository.GetByUserAndTenantAsync(null, null, cancellationToken).ConfigureAwait(false),
            tenantId.HasValue
                ? await repository.GetByUserAndTenantAsync(null, tenantId, cancellationToken).ConfigureAwait(false)
                : null,
            await repository.GetByUserAndTenantAsync(userId, tenantId, cancellationToken).ConfigureAwait(false)
        };

        var activeGrants = grants.Where(grant => grant is not null && !grant.IsExpired()).Cast<TenantPermission>().ToList();
        if (activeGrants.Any(grant => grant.HasDenyPermission(permission)))
            return false;

        if (activeGrants.Any(grant => grant.HasPermission(permission)))
            return true;

        if (!userId.HasValue || !tenantId.HasValue)
            return false;

        foreach (var provider in rolePermissionProviders ?? [])
        {
            var permissions = await provider.GetPermissionsAsync(userId.Value, tenantId.Value, cancellationToken).ConfigureAwait(false)
                              ?? [];
            if (permissions
                .Where(IsDelegableRolePermission)
                .Contains(permission, StringComparer.OrdinalIgnoreCase))
                return true;
        }

        // Just-in-Time elevation grants: an approved elevation that is inside its
        // time window temporarily grants the permission. Only tenant-scoped
        // (resource-unscoped) elevations apply here, "admin:*" is never grantable
        // through elevation, and explicit denies above already returned false, so
        // DENY-WINS precedence is preserved.
        if (jitElevationRepository is not null)
        {
            var elevations = await jitElevationRepository
                .GetActiveByUserAsync(userId.Value, tenantId, cancellationToken)
                .ConfigureAwait(false);

            if (elevations.Any(e =>
                    e.ResourceId is null &&
                    e.IsGrantInForce() &&
                    IsDelegableRolePermission(e.Permission) &&
                    string.Equals(e.Permission, permission, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<List<string>> GetTenantPermissionsAsync(
        Guid? userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetByUserAndTenantAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);

        if (existing == null) return new List<string>();

        return existing.Permissions.ToList();
    }

    /// <summary>
    ///     Get effective permissions for a user in a tenant.
    /// </summary>
    /// <remarks>
    ///     <b>Permission Evaluation Policy: DENY-WINS</b>
    ///     <para>
    ///         Permissions are resolved from three layers plus Just-in-Time (JIT)
    ///         elevation grants. Explicit denies at any layer remove the permission
    ///         from the effective set (deny takes precedence).
    ///     </para>
    ///     <para>
    ///         <b>SECURITY: FAIL-CLOSED</b> - If no tenant context is provided, returns empty permissions.
    ///         This prevents global defaults from being applied without proper tenant context.
    ///     </para>
    ///     <para>
    ///         Evaluation order:
    ///         <list type="number">
    ///             <item>Collect all ALLOW permissions from: Global defaults → Tenant defaults → Direct grants → Roles → JIT elevations</item>
    ///             <item>Collect all DENY permissions from: Global denies → Tenant denies → Direct denies</item>
    ///             <item>Effective = ALLOW - DENY (deny always wins)</item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         JIT elevations contribute their permission only while the elevation is
    ///         approved and inside its time window (see
    ///         <see cref="JitElevationRequest.IsGrantInForce"/>); resource-scoped
    ///         elevations and "admin:*" are excluded.
    ///     </para>
    /// </remarks>
    public async Task<List<string>> GetEffectivePermissionsAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        // SECURITY: FAIL-CLOSED - No tenant context = no permissions
        // This prevents global defaults from being applied without proper tenant isolation
        if (!tenantId.HasValue)
        {
            logger.LogWarning(
                "GetEffectivePermissionsAsync called without tenant context for user {UserId}. Returning empty permissions (fail-closed).",
                userId);
            return new List<string>();
        }

        var allowedPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deniedPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Layer 1: Global defaults (UserId=null, TenantId=null)
        var globalDefaults = await repository.GetByUserAndTenantAsync(null, null, cancellationToken).ConfigureAwait(false);
        if (globalDefaults != null && !globalDefaults.IsExpired())
        {
            allowedPermissions.UnionWith(globalDefaults.Permissions);
            deniedPermissions.UnionWith(globalDefaults.DenyPermissions);
        }

        // Layer 2: Tenant defaults (UserId=null, TenantId=X)
        var tenantDefaults = await repository.GetByUserAndTenantAsync(null, tenantId.Value, cancellationToken).ConfigureAwait(false);
        if (tenantDefaults != null && !tenantDefaults.IsExpired())
        {
            allowedPermissions.UnionWith(tenantDefaults.Permissions);
            deniedPermissions.UnionWith(tenantDefaults.DenyPermissions);
        }

        // Layer 3: Direct user permissions (UserId=Y, TenantId=X)
        var userPermissions = await repository.GetByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        var directGrants = userPermissions
            .Where(p => p.TenantId == tenantId.Value)
            .Where(p => !p.ExpiresAt.HasValue || p.ExpiresAt.Value > SystemClock.UtcNow)
            .ToList();

        foreach (var grant in directGrants)
        {
            allowedPermissions.UnionWith(grant.Permissions);
            deniedPermissions.UnionWith(grant.DenyPermissions);
        }

        foreach (var provider in rolePermissionProviders ?? [])
        {
            var rolePermissions = await provider.GetPermissionsAsync(userId, tenantId.Value, cancellationToken).ConfigureAwait(false)
                                  ?? [];
            allowedPermissions.UnionWith(rolePermissions.Where(IsDelegableRolePermission));
        }

        // Layer 4: Just-in-Time elevation grants (tenant-scoped, resource-unscoped).
        // An approved elevation inside its time window temporarily contributes its
        // permission to the allow set. JIT grants remain subject to DENY-WINS below,
        // and "admin:*" is never grantable through elevation.
        if (jitElevationRepository is not null)
        {
            var elevations = await jitElevationRepository
                .GetActiveByUserAsync(userId, tenantId, cancellationToken)
                .ConfigureAwait(false);

            foreach (var elevation in elevations)
            {
                if (elevation.ResourceId is null &&
                    elevation.IsGrantInForce() &&
                    IsDelegableRolePermission(elevation.Permission))
                {
                    allowedPermissions.Add(elevation.Permission);
                }
            }
        }

        // DENY-WINS: Subtract all denied permissions from allowed set
        allowedPermissions.ExceptWith(deniedPermissions);

        logger.LogDebug(
            "Effective permissions for user {UserId} in tenant {TenantId}: {Count} allowed, {DenyCount} denied",
            userId, tenantId.Value, allowedPermissions.Count, deniedPermissions.Count);

        return allowedPermissions.ToList();
    }

    private static bool IsDelegableRolePermission(string permission) =>
        !string.Equals(permission, "admin:*", StringComparison.OrdinalIgnoreCase);

    public async Task<List<string>> GetGlobalDefaultPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        var defaults = await repository.GetByUserAndTenantAsync(null, null, cancellationToken).ConfigureAwait(false);
        return defaults?.Permissions.ToList() ?? new List<string>();
    }

    public async Task<List<string>> GetTenantDefaultPermissionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var defaults = await repository.GetByUserAndTenantAsync(null, tenantId, cancellationToken).ConfigureAwait(false);
        return defaults?.Permissions.ToList() ?? new List<string>();
    }

    public async Task<bool> IsUserInTenantAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // SECURITY: Delegate to actual tenant membership check, not permission check
        // Having permissions in a tenant is NOT the same as being a member
        return await membershipChecker.IsUserMemberOfTenantAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     Implementation of <see cref="IPermissionBulkService"/> containing bulk operations.
///     Delegates to <see cref="IPermissionGrantService"/> and <see cref="IPermissionQueryService"/>.
/// </summary>
public sealed class PermissionBulkService(
    IPermissionGrantService grantService,
    IPermissionQueryService queryService,
    ILogger<PermissionBulkService> logger
) : IPermissionBulkService
{
    public async Task<List<TenantPermission>> BulkGrantTenantPermissionAsync(
        Guid[] userIds,
        Guid tenantId,
        string[] permissions,
        Guid? grantedBy = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Bulk granting permissions {Permissions} to {UserCount} users in tenant {TenantId}",
            string.Join(", ", permissions),
            userIds.Length,
            tenantId);

        var results = new List<TenantPermission>();

        foreach (var userId in userIds)
        {
            var result = await grantService.GrantTenantPermissionAsync(
                userId,
                tenantId,
                permissions,
                grantedBy,
                null,
                null,
                cancellationToken);
            results.Add(result);
        }

        return results;
    }

    public async Task<TenantPermission> JoinTenantAsync(
        Guid userId,
        Guid tenantId,
        Guid? invitedBy = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("User {UserId} joining tenant {TenantId}", userId, tenantId);

        var defaultPermissions = await queryService.GetTenantDefaultPermissionsAsync(tenantId, cancellationToken).ConfigureAwait(false);

        return await grantService.GrantTenantPermissionAsync(
            userId,
            tenantId,
            defaultPermissions.ToArray(),
            invitedBy,
            null,
            "User joined tenant",
            cancellationToken);
    }

    public async Task<bool> LeaveTenantAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("User {UserId} leaving tenant {TenantId}", userId, tenantId);

        var permissions = await queryService.GetTenantPermissionsAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);

        return await grantService.RevokeTenantPermissionAsync(
            userId,
            tenantId,
            permissions.ToArray(),
            cancellationToken).ConfigureAwait(false);
    }
}
