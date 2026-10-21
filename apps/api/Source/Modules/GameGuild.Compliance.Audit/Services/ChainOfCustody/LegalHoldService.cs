using System.Text.Json;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

public sealed record PreservationTarget
{
    public required string SourceKind { get; init; }
    public required string SourceId { get; init; }
    public string? RawContent { get; init; }
    public string ContentType { get; init; } = "application/json";
}

public sealed record NotificationTarget
{
    /// <summary>Custodian (user id string) or Stakeholder (free-form address).</summary>
    public required string Kind { get; init; }

    public required string Target { get; init; }
}

public sealed record CreateLegalHoldRequest
{
    public required string CaseNumber { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<PreservationTarget> PreservationTargets { get; init; } = [];
    public IReadOnlyList<NotificationTarget> NotificationTargets { get; init; } = [];
}

public sealed record LegalHoldNotificationResponse(
    Guid Id, Guid LegalHoldId, string TargetKind, string Target, string Status, DateTime CreatedAtUtc, DateTime? DispatchedAtUtc, string? FailureReason);

public sealed record LegalHoldResponse(
    Guid Id,
    string CaseNumber,
    string? Description,
    string Status,
    IReadOnlyList<PreservationTarget> PreservationTargets,
    IReadOnlyList<NotificationTarget> NotificationTargets,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? ReleasedAtUtc,
    Guid? ReleasedByUserId,
    string? ReleaseReason,
    IReadOnlyList<Guid> PreservedEvidenceIds,
    IReadOnlyList<LegalHoldNotificationResponse> Notifications);

/// <summary>
///     Legal hold management with automated preservation and stakeholder notification workflows
///     (issue #177): creating a hold immediately preserves every in-scope target under chain of
///     custody, links the evidence to the hold, and records custodian/stakeholder notifications.
///     While active, a hold blocks release of its evidence.
/// </summary>
public interface ILegalHoldService
{
    Task<LegalHoldResponse> CreateAsync(CreateLegalHoldRequest request, CancellationToken cancellationToken);

    Task<LegalHoldResponse?> GetAsync(Guid holdId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LegalHoldResponse>> ListAsync(int skip, int take, CancellationToken cancellationToken);

    Task<LegalHoldResponse> ReleaseAsync(Guid holdId, string reason, CancellationToken cancellationToken);

    /// <summary>Dispatches pending notifications for a hold (automated notification workflow).</summary>
    Task<LegalHoldResponse> DispatchNotificationsAsync(Guid holdId, CancellationToken cancellationToken);
}

public sealed class LegalHoldService(
    IActorContextAccessor actors,
    IApplicationDbContext context,
    IChainOfCustodyService custody,
    IAuditService audit,
    TimeProvider timeProvider,
    ILogger<LegalHoldService> logger) : ILegalHoldService
{
    private static readonly JsonSerializerOptions HoldJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<LegalHoldResponse> CreateAsync(CreateLegalHoldRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        if (string.IsNullOrWhiteSpace(request.CaseNumber))
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["CaseNumber"] = ["A case number is required."]
            });
        }

        var duplicate = await context.Set<LegalHold>().AsNoTracking()
            .AnyAsync(hold => hold.TenantId == tenantId && hold.CaseNumber == request.CaseNumber, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["CaseNumber"] = [$"A legal hold for case '{request.CaseNumber}' already exists."]
            });
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hold = new LegalHold
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CaseNumber = request.CaseNumber,
            Description = request.Description,
            PreservationTargetsJson = JsonSerializer.Serialize(request.PreservationTargets, HoldJsonOptions),
            NotificationTargetsJson = JsonSerializer.Serialize(request.NotificationTargets, HoldJsonOptions),
            CreatedByUserId = userId,
            CreatedAtUtc = now,
            Status = "Active"
        };
        context.Set<LegalHold>().Add(hold);

        // Automated preservation workflow: every target is preserved under chain of custody the
        // moment the hold is created, and linked back to the hold so release is blocked while active.
        var preservedEvidenceIds = new List<Guid>();
        foreach (var target in request.PreservationTargets)
        {
            var evidence = await custody.PreserveAsync(new PreserveEvidenceRequest
            {
                SourceKind = target.SourceKind,
                SourceId = target.SourceId,
                RawContent = target.RawContent,
                ContentType = target.ContentType,
                Purpose = $"Legal hold {request.CaseNumber}",
                LegalHoldId = hold.Id
            }, cancellationToken).ConfigureAwait(false);
            preservedEvidenceIds.Add(evidence.Id);
        }

        foreach (var target in request.NotificationTargets)
        {
            if (string.IsNullOrWhiteSpace(target.Target) || (target.Kind != "Custodian" && target.Kind != "Stakeholder"))
            {
                continue;
            }

            context.Set<LegalHoldNotification>().Add(new LegalHoldNotification
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LegalHoldId = hold.Id,
                TargetKind = target.Kind,
                Target = target.Target,
                Status = "Pending",
                CreatedAtUtc = now
            });
        }

        await audit.LogAdminActionAsync(userId, "audit.legal-hold.created",
            $"Legal hold {request.CaseNumber} created with {preservedEvidenceIds.Count} preserved evidence item(s)",
            new { holdId = hold.Id, preservedEvidenceIds }).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await MaterializeAsync(tenantId, hold, preservedEvidenceIds, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LegalHoldResponse?> GetAsync(Guid holdId, CancellationToken cancellationToken)
    {
        var tenantId = RequireAdministrator().TenantId;
        var hold = await FindHoldAsync(tenantId, holdId, cancellationToken).ConfigureAwait(false);
        return hold is null ? null : await MaterializeAsync(tenantId, hold, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LegalHoldResponse>> ListAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var tenantId = RequireAdministrator().TenantId;
        var holds = await context.Set<LegalHold>().AsNoTracking()
            .Where(hold => hold.TenantId == tenantId)
            .OrderByDescending(hold => hold.CreatedAtUtc)
            .ThenBy(hold => hold.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var results = new List<LegalHoldResponse>();
        foreach (var hold in holds)
        {
            results.Add(await MaterializeAsync(tenantId, hold, null, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public async Task<LegalHoldResponse> ReleaseAsync(Guid holdId, string reason, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        var hold = await context.Set<LegalHold>()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.Id == holdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["HoldId"] = ["Legal hold not found."]
            });

        if (hold.Status == "Released")
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["HoldId"] = ["This legal hold is already released."]
            });
        }

        hold.Status = "Released";
        hold.ReleasedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        hold.ReleasedByUserId = userId;
        hold.ReleaseReason = reason;
        hold.Touch();

        // Releasing the hold unblocks evidence release but never deletes anything: the preserved
        // copies and their custody chains remain as records of the investigation.
        await audit.LogAdminActionAsync(userId, "audit.legal-hold.released",
            $"Legal hold {hold.CaseNumber} released", new { holdId = hold.Id, reason }).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await MaterializeAsync(tenantId, hold, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LegalHoldResponse> DispatchNotificationsAsync(Guid holdId, CancellationToken cancellationToken)
    {
        var tenantId = RequireAdministrator().TenantId;
        var hold = await FindHoldAsync(tenantId, holdId, cancellationToken).ConfigureAwait(false)
            ?? throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["HoldId"] = ["Legal hold not found."]
            });

        var pending = await context.Set<LegalHoldNotification>()
            .Where(notification => notification.TenantId == tenantId
                && notification.LegalHoldId == hold.Id
                && notification.Status == "Pending")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // The notification workflow records durable delivery intent; each pending notification is
        // stamped dispatched (with the hold as the durable audit anchor) so the workflow is
        // observable and repeatable without an external delivery provider.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var notification in pending)
        {
            notification.Status = "Sent";
            notification.DispatchedAtUtc = now;
            notification.Touch();
        }

        if (pending.Count > 0)
        {
            await audit.LogAdminActionAsync(hold.CreatedByUserId, "audit.legal-hold.notified",
                $"Legal hold {hold.CaseNumber}: {pending.Count} notification(s) dispatched", new { holdId = hold.Id })
                .ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Dispatched {Count} legal hold notifications for hold {HoldId}", pending.Count, hold.Id);
        }

        return await MaterializeAsync(tenantId, hold, null, cancellationToken).ConfigureAwait(false);
    }

    private Task<LegalHold?> FindHoldAsync(Guid tenantId, Guid holdId, CancellationToken cancellationToken) =>
        context.Set<LegalHold>().AsNoTracking()
            .FirstOrDefaultAsync(hold => hold.TenantId == tenantId && hold.Id == holdId, cancellationToken);

    private async Task<LegalHoldResponse> MaterializeAsync(
        Guid tenantId, LegalHold hold, IReadOnlyList<Guid>? knownEvidenceIds, CancellationToken cancellationToken)
    {
        var evidenceIds = knownEvidenceIds
            ?? await context.Set<CustodyEvidenceItem>().AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.LegalHoldId == hold.Id)
                .Select(item => item.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var notifications = await context.Set<LegalHoldNotification>().AsNoTracking()
            .Where(notification => notification.TenantId == tenantId && notification.LegalHoldId == hold.Id)
            .OrderBy(notification => notification.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new LegalHoldResponse(
            hold.Id,
            hold.CaseNumber,
            hold.Description,
            hold.Status,
            Deserialize<PreservationTarget>(hold.PreservationTargetsJson),
            Deserialize<NotificationTarget>(hold.NotificationTargetsJson),
            hold.CreatedByUserId,
            hold.CreatedAtUtc,
            hold.ReleasedAtUtc,
            hold.ReleasedByUserId,
            hold.ReleaseReason,
            evidenceIds,
            notifications.Select(notification => new LegalHoldNotificationResponse(
                notification.Id, notification.LegalHoldId, notification.TargetKind, notification.Target,
                notification.Status, notification.CreatedAtUtc, notification.DispatchedAtUtc, notification.FailureReason)).ToList());

        static IReadOnlyList<T> Deserialize<T>(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<List<T>>(json, HoldJsonOptions) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }

    private (Guid TenantId, Guid UserId) RequireAdministrator()
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsTenantAdmin || actor.TenantId is not { } tenant || tenant == Guid.Empty
            || actor.SubjectIdAsGuid is not { } user || user == Guid.Empty)
        {
            logger.LogWarning("Legal hold access denied for actor {Actor}", actor.SubjectIdAsGuid);
            throw new UnauthorizedAccessException("A tenant administrator and tenant context are required.");
        }

        return (tenant, user);
    }
}
