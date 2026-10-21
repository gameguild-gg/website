using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

/// <summary>Validation failure for value snapshot surface requests.</summary>
public sealed class ValueSnapshotValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("The value snapshot request is invalid.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>Search request over the mutation value snapshot store (issue #176).</summary>
public sealed record ValueSnapshotSearchRequest
{
    public string? EntityName { get; init; }
    public string? KeyId { get; init; }
    public string? Field { get; init; }
    public string? ValuePattern { get; init; }
    public string? ChangeKind { get; init; }
    public Guid? ActorId { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; } = 50;
}

public sealed record ValueSnapshotFieldDiffResponse(
    string Field, string ChangeKind, string? Before, string? After, bool BeforeTruncated, bool AfterTruncated, decimal? NumericDelta);

public sealed record ValueSnapshotResponse(
    Guid Id,
    string EntityName,
    string KeyId,
    string ChangeKind,
    DateTimeOffset OccurredAtUtc,
    Guid? ActorId,
    string? CorrelationId,
    int DiffCount,
    string StorageMode,
    int BeforeBytes,
    int AfterBytes,
    bool BeforePayloadTruncated,
    bool AfterPayloadTruncated,
    string? BeforeIntegritySha256,
    string? AfterIntegritySha256,
    string? BeforePayload,
    string? AfterPayload,
    DateTime? ArchivedAtUtc,
    IReadOnlyList<ValueSnapshotFieldDiffResponse> FieldDiffs);

public sealed record ValueSnapshotSearchResult(IReadOnlyList<ValueSnapshotResponse> Items, int TotalCount);

/// <summary>Point-in-time value history entry for one entity field.</summary>
public sealed record ValueHistoryEntry(
    Guid SnapshotId,
    DateTimeOffset OccurredAtUtc,
    Guid? ActorId,
    string ChangeKind,
    string? Value,
    bool Truncated);

public sealed record ValueHistoryResponse(
    string EntityName,
    string KeyId,
    string Field,
    IReadOnlyList<ValueHistoryEntry> Entries);

public sealed record ValueSnapshotStorageOverview(
    int TotalSnapshots,
    int DeltaSnapshots,
    int FullSnapshots,
    int TruncatedPayloads,
    int ArchivedSnapshots,
    long InlinePayloadBytes,
    int ChunkCount,
    long ChunkBytes,
    long ChunkReferences,
    long DeduplicatedBytesSaved,
    int RollbackPlans,
    int ExecutedRollbackPlans);

/// <summary>
///     Searchable value history over the mutation value snapshot store (issue #176): indexed
///     entity/key/time narrowing, field and value-pattern refinement, point-in-time field history,
///     and storage-space accounting for the compression/deduplication story.
/// </summary>
public interface IValueSnapshotQueryService
{
    Task<ValueSnapshotSearchResult> SearchAsync(ValueSnapshotSearchRequest request, CancellationToken cancellationToken);

    Task<ValueSnapshotResponse?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ValueHistoryResponse?> GetHistoryAsync(string entityName, string keyId, string? field, CancellationToken cancellationToken);

    Task<ValueSnapshotStorageOverview> GetStorageOverviewAsync(CancellationToken cancellationToken);
}

/// <summary>
///     Searchable value history over the mutation value snapshot store (issue #176): indexed
///     entity/key/time narrowing, field and value-pattern refinement (bounded by a pattern scan
///     limit), point-in-time field history, and storage-space accounting for the
///     compression/deduplication story.
/// </summary>
public sealed class ValueSnapshotQueryService(
    IActorContextAccessor actors,
    IApplicationDbContext context,
    ILogger<ValueSnapshotQueryService> logger) : IValueSnapshotQueryService
{
    private const int PatternScanLimit = 5_000;
    private const int HistoryLimit = 2_000;

    public async Task<ValueSnapshotSearchResult> SearchAsync(ValueSnapshotSearchRequest request, CancellationToken cancellationToken)
    {
        RequireAdministrator();
        Validate(request);

        var query = Scope().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.EntityName))
        {
            query = query.Where(snapshot => snapshot.EntityName == request.EntityName);
        }

        if (!string.IsNullOrWhiteSpace(request.KeyId))
        {
            query = query.Where(snapshot => snapshot.KeyId == request.KeyId);
        }

        if (!string.IsNullOrWhiteSpace(request.ChangeKind))
        {
            query = query.Where(snapshot => snapshot.ChangeKind == request.ChangeKind);
        }

        if (request.ActorId is { } actorId)
        {
            query = query.Where(snapshot => snapshot.ActorId == actorId);
        }

        if (request.FromUtc is { } fromUtc)
        {
            var from = new DateTimeOffset(fromUtc, TimeSpan.Zero);
            query = query.Where(snapshot => snapshot.OccurredAtUtc >= from);
        }

        if (request.ToUtc is { } toUtc)
        {
            var to = new DateTimeOffset(toUtc, TimeSpan.Zero);
            query = query.Where(snapshot => snapshot.OccurredAtUtc <= to);
        }

        if (!string.IsNullOrWhiteSpace(request.Field))
        {
            query = query.Where(snapshot => snapshot.StructuredDiffJson.Contains(request.Field));
        }

        if (!string.IsNullOrWhiteSpace(request.ValuePattern))
        {
            var pattern = request.ValuePattern;
            query = query.Where(snapshot =>
                (snapshot.BeforePayload != null && snapshot.BeforePayload.Contains(pattern))
                || (snapshot.AfterPayload != null && snapshot.AfterPayload.Contains(pattern))
                || snapshot.StructuredDiffJson.Contains(pattern));
        }

        var candidates = await query
            .OrderBy(snapshot => snapshot.OccurredAtUtc)
            .ThenBy(snapshot => snapshot.Id)
            .Take(PatternScanLimit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var refined = Refine(candidates, request.Field, request.ValuePattern);
        var total = refined.Count;
        var page = refined.Skip(request.Skip).Take(request.Take).Select(Map).ToList();
        if (candidates.Count > PatternScanLimit)
        {
            logger.LogWarning(
                "Value snapshot search narrowed to the pattern scan limit of {Limit} rows; refine the indexed filters (entity/key/time)",
                PatternScanLimit);
        }

        return new ValueSnapshotSearchResult(page, total);
    }

    public async Task<ValueSnapshotResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        RequireAdministrator();
        var snapshot = await Scope().AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return snapshot is null ? null : Map(snapshot);
    }

    public async Task<ValueHistoryResponse?> GetHistoryAsync(string entityName, string keyId, string? field, CancellationToken cancellationToken)
    {
        RequireAdministrator();
        if (string.IsNullOrWhiteSpace(entityName) || string.IsNullOrWhiteSpace(keyId))
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["EntityName"] = ["Entity name and key id are required."],
                ["KeyId"] = ["Entity name and key id are required."]
            });
        }

        var snapshots = await Scope().AsNoTracking()
            .Where(snapshot => snapshot.EntityName == entityName && snapshot.KeyId == keyId)
            .OrderBy(snapshot => snapshot.OccurredAtUtc)
            .ThenBy(snapshot => snapshot.Id)
            .Take(HistoryLimit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (snapshots.Count == 0)
        {
            return null;
        }

        var effectiveField = field;
        if (string.IsNullOrWhiteSpace(effectiveField))
        {
            // Default to the most frequently changed field so a bare history request stays useful.
            effectiveField = snapshots
                .SelectMany(snapshot => ValueSnapshotDiffEngine.DeserializeDiffs(snapshot.StructuredDiffJson))
                .GroupBy(diff => diff.Field)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Key)
                .FirstOrDefault() ?? "Id";
        }

        var entries = new List<ValueHistoryEntry>();
        foreach (var snapshot in snapshots)
        {
            string? value = null;
            var truncated = false;

            if (snapshot.ChangeKind != "Deleted")
            {
                var fromPayload = ReadFieldValue(snapshot.AfterPayload, effectiveField);
                if (fromPayload is { } payloadValue)
                {
                    value = payloadValue.Value;
                    truncated = payloadValue.Truncated;
                }
            }

            // The after payload of a Modified row is the changed-field delta; when the field was
            // not touched the previous value carries forward (point-in-time history semantics).
            if (value is null && snapshot.ChangeKind != "Deleted" && entries.Count > 0)
            {
                value = entries[^1].Value;
                truncated = entries[^1].Truncated;
            }

            if (value is null && snapshot.ChangeKind == "Deleted")
            {
                var fromBefore = ReadFieldValue(snapshot.BeforePayload, effectiveField);
                if (fromBefore is { } beforeValue)
                {
                    value = beforeValue.Value;
                    truncated = beforeValue.Truncated;
                }
            }

            entries.Add(new ValueHistoryEntry(snapshot.Id, snapshot.OccurredAtUtc, snapshot.ActorId, snapshot.ChangeKind, value, truncated));
        }

        return new ValueHistoryResponse(entityName, keyId, effectiveField, entries);
    }

    public async Task<ValueSnapshotStorageOverview> GetStorageOverviewAsync(CancellationToken cancellationToken)
    {
        var tenantId = RequireAdministrator();
        var snapshots = Scope().AsNoTracking();
        var totalSnapshots = await snapshots.CountAsync(cancellationToken).ConfigureAwait(false);
        var deltaSnapshots = await snapshots.CountAsync(snapshot => snapshot.StorageMode == "Delta", cancellationToken).ConfigureAwait(false);
        var truncated = await snapshots.CountAsync(
            snapshot => snapshot.BeforePayloadTruncated || snapshot.AfterPayloadTruncated, cancellationToken)
            .ConfigureAwait(false);
        var archived = await snapshots.CountAsync(snapshot => snapshot.ArchivedAtUtc != null, cancellationToken).ConfigureAwait(false);
        var inlineBytes = await snapshots.SumAsync(snapshot =>
            (snapshot.BeforePayload != null ? snapshot.BeforePayload.Length : 0)
            + (snapshot.AfterPayload != null ? snapshot.AfterPayload.Length : 0), cancellationToken)
            .ConfigureAwait(false);

        var chunks = context.Set<MutationValuePayloadChunk>().AsNoTracking().Where(chunk => chunk.TenantId == tenantId);
        var chunkCount = await chunks.CountAsync(cancellationToken).ConfigureAwait(false);
        var chunkBytes = await chunks.SumAsync(chunk => chunk.LengthBytes, cancellationToken).ConfigureAwait(false);
        var chunkRefs = await chunks.SumAsync(chunk => chunk.RefCount, cancellationToken).ConfigureAwait(false);
        var saved = await chunks.SumAsync(chunk => (chunk.RefCount - 1) * chunk.LengthBytes, cancellationToken).ConfigureAwait(false);

        var plans = context.Set<ValueSnapshotRollbackPlan>().AsNoTracking().Where(plan => plan.TenantId == tenantId);
        var planCount = await plans.CountAsync(cancellationToken).ConfigureAwait(false);
        var executedPlans = await plans.CountAsync(plan => plan.Status == "Executed", cancellationToken).ConfigureAwait(false);

        return new ValueSnapshotStorageOverview(
            totalSnapshots,
            deltaSnapshots,
            totalSnapshots - deltaSnapshots,
            truncated,
            archived,
            inlineBytes,
            chunkCount,
            chunkBytes,
            chunkRefs,
            saved,
            planCount,
            executedPlans);
    }

    private IQueryable<MutationValueSnapshot> Scope()
    {
        var tenantId = actors.ActorContext;
        if (tenantId is not { IsAuthenticated: true, TenantId: { } tenant } || tenant == Guid.Empty)
        {
            throw new UnauthorizedAccessException("An authenticated tenant context is required.");
        }

        return context.Set<MutationValueSnapshot>().Where(snapshot => snapshot.TenantId == tenant);
    }

    private Guid RequireAdministrator()
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsTenantAdmin || actor.TenantId is not { } tenant || tenant == Guid.Empty)
        {
            logger.LogWarning("Value snapshot access denied for actor {Actor}", actor.SubjectIdAsGuid);
            throw new UnauthorizedAccessException("A tenant administrator and tenant context are required.");
        }

        return tenant;
    }

    private static List<MutationValueSnapshot> Refine(List<MutationValueSnapshot> candidates, string? field, string? valuePattern)
    {
        if (string.IsNullOrWhiteSpace(field) && string.IsNullOrWhiteSpace(valuePattern))
        {
            return candidates;
        }

        return candidates
            .Where(snapshot =>
            {
                var diffs = ValueSnapshotDiffEngine.DeserializeDiffs(snapshot.StructuredDiffJson);
                var fieldMatch = string.IsNullOrWhiteSpace(field)
                    || diffs.Any(diff => MatchesPath(diff.Field, field));
                var patternMatch = string.IsNullOrWhiteSpace(valuePattern)
                    || diffs.Any(diff => Contains(diff.Before, valuePattern) || Contains(diff.After, valuePattern))
                    || Contains(snapshot.BeforePayload, valuePattern)
                    || Contains(snapshot.AfterPayload, valuePattern);
                return fieldMatch && patternMatch;
            })
            .ToList();
    }

    private static bool MatchesPath(string diffPath, string filter) =>
        string.Equals(diffPath, filter, StringComparison.OrdinalIgnoreCase)
        || diffPath.StartsWith(filter + ".", StringComparison.OrdinalIgnoreCase)
        || diffPath.StartsWith(filter + "[", StringComparison.OrdinalIgnoreCase);

    private static bool Contains(string? value, string pattern) =>
        !string.IsNullOrEmpty(value) && value.Contains(pattern, StringComparison.OrdinalIgnoreCase);

    private static (string Value, bool Truncated)? ReadFieldValue(string? payloadJson, string field)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind is not System.Text.Json.JsonValueKind.Object)
            {
                return null;
            }

            if (!document.RootElement.TryGetProperty(field, out var property))
            {
                return null;
            }

            var rendered = property.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => property.GetString(),
                System.Text.Json.JsonValueKind.Null => "null",
                _ => property.GetRawText()
            };
            return (rendered ?? string.Empty, false);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static ValueSnapshotResponse Map(MutationValueSnapshot snapshot)
    {
        string? ResolvePayload(string? inline, Guid? chunkId)
        {
            if (inline is not null)
            {
                return inline;
            }

            return chunkId is { } chunk ? $"[deduplicated chunk {chunk}]" : null;
        }

        var diffs = ValueSnapshotDiffEngine.DeserializeDiffs(snapshot.StructuredDiffJson)
            .Select(diff => new ValueSnapshotFieldDiffResponse(
                diff.Field, diff.ChangeKind, diff.Before, diff.After, diff.BeforeTruncated, diff.AfterTruncated, diff.NumericDelta))
            .ToList();

        return new ValueSnapshotResponse(
            snapshot.Id,
            snapshot.EntityName,
            snapshot.KeyId,
            snapshot.ChangeKind,
            snapshot.OccurredAtUtc,
            snapshot.ActorId,
            snapshot.CorrelationId,
            snapshot.DiffCount,
            snapshot.StorageMode,
            snapshot.BeforeBytes,
            snapshot.AfterBytes,
            snapshot.BeforePayloadTruncated,
            snapshot.AfterPayloadTruncated,
            snapshot.BeforeIntegritySha256,
            snapshot.AfterIntegritySha256,
            ResolvePayload(snapshot.BeforePayload, snapshot.BeforeChunkId),
            ResolvePayload(snapshot.AfterPayload, snapshot.AfterChunkId),
            snapshot.ArchivedAtUtc,
            diffs);
    }

    private static void Validate(ValueSnapshotSearchRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Skip < 0)
        {
            errors["Skip"] = ["Skip must be zero or greater."];
        }

        if (request.Take is < 1 or > 200)
        {
            errors["Take"] = ["Take must be between 1 and 200."];
        }

        if (errors.Count > 0)
        {
            throw new ValueSnapshotValidationException(errors);
        }
    }
}
