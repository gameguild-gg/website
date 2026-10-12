namespace GameGuild.Identity.Authorization;

/// <summary>
///     Outcome of a single permission evaluation that is exposed to evaluation logging.
/// </summary>
public enum PermissionEvaluationOutcome
{
    /// <summary>The evaluated permission set was granted.</summary>
    Allow = 0,

    /// <summary>The evaluated permission set was denied (including unavailable resource identifiers or conditions).</summary>
    Deny = 1,

    /// <summary>The evaluation itself failed; the decision defaulted to closed. The permission was not granted.</summary>
    Error = 2,
}

/// <summary>
///     A single permission evaluation observation: who asked, in which tenant, for which
///     resource, which permission set, and what the outcome was. Evaluation records are
///     facts about authorization decisions; they never alter the decision they describe.
/// </summary>
/// <param name="UserId">Actor whose permissions were evaluated; taken from the trusted actor context.</param>
/// <param name="TenantId">Tenant scope the evaluation ran in; taken from the request context, never from input.</param>
/// <param name="Roles">
///     Role attribution of the evaluated user per the effective-permission contract (issue #330):
///     the role contributions the resolver would feed into the decision, with hierarchy-inherited
///     roles marked by <see cref="PermissionEvaluationRoles.InheritedSuffix"/>. Direct user grants
///     are not roles and never appear here. Never null; empty for anonymous or unattributed users.
///     Capped by <see cref="PermissionEvaluationRoles.Normalize"/> at 32 distinct entries with an
///     overflow marker when truncated.
/// </param>
/// <param name="ResourceType">Resource type identifier, for example <c>Project</c>.</param>
/// <param name="ResourceId">Optional resource identifier the permission was evaluated against.</param>
/// <param name="RequiredPermissions">Permission set the evaluation required.</param>
/// <param name="Outcome">Combined outcome of the evaluation.</param>
/// <param name="Source">Stable source tag identifying the calling surface, for example <c>graphql</c>.</param>
/// <param name="Operation">Optional operation or field name that triggered the evaluation.</param>
/// <param name="Reason">Optional machine-readable reason code, for example <c>permission_denied</c>.</param>
/// <param name="EvaluatedAtUtc">UTC timestamp of the evaluation; defaults to the current UTC time when unset.</param>
public sealed record PermissionEvaluationRecord(
    Guid? UserId,
    Guid? TenantId,
    IReadOnlyCollection<string> Roles,
    string ResourceType,
    string? ResourceId,
    IReadOnlyCollection<string> RequiredPermissions,
    PermissionEvaluationOutcome Outcome,
    string Source,
    string? Operation = null,
    string? Reason = null,
    DateTime EvaluatedAtUtc = default);

/// <summary>
///     Normalization and formatting rules for the role attribution carried on
///     <see cref="PermissionEvaluationRecord.Roles"/> (issue #359). Attribution follows the
///     effective-permission contract (issue #330): direct role assignments and roles reached
///     through role-hierarchy inheritance, the latter marked so auditors can distinguish
///     assigned from inherited authority.
/// </summary>
public static class PermissionEvaluationRoles
{
    /// <summary>Maximum number of distinct role entries retained on a record.</summary>
    public const int MaxRoles = 32;

    /// <summary>Suffix marking a role reached through hierarchy inheritance rather than direct assignment.</summary>
    public const string InheritedSuffix = " (inherited)";

    /// <summary>The empty, never-null role attribution used for anonymous or unattributed users.</summary>
    public static readonly IReadOnlyCollection<string> Empty = Array.Empty<string>();

    /// <summary>
    ///     Formats resolver role contributions (issue #330 contract) into record entries:
    ///     direct contributions use the role name, inherited ones append
    ///     <see cref="InheritedSuffix"/>. The result is deduplicated and capped by
    ///     <see cref="Normalize"/>.
    /// </summary>
    /// <param name="contributions">Role contributions reported by the permission resolver; null yields <see cref="Empty"/>.</param>
    public static IReadOnlyCollection<string> FromContributions(IEnumerable<RoleContribution>? contributions)
    {
        if (contributions is null)
        {
            return Empty;
        }

        return Normalize(contributions.Select(FormatContribution));

        static string FormatContribution(RoleContribution contribution)
            => contribution.IsInherited ? contribution.RoleName + InheritedSuffix : contribution.RoleName;
    }

    /// <summary>
    ///     Normalizes a role list for durable logging: drops null/blank entries, deduplicates
    ///     case-insensitively preserving first-seen order, and caps the result at
    ///     <see cref="MaxRoles"/> distinct entries; truncation is signalled by a trailing
    ///     <c>+N more roles</c> overflow marker. Never returns null; a null input yields
    ///     <see cref="Empty"/> (anonymous or unattributed users).
    /// </summary>
    /// <param name="roles">Raw role entries; may be null or contain blanks and duplicates.</param>
    public static IReadOnlyCollection<string> Normalize(IEnumerable<string>? roles)
    {
        if (roles is null)
        {
            return Empty;
        }

        var distinct = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            var trimmed = role?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            if (seen.Add(trimmed))
            {
                distinct.Add(trimmed);
            }
        }

        if (distinct.Count <= MaxRoles)
        {
            return distinct;
        }

        var overflow = distinct.Count - MaxRoles;
        var capped = distinct.Take(MaxRoles).ToList();
        capped.Add($"+{overflow} more roles");
        return capped;
    }
}

/// <summary>
///     Durable persistence target for permission evaluation records. Implementations
///     report whether the record was persisted so delivery failures stay observable.
/// </summary>
public interface IPermissionEvaluationLogSink
{
    /// <summary>Attempts to persist one evaluation record.</summary>
    /// <returns><see langword="true"/> when the record was durably persisted; <see langword="false"/> otherwise.</returns>
    Task<bool> TryRecordAsync(PermissionEvaluationRecord record, CancellationToken cancellationToken = default);
}

/// <summary>
///     Aggregated delivery result for one evaluation record across all configured sinks.
/// </summary>
/// <param name="Persisted">Whether at least one sink persisted the record.</param>
/// <param name="SinkCount">Number of configured durable sinks.</param>
/// <param name="FailureCount">Number of sinks that threw or reported a failed write.</param>
public sealed record PermissionEvaluationLogResult(
    bool Persisted,
    int SinkCount,
    int FailureCount);

/// <summary>
///     Records permission evaluations (user, tenant, resource, permission set, outcome)
///     for auditing and monitoring. Recording never changes the authorization decision:
///     failures are surfaced through the returned result and structured logging instead
///     of being thrown or silently swallowed.
/// </summary>
public interface IPermissionEvaluationLogService
{
    /// <summary>Records one permission evaluation in every configured sink.</summary>
    /// <param name="record">The evaluation observation to record.</param>
    /// <param name="cancellationToken">Cancellation token for the durable writes.</param>
    /// <returns>Aggregated delivery result; never throws for sink failures.</returns>
    Task<PermissionEvaluationLogResult> RecordAsync(
        PermissionEvaluationRecord record,
        CancellationToken cancellationToken = default);
}
