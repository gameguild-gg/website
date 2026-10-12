using GameGuild.Identity.Authorization;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Domain service interface for three-layer permission management
///     Layer 1: Tenant-wide permissions with default support
///     Layer 2: Content-Type permissions
///     Layer 3: Resource-specific permissions
///     Implements the Specification pattern for complex permission queries
///     and follows Domain-Driven Design principles
/// </summary>
public interface IPermissionService
{
    #region Layer 1: Tenant-Wide GameGuild.Permissions

    /// <summary>
    ///     Grant permissions to a user in a tenant, or set default permissions
    /// </summary>
    /// <param name="userId">User ID (null for default permissions)</param>
    /// <param name="tenantId">Tenant ID (null for global defaults)</param>
    /// <param name="permissions">GameGuild.Permissions to grant</param>
    /// <returns>The tenant permission entity</returns>
    Task<TenantPermission> GrantTenantPermissionAsync(Guid? userId, Guid? tenantId, PermissionType[ ] permissions);

    /// <summary>
    ///     Grant permissions to multiple users in a tenant efficiently in a single transaction
    /// </summary>
    /// <param name="userIds">User IDs to grant permissions to</param>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="permissions">GameGuild.Permissions to grant</param>
    /// <returns>List of tenant permission entities</returns>
    Task<List<TenantPermission>> BulkGrantTenantPermissionAsync(Guid[ ] userIds, Guid tenantId, PermissionType[ ] permissions);

    /// <summary>
    ///     Check if user has a specific tenant permission
    ///     Resolves through hierarchy: user -> tenant default -> global default
    /// </summary>
    Task<bool> HasTenantPermissionAsync(Guid? userId, Guid? tenantId, PermissionType permission);

    /// <summary>
    ///     Get all permissions for a user in a tenant
    /// </summary>
    Task<IEnumerable<PermissionType>> GetTenantPermissionsAsync(Guid? userId, Guid? tenantId);

    /// <summary>
    ///     Get global default permissions that apply to all users
    /// </summary>
    Task<IEnumerable<PermissionType>> GetGlobalDefaultPermissionsAsync();

    /// <summary>
    ///     Set global default permissions that apply to all users
    /// </summary>
    Task SetGlobalDefaultPermissionsAsync(PermissionType[ ] permissions);

    /// <summary>
    ///     Get tenant default permissions for a specific tenant
    /// </summary>
    Task<IEnumerable<PermissionType>> GetTenantDefaultPermissionsAsync(Guid tenantId);

    /// <summary>
    ///     Set tenant default permissions for a specific tenant
    /// </summary>
    Task SetTenantDefaultPermissionsAsync(Guid tenantId, PermissionType[ ] permissions);

    /// <summary>
    ///     Revoke specific tenant permissions from a user
    /// </summary>
    Task RevokeTenantPermissionAsync(Guid? userId, Guid? tenantId, PermissionType[ ] permissions);

    /// <summary>
    ///     Add user to tenant with minimal permissions
    /// </summary>
    Task<TenantPermission> JoinTenantAsync(Guid userId, Guid tenantId);

    /// <summary>
    ///     Remove user from tenant by expiring their membership
    /// </summary>
    Task LeaveTenantAsync(Guid userId, Guid tenantId);

    /// <summary>
    ///     Check if user is an active member of tenant
    /// </summary>
    Task<bool> IsUserInTenantAsync(Guid userId, Guid tenantId);

    /// <summary>
    ///     Get all tenants where user has active membership
    /// </summary>
    Task<IEnumerable<TenantPermission>> GetUserTenantsAsync(Guid userId);

    /// <summary>
    ///     Get effective tenant permissions for a user (includes hierarchy resolution)
    /// </summary>
    Task<IEnumerable<PermissionType>> GetEffectiveTenantPermissionsAsync(Guid? userId, Guid? tenantId);

    #endregion

    #region Layer 2: Content-Type GameGuild.Permissions

    /// <summary>
    ///     Grant content-type permissions to a user
    /// </summary>
    Task<ContentTypePermission> GrantContentTypePermissionAsync(Guid? userId, Guid? tenantId, string contentTypeName, PermissionType[ ] permissions);

    /// <summary>
    ///     Check if user has content-type permission
    /// </summary>
    Task<bool> HasContentTypePermissionAsync(Guid? userId, Guid? tenantId, string contentTypeName, PermissionType permission);

    /// <summary>
    ///     Get all content-type permissions for a user
    /// </summary>
    Task<IEnumerable<PermissionType>> GetContentTypePermissionsAsync(Guid? userId, Guid? tenantId, string contentTypeName);

    /// <summary>
    ///     Revoke content-type permissions from a user
    /// </summary>
    Task RevokeContentTypePermissionAsync(Guid? userId, Guid? tenantId, string contentTypeName, PermissionType[ ] permissions);

    /// <summary>
    ///     Get effective content-type permissions for a user (includes hierarchy resolution)
    /// </summary>
    Task<IEnumerable<PermissionType>> GetEffectiveContentTypePermissionsAsync(Guid? userId, Guid? tenantId, string contentTypeName);

    #endregion

    #region Layer 3: Resource-Specific GameGuild.Permissions

    /// <summary>
    ///     Grant resource-specific permissions using generic resource type
    /// </summary>
    Task<TPermission> GrantResourcePermissionAsync<TPermission, TResource>(Guid userId, Guid? tenantId, Guid resourceId, PermissionType[ ] permissions)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase;

    /// <summary>
    ///     Check if user has resource permission using generic resource type
    /// </summary>
    Task<bool> HasResourcePermissionAsync<TPermission, TResource>(Guid userId, Guid? tenantId, Guid resourceId, PermissionType permission)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase;

    /// <summary>
    ///     Get all resource permissions for a user using generic resource type
    /// </summary>
    Task<IEnumerable<PermissionType>> GetResourcePermissionsAsync<TPermission, TResource>(Guid? userId, Guid? tenantId, Guid resourceId)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase;

    /// <summary>
    ///     Bulk grant resource permissions for multiple resources
    /// </summary>
    Task BulkGrantResourcePermissionAsync<TPermission, TResource>(Guid userId, Guid? tenantId, Guid[ ] resourceIds, PermissionType[ ] permissions)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase;

    /// <summary>
    ///     Get bulk resource permissions for multiple resources
    /// </summary>
    Task<Dictionary<Guid, IEnumerable<PermissionType>>> GetBulkResourcePermissionsAsync<TPermission, TResource>(Guid? userId, Guid? tenantId, Guid[ ] resourceIds)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase;

    /// <summary>
    ///     Share a resource with another user by granting specific permissions
    /// </summary>
    Task ShareResourceAsync<TPermission, TResource>(Guid resourceId, Guid targetUserId, Guid? tenantId, PermissionType[ ] permissions, DateTime? expiresAt = null)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase;

    /// <summary>
    ///     Revoke all permissions for a user from a resource
    /// </summary>
    Task RevokeResourceAccessAsync<TPermission, TResource>(Guid userId, Guid? tenantId, Guid resourceId) where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase;

    /// <summary>
    ///     Get effective resource permissions for a user (includes all permission layers)
    /// </summary>
    Task<IEnumerable<PermissionType>> GetEffectiveResourcePermissionsAsync<TPermission, TResource>(Guid? userId, Guid? tenantId, Guid resourceId, string? contentTypeName = null)
        where TPermission : ResourcePermission<TResource>, new() where TResource : EntityBase;

    #endregion

    #region Permission Resolution & Hierarchy

    /// <summary>
    ///     Resolve effective permissions for any context using the hierarchy
    ///     Layer 3 (Resource) > Layer 2 (Content-Type) > Layer 1 (Tenant) > Global Defaults
    /// </summary>
    Task<IEnumerable<PermissionType>> ResolveEffectivePermissionsAsync(Guid? userId, Guid? tenantId, string? contentTypeName = null, Guid? resourceId = null, string? resourceTypeName = null);

    /// <summary>
    ///     Check if user has permission in any context using the hierarchy
    /// </summary>
    Task<bool> HasPermissionAsync(Guid? userId, Guid? tenantId, PermissionType permission, string? contentTypeName = null, Guid? resourceId = null, string? resourceTypeName = null);

    /// <summary>
    ///     Get permission source (which layer granted the permission)
    /// </summary>
    Task<string> GetPermissionSourceAsync(Guid? userId, Guid? tenantId, PermissionType permission, string? contentTypeName = null, Guid? resourceId = null, string? resourceTypeName = null);

    #endregion

    #region Utility Methods

    /// <summary>
    ///     Get all users with a specific permission in a tenant
    /// </summary>
    Task<IEnumerable<Guid>> GetUsersWithPermissionAsync(Guid tenantId, PermissionType permission);

    /// <summary>
    ///     Get all resources a user has access to with a specific permission
    /// </summary>
    Task<IEnumerable<Guid>> GetResourcesWithPermissionAsync(Guid userId, Guid? tenantId, PermissionType permission, string? resourceTypeName = null);

    /// <summary>
    ///     Bulk permission check for multiple users and permissions
    /// </summary>
    Task<Dictionary<Guid, Dictionary<PermissionType, bool>>> BulkCheckPermissionsAsync(Guid[ ] userIds, Guid? tenantId, PermissionType[ ] permissions);

    /// <summary>
    ///     Evaluate permission checks for different users, tenants, content types, and resources.
    /// </summary>
    /// <remarks>
    ///     Requests are evaluated in batches of at most 256 and results retain the input order.
    ///     This overload returns the full result set in memory. Use a streaming overload when
    ///     the request source or result set is too large to retain at once.
    /// </remarks>
    /// <param name="requests">The checks to evaluate.</param>
    /// <returns>One decision for each input request, in input order.</returns>
    Task<IReadOnlyList<BulkPermissionCheckResult>> BulkCheckPermissionsAsync(
        IReadOnlyCollection<BulkPermissionCheckRequest> requests);

    /// <summary>Evaluate a collection of permission checks and observe cancellation.</summary>
    /// <param name="requests">The checks to evaluate.</param>
    /// <param name="cancellationToken">Cancels database reads and batch evaluation.</param>
    /// <returns>One decision for each input request, in input order.</returns>
    Task<IReadOnlyList<BulkPermissionCheckResult>> BulkCheckPermissionsAsync(
        IReadOnlyCollection<BulkPermissionCheckRequest> requests,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Stream permission decisions in bounded batches so callers do not need to hold
    ///     the entire request and result set in memory.
    /// </summary>
    /// <remarks>Uses a batch size of 128 and preserves request order.</remarks>
    /// <param name="requests">An asynchronous source of permission checks.</param>
    /// <returns>Decisions as each bounded batch is evaluated.</returns>
    IAsyncEnumerable<BulkPermissionCheckResult> StreamBulkCheckPermissionsAsync(
        IAsyncEnumerable<BulkPermissionCheckRequest> requests);

    /// <summary>Stream decisions for an asynchronous source using the requested bounded batch size.</summary>
    /// <param name="requests">An asynchronous source of permission checks.</param>
    /// <param name="batchSize">The maximum checks evaluated together; must be between 1 and 256.</param>
    /// <returns>Decisions as each bounded batch is evaluated.</returns>
    IAsyncEnumerable<BulkPermissionCheckResult> StreamBulkCheckPermissionsAsync(
        IAsyncEnumerable<BulkPermissionCheckRequest> requests,
        int batchSize);

    /// <summary>Stream decisions for an asynchronous source and observe cancellation.</summary>
    /// <param name="requests">An asynchronous source of permission checks.</param>
    /// <param name="cancellationToken">Cancels input enumeration, database reads, and evaluation.</param>
    /// <returns>Decisions as each bounded batch is evaluated.</returns>
    IAsyncEnumerable<BulkPermissionCheckResult> StreamBulkCheckPermissionsAsync(
        IAsyncEnumerable<BulkPermissionCheckRequest> requests,
        CancellationToken cancellationToken);

    /// <summary>Stream decisions using a bounded batch size and observe cancellation.</summary>
    /// <param name="requests">An asynchronous source of permission checks.</param>
    /// <param name="batchSize">The maximum checks evaluated together; must be between 1 and 256.</param>
    /// <param name="cancellationToken">Cancels input enumeration, database reads, and evaluation.</param>
    /// <returns>Decisions as each bounded batch is evaluated.</returns>
    IAsyncEnumerable<BulkPermissionCheckResult> StreamBulkCheckPermissionsAsync(
        IAsyncEnumerable<BulkPermissionCheckRequest> requests,
        int batchSize,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Clean up expired permissions
    /// </summary>
    Task CleanupExpiredPermissionsAsync();

    #endregion
}
