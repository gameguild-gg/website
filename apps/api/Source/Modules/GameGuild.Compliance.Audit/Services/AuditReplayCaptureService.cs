using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Compliance.Audit;

public sealed record AuditReplayCaptureRequest(DateTimeOffset Start, DateTimeOffset End);

public interface IAuditReplayCaptureService
{
    Task<AuditReplaySnapshot> CaptureAsync(AuditReplayCaptureRequest request, CancellationToken cancellationToken);
}

/// <summary>Captures one bounded statement over retained, active audit rows in the authenticated
/// tenant. Null scope means global-only. Access must be durably audited before any result is returned.</summary>
public sealed class AuditReplayCaptureService(
    IApplicationDbContext context,
    IActorContextAccessor actors,
    IAuditService audit,
    TimeProvider clock) : IAuditReplayCaptureService
{
    public async Task<AuditReplaySnapshot> CaptureAsync(
        AuditReplayCaptureRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsSystemAdmin ||
            actor.SubjectIdAsGuid is not { } actorId || actorId == Guid.Empty || actor.TenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("An authenticated system administrator with a valid identity is required.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var start = request.Start.ToUniversalTime();
        var end = request.End.ToUniversalTime();
        var now = clock.GetUtcNow();
        if (start > end || end > now)
        {
            throw new ArgumentException("Replay range must be ordered and cannot extend into the future.", nameof(request));
        }

        // Freeze identity and scope before awaiting an external service.
        var tenantId = actor.TenantId;
        await RecordRequiredAsync(actorId, tenantId, start, end, "AuditReplayCaptureRequested",
            true, null, cancellationToken).ConfigureAwait(false);

        AuditReplaySnapshot snapshot;
        try
        {
            snapshot = await CaptureSelectionAsync(tenantId, start, end, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            await RecordRequiredAsync(actorId, tenantId, start, end, "AuditReplayCaptureFailed",
                false, null, cancellationToken).ConfigureAwait(false);
            throw;
        }

        await RecordRequiredAsync(actorId, tenantId, start, end, "AuditReplayCaptureCompleted",
            true, snapshot, cancellationToken).ConfigureAwait(false);
        return snapshot;
    }

    private async Task<AuditReplaySnapshot> CaptureSelectionAsync(Guid? tenantId,
        DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        // Retained PostgreSQL timestamps have microsecond precision. Align SQL parameters inward
        // so their encoding cannot include a row outside the original100ns-precision range.
        var startUtc = start.UtcDateTime;
        var startRemainder = startUtc.Ticks % TimeSpan.TicksPerMicrosecond;
        if (startRemainder != 0)
        {
            startUtc = startUtc.AddTicks(TimeSpan.TicksPerMicrosecond - startRemainder);
        }
        var endUtc = end.UtcDateTime;
        endUtc = endUtc.AddTicks(-(endUtc.Ticks % TimeSpan.TicksPerMicrosecond));
        var selection = context.Set<AuditLog>().AsNoTracking()
            .Where(item => item.DeletedAt == null && item.CreatedAt >= startUtc && item.CreatedAt <= endUtc);
        selection = tenantId.HasValue
            ? selection.Where(item => item.TenantId == tenantId.Value)
            : selection.Where(item => item.TenantId == null);

        // One bounded query avoids a count/read race and never materializes free-form content.
        // The extra row detects overflow; it is not returned as a truncated evidence set.
        var rows = await selection.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Take(AuditReplaySnapshot.MaximumEvents + 1)
            .Select(item => new AuditLogDto
            {
                Id = item.Id, TenantId = item.TenantId, CreatedAt = item.CreatedAt,
                ActionType = item.ActionType, ResourceType = item.ResourceType, ResourceId = item.ResourceId,
                UserId = item.UserId, SessionId = item.SessionId, CorrelationId = item.CorrelationId,
                Success = item.Success
            }).ToListAsync(cancellationToken).ConfigureAwait(false);

        return AuditReplaySnapshot.Capture(tenantId, rows, rows.Count, start, end, clock.GetUtcNow());
    }

    private async Task RecordRequiredAsync(Guid actorId, Guid? tenantId, DateTimeOffset start,
        DateTimeOffset end, string action, bool success, AuditReplaySnapshot? snapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var recorded = await audit.TryLogAsync(new CreateAuditLogRequest
        {
            ActionType = action, ResourceType = "AuditReplay", UserId = actorId, TenantId = tenantId,
            Success = success, Category = AuditCategory.Security, RiskLevel = AuditRiskLevel.Medium,
            Description = "Administrator requested an audit replay snapshot.",
            Metadata = new { StartUtc = start, EndUtc = end, TotalCount = snapshot?.TotalCount, SnapshotSha256 = snapshot?.Sha256 }
        }).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!recorded) { throw new AuditReplayCaptureAuditException(); }
    }
}

public sealed class AuditReplayCaptureAuditException() : InvalidOperationException(
    "Replay capture cannot release evidence because its required access audit was not persisted.");
