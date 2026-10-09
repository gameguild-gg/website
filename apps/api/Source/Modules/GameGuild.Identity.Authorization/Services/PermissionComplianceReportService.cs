using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Durable <see cref="IPermissionEvaluationLogSink"/> persisting evaluation records to
///     the <c>PermissionEvaluationLogs</c> table (issues #359, #358), plus the read side
///     used by the permission compliance report.
/// </summary>
public interface IPermissionEvaluationLogEntryRepository
{
    /// <summary>Loads evaluation rows for a tenant and time range (fail-closed: unbounded ranges are rejected).</summary>
    Task<IReadOnlyList<PermissionEvaluationLogEntry>> GetRangeAsync(
        Guid? tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Database-backed evaluation log sink and range reader.
/// </summary>
public class PermissionEvaluationLogEntryRepository(
    IApplicationDbContext context,
    ILogger<PermissionEvaluationLogEntryRepository> logger) : IPermissionEvaluationLogSink, IPermissionEvaluationLogEntryRepository
{
    private DbSet<PermissionEvaluationLogEntry> Entries => context.Set<PermissionEvaluationLogEntry>();

    /// <inheritdoc />
    public async Task<bool> TryRecordAsync(PermissionEvaluationRecord record, CancellationToken cancellationToken = default)
    {
        try
        {
            Entries.Add(PermissionEvaluationLogEntry.FromRecord(record));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Fail soft: logging must never change the authorization decision.
            logger.LogError(
                exception,
                "Failed to persist permission evaluation record for {ResourceType} {ResourceId}; the authorization decision is unaffected.",
                record.ResourceType,
                record.ResourceId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PermissionEvaluationLogEntry>> GetRangeAsync(
        Guid? tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        if (toUtc <= fromUtc)
        {
            return Array.Empty<PermissionEvaluationLogEntry>();
        }

        var query = Entries.AsNoTracking().Where(entry => entry.EvaluatedAtUtc >= fromUtc && entry.EvaluatedAtUtc <= toUtc);
        if (tenantId.HasValue)
        {
            query = query.Where(entry => entry.TenantId == tenantId.Value);
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     Builds permission-effectiveness compliance reports (issue #358) by aggregating the
///     durable permission evaluation log: overall and per-dimension allow/deny rates for
///     a tenant and time range.
/// </summary>
public interface IPermissionComplianceReportService
{
    /// <summary>
    ///     Builds the compliance report for a tenant. Tenant is taken from the trusted
    ///     actor/request context by the calling surface, never from report input.
    /// </summary>
    Task<PermissionComplianceReport> BuildReportAsync(
        Guid? tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Compliance report over recorded permission evaluations.
/// </summary>
/// <param name="TenantId">Tenant scope of the report (null = global report).</param>
/// <param name="FromUtc">Inclusive range start.</param>
/// <param name="ToUtc">Inclusive range end.</param>
/// <param name="TotalEvaluations">Recorded evaluations in range.</param>
/// <param name="AllowCount">Evaluations with outcome Allow.</param>
/// <param name="DenyCount">Evaluations with outcome Deny.</param>
/// <param name="ErrorCount">Evaluations with outcome Error (failed closed).</param>
/// <param name="ByPermission">Effectiveness breakdown per evaluated permission.</param>
/// <param name="BySource">Effectiveness breakdown per evaluation surface (the layer that reported the evaluation, e.g. <c>graphql</c>).</param>
/// <param name="ByOperation">Effectiveness breakdown per triggering operation (when recorded).</param>
public sealed record PermissionComplianceReport(
    Guid? TenantId,
    DateTime FromUtc,
    DateTime ToUtc,
    long TotalEvaluations,
    long AllowCount,
    long DenyCount,
    long ErrorCount,
    IReadOnlyList<PermissionComplianceBreakdown> ByPermission,
    IReadOnlyList<PermissionComplianceBreakdown> BySource,
    IReadOnlyList<PermissionComplianceBreakdown> ByOperation)
{
    /// <summary>Share of evaluations that allowed (0-1). Denominator excludes nothing.</summary>
    public double AllowRate => TotalEvaluations == 0 ? 0 : (double)AllowCount / TotalEvaluations;

    /// <summary>Share of evaluations that denied (0-1).</summary>
    public double DenyRate => TotalEvaluations == 0 ? 0 : (double)DenyCount / TotalEvaluations;

    /// <summary>Share of evaluations that errored and failed closed (0-1).</summary>
    public double ErrorRate => TotalEvaluations == 0 ? 0 : (double)ErrorCount / TotalEvaluations;
}

/// <summary>
///     Allow/deny rates for one reporting dimension value.
/// </summary>
/// <param name="Key">The dimension value (permission, source surface or operation).</param>
/// <param name="Total">Evaluations for this value.</param>
/// <param name="Allow">Allow outcomes.</param>
/// <param name="Deny">Deny outcomes.</param>
/// <param name="Error">Error outcomes.</param>
public sealed record PermissionComplianceBreakdown(
    string Key,
    long Total,
    long Allow,
    long Deny,
    long Error)
{
    /// <summary>Deny share for this value (0-1).</summary>
    public double DenyRate => Total == 0 ? 0 : (double)Deny / Total;

    /// <summary>Allow share for this value (0-1).</summary>
    public double AllowRate => Total == 0 ? 0 : (double)Allow / Total;
}

/// <summary>
///     Default compliance report builder: aggregates the evaluation log in memory after
///     loading the requested range (bounded by the caller-supplied window).
/// </summary>
public sealed class PermissionComplianceReportService(
    IPermissionEvaluationLogEntryRepository repository,
    ILogger<PermissionComplianceReportService> logger) : IPermissionComplianceReportService
{
    /// <inheritdoc />
    public async Task<PermissionComplianceReport> BuildReportAsync(
        Guid? tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        if (toUtc <= fromUtc)
        {
            throw new ArgumentException("The report range end must be after its start.", nameof(toUtc));
        }

        var entries = await repository.GetRangeAsync(tenantId, fromUtc, toUtc, cancellationToken).ConfigureAwait(false);

        long allow = 0, deny = 0, error = 0;
        var byPermission = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
        var bySource = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
        var byOperation = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);

        // index 0 = allow, 1 = deny, 2 = error
        long[] Bucket(Dictionary<string, long[]> map, string key)
        {
            if (!map.TryGetValue(key, out var bucket))
            {
                bucket = new long[3];
                map[key] = bucket;
            }

            return bucket;
        }

        foreach (var entry in entries)
        {
            var outcomeIndex = entry.Outcome switch
            {
                PermissionEvaluationOutcome.Allow => 0,
                PermissionEvaluationOutcome.Deny => 1,
                _ => 2
            };
            switch (outcomeIndex)
            {
                case 0: allow++; break;
                case 1: deny++; break;
                default: error++; break;
            }

            var sourceKey = string.IsNullOrWhiteSpace(entry.Source) ? "<unspecified>" : entry.Source;
            Bucket(bySource, sourceKey)[outcomeIndex]++;

            if (!string.IsNullOrWhiteSpace(entry.Operation))
            {
                Bucket(byOperation, entry.Operation!)[outcomeIndex]++;
            }

            // Multi-permission evaluations credit each required permission; the outcome
            // applies to the whole evaluated set.
            foreach (var permission in entry.RequiredPermissions.DefaultIfEmpty("<unspecified>"))
            {
                Bucket(byPermission, permission)[outcomeIndex]++;
            }
        }

        logger.LogInformation(
            "Built permission compliance report for tenant {TenantId} over [{FromUtc}, {ToUtc}]: {Total} evaluations, {Allow} allow, {Deny} deny, {Error} error.",
            tenantId,
            fromUtc,
            toUtc,
            entries.Count,
            allow,
            deny,
            error);

        return new PermissionComplianceReport(
            tenantId,
            fromUtc,
            toUtc,
            entries.Count,
            allow,
            deny,
            error,
            Breakdowns(byPermission),
            Breakdowns(bySource),
            Breakdowns(byOperation));
    }

    private static IReadOnlyList<PermissionComplianceBreakdown> Breakdowns(Dictionary<string, long[]> map)
        => map.Select(pair => new PermissionComplianceBreakdown(pair.Key, pair.Value[0] + pair.Value[1] + pair.Value[2], pair.Value[0], pair.Value[1], pair.Value[2]))
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
