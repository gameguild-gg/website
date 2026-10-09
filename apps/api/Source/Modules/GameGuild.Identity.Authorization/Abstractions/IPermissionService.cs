using System.ComponentModel;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Core permission service interface for managing tenant-level permissions.
/// </summary>
/// <remarks>
///     This interface is deprecated. Use the focused service interfaces instead:
///     <list type="bullet">
///         <item><see cref="IPermissionGrantService"/> for grant/revoke operations</item>
///         <item><see cref="IPermissionQueryService"/> for permission checks and queries</item>
///         <item><see cref="IPermissionBulkService"/> for bulk operations and tenant membership</item>
///     </list>
/// </remarks>
[Obsolete("Use IPermissionGrantService, IPermissionQueryService, or IPermissionBulkService instead. This facade will be removed in a future version.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IPermissionService
{
    // Tenant Permission Management
    Task<TenantPermission> GrantTenantPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        string[] permissions,
        Guid? grantedBy = null,
        DateTime? expiresAt = null,
        string? reason = null,
        CancellationToken cancellationToken = default
    );

    Task<List<TenantPermission>> BulkGrantTenantPermissionAsync(
        Guid[] userIds, 
        Guid tenantId, 
        string[] permissions, 
        Guid? grantedBy = null, 
        CancellationToken cancellationToken = default);

    Task<bool> RevokeTenantPermissionAsync(
        Guid? userId, 
        Guid? tenantId, 
        string[] permissions, 
        CancellationToken cancellationToken = default);

    Task<bool> HasTenantPermissionAsync(
        Guid? userId, 
        Guid? tenantId, 
        string permission, 
        CancellationToken cancellationToken = default);

    Task<List<string>> GetTenantPermissionsAsync(
        Guid? userId, 
        Guid? tenantId, 
        CancellationToken cancellationToken = default);

    Task<List<string>> GetEffectivePermissionsAsync(
        Guid userId, 
        Guid? tenantId, 
        CancellationToken cancellationToken = default);

    // Tenant Membership
    Task<TenantPermission> JoinTenantAsync(
        Guid userId, 
        Guid tenantId, 
        Guid? invitedBy = null, 
        CancellationToken cancellationToken = default);

    Task<bool> LeaveTenantAsync(
        Guid userId, 
        Guid tenantId, 
        CancellationToken cancellationToken = default);

    Task<bool> IsUserInTenantAsync(
        Guid userId, 
        Guid tenantId, 
        CancellationToken cancellationToken = default);

    // Default Permissions
    Task<List<string>> GetGlobalDefaultPermissionsAsync(CancellationToken cancellationToken = default);

    Task SetGlobalDefaultPermissionsAsync(
        string[] permissions, 
        Guid? setBy = null, 
        CancellationToken cancellationToken = default);

    Task<List<string>> GetTenantDefaultPermissionsAsync(
        Guid tenantId, 
        CancellationToken cancellationToken = default);

    Task SetTenantDefaultPermissionsAsync(
        Guid tenantId, 
        string[] permissions, 
        Guid? setBy = null, 
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Cached permission service for performance
/// </summary>
public interface ICachedPermissionService
{
    Task<bool> HasPermissionAsync(
        Guid userId, 
        Guid? tenantId, 
        string permission, 
        CancellationToken cancellationToken = default);

    Task<List<string>> GetPermissionsAsync(
        Guid userId, 
        Guid? tenantId, 
        CancellationToken cancellationToken = default);

    Task InvalidateCacheAsync(
        Guid userId, 
        Guid? tenantId, 
        CancellationToken cancellationToken = default);

    Task InvalidateTenantCacheAsync(
        Guid tenantId, 
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Permission template service interface
/// </summary>
public interface IPermissionTemplateService
{
    Task<PermissionTemplate> CreateTemplateAsync(
        PermissionTemplate template, 
        CancellationToken cancellationToken = default);

    Task<PermissionTemplate> UpdateTemplateAsync(
        PermissionTemplate template, 
        CancellationToken cancellationToken = default);

    Task<bool> DeleteTemplateAsync(
        Guid templateId, 
        CancellationToken cancellationToken = default);

    Task<PermissionTemplate?> GetTemplateByIdAsync(
        Guid templateId, 
        CancellationToken cancellationToken = default);

    Task<PermissionTemplate?> GetTemplateByNameAsync(
        string name, 
        CancellationToken cancellationToken = default);

    Task<List<PermissionTemplate>> GetTemplatesAsync(
        string? category = null, 
        bool includeInactive = false, 
        CancellationToken cancellationToken = default);

    Task<List<PermissionTemplate>> GetSystemTemplatesAsync(
        CancellationToken cancellationToken = default);

    Task<TenantPermission> ApplyTemplateToUserAsync(
        Guid templateId, 
        Guid userId, 
        Guid tenantId, 
        Guid? appliedBy = null, 
        CancellationToken cancellationToken = default);

    Task<int> BulkApplyTemplateAsync(
        Guid templateId, 
        Guid[] userIds, 
        Guid tenantId, 
        Guid? appliedBy = null, 
        CancellationToken cancellationToken = default);

    Task<List<string>> GetTemplatePermissionsAsync(
        Guid templateId, 
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Repository interface for TenantPermission
/// </summary>
public interface ITenantPermissionRepository
{
    Task<TenantPermission> CreateAsync(
        TenantPermission permission, 
        CancellationToken cancellationToken = default);

    Task<TenantPermission> UpdateAsync(
        TenantPermission permission, 
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid id, 
        CancellationToken cancellationToken = default);

    Task<TenantPermission?> GetByIdAsync(
        Guid id, 
        CancellationToken cancellationToken = default);

    Task<TenantPermission?> GetByUserAndTenantAsync(
        Guid? userId, 
        Guid? tenantId, 
        CancellationToken cancellationToken = default);

    Task<List<TenantPermission>> GetByTenantAsync(
        Guid tenantId, 
        CancellationToken cancellationToken = default);

    Task<List<TenantPermission>> GetByUserAsync(
        Guid userId, 
        CancellationToken cancellationToken = default);

    Task<List<TenantPermission>> GetExpiredPermissionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets active, non-deleted grants whose <c>ExpiresAt</c> falls within the open
    ///     interval (now, <paramref name="cutoff"/>], ordered by expiration instant.
    ///     Uses the <c>IX_TenantPermissions_ExpiresAt</c> index.
    /// </summary>
    Task<List<TenantPermission>> GetExpiringBeforeAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets non-deleted grants by ID, restricted to a single tenant so bulk
    ///     expiration mutations can never cross tenant boundaries.
    /// </summary>
    Task<List<TenantPermission>> GetByIdsInTenantAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}
