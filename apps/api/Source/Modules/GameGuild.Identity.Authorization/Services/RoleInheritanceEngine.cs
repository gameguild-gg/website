using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Computes the multi-parent role-inheritance closure for effective permission
///     resolution (issue #358).
/// </summary>
/// <remarks>
///     <para>
///         <b>Graph model.</b> A role's parents are its primary <c>ParentRoleId</c> plus
///         <c>AdditionalParentRoleIds</c> — the graph is a DAG. Traversal is breadth-first
///         with a visited set, so a cycle is detected and cut (the repeated role does not
///         contribute twice and never loops); <see cref="RoleInheritanceClosure.CyclesCut"/>
///         reports when that happened.
///     </para>
///     <para>
///         <b>Flow semantics (selective blocking).</b> Permissions flow down from parents:
///         <c>inheritedSet(R) = ∪ over parents P of ((directSet(P) ∪ inheritedSet(P)) − blocked(R))</c>.
///         A role's <c>BlockedInheritedPermissions</c> therefore opts it out of specific
///         inherited permissions from <i>all</i> of its ancestors, while its own direct
///         permissions and its <c>DenyPermissions</c> are unaffected. Denies always flow
///         down unblocked (DENY-WINS stays global).
///     </para>
///     <para>
///         <b>Configurable rules.</b> <c>PermissionEngine:Inheritance</c>: a global kill
///         switch (<c>Enabled=false</c> yields an empty inherited set) and a maximum
///         traversal depth (<c>MaxDepth</c>, default 10) enforced on top of the visited-set
///         cycle guard.
///     </para>
/// </remarks>
public interface IRoleInheritanceEngine
{
    /// <summary>
    ///     Computes the inheritance closure of a role: all ancestor roles, their depth,
    ///     the permissions that flow into the starting role and the aggregated deny set.
    /// </summary>
    /// <param name="roleId">The starting role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<RoleInheritanceClosure> GetClosureAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks whether adding <paramref name="candidateParentIds"/> to
    ///     <paramref name="roleId"/> would create a cycle in the inheritance graph.
    ///     Used to guard parent mutations (fail-closed: reject cycle-forming edits).
    /// </summary>
    Task<bool> WouldCreateCycleAsync(
        Guid roleId,
        IReadOnlyCollection<Guid> candidateParentIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Inheritance closure of one role.
/// </summary>
/// <param name="Role">The starting role (null when the role does not exist).</param>
/// <param name="Ancestors">Ancestor roles in breadth-first order with flow-filtered contributions.</param>
/// <param name="InheritedPermissions">Exact union of permissions flowing into the starting role from all ancestors.</param>
/// <param name="InheritedDenyPermissions">Union of ancestor deny permissions (unblocked — DENY-WINS is global).</param>
/// <param name="CyclesCut">Number of cycle edges detected and cut during traversal.</param>
/// <param name="DepthLimitReached">True when the configured max depth pruned further ancestors.</param>
public sealed record RoleInheritanceClosure(
    DynamicRole? Role,
    IReadOnlyList<RoleAncestorContribution> Ancestors,
    IReadOnlySet<string> InheritedPermissions,
    IReadOnlySet<string> InheritedDenyPermissions,
    int CyclesCut,
    bool DepthLimitReached);

/// <summary>
///     One ancestor's contribution to a starting role's inherited permission set.
/// </summary>
/// <param name="Role">The ancestor role.</param>
/// <param name="Depth">Graph distance from the starting role (1 = direct parent).</param>
/// <param name="Permissions">Permissions attributed to this ancestor (subset of <see cref="RoleInheritanceClosure.InheritedPermissions"/>).</param>
public sealed record RoleAncestorContribution(
    DynamicRole Role,
    int Depth,
    IReadOnlyList<string> Permissions);

/// <summary>
///     Default <see cref="IRoleInheritanceEngine"/> implementation: iterative
/// breadth-first traversal over multi-parent edges with cycle detection and
/// selective-inheritance filtering.
/// </summary>
public sealed class RoleInheritanceEngine(
    IDynamicRoleRepository roleRepository,
    IOptions<PermissionEngineOptions> engineOptions,
    ILogger<RoleInheritanceEngine> logger) : IRoleInheritanceEngine
{
    private readonly PermissionEngineOptions _options = engineOptions.Value;

    /// <inheritdoc />
    public async Task<RoleInheritanceClosure> GetClosureAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await roleRepository.GetByIdAsync(roleId, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return new RoleInheritanceClosure(
                null,
                Array.Empty<RoleAncestorContribution>(),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                CyclesCut: 0,
                DepthLimitReached: false);
        }

        if (!_options.Inheritance.Enabled)
        {
            return new RoleInheritanceClosure(
                role,
                Array.Empty<RoleAncestorContribution>(),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                CyclesCut: 0,
                DepthLimitReached: false);
        }

        var visited = new HashSet<Guid> { roleId };
        var ancestors = new List<RoleAncestorContribution>();
        var inheritedDenies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cyclesCut = 0;
        var depthLimitReached = false;

        // Breadth-first level walk: level 1 = direct parents, level 2 = grandparents, ...
        var currentLevel = new List<Guid>(role.GetEffectiveParentRoleIds());
        var depth = 0;

        while (currentLevel.Count > 0)
        {
            depth++;
            if (depth > _options.Inheritance.MaxDepth)
            {
                depthLimitReached = true;
                logger.LogWarning(
                    "Role inheritance depth limit {MaxDepth} reached while resolving role {RoleId} - deeper ancestors are ignored (PermissionEngine:Inheritance:MaxDepth).",
                    _options.Inheritance.MaxDepth,
                    roleId);
                break;
            }

            var nextLevelIds = new List<Guid>();
            var loadedRoles = await roleRepository
                .GetManyByIdAsync(currentLevel, cancellationToken)
                .ConfigureAwait(false);
            var byId = loadedRoles.ToDictionary(r => r.Id, r => r);

            foreach (var parentId in currentLevel)
            {
                // Cycle detection: a parent already visited (including the starting role)
                // would close a loop - cut the edge and continue.
                if (!visited.Add(parentId))
                {
                    cyclesCut++;
                    logger.LogWarning(
                        "Cycle detected in role inheritance graph at role {ParentId} while resolving {RoleId} - edge cut (fail-closed traversal).",
                        parentId,
                        roleId);
                    continue;
                }

                if (!byId.TryGetValue(parentId, out var parent))
                {
                    // Unknown parent reference contributes nothing (fail closed).
                    logger.LogWarning(
                        "Role {RoleId} references unknown parent role {ParentId} - parent ignored (fail-closed).",
                        roleId,
                        parentId);
                    continue;
                }

                var contributionPermissions = DirectPermissionsOf(parent);
                ancestors.Add(new RoleAncestorContribution(parent, depth, contributionPermissions));
                foreach (var deny in parent.DenyPermissions)
                {
                    inheritedDenies.Add(deny);
                }

                foreach (var grandParentId in parent.GetEffectiveParentRoleIds())
                {
                    if (!visited.Contains(grandParentId))
                    {
                        nextLevelIds.Add(grandParentId);
                    }
                }
            }

            currentLevel = nextLevelIds.Distinct().ToList();
        }

        // Compute the exact inherited set: each ancestor's blocked list filters what flows
        // INTO that ancestor; memoized post-order over the loaded graph.
        var graphRoles = new Dictionary<Guid, DynamicRole> { [role.Id] = role };
        foreach (var ancestor in ancestors)
        {
            graphRoles[ancestor.Role.Id] = ancestor.Role;
        }

        var memo = new Dictionary<Guid, IReadOnlySet<string>>();
        var visiting = new HashSet<Guid>();

        IReadOnlySet<string> InheritedSetOf(DynamicRole current)
        {
            if (memo.TryGetValue(current.Id, out var cached))
            {
                return cached;
            }

            // Cycle guard for the recursive flow computation.
            if (!visiting.Add(current.Id))
            {
                cyclesCut++;
                logger.LogWarning(
                    "Cycle encountered while computing inherited permission flow at role {RoleId} - flow cut at this edge.",
                    current.Id);
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var parentId in current.GetEffectiveParentRoleIds())
                {
                    if (!graphRoles.TryGetValue(parentId, out var parent))
                    {
                        continue;
                    }

                    foreach (var permission in DirectPermissionsOf(parent))
                    {
                        result.Add(permission);
                    }

                    foreach (var permission in InheritedSetOf(parent))
                    {
                        result.Add(permission);
                    }
                }

                foreach (var blocked in current.BlockedInheritedPermissions)
                {
                    result.Remove(blocked);
                }

                memo[current.Id] = result;
                return result;
            }
            finally
            {
                visiting.Remove(current.Id);
            }
        }

        var inheritedPermissions = InheritedSetOf(role);

        // Re-attribute per-ancestor contributions to the exact flowing union: an ancestor
        // is credited with the flowing permissions it directly owns (blocked sets of the
        // starting role already excluded by InheritedSetOf).
        var attributed = new List<RoleAncestorContribution>(ancestors.Count);
        foreach (var ancestor in ancestors)
        {
            var flowing = ancestor.Role.Permissions
                .Concat(StaticRolePermissions.GetStaticPermissions(ancestor.Role.Name))
                .Where(inheritedPermissions.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            attributed.Add(new RoleAncestorContribution(ancestor.Role, ancestor.Depth, flowing));
        }

        ancestors = attributed;

        if (cyclesCut > 0)
        {
            logger.LogWarning(
                "Role inheritance graph for role {RoleId} contained {CyclesCut} cycle edge(s); traversal completed with cycles cut.",
                roleId,
                cyclesCut);
        }

        return new RoleInheritanceClosure(
            role,
            ancestors,
            inheritedPermissions,
            inheritedDenies,
            cyclesCut,
            depthLimitReached);
    }

    /// <inheritdoc />
    public async Task<bool> WouldCreateCycleAsync(
        Guid roleId,
        IReadOnlyCollection<Guid> candidateParentIds,
        CancellationToken cancellationToken = default)
    {
        if (candidateParentIds.Count == 0)
        {
            return false;
        }

        if (candidateParentIds.Contains(roleId))
        {
            return true; // self-reference is a cycle
        }

        // Adding the edge roleId -> candidateParent closes a cycle iff roleId is already
        // an ancestor of the candidate (a directed path candidate ~~> roleId exists along
        // child -> parent edges). Walk UP from the candidates through parent edges; a
        // visited set guards against pre-existing cycles in persisted data.
        var frontier = new HashSet<Guid>(candidateParentIds);
        var visited = new HashSet<Guid>();

        while (frontier.Count > 0)
        {
            var batch = frontier.ToList();
            frontier.Clear();

            var batchRoles = await roleRepository
                .GetManyByIdAsync(batch, cancellationToken)
                .ConfigureAwait(false);

            foreach (var role in batchRoles)
            {
                if (!visited.Add(role.Id))
                {
                    continue;
                }

                foreach (var parentId in role.GetEffectiveParentRoleIds())
                {
                    if (parentId == roleId)
                    {
                        return true;
                    }

                    if (parentId != Guid.Empty)
                    {
                        frontier.Add(parentId);
                    }
                }
            }
        }

        return false;
    }

    private static IReadOnlyList<string> DirectPermissionsOf(DynamicRole role)
    {
        var permissions = new List<string>(role.Permissions.Length + 8);
        permissions.AddRange(role.Permissions);
        permissions.AddRange(StaticRolePermissions.GetStaticPermissions(role.Name));
        return permissions;
    }
}
