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
/// <remarks>
///     <para>
///         Effective-permission checks delegate to the shared <see cref="IEffectivePermissionResolver"/>
///         (issue #330), so authorization entry points and permission-query callers use one
///         documented DENY-WINS resolution contract:
///         <c>apps/api/docs/effective-permission-resolution.md</c>.
///     </para>
///     <para>
///         <b>SECURITY: FAIL-CLOSED</b> - a missing or invalid user/tenant context denies
///         the request instead of falling back to global defaults.
///     </para>
/// </remarks>
public sealed class PermissionQueryService(
    ITenantPermissionRepository repository,
    ITenantMembershipChecker membershipChecker,
    IEffectivePermissionResolver effectivePermissionResolver,
    ILogger<PermissionQueryService> logger
) : IPermissionQueryService
{
    public Task<bool> HasTenantPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        // SECURITY: FAIL-CLOSED - a missing or invalid user/tenant context denies.
        if (!userId.HasValue || userId.Value == Guid.Empty || !tenantId.HasValue || tenantId.Value == Guid.Empty)
        {
            logger.LogWarning(
                "HasTenantPermissionAsync called with missing or invalid context (user {UserId}, tenant {TenantId}) - denying (fail-closed).",
                userId, tenantId);
            return Task.FromResult(false);
        }

        // Just-in-Time elevation grants are enforced inside the shared resolver
        // (TemporaryElevation layer, issue #341): an approved elevation inside its
        // time window temporarily grants the permission, tenant-scoped only, never
        // "admin:*", and still subject to DENY-WINS precedence.
        return effectivePermissionResolver.HasPermissionAsync(userId.Value, tenantId.Value, permission, cancellationToken);
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
    ///     Delegates to the shared <see cref="IEffectivePermissionResolver"/> so every
    ///     permission-query caller (#307) uses the documented DENY-WINS contract.
    ///     <b>SECURITY: FAIL-CLOSED</b> - a missing or invalid tenant context returns an
    ///     empty permission set; global defaults never apply without tenant isolation.
    ///     Just-in-Time elevation grants are enforced inside the resolver (its
    ///     TemporaryElevation layer, issue #341): approved elevations inside their time
    ///     window contribute their permission, tenant-scoped only, never "admin:*",
    ///     and still subject to DENY-WINS precedence.
    ///     Contract: <c>apps/api/docs/effective-permission-resolution.md</c>.
    /// </remarks>
    public async Task<List<string>> GetEffectivePermissionsAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        // SECURITY: FAIL-CLOSED - No valid tenant context = no permissions
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty || userId == Guid.Empty)
        {
            logger.LogWarning(
                "GetEffectivePermissionsAsync called without a valid context for user {UserId}, tenant {TenantId}. Returning empty permissions (fail-closed).",
                userId, tenantId);
            return new List<string>();
        }

        var effective = await effectivePermissionResolver
            .ResolveAsync(EffectivePermissionContext.ForTenant(userId, tenantId.Value), cancellationToken)
            .ConfigureAwait(false);

        return effective.Permissions.ToList();
    }

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
