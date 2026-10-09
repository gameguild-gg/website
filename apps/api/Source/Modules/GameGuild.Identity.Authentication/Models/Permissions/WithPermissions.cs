using GameGuild.Identity.Authorization;
using GameGuild.CQRS.Models;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Base abstract class for the content-type and resource permission families
///     (layers 2 and 3 of the 3-layer permission system).
///     Inherits the shared permission-grant contract from <see cref="PermissionBase"/> (#352):
///     subject, grant metadata, activation, expiration, validation, audit and tenant-isolation
///     behaviors are defined once on the base and inherited by every permission entity.
/// </summary>
public abstract class WithPermissions : PermissionBase
{
    /// <summary>
    ///     Protected constructor for entity framework
    /// </summary>
    protected WithPermissions() { }

    /// <summary>
    ///     Constructor for creating permissions
    /// </summary>
    /// <param name="userId">User ID (null for default permissions)</param>
    /// <param name="tenantId">Tenant ID (null for global permissions)</param>
    // ReSharper disable once VirtualMemberCallInConstructor - TenantId is effectively sealed in this hierarchy
#pragma warning disable CA2214 // Do not call overridable methods in constructors
    protected WithPermissions(Guid? userId, Guid? tenantId)
    {
        UserId = userId;
        TenantId = tenantId.HasValue ? new TenantId(tenantId.Value) : null;
        GrantedAt = SystemClock.UtcNow;
    }
#pragma warning restore CA2214

    /// <summary>
    ///     Serialized permissions as a comma-separated string
    ///     Stores the actual permission values efficiently
    /// </summary>
    public string Permissions { get; set; } = string.Empty;

    /// <summary>
    ///     Notes or comments about these permissions
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    ///     User ID who granted these permissions
    /// </summary>
    public Guid? GrantedBy { get; set; }

    /// <summary>
    ///     Unified audit view: who granted this permission.
    /// </summary>
    public override Guid? CreatedBy => GrantedBy;

    /// <summary>
    ///     Unified audit view: tenant scope of this permission grant.
    /// </summary>
    public override Guid? PermissionTenantId => TenantId;

    /// <summary>
    ///     Fail-closed mapping of the stored comma-separated payload to defined permission
    ///     types. Undefined numeric values are dropped by <see cref="PermissionBase"/> validation.
    /// </summary>
    public override IReadOnlyList<PermissionType> GetGrantedPermissionTypes() { return GetPermissionsAsEnum().ToList(); }

    /// <summary>
    ///     Add a permission to this entity
    /// </summary>
    /// <param name="permission">Permission to add</param>
    public void AddPermission(PermissionType permission)
    {
        var permissions = GetPermissionsAsEnum().ToList();

        if (!permissions.Contains(permission))
        {
            permissions.Add(permission);
            Permissions = string.Join(",", permissions.Select(p => (int) p));
            UpdatedAt = SystemClock.UtcNow;
        }
    }

    /// <summary>
    ///     Remove a permission from this entity
    /// </summary>
    /// <param name="permission">Permission to remove</param>
    public void RemovePermission(PermissionType permission)
    {
        var permissions = GetPermissionsAsEnum().ToList();

        if (permissions.Contains(permission))
        {
            permissions.Remove(permission);
            Permissions = string.Join(",", permissions.Select(p => (int) p));
            UpdatedAt = SystemClock.UtcNow;
        }
    }

    /// <summary>
    ///     Check if this entity has a specific permission
    /// </summary>
    /// <param name="permission">Permission to check</param>
    /// <returns>True if permission exists</returns>
    public bool HasPermission(PermissionType permission) { return GetPermissionsAsEnum().Contains(permission); }

    /// <summary>
    ///     Get all permissions as enumeration values.
    ///     SECURITY: fail closed (#357) — every parsed value is validated with
    ///     <c>Enum.IsDefined</c>; undefined numeric values are dropped instead of being
    ///     blindly cast into the <see cref="PermissionType"/> domain.
    /// </summary>
    /// <returns>Collection of permission types</returns>
    public IEnumerable<PermissionType> GetPermissionsAsEnum()
    {
        if (string.IsNullOrWhiteSpace(Permissions))
        {
            return [];
        }

        return Permissions
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => TryParsePermissionType(p, out var value) ? value : (PermissionType?) null)
            .Where(p => p.HasValue)
            .Select(p => p!.Value);
    }

    /// <summary>
    ///     Set permissions from enumeration values
    /// </summary>
    /// <param name="permissions">GameGuild.Permissions to set</param>
    public void SetPermissions(IEnumerable<PermissionType> permissions)
    {
        Permissions = string.Join(",", permissions.Select(p => (int) p));
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Add multiple permissions at once
    /// </summary>
    /// <param name="permissions">GameGuild.Permissions to add</param>
    public void AddPermissions(IEnumerable<PermissionType> permissions)
    {
        var existingPermissions = GetPermissionsAsEnum().ToList();
        var newPermissions = permissions.Where(p => !existingPermissions.Contains(p)).ToList();

        if (newPermissions.Count > 0)
        {
            existingPermissions.AddRange(newPermissions);
            SetPermissions(existingPermissions);
        }
    }

    /// <summary>
    ///     Remove multiple permissions at once
    /// </summary>
    /// <param name="permissions">GameGuild.Permissions to remove</param>
    public void RemovePermissions(IEnumerable<PermissionType> permissions)
    {
        var existingPermissions = GetPermissionsAsEnum().ToList();
        var hasChanges = false;

        foreach (var permission in permissions)
        {
            if (existingPermissions.Contains(permission))
            {
                existingPermissions.Remove(permission);
                hasChanges = true;
            }
        }

        if (hasChanges) { SetPermissions(existingPermissions); }
    }
}
