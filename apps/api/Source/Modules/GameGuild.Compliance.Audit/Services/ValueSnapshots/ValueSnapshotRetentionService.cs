using System.Text.Json;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

public sealed record ConfigureValueSnapshotRetentionRequest
{
    public int RetentionDays { get; init; }
    public int ArchiveAfterDays { get; init; }
    public Dictionary<string, int>? EntityOverrides { get; init; }
    public DateTime? LegalHoldUntilUtc { get; init; }
}

public sealed record ValueSnapshotRetentionPolicyResponse(
    Guid Id,
    int Revision,
    int RetentionDays,
    int ArchiveAfterDays,
    IReadOnlyDictionary<string, int> EntityOverrides,
    DateTime? LegalHoldUntilUtc,
    bool LegalHoldActive,
    Guid UpdatedByUserId,
    DateTime ConfiguredAtUtc);

public sealed record EnforceValueSnapshotRetentionRequest
{
    public bool DryRun { get; init; }
    public bool IncludeCompaction { get; init; } = true;
}

public sealed record ValueSnapshotRetentionExecutionResponse(
    Guid Id,
    DateTime ExecutedAtUtc,
    Guid TriggeredByUserId,
    bool DryRun,
    bool LegalHoldActive,
    int DeletedCount,
    int ArchivedCount,
    int CompactedCount,
    long BytesReclaimed);

/// <summary>
///     Configurable retention for mutation value snapshots (issue #176): day-based retention with
///     per-entity overrides, an archive tier that strips payloads down to checksums, content
///     deduplication compaction, and a legal-hold window that suspends deletion. Every pass —
///     manual, dry-run, or scheduled — is recorded as an execution row.
/// </summary>
public interface IValueSnapshotRetentionService
{
    Task<ValueSnapshotRetentionPolicyResponse?> GetPolicyAsync(CancellationToken cancellationToken);

    Task<ValueSnapshotRetentionPolicyResponse> ConfigurePolicyAsync(ConfigureValueSnapshotRetentionRequest request, CancellationToken cancellationToken);

    Task<ValueSnapshotRetentionExecutionResponse> EnforceAsync(EnforceValueSnapshotRetentionRequest request, CancellationToken cancellationToken);
}

public sealed class ValueSnapshotRetentionService(
    IActorContextAccessor actors,
    IApplicationDbContext context,
    TimeProvider timeProvider,
    ILogger<ValueSnapshotRetentionService> logger) : IValueSnapshotRetentionService
{
    private const int CompactionGroupLimit = 500;

    private static readonly JsonSerializerOptions OverrideJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ValueSnapshotRetentionPolicyResponse?> GetPolicyAsync(CancellationToken cancellationToken)
    {
        var tenantId = RequireAdministrator();
        var policy = await Policies().FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return policy is null ? null : Map(policy, timeProvider.GetUtcNow().UtcDateTime);
    }

    public async Task<ValueSnapshotRetentionPolicyResponse> ConfigurePolicyAsync(
        ConfigureValueSnapshotRetentionRequest request,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        Validate(request);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var overridesJson = SerializeOverrides(request.EntityOverrides);

        var policy = await Policies().FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (policy is null)
        {
            policy = new ValueSnapshotRetentionPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RetentionDays = request.RetentionDays,
                ArchiveAfterDays = request.ArchiveAfterDays,
                EntityOverridesJson = overridesJson,
                LegalHoldUntilUtc = request.LegalHoldUntilUtc,
                UpdatedByUserId = userId,
                ConfiguredAtUtc = now,
                Revision = 1
            };
            context.Set<ValueSnapshotRetentionPolicy>().Add(policy);
        }
        else
        {
            policy.RetentionDays = request.RetentionDays;
            policy.ArchiveAfterDays = request.ArchiveAfterDays;
            policy.EntityOverridesJson = overridesJson;
            policy.LegalHoldUntilUtc = request.LegalHoldUntilUtc;
            policy.UpdatedByUserId = userId;
            policy.ConfiguredAtUtc = now;
            policy.Revision++;
            policy.Touch();
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(policy, now);
    }

    public async Task<ValueSnapshotRetentionExecutionResponse> EnforceAsync(
        EnforceValueSnapshotRetentionRequest request,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        var policy = await Policies().FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (policy is null)
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["Policy"] = ["Configure a retention policy before enforcing retention."]
            });
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var legalHoldActive = policy.LegalHoldUntilUtc.HasValue && policy.LegalHoldUntilUtc.Value > now;
        var overrides = DeserializeOverrides(policy.EntityOverridesJson);
        var deleted = 0;
        var archived = 0;
        var compacted = 0;
        var reclaimed = 0L;

        if (legalHoldActive)
        {
            // A legal hold suspends every deletion; archival and compaction are lossless so they
            // may still run, and the pass is still recorded for the audit trail.
            logger.LogInformation(
                "Value snapshot retention for tenant {TenantId}: legal hold active until {Until}; deletions suspended",
                tenantId, policy.LegalHoldUntilUtc);
        }
        else
        {
            var doomedIds = await SelectDoomedIdsAsync(tenantId, policy.RetentionDays, overrides, now, cancellationToken).ConfigureAwait(false);
            if (doomedIds.Count > 0)
            {
                var rows = await Snapshots(tenantId, tracked: true)
                    .Where(snapshot => doomedIds.Contains(snapshot.Id))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (request.DryRun)
                {
                    deleted = rows.Count;
                    reclaimed += rows.Sum(InlineBytes);
                }
                else
                {
                    reclaimed += await ReleaseChunkReferencesAsync(tenantId, rows, cancellationToken).ConfigureAwait(false);
                    reclaimed += rows.Sum(InlineBytes);
                    context.Set<MutationValueSnapshot>().RemoveRange(rows);
                    deleted = rows.Count;
                }
            }
        }

        // Archive tier: older-than-ArchiveAfterDays snapshots lose their inline payloads (and
        // chunk references) while every checksum is retained — the cost-effective storage tier.
        var archiveCutoff = new DateTimeOffset(now.AddDays(-policy.ArchiveAfterDays), TimeSpan.Zero);
        var archivable = await Snapshots(tenantId, tracked: true)
            .Where(snapshot => snapshot.ArchivedAtUtc == null && snapshot.OccurredAtUtc < archiveCutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var row in archivable)
        {
            reclaimed += InlineBytes(row);
            if (!request.DryRun)
            {
                reclaimed += await DropChunkReferencesAsync(tenantId, row, cancellationToken).ConfigureAwait(false);
                row.BeforePayload = null;
                row.AfterPayload = null;
                row.BeforeChunkId = null;
                row.AfterChunkId = null;
                row.ArchivedAtUtc = now;
                row.Touch();
            }

            archived++;
        }

        if (request.IncludeCompaction && !request.DryRun)
        {
            (compacted, var compactionReclaimed) = await CompactAsync(tenantId, cancellationToken).ConfigureAwait(false);
            reclaimed += compactionReclaimed;
        }

        var execution = new ValueSnapshotRetentionExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExecutedAtUtc = now,
            TriggeredByUserId = userId,
            DryRun = request.DryRun,
            LegalHoldActive = legalHoldActive,
            DeletedCount = deleted,
            ArchivedCount = archived,
            CompactedCount = compacted,
            BytesReclaimed = reclaimed
        };
        context.Set<ValueSnapshotRetentionExecution>().Add(execution);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ValueSnapshotRetentionExecutionResponse(
            execution.Id, execution.ExecutedAtUtc, execution.TriggeredByUserId, execution.DryRun,
            execution.LegalHoldActive, execution.DeletedCount, execution.ArchivedCount,
            execution.CompactedCount, execution.BytesReclaimed);
    }

    /// <summary>
    ///     Deduplication compaction: identical stored payloads (by stored SHA-256) are extracted
    ///     once into <see cref="MutationValuePayloadChunk"/> rows and every snapshot then references
    ///     the chunk instead of carrying its own copy.
    /// </summary>
    public async Task<(int CompactedPayloads, long BytesReclaimed)> CompactAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var compacted = 0;
        var reclaimed = 0L;
        var sides = await Snapshots(tenantId, tracked: true)
            .Where(snapshot => snapshot.ArchivedAtUtc == null
                && (snapshot.BeforePayload != null || snapshot.AfterPayload != null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var groups = sides
            .SelectMany(snapshot => new[]
            {
                (Snapshot: snapshot, Side: "B", snapshot.BeforePayload, snapshot.BeforeStoredSha256),
                (Snapshot: snapshot, Side: "A", snapshot.AfterPayload, snapshot.AfterStoredSha256)
            })
            .Where(item => item.Payload is not null && item.Sha is not null)
            .GroupBy(item => item.Sha!)
            .Take(CompactionGroupLimit);

        foreach (var group in groups)
        {
            var members = group.ToList();
            var content = members[0].Payload!;
            var length = System.Text.Encoding.UTF8.GetByteCount(content);
            if (length < 256)
            {
                // Small payloads cost more to reference than to store inline.
                continue;
            }

            var chunk = await context.Set<MutationValuePayloadChunk>()
                .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.ContentSha256 == group.Key, cancellationToken)
                .ConfigureAwait(false);
            var chunkWasNew = chunk is null;
            if (chunk is null)
            {
                chunk = new MutationValuePayloadChunk
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ContentSha256 = group.Key,
                    Content = content,
                    LengthBytes = length,
                    RefCount = 0
                };
                context.Set<MutationValuePayloadChunk>().Add(chunk);
            }

            chunk.RefCount += members.Count;
            foreach (var (snapshot, side, _, _) in members)
            {
                if (side == "B")
                {
                    snapshot.BeforePayload = null;
                    snapshot.BeforeChunkId = chunk.Id;
                }
                else
                {
                    snapshot.AfterPayload = null;
                    snapshot.AfterChunkId = chunk.Id;
                }

                snapshot.Touch();
                compacted++;
            }

            // The content stays stored once (in the chunk when newly created); every duplicate
            // inline copy removed is pure storage savings.
            reclaimed += length * (long)(chunkWasNew ? members.Count - 1 : members.Count);
        }

        if (compacted > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            // The first copy of the content stays stored in the chunk; only the duplicates are saved.
            reclaimed -= reclaimed / (compacted + 1);
        }

        return (compacted, reclaimed);
    }

    /// <summary>Selects the snapshot ids past their effective cutoff: the default retention for
    /// entities without overrides, and the per-entity override cutoff for the rest.</summary>
    private async Task<HashSet<Guid>> SelectDoomedIdsAsync(
        Guid tenantId,
        int retentionDays,
        IReadOnlyDictionary<string, int> overrides,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var doomed = new HashSet<Guid>();
        var overriddenEntities = overrides.Keys.ToList();
        var defaultCutoff = new DateTimeOffset(now.AddDays(-retentionDays), TimeSpan.Zero);

        var defaultDoomed = await Snapshots(tenantId)
            .Where(snapshot => !overriddenEntities.Contains(snapshot.EntityName) && snapshot.OccurredAtUtc < defaultCutoff)
            .Select(snapshot => snapshot.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var id in defaultDoomed)
        {
            doomed.Add(id);
        }

        foreach (var (entityName, days) in overrides)
        {
            var cutoff = new DateTimeOffset(now.AddDays(-days), TimeSpan.Zero);
            var entityDoomed = await Snapshots(tenantId)
                .Where(snapshot => snapshot.EntityName == entityName && snapshot.OccurredAtUtc < cutoff)
                .Select(snapshot => snapshot.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var id in entityDoomed)
            {
                doomed.Add(id);
            }
        }

        return doomed;
    }

    private async Task<long> ReleaseChunkReferencesAsync(Guid tenantId, List<MutationValueSnapshot> rows, CancellationToken cancellationToken)
    {
        var reclaimed = 0L;
        var chunkIds = rows
            .SelectMany(row => new[] { row.BeforeChunkId, row.AfterChunkId })
            .Where(id => id is not null)
            .Cast<Guid>()
            .Distinct()
            .ToList();
        if (chunkIds.Count == 0)
        {
            return reclaimed;
        }

        var chunks = await context.Set<MutationValuePayloadChunk>()
            .Where(chunk => chunk.TenantId == tenantId && chunkIds.Contains(chunk.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var chunk in chunks)
        {
            var references = rows.Count(row => row.BeforeChunkId == chunk.Id || row.AfterChunkId == chunk.Id);
            chunk.RefCount -= references;
            if (chunk.RefCount <= 0)
            {
                reclaimed += chunk.LengthBytes;
                context.Set<MutationValuePayloadChunk>().Remove(chunk);
            }
        }

        return reclaimed;
    }

    private async Task<long> DropChunkReferencesAsync(Guid tenantId, MutationValueSnapshot row, CancellationToken cancellationToken)
    {
        var reclaimed = 0L;
        var chunkIds = new[] { row.BeforeChunkId, row.AfterChunkId }.Where(id => id is not null).Cast<Guid>().ToList();
        if (chunkIds.Count == 0)
        {
            return reclaimed;
        }

        var chunks = await context.Set<MutationValuePayloadChunk>()
            .Where(chunk => chunk.TenantId == tenantId && chunkIds.Contains(chunk.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var chunk in chunks)
        {
            chunk.RefCount--;
            if (chunk.RefCount <= 0)
            {
                reclaimed += chunk.LengthBytes;
                context.Set<MutationValuePayloadChunk>().Remove(chunk);
            }
        }

        return reclaimed;
    }

    private IQueryable<MutationValueSnapshot> Snapshots(Guid tenantId, bool tracked = false)
    {
        var query = context.Set<MutationValueSnapshot>().Where(snapshot => snapshot.TenantId == tenantId);
        return tracked ? query : query.AsNoTracking();
    }

    private IQueryable<ValueSnapshotRetentionPolicy> Policies()
    {
        var tenantId = actors.ActorContext.TenantId ?? Guid.Empty;
        return context.Set<ValueSnapshotRetentionPolicy>().Where(policy => policy.TenantId == tenantId);
    }

    private static long InlineBytes(MutationValueSnapshot snapshot) =>
        (snapshot.BeforePayload?.Length ?? 0) + (snapshot.AfterPayload?.Length ?? 0);

    private static void Validate(ConfigureValueSnapshotRetentionRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.RetentionDays is < 30 or > 3650)
        {
            errors["RetentionDays"] = ["Retention days must be between 30 and 3650."];
        }

        if (request.ArchiveAfterDays is < 7 or > 3650)
        {
            errors["ArchiveAfterDays"] = ["Archive-after days must be between 7 and 3650."];
        }

        if (request.ArchiveAfterDays > request.RetentionDays)
        {
            errors["ArchiveAfterDays"] = ["Archive-after days cannot exceed retention days."];
        }

        if (request.EntityOverrides is { Count: > 0 })
        {
            foreach (var (entityName, days) in request.EntityOverrides)
            {
                if (string.IsNullOrWhiteSpace(entityName))
                {
                    errors[$"EntityOverrides[{entityName}]"] = ["Entity names must be non-empty."];
                }
                else if (days is < 30 or > 3650)
                {
                    errors[$"EntityOverrides[{entityName}]"] = ["Entity retention days must be between 30 and 3650."];
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ValueSnapshotValidationException(errors);
        }
    }

    private static string SerializeOverrides(Dictionary<string, int>? overrides) =>
        overrides is null || overrides.Count == 0 ? "{}" : JsonSerializer.Serialize(overrides, OverrideJsonOptions);

    private static Dictionary<string, int> DeserializeOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, int>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(json, OverrideJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static ValueSnapshotRetentionPolicyResponse Map(ValueSnapshotRetentionPolicy policy, DateTime nowUtc) =>
        new(policy.Id, policy.Revision, policy.RetentionDays, policy.ArchiveAfterDays,
            DeserializeOverrides(policy.EntityOverridesJson), policy.LegalHoldUntilUtc,
            policy.LegalHoldUntilUtc.HasValue && policy.LegalHoldUntilUtc.Value > nowUtc,
            policy.UpdatedByUserId, policy.ConfiguredAtUtc);

    private (Guid TenantId, Guid UserId) RequireAdministrator()
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsTenantAdmin || actor.TenantId is not { } tenant || tenant == Guid.Empty
            || actor.SubjectIdAsGuid is not { } user || user == Guid.Empty)
        {
            logger.LogWarning("Value snapshot retention access denied for actor {Actor}", actor.SubjectIdAsGuid);
            throw new UnauthorizedAccessException("A tenant administrator and tenant context are required.");
        }

        return (tenant, user);
    }
}
