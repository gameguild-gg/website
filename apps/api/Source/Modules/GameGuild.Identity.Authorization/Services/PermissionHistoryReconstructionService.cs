using System.Text.Json;
using GameGuild.CQRS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Raised when effective permissions at a requested instant cannot be reconstructed
///     from the available history (issue #189: fail closed on incomplete data — an
///     explicit error, never a guess).
/// </summary>
public sealed class PermissionHistoryIncompleteException : Exception
{
    /// <summary>
    ///     The instant that could not be reconstructed.
    /// </summary>
    public DateTime RequestedTimestamp { get; }

    /// <summary>
    ///     The concrete coverage gaps found, one entry per blocker.
    /// </summary>
    public IReadOnlyList<string> Gaps { get; }

    public PermissionHistoryIncompleteException(DateTime requestedTimestamp, IReadOnlyList<string> gaps)
        : base(
            $"Permission history for {requestedTimestamp:O} cannot be reconstructed: "
            + string.Join("; ", gaps))
    {
        RequestedTimestamp = requestedTimestamp;
        Gaps = gaps;
    }
}

/// <summary>
///     A resource grant effective at a past instant.
/// </summary>
public sealed record HistoricalResourceGrant(
    string ResourceType,
    string ResourceId,
    IReadOnlyList<string> Permissions,
    DateTime GrantedAt,
    DateTime? ExpiresAt,
    bool IsOwner);

/// <summary>
///     A role assignment effective at a past instant, with the role's permission
///     contribution expanded through the inheritance graph as of that instant.
/// </summary>
public sealed record HistoricalRoleAssignment(
    Guid RoleId,
    string RoleName,
    IReadOnlyList<string> DirectPermissions,
    IReadOnlyList<string> InheritedPermissions);

/// <summary>
///     A role-to-role inheritance edge effective at a past instant.
/// </summary>
public sealed record HistoricalInheritanceEdge(
    Guid ChildRoleId,
    string ChildRoleName,
    Guid ParentRoleId,
    string ParentRoleName);

/// <summary>
///     Effective permission state at a past instant (issue #189: time-based permission
///     state reconstruction showing access rights at any point in time).
/// </summary>
public sealed record PermissionHistoryAtSnapshot(
    DateTime Timestamp,
    Guid? TenantId,
    Guid UserId,
    IReadOnlyList<string> TenantGrants,
    IReadOnlyList<string> TenantDenies,
    IReadOnlyList<string> EffectivePermissions,
    IReadOnlyList<HistoricalResourceGrant> ResourceGrants,
    IReadOnlyList<HistoricalRoleAssignment> RoleAssignments,
    IReadOnlyList<HistoricalInheritanceEdge> InheritanceEdges);

/// <summary>
///     One role-inheritance change event (issue #189: inheritance history tracking).
/// </summary>
public sealed record InheritanceHistoryEvent(
    DateTime Timestamp,
    Guid RoleId,
    string? RoleName,
    Guid? PerformedBy,
    string? Description,
    string? BeforeState,
    string? AfterState,
    Guid? AffectedUserId);

/// <summary>
///     Reconstructs effective permission state at an arbitrary past instant from the
///     permission audit log plus the grant tables (issue #189).
/// </summary>
/// <remarks>
///     <para><b>Data sources and their guarantees.</b>
///         <list type="bullet">
///             <item><c>ResourceUserPermission</c> — soft-revoked with <c>RevokedAt</c> and
///             bounded by <c>ExpiresAt</c>, so the table alone is a complete history:
///             effective-at-T is a column predicate.</item>
///             <item><c>TenantPermission</c> — revocations rewrite the permission array or
///             deactivate the row, so the table is NOT sufficient; the subject's
///             <see cref="PermissionAuditLog"/> stream (ResourceType
///             <c>TenantPermission</c>) is replayed instead. Replay is only accepted when
///             the stream covers the grant from inception (earliest entry carries a null
///             OldValue); otherwise reconstruction fails closed.</item>
///             <item><c>DynamicRoleAssignment</c> — removals hard-delete rows, so the
///             table only proves CURRENT assignments. Grants/removals recorded by the
///             governance audit (<c>RoleAssignment</c> entries) are replayed on top.
///             For instants before the tenant's first <c>RoleAssignment</c> audit entry,
///             reconstruction fails closed whenever the subject currently holds any
///             assignment (absence of pre-coverage removals cannot be proven).</item>
///             <item><c>DynamicRole</c> hierarchy — parent edges are replayed backwards
///             from the current graph through <c>RoleHierarchy</c> audit entries. For
///             instants before the tenant's first <c>RoleHierarchy</c> entry, any role in
///             the subject's closure that predates that boundary fails reconstruction
///             closed.</item>
///         </list>
///     </para>
///     <para>
///         <b>Fail closed:</b> any detected coverage gap raises
///         <see cref="PermissionHistoryIncompleteException"/> naming the gap. An empty
///         subject (no grants, no roles anywhere in the sources) reconstructs to an empty
///         snapshot without error.
///     </para>
/// </remarks>
public interface IPermissionHistoryReconstructionService
{
    /// <summary>
    ///     Reconstructs the subject's effective permission state at
    ///     <paramref name="timestamp"/> (UTC, inclusive boundary: changes recorded at
    ///     exactly <paramref name="timestamp"/> are included).
    /// </summary>
    Task<PermissionHistoryAtSnapshot> ReconstructAtAsync(
        Guid? tenantId,
        Guid userId,
        DateTime timestamp,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Lists role-inheritance change events in a window (ascending). The events carry
    ///     the pre/post hierarchy state and the cascading effects recorded at mutation
    ///     time (issue #188/#189 shared surface).
    /// </summary>
    Task<IReadOnlyList<InheritanceHistoryEvent>> GetInheritanceHistoryAsync(
        Guid? tenantId,
        DateTime? fromTimestamp = null,
        DateTime? toTimestamp = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Default reconstruction implementation over <see cref="IApplicationDbContext"/>.
/// </summary>
public sealed class PermissionHistoryReconstructionService(
    IApplicationDbContext context,
    ILogger<PermissionHistoryReconstructionService> logger) : IPermissionHistoryReconstructionService
{
    private static readonly string[] GrantChannelOperations =
    [
        nameof(PermissionOperationType.Grant),
        nameof(PermissionOperationType.Update),
        nameof(PermissionOperationType.Restore),
        nameof(PermissionOperationType.Revoke)
    ];

    /// <inheritdoc />
    public async Task<PermissionHistoryAtSnapshot> ReconstructAtAsync(
        Guid? tenantId,
        Guid userId,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        var timestampUtc = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
        if (timestampUtc > SystemClock.UtcNow)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timestamp),
                "Point-in-time reconstruction targets past instants only (fail closed).");
        }

        var gaps = new List<string>();

        var (tenantGrants, tenantDenies) = await ReplayTenantPermissionsAsync(
            tenantId, userId, timestampUtc, gaps, cancellationToken).ConfigureAwait(false);

        var resourceGrants = await LoadResourceGrantsAtAsync(
            tenantId, userId, timestampUtc, cancellationToken).ConfigureAwait(false);

        var (assignments, edges) = await ReconstructRolesAtAsync(
            tenantId, userId, timestampUtc, gaps, cancellationToken).ConfigureAwait(false);

        if (gaps.Count > 0)
        {
            throw new PermissionHistoryIncompleteException(timestampUtc, gaps);
        }

        // Effective = direct tenant grants + role contributions (direct + inherited),
        // minus the replayed deny set. Static role permissions (OWNER/ADMIN/...) are a
        // function of tenant membership, which is out of scope for this projection.
        var contributed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var grant in tenantGrants)
        {
            contributed.Add(grant);
        }

        foreach (var assignment in assignments)
        {
            foreach (var permission in assignment.DirectPermissions.Concat(assignment.InheritedPermissions))
            {
                contributed.Add(permission);
            }
        }

        foreach (var deny in tenantDenies)
        {
            contributed.Remove(deny);
        }

        logger.LogInformation(
            "Reconstructed permission history for user {UserId} at {Timestamp}: {EffectiveCount} effective permission(s)",
            userId,
            timestampUtc,
            contributed.Count);

        return new PermissionHistoryAtSnapshot(
            timestampUtc,
            tenantId,
            userId,
            tenantGrants,
            tenantDenies,
            contributed.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            resourceGrants,
            assignments,
            edges);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InheritanceHistoryEvent>> GetInheritanceHistoryAsync(
        Guid? tenantId,
        DateTime? fromTimestamp = null,
        DateTime? toTimestamp = null,
        CancellationToken cancellationToken = default)
    {
        var query = context.Set<PermissionAuditLog>()
            .AsNoTracking()
            .Where(log => log.ResourceType == "RoleHierarchy"
                && log.OperationType == PermissionOperationType.InheritanceChange);

        if (tenantId.HasValue)
        {
            query = query.Where(log => log.TenantId != null && log.TenantId.Value.Value == tenantId.Value);
        }

        if (fromTimestamp.HasValue)
        {
            query = query.Where(log => log.Timestamp >= fromTimestamp.Value);
        }

        if (toTimestamp.HasValue)
        {
            query = query.Where(log => log.Timestamp <= toTimestamp.Value);
        }

        var rows = await query
            .OrderBy(log => log.Timestamp)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(log => new InheritanceHistoryEvent(
                log.Timestamp,
                log.ResourceId ?? Guid.Empty,
                log.PermissionType,
                log.PerformedBy,
                log.Reason,
                log.OldValue,
                log.NewValue,
                log.UserId))
            .ToArray();
    }

    /// <summary>
    ///     Replays the subject's tenant-permission audit stream. Grant and deny channels
    ///     are separated by operation type; deny-removals are identified by the fixed
    ///     reason recorded by the deny-service ("Deny permissions removed").
    /// </summary>
    private async Task<(List<string> Grants, List<string> Denies)> ReplayTenantPermissionsAsync(
        Guid? tenantId,
        Guid userId,
        DateTime timestampUtc,
        List<string> gaps,
        CancellationToken cancellationToken)
    {
        var auditQuery = context.Set<PermissionAuditLog>()
            .AsNoTracking()
            .Where(log => log.ResourceType == "TenantPermission"
                && log.UserId == userId
                && log.Success
                && log.Timestamp <= timestampUtc);

        if (tenantId.HasValue)
        {
            auditQuery = auditQuery.Where(log => log.TenantId != null && log.TenantId.Value.Value == tenantId.Value);
        }

        var entries = await auditQuery
            .OrderBy(log => log.Timestamp)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var grantRow = await context.Set<TenantPermission>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                permission => permission.UserId == userId
                    && permission.TenantId == tenantId
                    && permission.GrantedAt <= timestampUtc,
                cancellationToken)
            .ConfigureAwait(false);

        if (entries.Count == 0)
        {
            if (grantRow is not null)
            {
                gaps.Add(
                    $"Tenant permission history for user {userId} has no audit coverage before {timestampUtc:O} " +
                    "although a grant record exists from that period (pre-audit grant); the state at the requested " +
                    "instant cannot be proven.");
            }

            return ([], []);
        }

        var earliest = entries[0];
        var earliestInitial = ParsePermissionList(earliest.OldValue);
        if (earliestInitial.Count > 0 && grantRow is not null && grantRow.GrantedAt < earliest.Timestamp)
        {
            gaps.Add(
                $"Tenant permission audit for user {userId} starts at {earliest.Timestamp:O} with a non-empty prior " +
                $"state while the grant record dates from {grantRow.GrantedAt:O}; the pre-audit state at the " +
                "requested instant cannot be proven.");
        }

        string? grantState = null;
        string? denyState = null;
        foreach (var entry in entries)
        {
            var denyChannel = entry.OperationType == PermissionOperationType.Deny
                || (entry.OperationType == PermissionOperationType.Revoke
                    && string.Equals(entry.Reason, "Deny permissions removed", StringComparison.Ordinal));

            if (denyChannel)
            {
                denyState = entry.NewValue;
            }
            else if (GrantChannelOperations.Contains(entry.OperationType.ToString()))
            {
                grantState = entry.NewValue;
            }
        }

        return (ParsePermissionList(grantState), ParsePermissionList(denyState));
    }

    /// <summary>
    ///     Resource grants are soft-revoked with timestamps, so the table alone answers
    ///     "effective at T" (GrantedAt &lt;= T, not revoked before/at T, not expired at T).
    /// </summary>
    private async Task<List<HistoricalResourceGrant>> LoadResourceGrantsAtAsync(
        Guid? tenantId,
        Guid userId,
        DateTime timestampUtc,
        CancellationToken cancellationToken)
    {
        var grants = await context.Set<ResourceUserPermission>()
            .AsNoTracking()
            .Where(permission => permission.UserId == userId
                && permission.TenantId.Value == tenantId
                && permission.GrantedAt <= timestampUtc
                && (permission.RevokedAt == null || permission.RevokedAt > timestampUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return grants
            .Where(permission => permission.ExpiresAt is not { } expiresAt || expiresAt > timestampUtc)
            .Select(permission => new HistoricalResourceGrant(
                permission.ResourceType,
                permission.ResourceId,
                permission.Permissions.ToArray(),
                permission.GrantedAt,
                permission.ExpiresAt,
                permission.IsOwner))
            .ToList();
    }

    /// <summary>
    ///     Reconstructs the subject's role assignments at T and the role-hierarchy edges
    ///     those roles depend on, failing closed when pre-coverage state cannot be proven.
    /// </summary>
    private async Task<(List<HistoricalRoleAssignment>, List<HistoricalInheritanceEdge>)> ReconstructRolesAtAsync(
        Guid? tenantId,
        Guid userId,
        DateTime timestampUtc,
        List<string> gaps,
        CancellationToken cancellationToken)
    {
        var roles = await context.Set<DynamicRole>()
            .AsNoTracking()
            .Where(role => role.TenantId == tenantId || role.TenantId == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var rolesById = roles.ToDictionary(role => role.Id, role => role);

        var currentAssignments = await context.Set<DynamicRoleAssignment>()
            .AsNoTracking()
            .Where(assignment => assignment.UserId == userId
                && (assignment.TenantId == tenantId || assignment.TenantId == null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var assignmentAudit = await context.Set<PermissionAuditLog>()
            .AsNoTracking()
            .Where(log => log.ResourceType == "RoleAssignment"
                && log.UserId == userId
                && log.Timestamp <= timestampUtc)
            .OrderBy(log => log.Timestamp)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var assignmentCoverageStart = assignmentAudit.Count > 0
            ? assignmentAudit[0].Timestamp
            : (DateTime?)null;

        if (assignmentCoverageStart is null || timestampUtc < assignmentCoverageStart)
        {
            // No audit-era evidence covers the requested instant: surviving rows can be
            // trusted only in isolation, but pre-coverage removals are unprovable, so a
            // subject that currently holds ANY assignment fails closed.
            if (currentAssignments.Count > 0)
            {
                gaps.Add(
                    $"Role-assignment audit coverage for user {userId} starts " +
                    $"{(assignmentCoverageStart is { } start ? start.ToString("O") : "never")}; the requested instant " +
                    $"{timestampUtc:O} predates it while the user currently holds assignment(s), so pre-coverage " +
                    "removals cannot be ruled out.");
            }

            if (gaps.Count > 0)
            {
                return ([], []);
            }
        }

        // Replay the audit era: assigned-at-T = (added entry <= T without a later removal
        // entry <= T) UNION (surviving row whose validity window covers T).
        var assignedRoleIds = new HashSet<Guid>();
        foreach (var assignment in currentAssignments)
        {
            if (IsAssignmentEffectiveAt(assignment, timestampUtc))
            {
                assignedRoleIds.Add(assignment.RoleId);
            }
        }

        foreach (var entry in assignmentAudit)
        {
            var roleId = entry.ResourceId;
            if (roleId is not { } auditedRole)
            {
                continue;
            }

            var removed = entry.OperationType == PermissionOperationType.InheritanceChange
                && entry.Reason is not null
                && entry.Reason.Contains("removed", StringComparison.OrdinalIgnoreCase);
            if (removed)
            {
                assignedRoleIds.Remove(auditedRole);
            }
            else
            {
                assignedRoleIds.Add(auditedRole);
            }
        }

        // Reconstruct the hierarchy at T: replay RoleHierarchy entries backwards from the
        // current graph, then fail closed for any closure role whose pre-coverage edges
        // cannot be proven.
        var parentsAt = await ReconstructHierarchyAtAsync(
            tenantId, timestampUtc, roles, assignedRoleIds, gaps, cancellationToken).ConfigureAwait(false);

        var assignmentsAt = new List<HistoricalRoleAssignment>();
        foreach (var roleId in assignedRoleIds)
        {
            if (!rolesById.TryGetValue(roleId, out var role))
            {
                // Role deleted since; its contribution at T is only known through audit.
                gaps.Add(
                    $"Role {roleId} assigned to user {userId} at {timestampUtc:O} no longer exists; its permission " +
                    "contribution at the requested instant cannot be proven.");
                continue;
            }

            var parents = parentsAt.TryGetValue(roleId, out var effectiveParents)
                ? effectiveParents
                : role.GetEffectiveParentRoleIds();
            var inherited = CollectInherited(roleId, rolesById, parents);

            assignmentsAt.Add(new HistoricalRoleAssignment(
                roleId,
                role.Name,
                role.Permissions.ToArray(),
                inherited.Order(StringComparer.OrdinalIgnoreCase).ToArray()));
        }

        if (gaps.Count > 0)
        {
            return ([], []);
        }

        var edges = new List<HistoricalInheritanceEdge>();
        foreach (var (childId, parentIds) in parentsAt)
        {
            if (!rolesById.TryGetValue(childId, out var child) || !assignedRoleIds.Contains(childId))
            {
                continue;
            }

            foreach (var parentId in parentIds)
            {
                if (rolesById.TryGetValue(parentId, out var parent))
                {
                    edges.Add(new HistoricalInheritanceEdge(childId, child.Name, parentId, parent.Name));
                }
            }
        }

        return (assignmentsAt, edges);
    }

    /// <summary>
    ///     Replays <c>RoleHierarchy</c> audit entries backwards from the current graph to
    ///     the requested instant, returning each role's effective parent set at T.
    /// </summary>
    private async Task<Dictionary<Guid, IReadOnlyList<Guid>>> ReconstructHierarchyAtAsync(
        Guid? tenantId,
        DateTime timestampUtc,
        List<DynamicRole> roles,
        IReadOnlySet<Guid> assignedRoleIds,
        List<string> gaps,
        CancellationToken cancellationToken)
    {
        var parentsAt = roles.ToDictionary(
            role => role.Id,
            role => (IReadOnlyList<Guid>)role.GetEffectiveParentRoleIds().ToArray());

        var hierarchyAuditQuery = context.Set<PermissionAuditLog>()
            .AsNoTracking()
            .Where(log => log.ResourceType == "RoleHierarchy"
                && log.OperationType == PermissionOperationType.InheritanceChange
                && log.Timestamp > timestampUtc);

        if (tenantId.HasValue)
        {
            hierarchyAuditQuery = hierarchyAuditQuery.Where(
                log => log.TenantId == null || log.TenantId.Value.Value == tenantId.Value);
        }

        var laterEntries = await hierarchyAuditQuery
            .OrderByDescending(log => log.Timestamp)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var entry in laterEntries)
        {
            if (entry.ResourceId is not { } roleId)
            {
                continue;
            }

            var beforeParents = ReadParentIdsFromAudit(entry.OldValue);
            if (beforeParents is not null)
            {
                parentsAt[roleId] = beforeParents;
            }
        }

        // Coverage: a role that existed before the first hierarchy audit entry has
        // unprovable edges for instants before that boundary — fail closed when the
        // subject's closure depends on it.
        var coverageQuery = context.Set<PermissionAuditLog>()
            .AsNoTracking()
            .Where(log => log.ResourceType == "RoleHierarchy"
                && log.OperationType == PermissionOperationType.InheritanceChange);

        if (tenantId.HasValue)
        {
            coverageQuery = coverageQuery.Where(
                log => log.TenantId == null || log.TenantId.Value.Value == tenantId.Value);
        }

        var hierarchyCoverageStart = await coverageQuery
            .MinAsync(log => (DateTime?)log.Timestamp, cancellationToken)
            .ConfigureAwait(false);

        if (hierarchyCoverageStart is null || timestampUtc < hierarchyCoverageStart)
        {
            foreach (var role in roles)
            {
                if (assignedRoleIds.Contains(role.Id)
                    && role.CreatedAt < (hierarchyCoverageStart ?? DateTime.MaxValue)
                    && role.GetEffectiveParentRoleIds().Count > 0)
                {
                    gaps.Add(
                        $"Role '{role.Name}' ({role.Id}) has parent edges that predate hierarchy audit coverage " +
                        $"{(hierarchyCoverageStart is { } start ? start.ToString("O") : "(none recorded)")}; its " +
                        $"inheritance at {timestampUtc:O} cannot be proven.");
                }
            }
        }

        return parentsAt;
    }

    private static bool IsAssignmentEffectiveAt(DynamicRoleAssignment assignment, DateTime timestampUtc)
    {
        if (!assignment.IsActive)
        {
            // The row survives but is inactive; whether it was active at T without a
            // toggle audit record is unprovable for pre-coverage instants, and the
            // replayed audit stream (post-coverage) handles audited re-grants.
            return false;
        }

        if (assignment.StartsAt.HasValue && assignment.StartsAt.Value > timestampUtc)
        {
            return false;
        }

        return assignment.ExpiresAt is not { } expiresAt || expiresAt > timestampUtc;
    }

    private static IReadOnlySet<string> CollectInherited(
        Guid roleId,
        IReadOnlyDictionary<Guid, DynamicRole> roles,
        IReadOnlyList<Guid> parents)
    {
        var inherited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<Guid> { roleId };
        var frontier = new Queue<Guid>(parents);

        while (frontier.Count > 0)
        {
            var currentId = frontier.Dequeue();
            if (!visited.Add(currentId) || !roles.TryGetValue(currentId, out var current))
            {
                continue;
            }

            foreach (var permission in current.Permissions)
            {
                inherited.Add(permission);
            }

            foreach (var parentId in current.GetEffectiveParentRoleIds())
            {
                frontier.Enqueue(parentId);
            }
        }

        return inherited;
    }

    private static List<string> ParsePermissionList(string? commaList)
    {
        if (string.IsNullOrWhiteSpace(commaList))
        {
            return [];
        }

        return commaList
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<Guid>? ReadParentIdsFromAudit(string? oldValueJson)
    {
        if (string.IsNullOrEmpty(oldValueJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(oldValueJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var parents = new List<Guid>();
            var hasAny = false;

            if (root.TryGetProperty("ParentRoleId", out var primary))
            {
                hasAny = true;
                if (primary.ValueKind == JsonValueKind.String && Guid.TryParse(primary.GetString(), out var parentId))
                {
                    parents.Add(parentId);
                }
            }

            if (root.TryGetProperty("AdditionalParentRoleIds", out var additional)
                && additional.ValueKind == JsonValueKind.Array)
            {
                hasAny = true;
                foreach (var element in additional.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String
                        && Guid.TryParse(element.GetString(), out var extraId))
                    {
                        parents.Add(extraId);
                    }
                }
            }

            return hasAny ? parents.Distinct().ToArray() : null;
        }
        catch (JsonException)
        {
            // Not the shape we can replay; leave the current (later) state — the coverage
            // checks remain the fail-closed guard.
            return null;
        }
    }
}
