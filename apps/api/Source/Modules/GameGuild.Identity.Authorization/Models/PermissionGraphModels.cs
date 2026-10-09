namespace GameGuild.Identity.Authorization.Models;

/// <summary>
///     Type of a node in the permission graph.
/// </summary>
public enum PermissionGraphNodeType
{
    /// <summary>Dynamic (database-defined) role or the pseudo-role representing tenant-default permissions.</summary>
    Role = 1,

    /// <summary>User holding role assignments and/or direct permission grants.</summary>
    User = 2,

    /// <summary>Permission key referenced by at least one role, grant, or default.</summary>
    Permission = 3
}

/// <summary>
///     Type of an edge in the permission graph.
/// </summary>
public enum PermissionGraphEdgeType
{
    /// <summary>Child role inherits from parent role (child -> parent).</summary>
    RoleInheritsFrom = 1,

    /// <summary>Role grants a permission key (role -> permission).</summary>
    RoleGrantsPermission = 2,

    /// <summary>Role explicitly denies a permission key (role -> permission).</summary>
    RoleDeniesPermission = 3,

    /// <summary>User is assigned a role (user -> role).</summary>
    UserAssignedRole = 4,

    /// <summary>User holds a direct permission grant (user -> permission).</summary>
    UserDirectGrant = 5,

    /// <summary>User holds a direct permission deny (user -> permission).</summary>
    UserDirectDeny = 6
}

/// <summary>
///     A single node of the permission graph.
/// </summary>
public sealed class PermissionGraphNode
{
    /// <summary>
    ///     Stable node identifier: <c>role:{roleId}</c>, <c>user:{userId}</c>, or <c>perm:{permissionKey}</c>.
    ///     The tenant-default pseudo-role uses <c>role:tenant-default:{tenantId}</c>.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Node type.</summary>
    public PermissionGraphNodeType Type { get; init; }

    /// <summary>Human-readable label (role display name, user id, or permission key).</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Tenant scope of the entity (null for global roles).</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Role nodes: whether this is a system role that cannot be deleted.</summary>
    public bool? IsSystem { get; init; }

    /// <summary>Role nodes: whether the role is currently active.</summary>
    public bool? IsActive { get; init; }

    /// <summary>Permission nodes: resource segment of the key (the part before the first colon).</summary>
    public string? Resource { get; init; }

    /// <summary>Permission nodes: whether the key exists in the <see cref="PermissionRegistry"/>.</summary>
    public bool IsRegistered { get; init; }
}

/// <summary>
///     A directed edge of the permission graph.
/// </summary>
public sealed class PermissionGraphEdge
{
    /// <summary>Source node id.</summary>
    public string SourceId { get; init; } = string.Empty;

    /// <summary>Target node id.</summary>
    public string TargetId { get; init; } = string.Empty;

    /// <summary>Edge type.</summary>
    public PermissionGraphEdgeType Type { get; init; }
}

/// <summary>
///     A cycle detected in the role-inheritance graph.
/// </summary>
public sealed class PermissionGraphCycle
{
    /// <summary>Role ids participating in the cycle, in walk order.</summary>
    public IReadOnlyList<Guid> RoleIds { get; init; } = Array.Empty<Guid>();
}

/// <summary>
///     Aggregated quality/statistics report for a permission graph.
/// </summary>
public sealed class PermissionGraphSummary
{
    /// <summary>Number of role nodes (including the tenant-default pseudo-role when present).</summary>
    public int RoleCount { get; init; }

    /// <summary>Number of user nodes.</summary>
    public int UserCount { get; init; }

    /// <summary>Number of permission nodes.</summary>
    public int PermissionCount { get; init; }

    /// <summary>Total number of edges.</summary>
    public int EdgeCount { get; init; }

    /// <summary>Number of user -> permission direct grant edges.</summary>
    public int DirectGrantCount { get; init; }

    /// <summary>Cycles found in role inheritance (a data defect; resolution caps hierarchy walks).</summary>
    public IReadOnlyList<PermissionGraphCycle> InheritanceCycles { get; init; } = Array.Empty<PermissionGraphCycle>();

    /// <summary>
    ///     Permission keys present in stored grants/roles but not registered in
    ///     <see cref="PermissionRegistry"/>. Unknown keys fail closed at check time and usually
    ///     indicate a typo or a permission removed from code while still stored.
    /// </summary>
    public IReadOnlyList<string> UnregisteredPermissionKeys { get; init; } = Array.Empty<string>();

    /// <summary>Roles whose <c>ParentRoleId</c> points to a role that does not exist in scope.</summary>
    public IReadOnlyList<Guid> OrphanedRoleIds { get; init; } = Array.Empty<Guid>();
}

/// <summary>
///     Snapshot of a tenant's permission structure rendered as a directed graph:
///     users -> roles -> permissions, including inheritance, denies, and direct grants.
/// </summary>
public sealed class PermissionGraph
{
    /// <summary>Tenant the graph was built for (null = global scope).</summary>
    public Guid? TenantId { get; init; }

    /// <summary>UTC timestamp of generation.</summary>
    public DateTime GeneratedAtUtc { get; init; }

    /// <summary>Whether user nodes and their edges are included.</summary>
    public bool IncludesUsers { get; init; }

    /// <summary>Graph nodes.</summary>
    public IReadOnlyList<PermissionGraphNode> Nodes { get; init; } = Array.Empty<PermissionGraphNode>();

    /// <summary>Graph edges.</summary>
    public IReadOnlyList<PermissionGraphEdge> Edges { get; init; } = Array.Empty<PermissionGraphEdge>();

    /// <summary>Graph statistics and data-quality findings.</summary>
    public PermissionGraphSummary Summary { get; init; } = new();
}

/// <summary>
///     A user affected by a simulated permission change, with the reason.
/// </summary>
public sealed class ImpactedUser
{
    /// <summary>The affected user id.</summary>
    public Guid UserId { get; init; }

    /// <summary>Why the user is impacted (e.g. permission keys lost, or retention path).</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Permission keys the user loses entirely as a result of the change.</summary>
    public IReadOnlyList<string> LostPermissionKeys { get; init; } = Array.Empty<string>();

    /// <summary>For retention results: role chains that still provide the permission.</summary>
    public IReadOnlyList<Guid> RetainedViaRoleIds { get; init; } = Array.Empty<Guid>();

    /// <summary>For retention results: whether a direct user grant still provides the permission.</summary>
    public bool RetainedViaDirectGrant { get; init; }
}

/// <summary>
///     Result of simulating the deletion of a dynamic role.
/// </summary>
public sealed class RoleDeletionImpact
{
    /// <summary>Whether the role exists in the requested scope.</summary>
    public bool RoleFound { get; init; }

    /// <summary>The analyzed role id.</summary>
    public Guid RoleId { get; init; }

    /// <summary>The role name (empty when not found).</summary>
    public string RoleName { get; init; } = string.Empty;

    /// <summary>Whether the role is a system role (deletion must be refused).</summary>
    public bool IsSystemRole { get; init; }

    /// <summary>Number of currently valid user assignments to the role.</summary>
    public int DirectAssignmentCount { get; init; }

    /// <summary>Sample of user ids currently assigned (capped).</summary>
    public IReadOnlyList<Guid> AssignedUserIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Roles that inherit from this role and would need re-parenting.</summary>
    public IReadOnlyList<Guid> ChildRoleIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Users who lose at least one permission key entirely when the role is deleted.</summary>
    public IReadOnlyList<ImpactedUser> UsersLosingPermissions { get; init; } = Array.Empty<ImpactedUser>();

    /// <summary>Aggregated severity of the simulated change.</summary>
    public ImpactSeverity Severity { get; init; }

    /// <summary>Non-fatal findings (system role, orphaned children, wildcard grants lost, cycles).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

/// <summary>
///     Result of simulating the removal of a permission key from a role.
/// </summary>
public sealed class PermissionRemovalImpact
{
    /// <summary>Whether the role exists in the requested scope.</summary>
    public bool RoleFound { get; init; }

    /// <summary>The analyzed role id.</summary>
    public Guid RoleId { get; init; }

    /// <summary>The role name (empty when not found).</summary>
    public string RoleName { get; init; } = string.Empty;

    /// <summary>The permission key under analysis.</summary>
    public string PermissionKey { get; init; } = string.Empty;

    /// <summary>Whether the role's inheritance chain currently provides the key.</summary>
    public bool IsGranted { get; init; }

    /// <summary>Whether the key is stored verbatim on the role itself (removable directly).</summary>
    public bool RemovableDirectly { get; init; }

    /// <summary>Whether the key is only provided by an ancestor role (inherited).</summary>
    public bool GrantedViaInheritance { get; init; }

    /// <summary>Descendant roles whose inheritance chain passes through this role.</summary>
    public IReadOnlyList<Guid> DownstreamRoleIds { get; init; } = Array.Empty<Guid>();

    /// <summary>Users who currently hold the key and would lose it after the removal.</summary>
    public IReadOnlyList<ImpactedUser> UsersLosingPermission { get; init; } = Array.Empty<ImpactedUser>();

    /// <summary>Users who hold the key and would retain it through another path.</summary>
    public IReadOnlyList<ImpactedUser> UsersRetainingPermission { get; init; } = Array.Empty<ImpactedUser>();

    /// <summary>Aggregated severity of the simulated change.</summary>
    public ImpactSeverity Severity { get; init; }

    /// <summary>Non-fatal findings.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
