using System.Text.Json;
using GameGuild.CQRS.Models;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Append-only policy version history query surface.
/// </summary>
public interface IPolicyVersionHistoryRepository
{
    /// <summary>
    ///     Lists every snapshot of a policy, oldest version first.
    /// </summary>
    Task<List<PolicyVersionHistory>> ListAsync(
        Guid policyId,
        PolicyVersionKind? kind = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resolves the latest committed version number of a policy (0 when none exists).
    /// </summary>
    Task<int> GetLatestVersionAsync(Guid policyId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Appends a snapshot row inside the caller's unit of work (does NOT call
    ///     SaveChanges — the snapshot commits atomically with the policy mutation).
    /// </summary>
    void Append(PolicyVersionHistory snapshot);
}

/// <summary>
///     EF implementation of the policy version history store.
/// </summary>
public class PolicyVersionHistoryRepository(IApplicationDbContext context) : IPolicyVersionHistoryRepository
{
    private DbSet<PolicyVersionHistory> Snapshots => context.Set<PolicyVersionHistory>();

    /// <inheritdoc />
    public async Task<List<PolicyVersionHistory>> ListAsync(
        Guid policyId,
        PolicyVersionKind? kind = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = Snapshots.AsNoTracking().Where(snapshot => snapshot.PolicyId == policyId);

        if (kind.HasValue)
        {
            query = query.Where(snapshot => snapshot.Kind == kind.Value);
        }

        if (tenantId.HasValue)
        {
            query = query.Where(snapshot => snapshot.TenantId == tenantId.Value);
        }

        return await query
            .OrderBy(snapshot => snapshot.Version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> GetLatestVersionAsync(Guid policyId, CancellationToken cancellationToken = default)
    {
        var latest = await Snapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.PolicyId == policyId)
            .MaxAsync(snapshot => (int?)snapshot.Version, cancellationToken)
            .ConfigureAwait(false);

        return latest ?? 0;
    }

    /// <inheritdoc />
    public void Append(PolicyVersionHistory snapshot) => Snapshots.Add(snapshot);
}

/// <summary>
///     Writes the append-only policy snapshot on every permission-policy mutation
///     (issue #188: permission policy change tracking with complete version history).
/// </summary>
/// <remarks>
///     Snapshots are appended inside the mutating repository's unit of work: either the
///     policy change and its snapshot commit together, or neither does. There is no
///     update or delete path — history is immutable.
/// </remarks>
public interface IPolicyVersionSnapshotService
{
    /// <summary>
    ///     Appends a <see cref="ConditionalPolicy"/> snapshot (version = previous + 1).
    ///     Must be called before the repository saves its changes.
    /// </summary>
    Task<PolicyVersionHistory> SnapshotConditionalPolicyAsync(
        ConditionalPolicy policy,
        PolicyVersionChangeType changeType,
        Guid? changedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Appends a <see cref="PolicyDefinitionEntity"/> snapshot (version = previous + 1).
    ///     Must be called before the repository saves its changes.
    /// </summary>
    Task<PolicyVersionHistory> SnapshotPolicyDefinitionAsync(
        PolicyDefinitionEntity policy,
        PolicyVersionChangeType changeType,
        Guid? changedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Default snapshot writer: serializes the policy state as JSON and appends the next
///     per-policy version row via <see cref="IPolicyVersionHistoryRepository.Append"/>.
/// </summary>
public class PolicyVersionSnapshotService(
    IPolicyVersionHistoryRepository historyRepository) : IPolicyVersionSnapshotService
{
    private static readonly JsonSerializerOptions SerializationOptions = new()
    {
        WriteIndented = false
    };

    /// <inheritdoc />
    public async Task<PolicyVersionHistory> SnapshotConditionalPolicyAsync(
        ConditionalPolicy policy,
        PolicyVersionChangeType changeType,
        Guid? changedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var version = await historyRepository
            .GetLatestVersionAsync(policy.Id, cancellationToken)
            .ConfigureAwait(false) + 1;

        var snapshot = new PolicyVersionHistory
        {
            PolicyId = policy.Id,
            Kind = PolicyVersionKind.ConditionalPolicy,
            TenantId = policy.TenantId?.Value,
            Version = version,
            ChangeType = changeType,
            SnapshotJson = JsonSerializer.Serialize(new
            {
                policy.Id,
                TenantId = policy.TenantId?.Value,
                policy.Name,
                policy.Description,
                policy.ConditionType,
                policy.PermissionType,
                policy.ResourceType,
                policy.Action,
                policy.Priority,
                policy.IsEnabled,
                policy.TimeConditions,
                policy.EnvironmentConditions,
                policy.LocationConditions,
                policy.DeviceConditions,
                policy.CustomConditions,
                policy.CreatedAt,
                policy.UpdatedAt,
                policy.CreatedBy
            }, SerializationOptions),
            PolicyName = policy.Name,
            ChangedBy = changedBy,
            Reason = reason,
            RecordedAt = SystemClock.UtcNow
        };

        historyRepository.Append(snapshot);
        return snapshot;
    }

    /// <inheritdoc />
    public async Task<PolicyVersionHistory> SnapshotPolicyDefinitionAsync(
        PolicyDefinitionEntity policy,
        PolicyVersionChangeType changeType,
        Guid? changedBy = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var version = await historyRepository
            .GetLatestVersionAsync(policy.Id, cancellationToken)
            .ConfigureAwait(false) + 1;

        var snapshot = new PolicyVersionHistory
        {
            PolicyId = policy.Id,
            Kind = PolicyVersionKind.PolicyDefinition,
            TenantId = policy.TenantId,
            Version = version,
            ChangeType = changeType,
            SnapshotJson = JsonSerializer.Serialize(new
            {
                policy.Id,
                policy.TenantId,
                policy.PolicyName,
                policy.PolicyVersion,
                policy.RequireAuthentication,
                policy.AuthenticationSchemesJson,
                policy.RequiredPermissionsJson,
                policy.RequiredRolesJson,
                policy.RequireAccessControlListAccess
            }, SerializationOptions),
            PolicyName = policy.PolicyName,
            ChangedBy = changedBy,
            Reason = reason,
            RecordedAt = SystemClock.UtcNow
        };

        historyRepository.Append(snapshot);
        return snapshot;
    }
}
