namespace GameGuild.Identity.Authorization;

/// <summary>
///     Service interface for granting and revoking <b>tenant-level</b> permissions.
///     Follows Single Responsibility Principle by focusing only on permission mutations.
/// </summary>
/// <remarks>
///     <para>
///         <b>Scope: TENANT-LEVEL OPERATIONS</b>
///     </para>
///     <para>
///         This service manages permissions that control what <b>operations</b> a user can perform
///         within a tenant (e.g., "courses:create", "projects:delete", "users:manage").
///     </para>
///     <para>
///         For <b>resource-level access control</b> (e.g., "Can user X edit Course #123?"),
///         use <see cref="IAccessControlListService"/> instead.
///     </para>
///     <para>
///         This service was extracted from <c>IPermissionService</c> to improve SRP compliance.
///         For querying permissions, use <see cref="IPermissionQueryService"/>.
///         For bulk operations, use <see cref="IPermissionBulkService"/>.
///     </para>
/// </remarks>
public interface IPermissionGrantService
{
    /// <summary>
    ///     Grant tenant-level permissions to a user.
    /// </summary>
    Task<TenantPermission> GrantTenantPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        string[] permissions,
        Guid? grantedBy = null,
        DateTime? expiresAt = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revoke tenant-level permissions from a user.
    /// </summary>
    Task<bool> RevokeTenantPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        string[] permissions,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Set global default permissions (applies to all users in all tenants).
    /// </summary>
    Task SetGlobalDefaultPermissionsAsync(
        string[] permissions,
        Guid? setBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Set tenant-specific default permissions (applies to all users in the tenant).
    /// </summary>
    Task SetTenantDefaultPermissionsAsync(
        Guid tenantId,
        string[] permissions,
        Guid? setBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Add deny permissions to a user in a tenant. Deny takes precedence over allow.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Denied permissions are subtracted from allowed permissions during evaluation.
    ///         Use this to explicitly block specific permissions for a user, even if they
    ///         are granted via roles or tenant defaults.
    ///     </para>
    /// </remarks>
    Task<TenantPermission> DenyTenantPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        string[] permissions,
        Guid? deniedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Remove deny permissions from a user in a tenant.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This removes permissions from the deny list, potentially allowing them
    ///         if they are granted via roles or tenant defaults.
    ///     </para>
    /// </remarks>
    Task<bool> RemoveDenyPermissionsAsync(
        Guid? userId,
        Guid? tenantId,
        string[] permissions,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Service interface for querying <b>tenant-level</b> permissions.
///     Follows Single Responsibility Principle by focusing only on permission queries.
/// </summary>
/// <remarks>
///     <para>
///         <b>Scope: TENANT-LEVEL OPERATIONS</b>
///     </para>
///     <para>
///         This service queries permissions that control what <b>operations</b> a user can perform
///         within a tenant (e.g., "Can user create courses in this tenant?").
///     </para>
///     <para>
///         For <b>resource-level access control</b> (e.g., "Can user X read/write Course #123?"),
///         use <see cref="IAccessControlListService"/> instead.
///     </para>
///     <para>
///         This service was extracted from <c>IPermissionService</c> to improve SRP compliance.
///         For granting/revoking permissions, use <see cref="IPermissionGrantService"/>.
///     </para>
/// </remarks>
public interface IPermissionQueryService
{
    /// <summary>
    ///     Check if a user has a specific permission in a tenant.
    /// </summary>
    Task<bool> HasTenantPermissionAsync(
        Guid? userId,
        Guid? tenantId,
        string permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all direct permissions granted to a user in a tenant.
    /// </summary>
    Task<List<string>> GetTenantPermissionsAsync(
        Guid? userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get effective permissions for a user in a tenant.
    ///     Merges global defaults, tenant defaults, and direct grants.
    /// </summary>
    Task<List<string>> GetEffectivePermissionsAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get global default permissions.
    /// </summary>
    Task<List<string>> GetGlobalDefaultPermissionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get tenant-specific default permissions.
    /// </summary>
    Task<List<string>> GetTenantDefaultPermissionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if a user is a member of a tenant.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Important:</b> This checks actual tenant membership (TenantMember entity),
    ///         not whether the user has permissions in the tenant. A user may have permissions
    ///         granted globally without being a member of any specific tenant.
    ///     </para>
    ///     <para>
    ///         This method delegates to <see cref="ITenantMembershipChecker"/> which is
    ///         implemented by the Tenants module using the TenantMember repository.
    ///     </para>
    /// </remarks>
    Task<bool> IsUserInTenantAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Service interface for bulk permission operations and tenant membership.
///     Follows Single Responsibility Principle by focusing on multi-user operations.
/// </summary>
/// <remarks>
///     <para>
///         This service was extracted from <c>IPermissionService</c> to improve SRP compliance.
///         For single-user grants/revokes, use <see cref="IPermissionGrantService"/>.
///     </para>
/// </remarks>
public interface IPermissionBulkService
{
    /// <summary>
    ///     Grant permissions to multiple users at once.
    /// </summary>
    /// <remarks>
    ///     <para><b>Issue #310:</b> the expiry envelope and audit reason supplied by the
    ///     caller are forwarded to every per-user grant instead of being discarded, so a
    ///     finite bulk grant stays finite.</para>
    ///     <para><b>Issue #302/#365:</b> every per-user grant runs inside the caller's
    ///     ambient transaction (the host pipeline opens one for commands); callers that
    ///     need all-or-nothing semantics must observe per-user failures.</para>
    /// </remarks>
    Task<List<TenantPermission>> BulkGrantTenantPermissionAsync(
        Guid[] userIds,
        Guid tenantId,
        string[] permissions,
        Guid? grantedBy = null,
        DateTime? expiresAt = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Add a user to a tenant with default permissions.
    /// </summary>
    Task<TenantPermission> JoinTenantAsync(
        Guid userId,
        Guid tenantId,
        Guid? invitedBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Remove a user from a tenant (revoke all permissions).
    /// </summary>
    Task<bool> LeaveTenantAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
