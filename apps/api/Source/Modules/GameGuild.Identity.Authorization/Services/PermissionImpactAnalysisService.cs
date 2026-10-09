using GameGuild.Identity.Authorization.Models;
using GameGuild.Identity.Authorization.Utilities;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Read-only impact analysis over a <see cref="TenantAuthorizationSnapshot"/>: simulates
///     role deletions and permission-key removals and reports which users would lose or retain
///     access, with an aggregated severity. Nothing is persisted or mutated.
/// </summary>
public sealed class PermissionImpactAnalysisService(
    IDynamicRoleRepository roleRepository,
    IDynamicRoleAssignmentRepository assignmentRepository,
    ITenantPermissionRepository tenantPermissionRepository) : IPermissionImpactAnalysisService
{
    /// <summary>Maximum number of impacted users listed in a single result.</summary>
    public const int MaxListedUsers = 100;

    private static readonly int HighSeverityUserThreshold = 5;
    private static readonly int CriticalSeverityUserThreshold = 20;

    /// <inheritdoc />
    public async Task<RoleDeletionImpact> AnalyzeRoleDeletionAsync(Guid? tenantId, Guid roleId, CancellationToken ct = default)
    {
        var snapshot = await TenantAuthorizationSnapshotLoader
            .LoadAsync(roleRepository, assignmentRepository, tenantPermissionRepository, tenantId, includeAssignments: true, ct)
            .ConfigureAwait(false);

        var role = snapshot.FindRole(roleId);
        if (role is null)
        {
            return new RoleDeletionImpact
            {
                RoleFound = false,
                RoleId = roleId,
                Severity = ImpactSeverity.Low
            };
        }

        var childRoleIds = snapshot.Roles
            .Where(r => r.ParentRoleId == roleId)
            .Select(r => r.Id)
            .ToList();

        var assignedUsers = snapshot.Assignments
            .Where(a => a.RoleId == roleId)
            .Select(a => a.UserId)
            .Distinct()
            .ToList();

        var keys = snapshot.StoredConcreteKeys().ToList();
        var losingUsers = new List<ImpactedUser>();
        var warnings = new List<string>();

        foreach (var userId in snapshot.UsersInScope())
        {
            var lostKeys = new List<string>();
            foreach (var key in keys)
            {
                if (snapshot.UserHolds(userId, key)
                    && !snapshot.UserHoldsAfterRoleDeletion(userId, key, roleId))
                {
                    lostKeys.Add(key);
                }
            }

            if (lostKeys.Count > 0)
            {
                losingUsers.Add(new ImpactedUser
                {
                    UserId = userId,
                    Reason = $"Loses {lostKeys.Count} permission key(s) held only through this role's assignment or inheritance path.",
                    LostPermissionKeys = lostKeys
                });
            }
        }

        var totalLosingUsers = losingUsers.Count;
        if (totalLosingUsers > MaxListedUsers)
        {
            warnings.Add($"Impacted-user list truncated to {MaxListedUsers} entries; {totalLosingUsers} users are impacted in total.");
            losingUsers = losingUsers.Take(MaxListedUsers).ToList();
        }

        if (role.IsSystem)
        {
            warnings.Add("Role is a system role; deletion must be refused by the mutation endpoint.");
        }

        if (childRoleIds.Count > 0)
        {
            warnings.Add($"{childRoleIds.Count} child role(s) inherit from this role and must be re-parented before deletion.");
        }

        var wildcardKeys = role.Permissions
            .Where(k => k == "*" || k.EndsWith(":*", StringComparison.Ordinal))
            .ToList();
        if (wildcardKeys.Count > 0)
        {
            warnings.Add($"Role stores wildcard grant(s) ({string.Join(", ", wildcardKeys)}); deleting the role removes every concrete key they cover, including keys not stored anywhere else.");
        }

        if (ParticipatesInCycle(snapshot, roleId))
        {
            warnings.Add("Role participates in a role-inheritance cycle; hierarchy-derived impact may be understated.");
        }

        return new RoleDeletionImpact
        {
            RoleFound = true,
            RoleId = roleId,
            RoleName = role.Name,
            IsSystemRole = role.IsSystem,
            DirectAssignmentCount = snapshot.Assignments.Count(a => a.RoleId == roleId),
            AssignedUserIds = assignedUsers.Take(20).ToList(),
            ChildRoleIds = childRoleIds,
            UsersLosingPermissions = losingUsers,
            Severity = ClassifyRoleDeletionSeverity(role.IsSystem, totalLosingUsers, childRoleIds.Count),
            Warnings = warnings
        };
    }

    /// <inheritdoc />
    public async Task<PermissionRemovalImpact> AnalyzePermissionRemovalAsync(
        Guid? tenantId,
        Guid roleId,
        string permissionKey,
        CancellationToken ct = default)
    {
        var snapshot = await TenantAuthorizationSnapshotLoader
            .LoadAsync(roleRepository, assignmentRepository, tenantPermissionRepository, tenantId, includeAssignments: true, ct)
            .ConfigureAwait(false);

        var role = snapshot.FindRole(roleId);
        if (role is null)
        {
            return new PermissionRemovalImpact
            {
                RoleFound = false,
                RoleId = roleId,
                PermissionKey = permissionKey,
                Severity = ImpactSeverity.Low
            };
        }

        var warnings = new List<string>();
        var chain = snapshot.ChainFrom(roleId);
        var chainAllows = new List<string>();
        var chainDenies = new List<string>();
        foreach (var chainRoleId in chain)
        {
            if (snapshot.FindRole(chainRoleId) is not { IsActive: true } chainRole)
            {
                continue;
            }

            chainAllows.AddRange(chainRole.Permissions);
            chainDenies.AddRange(chainRole.DenyPermissions);
        }

        var isGranted = !string.IsNullOrWhiteSpace(permissionKey)
            && PermissionKeyMatcher.AnyCovers(chainAllows, permissionKey)
            && !PermissionKeyMatcher.ContainsExact(chainDenies, permissionKey);

        // The verbatim stored entries on the role itself that cover the requested key;
        // removing "the key from the role" removes these entries.
        var coveringStoredKeys = string.IsNullOrWhiteSpace(permissionKey)
            ? new List<string>()
            : role.Permissions.Where(stored => PermissionKeyMatcher.Covers(stored, permissionKey)).ToList();

        var losingUsers = new List<ImpactedUser>();
        var retainingUsers = new List<ImpactedUser>();

        if (isGranted)
        {
            foreach (var userId in snapshot.UsersInScope())
            {
                if (!snapshot.UserHolds(userId, permissionKey))
                {
                    continue;
                }

                if (!snapshot.UserHoldsAfterRoleKeyRemoval(userId, permissionKey, roleId, coveringStoredKeys))
                {
                    losingUsers.Add(new ImpactedUser
                    {
                        UserId = userId,
                        Reason = "Loses the permission key entirely: no other role, direct grant, or default provides it after the removal.",
                        LostPermissionKeys = [permissionKey]
                    });
                }
                else
                {
                    var retainedRoleIds = RoleChainsProvidingAfterRemoval(snapshot, userId, roleId, coveringStoredKeys, permissionKey);
                    var directGrant = snapshot.DirectGrantOf(userId);
                    var retainedViaDirect = directGrant is not null
                        && PermissionKeyMatcher.AnyCovers(directGrant.Permissions, permissionKey);

                    var reason = (retainedRoleIds.Count, retainedViaDirect) switch
                    {
                        (0, false) => "Retains the key through tenant or global default permissions.",
                        (_, true) => $"Retains the key through {retainedRoleIds.Count} other role path(s) and a direct grant.",
                        _ => $"Retains the key through {retainedRoleIds.Count} other role path(s)."
                    };

                    retainingUsers.Add(new ImpactedUser
                    {
                        UserId = userId,
                        Reason = reason,
                        RetainedViaRoleIds = retainedRoleIds,
                        RetainedViaDirectGrant = retainedViaDirect
                    });
                }
            }
        }

        if (losingUsers.Count > MaxListedUsers)
        {
            warnings.Add($"Impacted-user list truncated to {MaxListedUsers} entries; {losingUsers.Count} users lose the key in total.");
            losingUsers = losingUsers.Take(MaxListedUsers).ToList();
        }

        if (retainingUsers.Count > MaxListedUsers)
        {
            warnings.Add($"Retaining-user list truncated to {MaxListedUsers} entries; {retainingUsers.Count} users retain the key in total.");
            retainingUsers = retainingUsers.Take(MaxListedUsers).ToList();
        }

        if (!isGranted)
        {
            warnings.Add("The role's inheritance chain does not currently grant the key; the removal is a no-op.");
        }

        if (isGranted && coveringStoredKeys.Count == 0)
        {
            warnings.Add("The key reaches the role only through inheritance; remove it on the ancestor role that stores it.");
        }

        var wildcardCovers = coveringStoredKeys
            .Where(k => k == "*" || k.EndsWith(":*", StringComparison.Ordinal))
            .ToList();
        if (wildcardCovers.Count > 0)
        {
            warnings.Add($"Stored wildcard grant(s) ({string.Join(", ", wildcardCovers)}) cover the key; removing them also removes every sibling key they cover.");
        }

        if (chain.Any(id => snapshot.FindRole(id) is not { IsActive: true }))
        {
            warnings.Add("The role or one of its ancestors is inactive; the chain currently contributes nothing at check time.");
        }

        if (ParticipatesInCycle(snapshot, roleId))
        {
            warnings.Add("Role participates in a role-inheritance cycle; results may understate impact.");
        }

        return new PermissionRemovalImpact
        {
            RoleFound = true,
            RoleId = roleId,
            RoleName = role.Name,
            PermissionKey = permissionKey,
            IsGranted = isGranted,
            RemovableDirectly = coveringStoredKeys.Count > 0,
            GrantedViaInheritance = isGranted && coveringStoredKeys.Count == 0,
            DownstreamRoleIds = snapshot.DescendantsOf(roleId).Skip(1).ToList(),
            UsersLosingPermission = losingUsers,
            UsersRetainingPermission = retainingUsers,
            Severity = ClassifyPermissionRemovalSeverity(isGranted, coveringStoredKeys.Count, losingUsers.Count),
            Warnings = warnings
        };
    }

    private static IReadOnlyList<Guid> RoleChainsProvidingAfterRemoval(
        TenantAuthorizationSnapshot snapshot,
        Guid userId,
        Guid removedFromRoleId,
        IReadOnlyList<string> coveringStoredKeys,
        string key)
    {
        var providers = new List<Guid>();
        foreach (var assignment in snapshot.AssignmentsOf(userId))
        {
            var chain = snapshot.ChainFrom(assignment.RoleId);
            var allows = new List<string>();
            var denies = new List<string>();
            foreach (var chainRoleId in chain)
            {
                if (snapshot.FindRole(chainRoleId) is not { IsActive: true } chainRole)
                {
                    continue;
                }

                if (chainRoleId == removedFromRoleId)
                {
                    allows.AddRange(chainRole.Permissions.Where(stored =>
                        !coveringStoredKeys.Contains(stored, StringComparer.OrdinalIgnoreCase)));
                }
                else
                {
                    allows.AddRange(chainRole.Permissions);
                }

                denies.AddRange(chainRole.DenyPermissions);
            }

            if (PermissionKeyMatcher.AnyCovers(allows, key) && !PermissionKeyMatcher.ContainsExact(denies, key))
            {
                providers.Add(assignment.RoleId);
            }
        }

        return providers;
    }

    private static bool ParticipatesInCycle(TenantAuthorizationSnapshot snapshot, Guid roleId)
    {
        var path = new HashSet<Guid>();
        var current = snapshot.FindRole(roleId);
        while (current is not null)
        {
            if (!path.Add(current.Id))
            {
                return true;
            }

            current = current.ParentRoleId is Guid parentId
                ? snapshot.FindRole(parentId)
                : null;
        }

        return false;
    }

    private static ImpactSeverity ClassifyRoleDeletionSeverity(bool isSystemRole, int losingUsers, int childRoleCount)
    {
        if (isSystemRole || losingUsers > CriticalSeverityUserThreshold)
        {
            return ImpactSeverity.Critical;
        }

        if (losingUsers > HighSeverityUserThreshold || (losingUsers > 0 && childRoleCount > 0))
        {
            return ImpactSeverity.High;
        }

        return losingUsers > 0 ? ImpactSeverity.Medium : ImpactSeverity.Low;
    }

    private static ImpactSeverity ClassifyPermissionRemovalSeverity(bool isGranted, int coveringStoredKeys, int losingUsers)
    {
        if (losingUsers > CriticalSeverityUserThreshold)
        {
            return ImpactSeverity.Critical;
        }

        if (losingUsers > HighSeverityUserThreshold)
        {
            return ImpactSeverity.High;
        }

        return losingUsers > 0 || (isGranted && coveringStoredKeys == 0)
            ? ImpactSeverity.Medium
            : ImpactSeverity.Low;
    }
}
