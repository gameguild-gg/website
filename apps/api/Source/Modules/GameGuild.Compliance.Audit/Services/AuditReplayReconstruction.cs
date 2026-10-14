using System.Globalization;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Compliance.Audit;

/// <summary>Client-held snapshot document posted for reconstruction (issue #169).
/// Field order mirrors the canonical capture document so the reconstructed bytes are deterministic.</summary>
public sealed record AuditReplaySnapshotDocument(
    int SchemaVersion,
    string? TenantId,
    int TotalCount,
    string StartUtc,
    string EndUtc,
    string CapturedAtUtc,
    IReadOnlyList<AuditReplaySnapshotEvent> Events);

/// <summary>A reconstructed multi-user timeline over one verified snapshot (issue #169).
/// Summaries are investigation aids; they are not verified attribution or provenance.</summary>
public sealed record AuditReplayReconstruction(
    string Sha256,
    Guid? TenantId,
    int TotalCount,
    int FailedCount,
    string StartUtc,
    string EndUtc,
    string CapturedAtUtc,
    IReadOnlyList<AuditReplaySnapshotEvent> Events,
    IReadOnlyList<AuditReplayUserJourney> Users,
    IReadOnlyList<AuditReplaySessionSummary> Sessions);

/// <summary>Per-user journey: chronological activity of one subject across the captured window.</summary>
public sealed record AuditReplayUserJourney(
    string UserId,
    int EventCount,
    int FailedCount,
    string FirstSeenAtUtc,
    string LastSeenAtUtc,
    IReadOnlyList<string> SessionIds);

/// <summary>Per-session summary: the parallel activity of one session and the subjects seen in it.</summary>
public sealed record AuditReplaySessionSummary(
    string SessionId,
    int EventCount,
    int FailedCount,
    string FirstAtUtc,
    string LastAtUtc,
    IReadOnlyList<string> UserIds);

/// <summary>Reconstructs an incident timeline from a client-held snapshot (issue #169).
/// Integrity is verified against the digest of the canonical re-serialization; a digest match
/// detects content changes but does not establish provenance or source clock synchronization.</summary>
public interface IAuditReplayReconstructionService
{
    Task<AuditReplayReconstruction> ReconstructAsync(AuditReplaySnapshotDocument document, string expectedSha256, CancellationToken cancellationToken);
}

public sealed class AuditReplayReconstructionService(
    IActorContextAccessor actors,
    IAuditService audit) : IAuditReplayReconstructionService
{
    public async Task<AuditReplayReconstruction> ReconstructAsync(
        AuditReplaySnapshotDocument document, string expectedSha256, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            throw new ArgumentException("Replay reconstruction requires the snapshot integrity digest.", nameof(expectedSha256));
        }
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsSystemAdmin ||
            actor.SubjectIdAsGuid is not { } actorId || actorId == Guid.Empty || actor.TenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("An authenticated system administrator with a valid identity is required.");
        }
        // Tenant isolation: the reconstruction scope must match the administrator's context.
        // A null tenant document is only reconstructable from the global-only scope.
        var actorTenant = actor.TenantId;
        Guid? documentTenant = null;
        if (document.TenantId is not null)
        {
            if (!Guid.TryParse(document.TenantId, out var parsedTenant))
            {
                throw new ArgumentException("Replay snapshot tenant must be a valid identifier when supplied.");
            }
            if (parsedTenant != actorTenant)
            {
                throw new UnauthorizedAccessException("Replay snapshots can only be reconstructed within their captured tenant scope.");
            }
            documentTenant = parsedTenant;
        }

        cancellationToken.ThrowIfCancellationRequested();
        // Every reconstruction attempt — including malformed input — is durably audited first.
        await RecordRequiredAsync(actorId, actorTenant, document, expectedSha256,
            "AuditReplayReconstructionRequested", true, null, cancellationToken).ConfigureAwait(false);

        AuditReplaySnapshot snapshot;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = ParseUtc(document.StartUtc);
            var end = ParseUtc(document.EndUtc);
            var capturedAt = ParseUtc(document.CapturedAtUtc);
            snapshot = RebuildCanonicalSnapshot(document, start, end, capturedAt, documentTenant);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            await RecordRequiredAsync(actorId, actorTenant, document, expectedSha256,
                "AuditReplayReconstructionFailed", false, null, cancellationToken).ConfigureAwait(false);
            throw;
        }

        // Fail closed: the posted document must hash to its expected canonical digest.
        if (!AuditReplaySnapshot.MatchesDigest(snapshot.Json, expectedSha256.ToLowerInvariant()))
        {
            await RecordRequiredAsync(actorId, actorTenant, document, expectedSha256,
                "AuditReplayReconstructionFailed", false, null, cancellationToken).ConfigureAwait(false);
            throw new AuditReplayIntegrityMismatchException();
        }

        var reconstruction = BuildReconstruction(snapshot, expectedSha256.ToLowerInvariant());
        await RecordRequiredAsync(actorId, actorTenant, document, reconstruction.Sha256,
            "AuditReplayReconstructionCompleted", true, reconstruction, cancellationToken).ConfigureAwait(false);
        return reconstruction;
    }

    /// <summary>Re-serializes the posted document through the capture core: every original
    /// invariant (bounds, uniqueness, tenant match, ordering, 10 000-event and 16 MiB limits)
    /// is revalidated and the canonical bytes are reproduced deterministically.</summary>
    private static AuditReplaySnapshot RebuildCanonicalSnapshot(
        AuditReplaySnapshotDocument document, DateTimeOffset start, DateTimeOffset end,
        DateTimeOffset capturedAt, Guid? tenantId)
    {
        if (document.Events is null)
        {
            throw new ArgumentException("Replay reconstruction requires the snapshot events.");
        }
        if (document.TotalCount < 0 || document.TotalCount != document.Events.Count)
        {
            throw new ArgumentException("Replay requires the complete declared result set.");
        }
        var rebuilt = new List<AuditLogDto>(document.Events.Count);
        foreach (var posted in document.Events)
        {
            if (posted is null) { throw new ArgumentException("Replay event is missing."); }
            rebuilt.Add(new AuditLogDto
            {
                Id = RequiredGuid(posted.Id, "Replay event ID"),
                TenantId = tenantId,
                CreatedAt = ParseUtc(posted.CreatedAt).UtcDateTime,
                ActionType = posted.ActionType,
                ResourceType = posted.ResourceType,
                ResourceId = posted.ResourceId,
                UserId = posted.UserId is null ? null : RequiredGuid(posted.UserId, "Replay user ID"),
                SessionId = posted.SessionId is null ? null : RequiredGuid(posted.SessionId, "Replay session ID"),
                CorrelationId = posted.CorrelationId,
                Success = posted.Success
            });
        }
        return AuditReplaySnapshot.Capture(tenantId, rebuilt, document.TotalCount, start, end, capturedAt);
    }

    private static Guid RequiredGuid(string value, string field) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty
            ? parsed
            : throw new ArgumentException(field + " must be a non-empty identifier.");

    private static AuditReplayReconstruction BuildReconstruction(AuditReplaySnapshot snapshot, string sha256)
    {
        var events = snapshot.Events; // already chronological (createdAt, id)
        var users = new Dictionary<string, AuditReplayUserJourney>();
        var sessions = new Dictionary<string, AuditReplaySessionSummary>();
        foreach (var grouped in events.GroupBy(item => item.UserId))
        {
            var journey = grouped.ToList();
            var userId = grouped.Key ?? "anonymous";
            users.Add(userId, new AuditReplayUserJourney(
                userId, journey.Count, journey.Count(item => !item.Success),
                journey[0].CreatedAt, journey[^1].CreatedAt,
                journey.Select(item => item.SessionId).Where(id => id is not null).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray()!));
        }
        foreach (var grouped in events.GroupBy(item => item.SessionId))
        {
            var activity = grouped.ToList();
            var sessionId = grouped.Key ?? "no-session";
            sessions.Add(sessionId, new AuditReplaySessionSummary(
                sessionId, activity.Count, activity.Count(item => !item.Success),
                activity[0].CreatedAt, activity[^1].CreatedAt,
                activity.Select(item => item.UserId).Where(id => id is not null).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray()!));
        }
        return new AuditReplayReconstruction(
            sha256, snapshot.TenantId, snapshot.TotalCount, events.Count(item => !item.Success),
            snapshot.StartUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            snapshot.EndUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            snapshot.CapturedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            events,
            users.Values.OrderBy(item => item.UserId, StringComparer.Ordinal).ToArray(),
            sessions.Values.OrderBy(item => item.SessionId, StringComparer.Ordinal).ToArray());
    }

    private static DateTimeOffset ParseUtc(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            throw new ArgumentException("Replay timestamps must be UTC values with seven fraction digits.");
        }
        return parsed;
    }

    private async Task RecordRequiredAsync(Guid actorId, Guid? tenantId, AuditReplaySnapshotDocument document,
        string digest, string action, bool success, AuditReplayReconstruction? reconstruction, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var recorded = await audit.TryLogAsync(new CreateAuditLogRequest
        {
            ActionType = action, ResourceType = "AuditReplay", UserId = actorId, TenantId = tenantId,
            Success = success, Category = AuditCategory.Security, RiskLevel = AuditRiskLevel.Medium,
            Description = "Administrator reconstructed an audit replay timeline.",
            Metadata = new
            {
                StartUtc = document.StartUtc, EndUtc = document.EndUtc,
                TotalCount = reconstruction?.TotalCount ?? document.TotalCount,
                SnapshotSha256 = digest,
                Users = reconstruction?.Users.Count, Sessions = reconstruction?.Sessions.Count
            }
        }).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!recorded) { throw new AuditReplayCaptureAuditException(); }
    }
}

public sealed class AuditReplayIntegrityMismatchException() : ArgumentException(
    "Replay snapshot does not match its expected digest; no timeline is reconstructed.");
