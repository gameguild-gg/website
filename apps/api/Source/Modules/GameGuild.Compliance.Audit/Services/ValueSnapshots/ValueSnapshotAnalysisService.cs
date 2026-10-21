using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

/// <summary>Statistical summary of how one field changed over a time window (issue #176).</summary>
public sealed record ValueTrendStatistics(
    int ChangeCount,
    int DistinctValues,
    DateTime FirstObservedAtUtc,
    DateTime LastObservedAtUtc,
    string? FirstValue,
    string? LastValue,
    decimal? MinNumeric,
    decimal? MaxNumeric,
    decimal? AverageNumeric,
    decimal? NetChange,
    double ChangesPerDay,
    double LongestStableDays);

public sealed record ValueTrendResponse(
    string EntityName,
    string KeyId,
    string Field,
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<ValueHistoryEntry> Series,
    ValueTrendStatistics Statistics);

/// <summary>Cascading-effect analysis of one mutation across its operation and entity lineage (issue #176).</summary>
public sealed record ChangeImpactChange(
    Guid SnapshotId,
    string EntityName,
    string KeyId,
    string ChangeKind,
    int DiffCount,
    DateTimeOffset OccurredAtUtc,
    Guid? ActorId,
    bool IsAnchor);

public sealed record ChangeImpactResponse(
    Guid AnchorSnapshotId,
    string EntityName,
    string KeyId,
    string? CorrelationId,
    int SameOperationChangeCount,
    int EntityHistoryChangeCount,
    int EntitiesTouched,
    DateTimeOffset? CascadeStartedAtUtc,
    DateTimeOffset? CascadeEndedAtUtc,
    IReadOnlyList<ChangeImpactChange> SameOperationChanges,
    IReadOnlyList<ChangeImpactChange> EntityHistory,
    IReadOnlyList<ValueSnapshotFieldDiffResponse> AnchorFieldDiffs);

/// <summary>
///     Change impact analysis and value trend analysis over the mutation value snapshot store
///     (issue #176). Impact analysis reconstructs the cascade of one mutation through its
///     correlation group (every change staged by the same use-case operation) and the entity's
///     own change lineage; trend analysis aggregates one field's change series with statistics.
/// </summary>
public interface IValueSnapshotAnalysisService
{
    Task<ChangeImpactResponse?> GetImpactAsync(Guid snapshotId, CancellationToken cancellationToken);

    Task<ValueTrendResponse?> GetTrendAsync(string entityName, string keyId, string field, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}

public sealed class ValueSnapshotAnalysisService(
    IActorContextAccessor actors,
    IApplicationDbContext context,
    ILogger<ValueSnapshotAnalysisService> logger) : IValueSnapshotAnalysisService
{
    private const int TrendSeriesLimit = 2_000;

    public async Task<ChangeImpactResponse?> GetImpactAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        var tenantId = RequireAdministrator();
        var anchor = await context.Set<MutationValueSnapshot>().AsNoTracking()
            .FirstOrDefaultAsync(snapshot => snapshot.TenantId == tenantId && snapshot.Id == snapshotId, cancellationToken)
            .ConfigureAwait(false);
        if (anchor is null)
        {
            return null;
        }

        // The correlation id groups every mutation staged inside the same SaveChanges/use-case
        // operation — the direct cascade of the anchor change.
        var cascade = await context.Set<MutationValueSnapshot>().AsNoTracking()
            .Where(snapshot => snapshot.TenantId == tenantId
                && snapshot.CorrelationId != null
                && snapshot.CorrelationId == anchor.CorrelationId)
            .OrderBy(snapshot => snapshot.OccurredAtUtc)
            .ThenBy(snapshot => snapshot.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (cascade.Count == 0)
        {
            cascade = [anchor];
        }

        // The entity's own lineage: every recorded change touching the same entity key, which is
        // how downstream modifications relate back to the anchor even across operations.
        var lineage = await context.Set<MutationValueSnapshot>().AsNoTracking()
            .Where(snapshot => snapshot.TenantId == tenantId
                && snapshot.EntityName == anchor.EntityName
                && snapshot.KeyId == anchor.KeyId)
            .OrderBy(snapshot => snapshot.OccurredAtUtc)
            .ThenBy(snapshot => snapshot.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var cascadingChanges = cascade
            .Select(snapshot => ToImpactChange(snapshot, snapshot.Id == anchor.Id))
            .ToList();
        var historyChanges = lineage
            .Select(snapshot => ToImpactChange(snapshot, snapshot.Id == anchor.Id))
            .ToList();
        var timestamps = cascade.Select(snapshot => snapshot.OccurredAtUtc).ToList();

        return new ChangeImpactResponse(
            anchor.Id,
            anchor.EntityName,
            anchor.KeyId,
            anchor.CorrelationId,
            cascade.Count,
            lineage.Count,
            cascade.Select(snapshot => snapshot.EntityName).Distinct(StringComparer.Ordinal).Count(),
            timestamps.Count == 0 ? null : timestamps.Min().UtcDateTime,
            timestamps.Count == 0 ? null : timestamps.Max().UtcDateTime,
            cascadingChanges,
            historyChanges,
            ValueSnapshotDiffEngine.DeserializeDiffs(anchor.StructuredDiffJson)
                .Select(diff => new ValueSnapshotFieldDiffResponse(
                    diff.Field, diff.ChangeKind, diff.Before, diff.After, diff.BeforeTruncated, diff.AfterTruncated, diff.NumericDelta))
                .ToList());
    }

    public async Task<ValueTrendResponse?> GetTrendAsync(
        string entityName,
        string keyId,
        string field,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireAdministrator();
        if (string.IsNullOrWhiteSpace(entityName) || string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(field))
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["EntityName"] = ["Entity name, key id, and field are required."],
                ["KeyId"] = ["Entity name, key id, and field are required."],
                ["Field"] = ["Entity name, key id, and field are required."]
            });
        }

        if (toUtc <= fromUtc)
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["ToUtc"] = ["The window end must be after the window start."]
            });
        }

        var from = new DateTimeOffset(fromUtc, TimeSpan.Zero);
        var to = new DateTimeOffset(toUtc, TimeSpan.Zero);
        var snapshots = await context.Set<MutationValueSnapshot>().AsNoTracking()
            .Where(snapshot => snapshot.TenantId == tenantId
                && snapshot.EntityName == entityName
                && snapshot.KeyId == keyId
                && snapshot.OccurredAtUtc >= from
                && snapshot.OccurredAtUtc <= to)
            .OrderBy(snapshot => snapshot.OccurredAtUtc)
            .ThenBy(snapshot => snapshot.Id)
            .Take(TrendSeriesLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (snapshots.Count == 0)
        {
            return null;
        }

        // Carry the last observed value forward over untouched snapshots so the series is a
        // complete point-in-time view of the field, then reduce to actual change events.
        var series = new List<ValueHistoryEntry>();
        var fieldChanged = new List<ValueHistoryEntry>();
        string? lastValue = null;
        foreach (var snapshot in snapshots)
        {
            var diffs = ValueSnapshotDiffEngine.DeserializeDiffs(snapshot.StructuredDiffJson);
            var fieldDiff = diffs.FirstOrDefault(diff => PathMatches(diff.Field, field));
            string? value;
            var truncated = false;
            if (fieldDiff is not null && snapshot.ChangeKind != "Deleted")
            {
                value = fieldDiff.After;
                truncated = fieldDiff.AfterTruncated;
                series.Add(new ValueHistoryEntry(snapshot.Id, snapshot.OccurredAtUtc, snapshot.ActorId, snapshot.ChangeKind, value, truncated));
                fieldChanged.Add(series[^1]);
                lastValue = value;
            }
            else if (snapshot.ChangeKind == "Added")
            {
                value = ReadFieldFromPayload(snapshot.AfterPayload, field);
                if (value is not null)
                {
                    series.Add(new ValueHistoryEntry(snapshot.Id, snapshot.OccurredAtUtc, snapshot.ActorId, snapshot.ChangeKind, value, false));
                    fieldChanged.Add(series[^1]);
                    lastValue = value;
                }
            }
            else if (snapshot.ChangeKind == "Deleted")
            {
                value = null;
                series.Add(new ValueHistoryEntry(snapshot.Id, snapshot.OccurredAtUtc, snapshot.ActorId, snapshot.ChangeKind, null, false));
                fieldChanged.Add(series[^1]);
                lastValue = null;
            }
            else if (lastValue is not null)
            {
                series.Add(new ValueHistoryEntry(snapshot.Id, snapshot.OccurredAtUtc, snapshot.ActorId, snapshot.ChangeKind, lastValue, false));
            }
        }

        return new ValueTrendResponse(
            entityName,
            keyId,
            field,
            fromUtc,
            toUtc,
            series,
            ComputeStatistics(fieldChanged, fromUtc, toUtc));
    }

    internal static ValueTrendStatistics ComputeStatistics(IReadOnlyList<ValueHistoryEntry> changes, DateTime fromUtc, DateTime toUtc)
    {
        var values = changes.Select(change => change.Value).Where(value => value is not null).ToList()!;
        var numerics = new List<decimal>();
        foreach (var value in values)
        {
            if (decimal.TryParse(TrimJsonScalar(value), out var parsed))
            {
                numerics.Add(parsed);
            }
        }

        var changeTimes = changes.Select(change => change.OccurredAtUtc.UtcDateTime).ToList();
        var windowDays = Math.Max((toUtc - fromUtc).TotalDays, 1);
        var longestStableDays = 0.0;
        for (var index = 1; index < changeTimes.Count; index++)
        {
            longestStableDays = Math.Max(longestStableDays, (changeTimes[index] - changeTimes[index - 1]).TotalDays);
        }

        decimal? netChange = null;
        if (changes.Count >= 2
            && decimal.TryParse(TrimJsonScalar(changes[0].Value ?? string.Empty), out var first)
            && decimal.TryParse(TrimJsonScalar(changes[^1].Value ?? string.Empty), out var last))
        {
            netChange = last - first;
        }

        return new ValueTrendStatistics(
            changes.Count,
            values.Distinct(StringComparer.Ordinal).Count(),
            changeTimes.Count == 0 ? fromUtc : changeTimes.Min(),
            changeTimes.Count == 0 ? fromUtc : changeTimes.Max(),
            changes.Count == 0 ? null : changes[0].Value,
            changes.Count == 0 ? null : changes[^1].Value,
            numerics.Count == 0 ? null : numerics.Min(),
            numerics.Count == 0 ? null : numerics.Max(),
            numerics.Count == 0 ? null : Math.Round(numerics.Average(), 4),
            netChange,
            Math.Round(changes.Count / windowDays, 4),
            Math.Round(longestStableDays, 4));
    }

    private static string TrimJsonScalar(string value) =>
        value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2
            ? value[1..^1]
            : value;

    private static bool PathMatches(string diffPath, string field) =>
        string.Equals(diffPath, field, StringComparison.OrdinalIgnoreCase)
        || diffPath.StartsWith(field + ".", StringComparison.OrdinalIgnoreCase)
        || diffPath.StartsWith(field + "[", StringComparison.OrdinalIgnoreCase);

    private static string? ReadFieldFromPayload(string? payloadJson, string field)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind is not System.Text.Json.JsonValueKind.Object
                || !document.RootElement.TryGetProperty(field, out var property))
            {
                return null;
            }

            return property.ValueKind == System.Text.Json.JsonValueKind.String
                ? property.GetString()
                : property.GetRawText();
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static ChangeImpactChange ToImpactChange(MutationValueSnapshot snapshot, bool isAnchor) =>
        new(snapshot.Id, snapshot.EntityName, snapshot.KeyId, snapshot.ChangeKind, snapshot.DiffCount, snapshot.OccurredAtUtc, snapshot.ActorId, isAnchor);

    private Guid RequireAdministrator()
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsTenantAdmin || actor.TenantId is not { } tenant || tenant == Guid.Empty)
        {
            logger.LogWarning("Value snapshot analysis access denied for actor {Actor}", actor.SubjectIdAsGuid);
            throw new UnauthorizedAccessException("A tenant administrator and tenant context are required.");
        }

        return tenant;
    }
}
