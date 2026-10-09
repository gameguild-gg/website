namespace GameGuild.Identity.Authorization;

/// <summary>
///     Canonical effective-permission resolution contract for the platform.
///     Full contract: <c>apps/api/docs/effective-permission-resolution.md</c> (issue #330).
/// </summary>
/// <remarks>
///     <para>
///         <b>Evaluation policy: DENY-WINS, deny-by-default.</b> All applicable layers
///         contribute ALLOW permissions; the union of every layer's explicit DENY
///         permissions is subtracted from that union:
///         <c>Effective = (Union of allows) - (Union of denies)</c>.
///         A permission that no layer grants is denied (absent = deny, aligned with #327).
///         The only non-deniable grants are static system-account permissions.
///     </para>
///     <para>
///         <b>Layers (evaluated in a fixed, deterministic order):</b>
///         <list type="number">
///             <item>Static system-account wildcard (non-deniable)</item>
///             <item>Dynamic RBAC roles with hierarchy inheritance (<see cref="IRbacPermissionResolver"/>)</item>
///             <item>Role permission providers (<see cref="IAuthorizationRolePermissionProvider"/>; universal wildcards like <c>admin:*</c> are not delegable)</item>
///             <item>Global defaults (data-driven row UserId=null, TenantId=null)</item>
///             <item>Tenant defaults (UserId=null, TenantId=current tenant)</item>
///             <item>Direct user grants (UserId=current user, TenantId=current tenant)</item>
///             <item>Just-in-Time elevation grants — approved, inside their time window, tenant-scoped only, never <c>admin:*</c> (issue #341)</item>
///             <item>Resource grants — only when the context carries a resource type and id that match the grant</item>
///         </list>
///     </para>
///     <para>
///         <b>Context isolation.</b> Only grants whose user and tenant match the request
///         context contribute. Resource grants contribute only when the resolution context
///         names that exact resource (type + id); they never leak into tenant-wide results.
///     </para>
///     <para>
///         <b>Fail-closed.</b> A missing or invalid context (empty user, empty tenant,
///         half-specified resource) resolves to an empty permission set with
///         <see cref="EffectivePermissions.ContextValid"/> = <c>false</c>.
///     </para>
///     <para>
///         <b>Caching.</b> The resolver is intentionally cache-free and deterministic.
///         Authorization caches elsewhere (ACL cache, policy store cache, hybrid permission
///         cache) must scope entries by authorization context (tenant/user/global security
///         version) and are invalidated when permissions change (see
///         <c>CachedAccessControlListService</c>, <c>CacheInvalidationService</c>).
///     </para>
/// </remarks>
public interface IEffectivePermissionResolver
{
    /// <summary>
    ///     Resolves the effective permissions for a validated authorization context.
    /// </summary>
    /// <param name="context">The authorization context (user + tenant required, resource optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The effective permissions with source tracking. Invalid contexts resolve to an empty, fail-closed result.</returns>
    Task<EffectivePermissions> ResolveAsync(
        EffectivePermissionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Legacy tenant-scoped overload. A null or empty tenant fails closed (empty result),
    ///     consistent with the documented contract and deny-by-default (#327).
    /// </summary>
    Task<EffectivePermissions> ResolveAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a user has a specific permission. Fails closed (false) without a valid tenant context.
    /// </summary>
    Task<bool> HasPermissionAsync(
        Guid userId,
        Guid? tenantId,
        string permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a user has all of the specified permissions. Fails closed without a valid tenant context.
    /// </summary>
    Task<bool> HasAllPermissionsAsync(
        Guid userId,
        Guid? tenantId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if a user has any of the specified permissions. Fails closed without a valid tenant context.
    /// </summary>
    Task<bool> HasAnyPermissionAsync(
        Guid userId,
        Guid? tenantId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Authorization context for effective-permission resolution (issue #330).
///     User and tenant are always required; the resource layer is optional and must be
///     specified as a complete (type, id) pair or omitted entirely.
/// </summary>
public sealed record EffectivePermissionContext
{
    /// <summary>Required: the user whose permissions are resolved. <see cref="Guid.Empty"/> is invalid.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Required: the tenant scope. <see cref="Guid.Empty"/> is invalid (fail-closed).</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Optional: resource type for the resource layer (must be paired with <see cref="ResourceId"/>).</summary>
    public string? ResourceType { get; init; }

    /// <summary>Optional: resource id for the resource layer (must be paired with <see cref="ResourceType"/>).</summary>
    public string? ResourceId { get; init; }

    /// <summary>True when a complete resource (type, id) pair is present.</summary>
    public bool HasResource =>
        !string.IsNullOrWhiteSpace(ResourceType) && !string.IsNullOrWhiteSpace(ResourceId);

    /// <summary>
    ///     True when the context is safe to resolve: non-empty user and tenant, and the
    ///     resource layer is either fully specified or fully absent.
    /// </summary>
    public bool IsValid =>
        UserId != Guid.Empty
        && TenantId != Guid.Empty
        && (HasResource || (string.IsNullOrWhiteSpace(ResourceType) && string.IsNullOrWhiteSpace(ResourceId)));

    /// <summary>Creates a tenant-scoped context (no resource layer).</summary>
    public static EffectivePermissionContext ForTenant(Guid userId, Guid tenantId)
        => new() { UserId = userId, TenantId = tenantId };

    /// <summary>Creates a resource-scoped context (tenant + exact resource).</summary>
    public static EffectivePermissionContext ForResource(Guid userId, Guid tenantId, string resourceType, string resourceId)
        => new() { UserId = userId, TenantId = tenantId, ResourceType = resourceType, ResourceId = resourceId };
}

/// <summary>
///     Effective permissions for a user with source tracking.
/// </summary>
public record EffectivePermissions
{
    /// <summary>
    ///     The user ID.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    ///     The tenant ID.
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    ///     All effective permissions.
    /// </summary>
    public required IReadOnlySet<string> Permissions { get; init; }

    /// <summary>
    ///     Source of each permission for auditing.
    /// </summary>
    public required IReadOnlyDictionary<string, PermissionSource> Sources { get; init; }

    /// <summary>
    ///     Roles that contributed to the permissions.
    /// </summary>
    public IReadOnlyList<RoleContribution>? RoleContributions { get; init; }

    /// <summary>
    ///     The context this result was resolved for (null for legacy fail-closed results).
    /// </summary>
    public EffectivePermissionContext? Context { get; init; }

    /// <summary>
    ///     False when the resolution context was missing or invalid and the result was
    ///     fail-closed to an empty permission set (issue #330).
    /// </summary>
    public bool ContextValid { get; init; } = true;

    /// <summary>
    ///     True when the evaluation-layer denial throttle (enumeration protection,
    ///     issue #358) short-circuited this resolution: the result is fail-closed
    ///     (empty) for a throttled user+tenant pair even though the context was valid.
    /// </summary>
    public bool Throttled { get; init; }

    /// <summary>
    ///     When the permissions were resolved.
    /// </summary>
    public DateTime ResolvedAt { get; init; } = SystemClock.UtcNow;

    /// <summary>
    ///     Checks if a specific permission is present.
    /// </summary>
    public bool HasPermission(string permission)
        => Permissions.Contains(permission);

    /// <summary>
    ///     Checks if all specified permissions are present.
    /// </summary>
    public bool HasAllPermissions(IEnumerable<string> permissions)
        => permissions.All(p => Permissions.Contains(p));

    /// <summary>
    ///     Checks if any of the specified permissions are present.
    /// </summary>
    public bool HasAnyPermission(IEnumerable<string> permissions)
        => permissions.Any(p => Permissions.Contains(p));
}

/// <summary>
///     Source of a permission grant.
/// </summary>
public enum PermissionSource
{
    /// <summary>
    ///     Hard-coded system permission (cannot be revoked).
    /// </summary>
    Static = 0,

    /// <summary>
    ///     Permission from role assignment (RBAC).
    /// </summary>
    Role = 1,

    /// <summary>
    ///     Global default permission (system-wide).
    /// </summary>
    GlobalDefault = 2,

    /// <summary>
    ///     Tenant default permission.
    /// </summary>
    TenantDefault = 3,

    /// <summary>
    ///     Direct user grant.
    /// </summary>
    DirectGrant = 4,

    /// <summary>
    ///     Inherited from role hierarchy.
    /// </summary>
    RoleInheritance = 5,

    /// <summary>
    ///     Temporary/JIT elevation.
    /// </summary>
    TemporaryElevation = 6,

    /// <summary>
    ///     Grant scoped to a specific resource (only contributes when the resolution
    ///     context matches the resource type and id).
    /// </summary>
    ResourceGrant = 7,

    /// <summary>
    ///     Contribution from a registered <see cref="IPermissionEvaluationExtension"/>
    ///     plugin (issue #358). Extension allows are subject to DENY-WINS like every
    ///     other layer.
    /// </summary>
    Extension = 8
}

/// <summary>
///     Contribution of a role to effective permissions.
/// </summary>
public record RoleContribution(
    Guid RoleId,
    string RoleName,
    IReadOnlyList<string> Permissions,
    bool IsInherited,
    Guid? InheritedFromRoleId);
