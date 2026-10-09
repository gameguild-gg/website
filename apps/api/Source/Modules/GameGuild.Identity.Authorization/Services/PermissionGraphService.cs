using GameGuild.Identity.Authorization.Models;
using GameGuild.Identity.Authorization.Utilities;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Builds the tenant permission graph from a <see cref="TenantAuthorizationSnapshot"/>.
///     The graph is a read-only projection: users -> roles -> permissions, including
///     inheritance edges, explicit denies, and direct grants, plus a data-quality summary.
/// </summary>
public sealed class PermissionGraphService(
    IDynamicRoleRepository roleRepository,
    IDynamicRoleAssignmentRepository assignmentRepository,
    ITenantPermissionRepository tenantPermissionRepository) : IPermissionGraphService
{
    /// <inheritdoc />
    public async Task<PermissionGraph> BuildGraphAsync(Guid? tenantId, bool includeUsers, CancellationToken ct = default)
    {
        var snapshot = await TenantAuthorizationSnapshotLoader
            .LoadAsync(roleRepository, assignmentRepository, tenantPermissionRepository, tenantId, includeUsers, ct)
            .ConfigureAwait(false);

        var nodes = new List<PermissionGraphNode>();
        var edges = new List<PermissionGraphEdge>();

        // Role nodes.
        foreach (var role in snapshot.Roles)
        {
            nodes.Add(new PermissionGraphNode
            {
                Id = NodeIds.Role(role.Id),
                Type = PermissionGraphNodeType.Role,
                Label = string.IsNullOrWhiteSpace(role.DisplayName) ? role.Name : role.DisplayName,
                TenantId = role.TenantId,
                IsSystem = role.IsSystem,
                IsActive = role.IsActive
            });

            if (role.ParentRoleId is Guid parentId)
            {
                // Only emit the edge when the parent is part of the snapshot; a dangling
                // parent is reported as an orphan in the summary instead.
                if (snapshot.FindRole(parentId) is not null)
                {
                    edges.Add(new PermissionGraphEdge
                    {
                        SourceId = NodeIds.Role(role.Id),
                        TargetId = NodeIds.Role(parentId),
                        Type = PermissionGraphEdgeType.RoleInheritsFrom
                    });
                }
            }

            AddPermissionEdges(edges, NodeIds.Role(role.Id), role.Permissions, role.DenyPermissions);
        }

        // Tenant-default pseudo-role node (its keys apply to every user of the tenant).
        if (tenantId.HasValue && snapshot.TenantDefault is not null)
        {
            var pseudoId = NodeIds.TenantDefault(tenantId.Value);
            nodes.Add(new PermissionGraphNode
            {
                Id = pseudoId,
                Type = PermissionGraphNodeType.Role,
                Label = "Tenant defaults",
                TenantId = tenantId
            });
            AddPermissionEdges(edges, pseudoId, snapshot.TenantDefault.Permissions, snapshot.TenantDefault.DenyPermissions);
        }

        // User nodes and their edges.
        if (includeUsers)
        {
            foreach (var userId in snapshot.UsersInScope())
            {
                nodes.Add(new PermissionGraphNode
                {
                    Id = NodeIds.User(userId),
                    Type = PermissionGraphNodeType.User,
                    Label = userId.ToString(),
                    TenantId = tenantId
                });

                foreach (var assignment in snapshot.AssignmentsOf(userId))
                {
                    if (snapshot.FindRole(assignment.RoleId) is not null)
                    {
                        edges.Add(new PermissionGraphEdge
                        {
                            SourceId = NodeIds.User(userId),
                            TargetId = NodeIds.Role(assignment.RoleId),
                            Type = PermissionGraphEdgeType.UserAssignedRole
                        });
                    }
                }

                var direct = snapshot.DirectGrantOf(userId);
                if (direct is not null)
                {
                    AddPermissionEdges(edges, NodeIds.User(userId), direct.Permissions, direct.DenyPermissions, denyAsDirect: true);
                }
            }
        }

        // Permission nodes for every stored key referenced by the emitted edges.
        var permissionIds = new HashSet<string>(edges
            .Where(e => e.Type is PermissionGraphEdgeType.RoleGrantsPermission
                or PermissionGraphEdgeType.RoleDeniesPermission
                or PermissionGraphEdgeType.UserDirectGrant
                or PermissionGraphEdgeType.UserDirectDeny)
            .Select(e => e.TargetId), StringComparer.OrdinalIgnoreCase);

        var storedKeys = snapshot.StoredKeys().ToList();
        foreach (var key in storedKeys.Where(k => permissionIds.Contains("perm:" + k, StringComparer.OrdinalIgnoreCase)))
        {
            nodes.Add(new PermissionGraphNode
            {
                Id = NodeIds.Permission(key),
                Type = PermissionGraphNodeType.Permission,
                Label = key,
                TenantId = tenantId,
                Resource = key.Contains(':') ? key[..key.IndexOf(':')] : key,
                IsRegistered = IsRegisteredKey(key)
            });
        }

        var summary = new PermissionGraphSummary
        {
            RoleCount = nodes.Count(n => n.Type == PermissionGraphNodeType.Role),
            UserCount = nodes.Count(n => n.Type == PermissionGraphNodeType.User),
            PermissionCount = nodes.Count(n => n.Type == PermissionGraphNodeType.Permission),
            EdgeCount = edges.Count,
            DirectGrantCount = edges.Count(e => e.Type == PermissionGraphEdgeType.UserDirectGrant),
            InheritanceCycles = DetectInheritanceCycles(snapshot),
            UnregisteredPermissionKeys = storedKeys.Where(k => !IsRegisteredKey(k)).ToList(),
            OrphanedRoleIds = snapshot.Roles
                .Where(r => r.ParentRoleId is Guid parentId && snapshot.FindRole(parentId) is null)
                .Select(r => r.Id)
                .ToList()
        };

        return new PermissionGraph
        {
            TenantId = tenantId,
            GeneratedAtUtc = SystemClock.UtcNow,
            IncludesUsers = includeUsers,
            Nodes = nodes,
            Edges = edges,
            Summary = summary
        };
    }

    private static void AddPermissionEdges(
        List<PermissionGraphEdge> edges,
        string sourceId,
        IEnumerable<string> allows,
        IEnumerable<string> denies,
        bool denyAsDirect = false)
    {
        foreach (var key in allows)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            edges.Add(new PermissionGraphEdge
            {
                SourceId = sourceId,
                TargetId = NodeIds.Permission(key),
                Type = denyAsDirect
                    ? PermissionGraphEdgeType.UserDirectGrant
                    : PermissionGraphEdgeType.RoleGrantsPermission
            });
        }

        foreach (var key in denies)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            edges.Add(new PermissionGraphEdge
            {
                SourceId = sourceId,
                TargetId = NodeIds.Permission(key),
                Type = denyAsDirect
                    ? PermissionGraphEdgeType.UserDirectDeny
                    : PermissionGraphEdgeType.RoleDeniesPermission
            });
        }
    }

    private static IReadOnlyList<PermissionGraphCycle> DetectInheritanceCycles(TenantAuthorizationSnapshot snapshot)
    {
        var cycles = new List<PermissionGraphCycle>();
        var reported = new HashSet<Guid>();

        foreach (var role in snapshot.Roles)
        {
            if (reported.Contains(role.Id))
            {
                continue;
            }

            var cycleRoles = ExtractCycle(snapshot, role.Id);
            if (cycleRoles.Count == 0)
            {
                continue;
            }

            foreach (var id in cycleRoles)
            {
                reported.Add(id);
            }

            cycles.Add(new PermissionGraphCycle { RoleIds = cycleRoles });
        }

        return cycles;
    }

    private static IReadOnlyList<Guid> ExtractCycle(TenantAuthorizationSnapshot snapshot, Guid roleId)
    {
        var path = new List<Guid>();
        var positions = new Dictionary<Guid, int>();
        var current = snapshot.FindRole(roleId);
        while (current is not null)
        {
            if (positions.TryGetValue(current.Id, out var startIndex))
            {
                return path.Skip(startIndex).ToList();
            }

            positions[current.Id] = path.Count;
            path.Add(current.Id);
            current = current.ParentRoleId is Guid parentId
                ? snapshot.FindRole(parentId)
                : null;
        }

        return [];
    }

    private static bool IsRegisteredKey(string key)
        => key == "*" || PermissionRegistry.IsValidKey(key);
}

/// <summary>
///     Stable node identifiers used by the permission graph.
/// </summary>
internal static class NodeIds
{
    internal static string Role(Guid roleId) => $"role:{roleId}";

    internal static string User(Guid userId) => $"user:{userId}";

    internal static string Permission(string key) => $"perm:{key}";

    internal static string TenantDefault(Guid tenantId) => $"role:tenant-default:{tenantId}";
}
