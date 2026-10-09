using GameGuild.Identity.Authorization.Utilities;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Read-only snapshot of the authorization state of a tenant: dynamic roles (with hierarchy),
///     valid role assignments, direct user grants, tenant defaults, and global defaults.
///     Shared by the permission graph builder and the impact analysis services so both reason
///     over exactly the same data and semantics as <see cref="EffectivePermissionResolverService"/>.
/// </summary>
/// <remarks>
///     <para>
///         Evaluation semantics mirror the runtime resolver: allows are unioned across the user's
///         assignment chains, direct grants, tenant defaults, and the data-driven global-default row
///         (ALLOW-WINS); exact-key denies then remove permissions (DENY-WINS). Wildcard grants
///         (<c>*</c> and <c>resource:*</c>) cover keys; denies match exact keys only.
///     </para>
///     <para>
///         One conservative divergence: inactive ancestor roles do not contribute to impact
///         evaluation, while the runtime resolver only checks the directly assigned role's
///         activity. Impact analysis intentionally never reports access held through inactive roles.
///     </para>
/// </remarks>
internal sealed class TenantAuthorizationSnapshot
{
    private readonly Dictionary<Guid, DynamicRole> _rolesById;
    private readonly Dictionary<Guid, List<Guid>> _childrenMap;
    private readonly Dictionary<Guid, List<DynamicRoleAssignment>> _assignmentsByUser;

    internal TenantAuthorizationSnapshot(
        Guid? tenantId,
        IReadOnlyList<DynamicRole> roles,
        IReadOnlyList<DynamicRoleAssignment> assignments,
        IReadOnlyList<TenantPermission> directUserGrants,
        TenantPermission? tenantDefault,
        TenantPermission? globalDefault)
    {
        TenantId = tenantId;
        Roles = roles;
        Assignments = assignments;
        DirectUserGrants = directUserGrants;
        TenantDefault = tenantDefault;
        GlobalDefault = globalDefault;

        _rolesById = roles.ToDictionary(r => r.Id, r => r);
        _childrenMap = [];
        foreach (var role in roles)
        {
            if (role.ParentRoleId is Guid parentId)
            {
                if (!_childrenMap.TryGetValue(parentId, out var children))
                {
                    children = [];
                    _childrenMap[parentId] = children;
                }

                children.Add(role.Id);
            }
        }

        _assignmentsByUser = assignments
            .GroupBy(a => a.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>Tenant scope of the snapshot (null = global).</summary>
    internal Guid? TenantId { get; }

    /// <summary>All dynamic roles in scope (tenant roles plus global roles).</summary>
    internal IReadOnlyList<DynamicRole> Roles { get; }

    /// <summary>Currently valid role assignments (active, started, not expired).</summary>
    internal IReadOnlyList<DynamicRoleAssignment> Assignments { get; }

    /// <summary>Active, unexpired direct per-user grants.</summary>
    internal IReadOnlyList<TenantPermission> DirectUserGrants { get; }

    /// <summary>Tenant-default permissions (row with null user), when present.</summary>
    internal TenantPermission? TenantDefault { get; }

    /// <summary>
    ///     Data-driven global-default permissions (row with null user and null tenant), when
    ///     present. There are no hard-coded global defaults; the baseline is a database row.
    /// </summary>
    internal TenantPermission? GlobalDefault { get; }

    /// <summary>Finds a role by id within the snapshot.</summary>
    internal DynamicRole? FindRole(Guid roleId) => _rolesById.GetValueOrDefault(roleId);

    /// <summary>
    ///     Returns the inheritance chain starting at the given role and walking parents,
    ///     cycle-guarded (stops when a role repeats). The first element is the role itself.
    /// </summary>
    internal IReadOnlyList<Guid> ChainFrom(Guid roleId)
    {
        var chain = new List<Guid>();
        var visited = new HashSet<Guid>();
        var currentId = roleId;
        while (currentId != default && _rolesById.ContainsKey(currentId) && visited.Add(currentId))
        {
            chain.Add(currentId);
            currentId = _rolesById[currentId].ParentRoleId ?? default;
        }

        return chain;
    }

    /// <summary>
    ///     Returns the ids of all roles whose inheritance chain passes through the given role
    ///     (the role itself plus its transitive children), cycle-guarded.
    /// </summary>
    internal IReadOnlyList<Guid> DescendantsOf(Guid roleId)
    {
        var result = new List<Guid> { roleId };
        var visited = new HashSet<Guid> { roleId };
        var queue = new Queue<Guid>();
        queue.Enqueue(roleId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!_childrenMap.TryGetValue(current, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                if (visited.Add(child))
                {
                    result.Add(child);
                    queue.Enqueue(child);
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     Evaluates whether a user currently holds a concrete permission key
    ///     (ALLOW-WINS across sources, then exact-key DENY-WINS).
    /// </summary>
    internal bool UserHolds(Guid userId, string key)
        => UserHolds(userId, key, deletedRoleId: null, keyRemovalRoleId: null, keyRemovalStoredKeys: null);

    /// <summary>
    ///     Simulates deletion of a role and evaluates whether the user would still hold the key.
    ///     Assignments to the deleted role disappear; assignments to descendants keep only the
    ///     part of their chain strictly below the deleted role (the parent pointer dangles).
    /// </summary>
    internal bool UserHoldsAfterRoleDeletion(Guid userId, string key, Guid deletedRoleId)
        => UserHolds(userId, key, deletedRoleId, keyRemovalRoleId: null, keyRemovalStoredKeys: null);

    /// <summary>
    ///     Simulates removal of one exact stored permission key from one role and evaluates
    ///     whether the user would still hold the key. Only the verbatim stored entries on that
    ///     specific role are ignored; other roles and sources are untouched.
    /// </summary>
    internal bool UserHoldsAfterRoleKeyRemoval(Guid userId, string key, Guid roleId, string storedKey)
        => UserHoldsAfterRoleKeyRemoval(userId, key, roleId, [storedKey]);

    /// <summary>
    ///     Simulates removal of a set of stored permission keys from one role and evaluates
    ///     whether the user would still hold the key.
    /// </summary>
    internal bool UserHoldsAfterRoleKeyRemoval(Guid userId, string key, Guid roleId, IReadOnlyCollection<string> storedKeys)
        => UserHolds(userId, key, deletedRoleId: null, keyRemovalRoleId: roleId, keyRemovalStoredKeys: storedKeys);

    private bool UserHolds(
        Guid userId,
        string key,
        Guid? deletedRoleId,
        Guid? keyRemovalRoleId,
        IReadOnlyCollection<string>? keyRemovalStoredKeys)
    {
        var allows = new List<string>();
        var denies = new List<string>();

        foreach (var assignment in AssignmentsOf(userId))
        {
            var chain = ChainFrom(assignment.RoleId);

            if (deletedRoleId.HasValue)
            {
                if (assignment.RoleId == deletedRoleId.Value)
                {
                    // Direct assignment to the deleted role: contributes nothing after deletion.
                    continue;
                }

                var deletedIndex = chain.ToList().IndexOf(deletedRoleId.Value);
                if (deletedIndex >= 0)
                {
                    // Descendant chain truncated below the deleted role.
                    chain = chain.Take(deletedIndex).ToList();
                }
            }

            CollectChain(chain, allows, denies, keyRemovalRoleId, keyRemovalStoredKeys);
        }

        var direct = DirectGrantOf(userId);
        if (direct is not null)
        {
            allows.AddRange(direct.Permissions);
            denies.AddRange(direct.DenyPermissions);
        }

        if (TenantDefault is not null)
        {
            allows.AddRange(TenantDefault.Permissions);
            denies.AddRange(TenantDefault.DenyPermissions);
        }

        if (GlobalDefault is not null)
        {
            allows.AddRange(GlobalDefault.Permissions);
            denies.AddRange(GlobalDefault.DenyPermissions);
        }

        return PermissionKeyMatcher.AnyCovers(allows, key)
            && !PermissionKeyMatcher.ContainsExact(denies, key);
    }

    private void CollectChain(
        IReadOnlyList<Guid> chain,
        List<string> allows,
        List<string> denies,
        Guid? keyRemovalRoleId,
        IReadOnlyCollection<string>? keyRemovalStoredKeys)
    {
        foreach (var roleId in chain)
        {
            var role = _rolesById.GetValueOrDefault(roleId);
            if (role is not { IsActive: true })
            {
                continue;
            }

            allows.AddRange(StaticRolePermissions.GetStaticPermissions(role.Name));

            if (keyRemovalRoleId == roleId && keyRemovalStoredKeys is not null)
            {
                allows.AddRange(role.Permissions.Where(stored =>
                    !keyRemovalStoredKeys.Contains(stored, StringComparer.OrdinalIgnoreCase)));
            }
            else
            {
                allows.AddRange(role.Permissions);
            }

            denies.AddRange(role.DenyPermissions);
        }
    }

    /// <summary>Currently valid assignments of a user in this tenant.</summary>
    internal IReadOnlyList<DynamicRoleAssignment> AssignmentsOf(Guid userId)
        => _assignmentsByUser.GetValueOrDefault(userId) ?? (IReadOnlyList<DynamicRoleAssignment>)[];

    /// <summary>The active, unexpired direct grant row of a user, when present.</summary>
    internal TenantPermission? DirectGrantOf(Guid userId)
        => DirectUserGrants.FirstOrDefault(g => g.UserId == userId);

    /// <summary>
    ///     All distinct users referenced by valid assignments or direct grants in this tenant.
    /// </summary>
    internal IEnumerable<Guid> UsersInScope()
        => Assignments.Select(a => a.UserId)
            .Concat(DirectUserGrants.Where(g => g.UserId.HasValue).Select(g => g.UserId!.Value))
            .Distinct();

    /// <summary>
    ///     All distinct permission keys visible in the snapshot, from stored arrays only
    ///     (dynamic roles, direct grants, tenant defaults) - static built-ins excluded.
    /// </summary>
    internal IEnumerable<string> StoredKeys()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in Roles)
        {
            CollectKey(role.Permissions, keys);
            CollectKey(role.DenyPermissions, keys);
        }

        foreach (var grant in DirectUserGrants)
        {
            CollectKey(grant.Permissions, keys);
            CollectKey(grant.DenyPermissions, keys);
        }

        if (TenantDefault is not null)
        {
            CollectKey(TenantDefault.Permissions, keys);
            CollectKey(TenantDefault.DenyPermissions, keys);
        }

        return keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     All distinct concrete (non-wildcard) permission keys visible in the snapshot's
    ///     stored arrays - the candidate set for impact simulation.
    /// </summary>
    internal IEnumerable<string> StoredConcreteKeys()
        => StoredKeys().Where(k => k != "*" && !k.EndsWith(":*", StringComparison.Ordinal));

    private static void CollectKey(IEnumerable<string> keys, ISet<string> target)
    {
        foreach (var key in keys)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                target.Add(key);
            }
        }
    }
}

/// <summary>
///     Loads <see cref="TenantAuthorizationSnapshot"/> instances from the authorization repositories.
/// </summary>
internal static class TenantAuthorizationSnapshotLoader
{
    /// <summary>
    ///     Loads the snapshot for a tenant: roles in scope (tenant + global), assignments,
    ///     direct grants, the tenant-default row, and the data-driven global-default row.
    /// </summary>
    internal static async Task<TenantAuthorizationSnapshot> LoadAsync(
        IDynamicRoleRepository roleRepository,
        IDynamicRoleAssignmentRepository assignmentRepository,
        ITenantPermissionRepository tenantPermissionRepository,
        Guid? tenantId,
        bool includeAssignments,
        CancellationToken ct = default)
    {
        var roles = await roleRepository.GetByTenantAsync(tenantId, includeGlobal: true, ct).ConfigureAwait(false);

        var assignments = includeAssignments
            ? await assignmentRepository.GetByTenantAsync(tenantId, ct).ConfigureAwait(false)
            : [];

        var assignmentsValid = assignments.Where(a => a.IsValid()).ToList();

        // Global defaults are a data-driven row (user null, tenant null) shared by every tenant.
        var globalDefault = await tenantPermissionRepository
            .GetByUserAndTenantAsync(null, null, ct)
            .ConfigureAwait(false);

        TenantPermission? tenantDefault = null;
        var directUserGrants = new List<TenantPermission>();
        if (tenantId.HasValue)
        {
            var tenantPermissions = await tenantPermissionRepository
                .GetByTenantAsync(tenantId.Value, ct)
                .ConfigureAwait(false);

            var now = SystemClock.UtcNow;
            directUserGrants = tenantPermissions
                .Where(p => p.UserId.HasValue && p.IsActive && (p.ExpiresAt is null || p.ExpiresAt.Value > now))
                .ToList();

            tenantDefault = tenantPermissions
                .FirstOrDefault(p => p.UserId is null && p.IsActive && (p.ExpiresAt is null || p.ExpiresAt.Value > now));
        }

        return new TenantAuthorizationSnapshot(
            tenantId,
            roles,
            assignmentsValid,
            directUserGrants,
            tenantDefault,
            globalDefault is { IsActive: true } && (globalDefault.ExpiresAt is null || globalDefault.ExpiresAt.Value > SystemClock.UtcNow)
                ? globalDefault
                : null);
    }
}
