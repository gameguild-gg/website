using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Tenant-wide permissions - Core permission entity for user access control.
///     Inherits the shared permission-grant contract from <see cref="PermissionBase"/> (#352):
///     subject, grant metadata, activation, expiration, validation, audit and tenant-isolation
///     behaviors are defined once on the base and inherited by every permission entity.
///     The string-based allow/deny payload is bridged to <see cref="PermissionType"/> through
///     the fail-closed <see cref="PermissionBase.GetGrantedPermissionTypes"/> mapping layer
///     (no destructive schema change).
/// </summary>
[Table("TenantPermissions")]
[Index(nameof(TenantId), Name = "IX_TenantPermissions_TenantId")]
[Index(nameof(UserId), Name = "IX_TenantPermissions_UserId")]
[Index(nameof(ExpiresAt), Name = "IX_TenantPermissions_ExpiresAt")]
public class TenantPermission : PermissionBase
{
    /// <summary>
    ///     Tenant ID (null for global defaults)
    /// </summary>
    public new Guid? TenantId { get; set; }

    /// <summary>
    ///     Array of permission strings (e.g., ["Read", "Write", "Delete"])
    /// </summary>
    [Column(TypeName = "text[]")]
    public string[] Permissions { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Array of denied permission strings. These take precedence over allows.
    ///     When evaluating effective permissions: EffectivePerms = AllowedPerms - DeniedPerms
    /// </summary>
    [Column(TypeName = "text[]")]
    public string[] DenyPermissions { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Who granted this permission
    /// </summary>
    public Guid? GrantedBy { get; set; }

    /// <summary>
    ///     Reason for granting (audit trail)
    /// </summary>
    [MaxLength(500)]
    public string? Reason { get; set; }

    /// <summary>
    ///     Additional metadata as JSON
    /// </summary>
    [Column(TypeName = "jsonb")]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    ///     Unified audit view: who granted this permission.
    /// </summary>
    public override Guid? CreatedBy => GrantedBy;

    /// <summary>
    ///     Unified audit view: tenant scope of this permission grant.
    /// </summary>
    public override Guid? PermissionTenantId => TenantId;

    /// <summary>
    ///     Fail-closed mapping of the stored permission strings to defined permission types.
    ///     Strings that do not map to a defined <see cref="PermissionType"/> are dropped.
    /// </summary>
    public override IReadOnlyList<PermissionType> GetGrantedPermissionTypes() { return ParsePermissionTypes(Permissions).ToList(); }

    /// <summary>
    ///     Check if a specific permission is granted
    /// </summary>
    public bool HasPermission(string permission)
    {
        return Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Check if a specific permission is denied
    /// </summary>
    public bool HasDenyPermission(string permission)
    {
        return DenyPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Check if permission is effectively granted (allowed and not denied)
    /// </summary>
    public bool HasEffectivePermission(string permission)
    {
        return HasPermission(permission) && !HasDenyPermission(permission);
    }

    /// <summary>
    ///     Add permissions to this entity
    /// </summary>
    public void AddPermissions(params string[] permissions)
    {
        var current = Permissions.ToList();

        foreach (var perm in permissions)
        {
            if (!current.Contains(perm, StringComparer.OrdinalIgnoreCase))
            {
                current.Add(perm);
            }
        }

        Permissions = current.ToArray();
    }

    /// <summary>
    ///     Remove permissions from this entity
    /// </summary>
    public void RemovePermissions(params string[] permissions)
    {
        Permissions = Permissions
            .Where(p => !permissions.Contains(p, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <summary>
    ///     Add deny permissions to this entity (deny takes precedence over allow)
    /// </summary>
    public void AddDenyPermissions(params string[] permissions)
    {
        var current = DenyPermissions.ToList();

        foreach (var perm in permissions)
        {
            if (!current.Contains(perm, StringComparer.OrdinalIgnoreCase))
            {
                current.Add(perm);
            }
        }

        DenyPermissions = current.ToArray();
    }

    /// <summary>
    ///     Remove deny permissions from this entity
    /// </summary>
    public void RemoveDenyPermissions(params string[] permissions)
    {
        DenyPermissions = DenyPermissions
            .Where(p => !permissions.Contains(p, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }
}

/// <summary>
///     Permission templates for quick role assignment
/// </summary>
[Table("PermissionTemplates")]
[Index(nameof(Name), IsUnique = true, Name = "IX_PermissionTemplates_Name")]
[Index(nameof(IsSystemTemplate), Name = "IX_PermissionTemplates_IsSystemTemplate")]
public class PermissionTemplate : EntityBase
{
    /// <summary>
    ///     Template name (must be unique)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    /// <summary>
    ///     Description of what this template provides
    /// </summary>
    [MaxLength(500)]
    public string Description { get; set; } = null!;

    /// <summary>
    ///     Permissions included in this template
    /// </summary>
    [Column(TypeName = "text[]")]
    public string[] Permissions { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Whether this is a system-defined template (cannot be modified/deleted)
    /// </summary>
    public bool IsSystemTemplate { get; set; }

    /// <summary>
    ///     Whether this template is active and available for use
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Template category for organization
    /// </summary>
    [MaxLength(50)]
    public string? Category { get; set; }

    /// <summary>
    ///     Minimum tenant tier required to use this template
    /// </summary>
    [MaxLength(50)]
    public string? MinimumTier { get; set; }

    /// <summary>
    ///     Additional metadata
    /// </summary>
    [Column(TypeName = "jsonb")]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    ///     Activate this template
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    ///     Deactivate this template
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}
