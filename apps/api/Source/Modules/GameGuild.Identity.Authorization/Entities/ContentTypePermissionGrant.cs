using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Content-type scoped permission grants - layer 2 of the 3-layer DAC permission
///     system (issues #360/#365/#314). A grant row holds the permissions a user (or, when
///     <see cref="PermissionBase.UserId"/> is null, every user of the tenant) holds for <b>all content of
///     one content type</b> inside one tenant — more granular than a tenant-wide grant,
///     broader than a single-resource grant.
/// </summary>
/// <remarks>
///     <para>
///         Inherits the shared permission-grant contract from <see cref="PermissionBase"/>
///         (subject, grant metadata, activation, expiration, validation, audit and
///         tenant-isolation behaviors). The string-based allow/deny payload is bridged to
///         <see cref="PermissionType"/> through the fail-closed
///         <see cref="PermissionBase.GetGrantedPermissionTypes"/> mapping layer.
///     </para>
///     <para>
///         <b>Name minting (single source of truth: the effective-permission engine).</b>
///         During effective resolution the content-type layer mints each allowed entry as
///         the content-type qualified name <c>{ContentTypeName}.{permission}</c>. Qualified
///         minting keeps the layer scoped: a content-type grant never conveys a tenant-wide
///         (unqualified) permission and never applies to another content type. Inheritance
///         still flows downward: tenant-layer grants satisfy layer-<c>Auto</c> checks, and
///         content-type grants apply to every resource of that type (the engine derives the
///         content type from a resource context's resource type). DENY entries subtract the
///         same qualified name (DENY-WINS).
///     </para>
///     <para>
///         <b>DAC attribute contract (issues #318/#320).</b> The entity implements
///         <see cref="IDacAttribute"/>: the grant subject is the DAC owner of this
///         attribute view, the payload is the <see cref="IDacAttribute.PermissionList"/>, and the
///         inheritance policy is <see cref="DacInheritancePolicy.PropagateToChildren"/>
///         because content-type grants flow down to all resources of the type.
///     </para>
/// </remarks>
[Table("ContentTypePermissionGrants")]
[Index(nameof(TenantId), Name = "IX_ContentTypePermissionGrants_TenantId")]
[Index(nameof(UserId), Name = "IX_ContentTypePermissionGrants_UserId")]
[Index(nameof(ContentTypeName), Name = "IX_ContentTypePermissionGrants_ContentTypeName")]
[Index(nameof(ExpiresAt), Name = "IX_ContentTypePermissionGrants_ExpiresAt")]
public class ContentTypePermissionGrant : PermissionBase, IDacAttribute
{
    /// <summary>
    ///     Tenant ID (null for global defaults). Hides the optional base property so the
    ///     column shape matches the <see cref="TenantPermission"/> family.
    /// </summary>
    public new Guid? TenantId { get; set; }

    /// <summary>
    ///     Name of the content type this grant applies to (e.g. <c>Project</c>,
    ///     <c>Document</c>). Matched case-sensitively (ordinal) against the resolution
    ///     context's content type / resource type.
    /// </summary>
    [MaxLength(256)]
    public string ContentTypeName { get; set; } = string.Empty;

    /// <summary>
    ///     Array of allowed permission strings (e.g. ["Read", "Write"]).
    /// </summary>
    [Column(TypeName = "text[]")]
    public string[] Permissions { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Array of denied permission strings. Denied entries subtract the content-type
    ///     qualified name <c>{ContentTypeName}.{permission}</c> from the effective set
    ///     (DENY-WINS).
    /// </summary>
    [Column(TypeName = "text[]")]
    public string[] DenyPermissions { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Who granted this permission (audit trail).
    /// </summary>
    public Guid? GrantedBy { get; set; }

    /// <summary>
    ///     Reason for granting (audit trail).
    /// </summary>
    [MaxLength(500)]
    public string? Reason { get; set; }

    /// <summary>
    ///     Additional metadata as JSON.
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
    public override IReadOnlyList<PermissionType> GetGrantedPermissionTypes()
        => ParsePermissionTypes(Permissions).ToList();

    /// <summary>
    ///     DAC attribute view (issues #318/#320): the grant subject is the owner of this
    ///     attribute view; null for tenant-default rows.
    /// </summary>
    Guid? IDacAttribute.Owner => UserId;

    /// <summary>
    ///     DAC attribute view: content-type grants carry no owner-group concept in v1
    ///     (group-scoped content-type grants are a future extension point).
    /// </summary>
    string? IDacAttribute.OwnerGroup => null;

    /// <summary>
    ///     DAC attribute view: the granted permission entries.
    /// </summary>
    IReadOnlyList<string> IDacAttribute.PermissionList => Permissions;

    /// <summary>
    ///     DAC attribute view: content-type grants propagate down to every resource of the
    ///     content type (enforced by the engine's content-type layer).
    /// </summary>
    DacInheritancePolicy IDacAttribute.InheritancePolicy => DacInheritancePolicy.PropagateToChildren;

    /// <summary>
    ///     Check if a specific permission is granted on this row (payload check only —
    ///     activity/expiration are enforced by <see cref="PermissionBase.IsEffective"/>).
    /// </summary>
    public bool HasPermission(string permission)
        => Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Check if a specific permission is denied on this row.
    /// </summary>
    public bool HasDenyPermission(string permission)
        => DenyPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Check whether the permission is effectively granted by this row
    ///     (allowed and not denied).
    /// </summary>
    public bool HasEffectivePermission(string permission)
        => HasPermission(permission) && !HasDenyPermission(permission);

    /// <summary>
    ///     True when this row is a tenant default (applies to every user of the tenant).
    /// </summary>
    public bool IsTenantDefault() => !UserId.HasValue;

    /// <summary>
    ///     Add permissions to this grant (case-insensitive de-duplication).
    /// </summary>
    public void AddPermissions(params string[] permissions)
    {
        var current = Permissions.ToList();
        foreach (var permission in permissions)
        {
            if (!current.Contains(permission, StringComparer.OrdinalIgnoreCase))
            {
                current.Add(permission);
            }
        }

        Permissions = current.ToArray();
    }

    /// <summary>
    ///     Remove permissions from this grant.
    /// </summary>
    public void RemovePermissions(params string[] permissions)
    {
        Permissions = Permissions
            .Where(p => !permissions.Contains(p, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    /// <summary>
    ///     Add deny permissions to this grant (deny takes precedence over allow,
    ///     DENY-WINS).
    /// </summary>
    public void AddDenyPermissions(params string[] permissions)
    {
        var current = DenyPermissions.ToList();
        foreach (var permission in permissions)
        {
            if (!current.Contains(permission, StringComparer.OrdinalIgnoreCase))
            {
                current.Add(permission);
            }
        }

        DenyPermissions = current.ToArray();
    }

    /// <summary>
    ///     Remove deny permissions from this grant.
    /// </summary>
    public void RemoveDenyPermissions(params string[] permissions)
    {
        DenyPermissions = DenyPermissions
            .Where(p => !permissions.Contains(p, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }
}
