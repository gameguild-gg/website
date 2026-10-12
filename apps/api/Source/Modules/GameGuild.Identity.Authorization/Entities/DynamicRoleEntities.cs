
namespace GameGuild.Identity.Authorization;

/// <summary>
///     Dynamic role definition stored in database.
///     Supports role hierarchy and permission mapping.
/// </summary>
/// <remarks>
///     <para>
///         <b>Static vs Dynamic Roles:</b>
///         <list type="bullet">
///             <item>Static roles (Owner, Admin, etc.) are defined in code as TenantRole constants</item>
///             <item>Dynamic roles are stored in database and can be created/modified at runtime</item>
///         </list>
///     </para>
///     <para>
///         <b>Role Hierarchy:</b>
///         A role can have multiple parent roles (multi-parent inheritance, issue #358):
///         one primary <see cref="DynamicRole.ParentRoleId"/> plus any
///         <see cref="DynamicRole.AdditionalParentRoleIds"/>. Permissions are inherited
///         from all parents; cycles are detected and cut during traversal.
///         Example: "Senior Developer" inherits from "Developer" and "Reviewer",
///         both of which inherit from "Member".
///     </para>
/// </remarks>
public class DynamicRole : EntityBase
{
    /// <summary>
    ///     Unique name of the role within the tenant.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Display name for UI.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    ///     Description of the role's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     The tenant this role belongs to.
    ///     Null = global role (applies to all tenants).
    /// </summary>
    public new Guid? TenantId { get; set; }

    /// <summary>
    ///     Parent role ID for hierarchy (primary parent).
    ///     This role inherits all permissions from its parent.
    /// </summary>
    public Guid? ParentRoleId { get; set; }

    /// <summary>
    ///     Additional parent role IDs beyond the primary <see cref="ParentRoleId"/>
    ///     (issue #358: multi-parent inheritance). The effective parent set is
    ///     <c>{ ParentRoleId } ∪ AdditionalParentRoleIds</c>; the inheritance graph is a
    ///     DAG and cycle detection is enforced during traversal.
    /// </summary>
    public Guid[] AdditionalParentRoleIds { get; set; } = Array.Empty<Guid>();

    /// <summary>
    ///     Parent role (navigation property for the primary parent).
    /// </summary>
    public DynamicRole? ParentRole { get; set; }

    /// <summary>
    ///     Child roles (navigation property).
    /// </summary>
    public ICollection<DynamicRole> ChildRoles { get; set; } = new List<DynamicRole>();

    /// <summary>
    ///     Permissions inherited from parent roles that this role explicitly opts out
    ///     of (issue #358: selective inheritance blocking). Blocked permissions do not
    ///     flow into this role from any ancestor; the role's own direct permissions and
    ///     denies are unaffected. Applies only to inherited contributions.
    /// </summary>
    public string[] BlockedInheritedPermissions { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Permissions directly assigned to this role.
    ///     Use string[] for PostgreSQL native array support.
    /// </summary>
    public string[] Permissions { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Permissions explicitly denied by this role.
    ///     Deny takes precedence over allow (DENY-WINS semantics).
    /// </summary>
    /// <remarks>
    ///     Used to restrict permissions that would otherwise be granted
    ///     by global defaults or parent roles. Example: A "ReadOnly" role
    ///     might deny "projects:edit" to prevent inheritance from base roles.
    /// </remarks>
    public string[] DenyPermissions { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Priority for conflict resolution (higher = more important).
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    ///     Whether this role is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Whether this is a system role (cannot be deleted).
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    ///     Roles that are mutually exclusive with this one.
    ///     A user cannot have both this role and any of the exclusive roles.
    /// </summary>
    public Guid[] MutuallyExclusiveRoleIds { get; set; } = Array.Empty<Guid>();

    /// <summary>
    ///     Prerequisite roles that must be assigned before this one.
    /// </summary>
    public Guid[] PrerequisiteRoleIds { get; set; } = Array.Empty<Guid>();

    /// <summary>
    ///     Maximum number of users that can have this role (0 = unlimited).
    /// </summary>
    public int MaxAssignments { get; set; }

    /// <summary>
    ///     Metadata for extensibility.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    ///     The effective set of parent role IDs: the primary parent plus any additional
    ///     parents, de-duplicated and ordered (primary first). Used by the inheritance
    ///     engine for multi-parent traversal.
    /// </summary>
    public IReadOnlyList<Guid> GetEffectiveParentRoleIds()
    {
        var parents = new List<Guid>(AdditionalParentRoleIds.Length + 1);
        if (ParentRoleId.HasValue)
        {
            parents.Add(ParentRoleId.Value);
        }

        foreach (var parentId in AdditionalParentRoleIds)
        {
            if (parentId != Guid.Empty && !parents.Contains(parentId))
            {
                parents.Add(parentId);
            }
        }

        return parents;
    }
}

/// <summary>
///     Assignment of a dynamic role to a user.
/// </summary>
public class DynamicRoleAssignment : EntityBase
{
    /// <summary>
    ///     The user ID.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    ///     The role ID.
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    ///     The role (navigation property).
    /// </summary>
    public DynamicRole? Role { get; set; }

    /// <summary>
    ///     The tenant ID (for scoped assignments).
    /// </summary>
    public new Guid? TenantId { get; set; }

    /// <summary>
    ///     When the assignment becomes active (null = immediately).
    /// </summary>
    public DateTime? StartsAt { get; set; }

    /// <summary>
    ///     When the assignment expires (null = never).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    ///     Whether this assignment is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Who granted this role.
    /// </summary>
    public Guid? GrantedBy { get; set; }

    /// <summary>
    ///     Reason for the assignment.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    ///     Whether the assignment is currently valid (active and not expired).
    /// </summary>
    public bool IsValid()
    {
        if (!IsActive)
        {
            return false;
        }

        var now = SystemClock.UtcNow;
        if (StartsAt.HasValue && StartsAt.Value > now)
        {
            return false;
        }

        if (ExpiresAt.HasValue && ExpiresAt.Value <= now)
        {
            return false;
        }

        return true;
    }
}

/// <summary>
///     Static/hard-coded role-permission mappings.
///     These cannot be overridden by database configuration.
/// </summary>
public static class StaticRolePermissions
{
    /// <summary>
    ///     Gets the static permissions for a role name.
    ///     These are non-negotiable and always apply.
    /// </summary>
    public static IReadOnlyList<string> GetStaticPermissions(string roleName)
    {
        return roleName.ToUpperInvariant() switch
        {
            "OWNER" => OwnerPermissions,
            "ADMIN" => AdminPermissions,
            "MODERATOR" => ModeratorPermissions,
            "MEMBER" => MemberPermissions,
            "CONTRIBUTOR" => ContributorPermissions,
            "VIEWER" => ViewerPermissions,
            "GUEST" => GuestPermissions,
            _ => Array.Empty<string>()
        };
    }

    /// <summary>
    ///     Owner permissions - full control.
    /// </summary>
    public static readonly IReadOnlyList<string> OwnerPermissions = new[]
    {
        "tenant:*",
        "members:*",
        "roles:*",
        "settings:*",
        "billing:*",
        "content:*",
        "moderation:*"
    };

    /// <summary>
    ///     Admin permissions - everything except tenant deletion.
    /// </summary>
    public static readonly IReadOnlyList<string> AdminPermissions = new[]
    {
        "tenant:read",
        "tenant:update",
        "members:*",
        "roles:*",
        "settings:*",
        "content:*",
        "moderation:*"
    };

    /// <summary>
    ///     Moderator permissions - content and member management.
    /// </summary>
    public static readonly IReadOnlyList<string> ModeratorPermissions = new[]
    {
        "tenant:read",
        "members:read",
        "members:update",
        "content:*",
        ModerationPermission.Keys.Moderate,
        ModerationPermission.Keys.Flag,
        ModerationPermission.Keys.Ban,
        ModerationPermission.Keys.Warn
    };

    /// <summary>
    ///     Member permissions - standard access.
    /// </summary>
    public static readonly IReadOnlyList<string> MemberPermissions = new[]
    {
        "tenant:read",
        "members:read",
        "content:read",
        "content:create",
        "content:update:own",
        "content:delete:own"
    };

    /// <summary>
    ///     Contributor permissions - can create content.
    /// </summary>
    public static readonly IReadOnlyList<string> ContributorPermissions = new[]
    {
        "tenant:read",
        "content:read",
        "content:create",
        "content:update:own"
    };

    /// <summary>
    ///     Viewer permissions - read-only.
    /// </summary>
    public static readonly IReadOnlyList<string> ViewerPermissions = new[]
    {
        "tenant:read",
        "content:read"
    };

    /// <summary>
    ///     Guest permissions - minimal access.
    /// </summary>
    public static readonly IReadOnlyList<string> GuestPermissions = new[]
    {
        "content:read:public"
    };
}
