using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     A persisted permission change relevant to governance auditing (role hierarchy,
///     role assignments, permission policies), mirrored from the EF change-capture
///     pipeline so this module stays independent of the host's persistence layer.
/// </summary>
public sealed record GovernancePermissionChange(
    string EntityType,
    string Operation,
    string? ResourceId,
    Guid? TargetUserId,
    Guid? TenantId,
    Guid ActorId,
    string? CorrelationId,
    string? CommandType,
    string? BeforeState,
    string? AfterState);

/// <summary>
///     Cascading effects of one role-inheritance mutation, computed against the live graph
///     at audit time (issue #188: inheritance change monitoring showing cascading effects).
/// </summary>
public sealed record RoleHierarchyCascade(
    Guid RoleId,
    string? RoleName,
    IReadOnlyList<Guid> AffectedDescendantRoleIds,
    IReadOnlyList<string> AffectedDescendantRoleNames,
    int AffectedUserCount,
    IReadOnlyList<string> InheritedPermissionsBefore,
    IReadOnlyList<string> InheritedPermissionsAfter,
    IReadOnlyList<string> PermissionsGained,
    IReadOnlyList<string> PermissionsLost);

/// <summary>
///     Writes <see cref="PermissionAuditLog"/> entries for the governance mutations the
///     generic EF permission hook captures but that previously escaped every audit path:
///     role-hierarchy edits (<see cref="DynamicRole"/> parent edges), role assignments
///     (<see cref="DynamicRoleAssignment"/>) and permission-policy mutations
///     (<see cref="ConditionalPolicy"/> / <see cref="PolicyDefinitionEntity"/>).
/// </summary>
/// <remarks>
///     Entries are written after the mutation has committed (the hook machinery flushes
///     post-commit). Audit failures are isolated by the caller and never roll back or
///     block the permission mutation itself.
/// </remarks>
public interface IRoleHierarchyAuditService
{
    /// <summary>
    ///     Computes the cascade of a role-hierarchy mutation: descendant roles affected,
    ///     users affected, and the inherited-permission delta for the mutated role using
    ///     the supplied pre/post parent sets.
    /// </summary>
    Task<RoleHierarchyCascade> ComputeCascadeAsync(
        Guid roleId,
        Guid? tenantId,
        IReadOnlyList<Guid>? beforeParentIds,
        IReadOnlyList<Guid>? afterParentIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Expands a role's effective permission set (direct + inherited, honoring
    ///     <see cref="DynamicRole.BlockedInheritedPermissions"/> and DENY-WINS) against the
    ///     live graph. Used for assignment audits and point-in-time expansion.
    /// </summary>
    Task<(IReadOnlyList<string> Direct, IReadOnlyList<string> Inherited)> ExpandRoleAsync(
        Guid roleId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Writes the governance audit entries for the supplied captured changes. Unknown
    ///     entity types are ignored (the generic permission audit path already covers them).
    /// </summary>
    Task WriteGovernanceAuditEntriesAsync(
        IReadOnlyList<GovernancePermissionChange> changes,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Default <see cref="IRoleHierarchyAuditService"/> implementation. All graph math is
///     done in memory over one tenant+global role load, mirroring the flow semantics of
///     <see cref="RoleInheritanceEngine"/> (multi-parent DAG, selective blocking,
///     DENY-WINS) with support for hypothetical pre/post parent sets.
/// </summary>
public sealed class RoleHierarchyAuditService(
    IDynamicRoleRepository roleRepository,
    IDynamicRoleAssignmentRepository assignmentRepository,
    IPermissionAuditService auditService,
    ILogger<RoleHierarchyAuditService> logger) : IRoleHierarchyAuditService
{
    private static readonly string[] HierarchyProperties =
    [
        "ParentRoleId",
        "AdditionalParentRoleIds",
        "BlockedInheritedPermissions"
    ];

    /// <inheritdoc />
    public async Task<RoleHierarchyCascade> ComputeCascadeAsync(
        Guid roleId,
        Guid? tenantId,
        IReadOnlyList<Guid>? beforeParentIds,
        IReadOnlyList<Guid>? afterParentIds,
        CancellationToken cancellationToken = default)
    {
        var roles = await LoadGraphAsync(tenantId, cancellationToken).ConfigureAwait(false);
        roles.TryGetValue(roleId, out var role);

        // Descendants: recursive expansion of roles inheriting from the mutated role on
        // the current (post-commit) graph. For deletes the graph no longer contains the
        // role, but dangling parent references in surviving children are still reported.
        var descendants = CollectDescendants(roleId, roles);

        var affectedUsers = 0;
        var affectedRoleIds = new HashSet<Guid> { roleId };
        foreach (var descendantId in descendants)
        {
            affectedRoleIds.Add(descendantId);
        }

        var assignments = await assignmentRepository
            .GetByTenantAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        foreach (var assignment in assignments)
        {
            if (affectedRoleIds.Contains(assignment.RoleId) && assignment.IsValid())
            {
                affectedUsers++;
            }
        }

        var before = ComputeInheritedSet(roleId, roles, beforeParentIds);
        var after = ComputeInheritedSet(roleId, roles, afterParentIds);

        return new RoleHierarchyCascade(
            roleId,
            role?.Name,
            descendants.ToArray(),
            descendants.Select(id => roles.TryGetValue(id, out var descendant) ? descendant.Name : id.ToString()).ToArray(),
            affectedUsers,
            before.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            after.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            after.Except(before, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            before.Except(after, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<string> Direct, IReadOnlyList<string> Inherited)> ExpandRoleAsync(
        Guid roleId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var roles = await LoadGraphAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (!roles.TryGetValue(roleId, out var role))
        {
            return (Array.Empty<string>(), Array.Empty<string>());
        }

        var inherited = ComputeInheritedSet(roleId, roles, role.GetEffectiveParentRoleIds());
        return (role.Permissions.ToArray(), inherited.Order(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    /// <inheritdoc />
    public async Task WriteGovernanceAuditEntriesAsync(
        IReadOnlyList<GovernancePermissionChange> changes,
        CancellationToken cancellationToken = default)
    {
        foreach (var change in changes)
        {
            try
            {
                switch (change.EntityType)
                {
                    case "DynamicRole":
                        await AuditRoleChangeAsync(change, cancellationToken).ConfigureAwait(false);
                        break;
                    case "DynamicRoleAssignment":
                        await AuditAssignmentChangeAsync(change, cancellationToken).ConfigureAwait(false);
                        break;
                    case "ConditionalPolicy":
                    case "PolicyDefinitionEntity":
                        await AuditPolicyChangeAsync(change, cancellationToken).ConfigureAwait(false);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Auditing must never surface as a permission-mutation failure.
                logger.LogWarning(
                    exception,
                    "Failed to write governance audit entry for {EntityType} {ResourceId}",
                    change.EntityType,
                    change.ResourceId);
            }
        }
    }

    private async Task AuditRoleChangeAsync(GovernancePermissionChange change, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(change.ResourceId, out var roleId))
        {
            logger.LogWarning("DynamicRole audit change without a parseable id: {ResourceId}", change.ResourceId);
            return;
        }

        var hierarchyRelevant = change.Operation is "Added" or "Deleted"
            || ReferencesHierarchyProperty(change.BeforeState)
            || ReferencesHierarchyProperty(change.AfterState);

        if (!hierarchyRelevant)
        {
            // Pure permission-list / metadata edits are covered by the generic permission
            // audit path (entity properties contain "Permission"); skip a duplicate entry.
            return;
        }

        var beforeParents = ReadParentIds(change.BeforeState);
        var afterParents = ReadParentIds(change.AfterState);
        var cascade = await ComputeCascadeAsync(
            roleId,
            change.TenantId,
            beforeParents,
            afterParents,
            cancellationToken).ConfigureAwait(false);

        var payload = JsonSerializer.Serialize(new
        {
            change.CommandType,
            change.CorrelationId,
            Operation = change.Operation,
            BeforeState = ParseJson(change.BeforeState),
            AfterState = ParseJson(change.AfterState),
            Cascade = new
            {
                cascade.AffectedDescendantRoleIds,
                cascade.AffectedDescendantRoleNames,
                cascade.AffectedUserCount,
                cascade.InheritedPermissionsBefore,
                cascade.InheritedPermissionsAfter,
                cascade.PermissionsGained,
                cascade.PermissionsLost
            }
        });

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.InheritanceChange,
            userId: null,
            performedBy: change.ActorId == Guid.Empty ? Guid.Empty : change.ActorId,
            tenantId: change.TenantId,
            permissionType: $"DynamicRole:{cascade.RoleName ?? roleId.ToString()}",
            resourceId: roleId,
            resourceType: "RoleHierarchy",
            oldValue: change.BeforeState,
            newValue: payload,
            reason: $"Role hierarchy {change.Operation.ToLowerInvariant()} (command: {change.CommandType ?? "direct"}).",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task AuditAssignmentChangeAsync(GovernancePermissionChange change, CancellationToken cancellationToken)
    {
        var (roleId, userId) = ReadAssignmentTarget(change);
        var expansion = await ExpandRoleAsync(roleId, change.TenantId, cancellationToken).ConfigureAwait(false);
        var granted = change.Operation == "Added";
        var affected = roleId == Guid.Empty
            ? Array.Empty<string>()
            : granted
                ? expansion.Direct.Concat(expansion.Inherited).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                : Array.Empty<string>();

        var payload = JsonSerializer.Serialize(new
        {
            change.CommandType,
            change.CorrelationId,
            Operation = change.Operation,
            RoleId = roleId,
            BeforeState = ParseJson(change.BeforeState),
            AfterState = ParseJson(change.AfterState),
            EffectivePermissionsForUser = affected,
            InheritedFrom = expansion.Inherited
        });

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.InheritanceChange,
            userId: userId,
            performedBy: change.ActorId == Guid.Empty ? Guid.Empty : change.ActorId,
            tenantId: change.TenantId,
            permissionType: $"RoleAssignment:{roleId}",
            resourceId: roleId,
            resourceType: "RoleAssignment",
            oldValue: change.BeforeState,
            newValue: payload,
            reason: granted
                ? $"Role assigned (command: {change.CommandType ?? "direct"}); inherited permissions expanded."
                : $"Role assignment removed (command: {change.CommandType ?? "direct"}).",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task AuditPolicyChangeAsync(GovernancePermissionChange change, CancellationToken cancellationToken)
    {
        Guid? policyId = Guid.TryParse(change.ResourceId, out var parsed) ? parsed : null;

        var payload = JsonSerializer.Serialize(new
        {
            change.CommandType,
            change.CorrelationId,
            Operation = change.Operation,
            BeforeState = ParseJson(change.BeforeState),
            AfterState = ParseJson(change.AfterState)
        });

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.PolicyChange,
            userId: null,
            performedBy: change.ActorId,
            tenantId: change.TenantId,
            permissionType: change.EntityType,
            resourceId: policyId,
            resourceType: change.EntityType,
            oldValue: change.BeforeState,
            newValue: payload,
            reason: $"Permission policy {change.Operation.ToLowerInvariant()} (command: {change.CommandType ?? "direct"}); version snapshot recorded in PolicyVersionHistory.",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<Dictionary<Guid, DynamicRole>> LoadGraphAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        var roles = await roleRepository
            .GetByTenantAsync(tenantId, includeGlobal: true, cancellationToken)
            .ConfigureAwait(false);
        return roles.ToDictionary(role => role.Id, role => role);
    }

    private static List<Guid> CollectDescendants(Guid roleId, IReadOnlyDictionary<Guid, DynamicRole> roles)
    {
        var descendants = new List<Guid>();
        var visited = new HashSet<Guid> { roleId };
        var frontier = new Queue<Guid>();
        frontier.Enqueue(roleId);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var (candidateId, candidate) in roles)
            {
                if (visited.Contains(candidateId))
                {
                    continue;
                }

                var parents = candidate.GetEffectiveParentRoleIds();
                if (!parents.Contains(current))
                {
                    continue;
                }

                visited.Add(candidateId);
                descendants.Add(candidateId);
                frontier.Enqueue(candidateId);
            }
        }

        return descendants;
    }

    /// <summary>
    ///     Inherited permission set for <paramref name="roleId"/> under an optional
    ///     hypothetical parent set, honoring selective blocking at every hop
    ///     (<see cref="DynamicRole.BlockedInheritedPermissions"/>) and cycle safety.
    ///     DENY-WINS is applied by evaluation time, not by this union.
    /// </summary>
    private static IReadOnlySet<string> ComputeInheritedSet(
        Guid roleId,
        IReadOnlyDictionary<Guid, DynamicRole> roles,
        IReadOnlyList<Guid>? overrideParents)
    {
        var inherited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (overrideParents is { Count: 0 })
        {
            return inherited;
        }

        var visited = new HashSet<Guid> { roleId };
        var frontier = new Queue<(Guid RoleId, IReadOnlyList<Guid> Parents)>();
        frontier.Enqueue((roleId, overrideParents ?? Array.Empty<Guid>()));

        while (frontier.Count > 0)
        {
            var (currentId, parents) = frontier.Dequeue();
            roles.TryGetValue(currentId, out var current);
            var blocked = current?.BlockedInheritedPermissions ?? Array.Empty<string>();
            var blockedSet = new HashSet<string>(blocked, StringComparer.OrdinalIgnoreCase);

            foreach (var parentId in parents)
            {
                if (!visited.Add(parentId) || !roles.TryGetValue(parentId, out var parent))
                {
                    continue;
                }

                foreach (var permission in parent.Permissions)
                {
                    if (!blockedSet.Contains(permission))
                    {
                        inherited.Add(permission);
                    }
                }

                frontier.Enqueue((parentId, parent.GetEffectiveParentRoleIds()));
            }
        }

        return inherited;
    }

    private static bool ReferencesHierarchyProperty(string? stateJson)
    {
        if (string.IsNullOrEmpty(stateJson))
        {
            return false;
        }

        foreach (var property in HierarchyProperties)
        {
            if (stateJson.Contains($"\"{property}\"", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<Guid>? ReadParentIds(string? stateJson)
    {
        if (string.IsNullOrEmpty(stateJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(stateJson);
        var root = document.RootElement;
        var hasAny = false;
        var parents = new List<Guid>();

        if (root.TryGetProperty("ParentRoleId", out var primary))
        {
            hasAny = true; // present in the modified-property set (null = parent removed)
            if (primary.ValueKind == JsonValueKind.String && Guid.TryParse(primary.GetString(), out var parentId))
            {
                parents.Add(parentId);
            }
        }

        if (root.TryGetProperty("AdditionalParentRoleIds", out var additional))
        {
            hasAny = true;
            if (additional.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in additional.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out var extraId))
                    {
                        parents.Add(extraId);
                    }
                }
            }
        }

        return hasAny ? parents.Distinct().ToArray() : null;
    }

    private static (Guid RoleId, Guid? UserId) ReadAssignmentTarget(GovernancePermissionChange change)
    {
        Guid? roleId = null;
        Guid? userId = null;

        foreach (var state in new[] { change.AfterState, change.BeforeState })
        {
            if (string.IsNullOrEmpty(state))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(state);
                if (roleId is null
                    && document.RootElement.TryGetProperty("RoleId", out var roleElement)
                    && roleElement.ValueKind == JsonValueKind.String
                    && Guid.TryParse(roleElement.GetString(), out var parsedRole))
                {
                    roleId = parsedRole;
                }

                if (userId is null
                    && document.RootElement.TryGetProperty("UserId", out var userElement)
                    && userElement.ValueKind == JsonValueKind.String
                    && Guid.TryParse(userElement.GetString(), out var parsedUser))
                {
                    userId = parsedUser;
                }
            }
            catch (JsonException)
            {
                // Best effort: the raw before/after states are preserved verbatim on the
                // audit entry even when the payload is not parseable JSON.
            }
        }

        return (roleId ?? Guid.Empty, userId ?? change.TargetUserId);
    }

    private static object? ParseJson(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
