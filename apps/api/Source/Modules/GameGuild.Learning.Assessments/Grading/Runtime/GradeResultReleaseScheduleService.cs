using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Assessments.Grading.Runtime;

public sealed record GradeResultReleaseScheduleViewV1(
    Guid ScheduleId,
    Guid GradeRoundId,
    DateTime ScheduledFor,
    GradeResultReleaseScheduleStatus Status,
    int Version,
    DateTime? CancelledAt,
    DateTime? ReleasedAt);

public interface IGradeResultReleaseScheduleService
{
    Task<GradeResultReleaseScheduleViewV1> ScheduleByActorAsync(
        Guid submissionId,
        Guid expectedRoundId,
        int expectedSubmissionVersion,
        int expectedScheduleVersion,
        DateTime scheduledFor,
        Guid actorId,
        string idempotencyKey,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<GradeResultReleaseScheduleViewV1> ScheduleByServiceAsync(
        Guid submissionId,
        Guid expectedRoundId,
        int expectedSubmissionVersion,
        DateTime scheduledFor,
        string service,
        string idempotencyKey,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<GradeResultReleaseScheduleViewV1> CancelByActorAsync(
        Guid submissionId,
        Guid expectedRoundId,
        int expectedSubmissionVersion,
        int expectedScheduleVersion,
        Guid actorId,
        string idempotencyKey,
        string reason,
        CancellationToken cancellationToken = default);

    Task<int> ReleaseDueBatchAsync(CancellationToken cancellationToken = default);
}

public sealed class GradeResultReleaseScheduleService(
    IApplicationDbContext context,
    IAcademicOutboxWriter outbox,
    IGradeReleaseService releaseService,
    TimeProvider timeProvider,
    ILogger<GradeResultReleaseScheduleService> logger) : IGradeResultReleaseScheduleService
{
    private const int BatchSize = 25;
    private static readonly Guid ServiceActorId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public Task<GradeResultReleaseScheduleViewV1> ScheduleByActorAsync(
        Guid submissionId,
        Guid expectedRoundId,
        int expectedSubmissionVersion,
        int expectedScheduleVersion,
        DateTime scheduledFor,
        Guid actorId,
        string idempotencyKey,
        string? reason,
        CancellationToken cancellationToken = default) =>
        ScheduleAsync(
            submissionId,
            expectedRoundId,
            expectedSubmissionVersion,
            expectedScheduleVersion,
            scheduledFor,
            actorId,
            null,
            idempotencyKey,
            reason,
            cancellationToken);

    public Task<GradeResultReleaseScheduleViewV1> ScheduleByServiceAsync(
        Guid submissionId,
        Guid expectedRoundId,
        int expectedSubmissionVersion,
        DateTime scheduledFor,
        string service,
        string idempotencyKey,
        string? reason,
        CancellationToken cancellationToken = default) =>
        ScheduleAsync(
            submissionId,
            expectedRoundId,
            expectedSubmissionVersion,
            null,
            scheduledFor,
            ServiceActorId,
            RequireText(service, nameof(service)),
            idempotencyKey,
            reason,
            cancellationToken);

    public async Task<GradeResultReleaseScheduleViewV1> CancelByActorAsync(
        Guid submissionId,
        Guid expectedRoundId,
        int expectedSubmissionVersion,
        int expectedScheduleVersion,
        Guid actorId,
        string idempotencyKey,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ValidateIds(submissionId, expectedRoundId, actorId);
        var key = RequireIdempotencyKey(idempotencyKey);
        var normalizedReason = RequireText(reason, nameof(reason));
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireAsync(
            context,
            "grade-release-schedule",
            expectedRoundId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var operation = await RequireOperationAsync(
            submissionId,
            expectedRoundId,
            expectedSubmissionVersion,
            requireActiveRound: true,
            cancellationToken).ConfigureAwait(false);
        var request = new
        {
            schemaVersion = 1,
            submissionId,
            expectedRoundId,
            expectedSubmissionVersion,
            expectedScheduleVersion,
            reason = normalizedReason,
        };
        var replay = await ReadReceiptAsync(
            operation.TenantId,
            expectedRoundId,
            "cancel-grade-result-release-schedule",
            actorId,
            key,
            RequestHash(request),
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
            return replay;
        }

        var schedule = await context.Set<GradeResultReleaseSchedule>()
            .SingleOrDefaultAsync(value => value.GradeRoundId == expectedRoundId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("The grade round does not have a release schedule.");
        if (schedule.Version != expectedScheduleVersion)
        {
            throw new InvalidOperationException("The release schedule version is stale.");
        }

        schedule.Cancel(actorId, normalizedReason, UtcNow);
        Enqueue(operation.TenantId, "grade-result-release-schedule-cancelled", new
        {
            schemaVersion = 1,
            scheduleId = schedule.Id,
            submissionId,
            gradeRoundId = expectedRoundId,
            actorId,
            reason = normalizedReason,
        });
        AddReceipt(
            operation.TenantId,
            expectedRoundId,
            "cancel-grade-result-release-schedule",
            actorId,
            key,
            RequestHash(request),
            schedule.Id);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return ToView(schedule);
    }

    public async Task<int> ReleaseDueBatchAsync(CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        var scheduleIds = await context.Set<GradeResultReleaseSchedule>()
            .AsNoTracking()
            .Where(value => value.Status == GradeResultReleaseScheduleStatus.Scheduled && value.ScheduledFor <= now)
            .OrderBy(value => value.ScheduledFor)
            .ThenBy(value => value.Id)
            .Select(value => value.Id)
            .Take(BatchSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var released = 0;
        foreach (var scheduleId in scheduleIds)
        {
            try
            {
                if (await ReleaseDueAsync(scheduleId, cancellationToken).ConfigureAwait(false))
                {
                    released++;
                    GradingOperationalMetrics.RecordScheduledRelease(succeeded: true);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Scheduled grade release {ScheduleId} failed", scheduleId);
                GradingOperationalMetrics.RecordScheduledRelease(succeeded: false);
            }
        }

        return released;
    }

    private async Task<GradeResultReleaseScheduleViewV1> ScheduleAsync(
        Guid submissionId,
        Guid expectedRoundId,
        int expectedSubmissionVersion,
        int? expectedScheduleVersion,
        DateTime scheduledFor,
        Guid receiptActorId,
        string? service,
        string idempotencyKey,
        string? reason,
        CancellationToken cancellationToken)
    {
        ValidateIds(submissionId, expectedRoundId, receiptActorId);
        var key = RequireIdempotencyKey(idempotencyKey);
        var normalizedScheduledFor = scheduledFor.ToUniversalTime();
        if (normalizedScheduledFor <= UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(scheduledFor), "Scheduled release must be in the future.");
        }

        await using var transaction = await GradingRuntimeDatabaseLock.AcquireAsync(
            context,
            "grade-release-schedule",
            expectedRoundId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var operation = await RequireOperationAsync(
            submissionId,
            expectedRoundId,
            expectedSubmissionVersion,
            requireActiveRound: service is null,
            cancellationToken).ConfigureAwait(false);
        var request = new
        {
            schemaVersion = 1,
            submissionId,
            expectedRoundId,
            expectedSubmissionVersion,
            expectedScheduleVersion,
            scheduledFor = normalizedScheduledFor,
            reason,
            service,
        };
        var requestHash = RequestHash(request);
        var replay = await ReadReceiptAsync(
            operation.TenantId,
            expectedRoundId,
            "schedule-grade-result-release",
            receiptActorId,
            key,
            requestHash,
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
            return replay;
        }

        if (await context.Set<GradeResultRelease>()
                .AsNoTracking()
                .AnyAsync(value => value.GradeRoundId == expectedRoundId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new InvalidOperationException("A released grade round cannot be scheduled.");
        }

        var schedule = await context.Set<GradeResultReleaseSchedule>()
            .SingleOrDefaultAsync(value => value.GradeRoundId == expectedRoundId, cancellationToken)
            .ConfigureAwait(false);
        var now = UtcNow;
        if (schedule is null)
        {
            if (expectedScheduleVersion.HasValue && expectedScheduleVersion.Value != 0)
            {
                throw new InvalidOperationException("The release schedule version is stale.");
            }

            schedule = service is null
                ? GradeResultReleaseSchedule.CreateByActor(
                    operation.TenantId,
                    expectedRoundId,
                    normalizedScheduledFor,
                    receiptActorId,
                    now)
                : GradeResultReleaseSchedule.CreateByService(
                    operation.TenantId,
                    expectedRoundId,
                    normalizedScheduledFor,
                    service,
                    now);
            context.Set<GradeResultReleaseSchedule>().Add(schedule);
        }
        else
        {
            if (expectedScheduleVersion.HasValue && schedule.Version != expectedScheduleVersion.Value)
            {
                throw new InvalidOperationException("The release schedule version is stale.");
            }

            if (service is null)
            {
                schedule.RescheduleByActor(normalizedScheduledFor, receiptActorId, now);
            }
            else
            {
                schedule.RescheduleByService(normalizedScheduledFor, service, now);
            }
        }

        Enqueue(operation.TenantId, "grade-result-release-scheduled", new
        {
            schemaVersion = 1,
            scheduleId = schedule.Id,
            submissionId,
            gradeRoundId = expectedRoundId,
            scheduledFor = normalizedScheduledFor,
            actorId = service is null ? receiptActorId : (Guid?)null,
            service,
            reason,
        });
        AddReceipt(
            operation.TenantId,
            expectedRoundId,
            "schedule-grade-result-release",
            receiptActorId,
            key,
            requestHash,
            schedule.Id);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return ToView(schedule);
    }

    private async Task<bool> ReleaseDueAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        var lookup = await context.Set<GradeResultReleaseSchedule>()
            .AsNoTracking()
            .Where(value => value.Id == scheduleId)
            .Select(value => new { value.GradeRoundId })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (lookup is null)
        {
            return false;
        }

        await using var transaction = await GradingRuntimeDatabaseLock.AcquireAsync(
            context,
            "grade-release-schedule",
            lookup.GradeRoundId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var schedule = await context.Set<GradeResultReleaseSchedule>()
            .SingleAsync(value => value.Id == scheduleId, cancellationToken)
            .ConfigureAwait(false);
        if (schedule.Status != GradeResultReleaseScheduleStatus.Scheduled || schedule.ScheduledFor > UtcNow)
        {
            await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
            return false;
        }

        var operation = await context.Set<GradeRound>()
            .Where(round => round.Id == schedule.GradeRoundId)
            .Join(
                context.Set<GradingExecution>(),
                round => round.GradingExecutionId,
                execution => execution.Id,
                (round, execution) => new { round, execution })
            .Join(
                context.Set<AssessmentSubmission>(),
                pair => pair.execution.AssessmentSubmissionId,
                submission => submission.Id,
                (pair, submission) => new { pair.round, pair.execution, submission })
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        var release = await releaseService.ReleaseByServiceAsync(
            operation.submission.Id,
            operation.round.Id,
            operation.submission.Version,
            "grading.scheduled-release.v1",
            schedule.Id.ToString("N"),
            "scheduled-release-policy",
            cancellationToken).ConfigureAwait(false);
        schedule.MarkReleased(release.ReleasedAt);
        Enqueue(schedule.TenantId!.Value, "grade-result-release-schedule-completed", new
        {
            schemaVersion = 1,
            scheduleId = schedule.Id,
            releaseId = release.Id,
            submissionId = operation.submission.Id,
            gradeRoundId = operation.round.Id,
            releasedAt = release.ReleasedAt,
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<OperationContext> RequireOperationAsync(
        Guid submissionId,
        Guid expectedRoundId,
        int expectedSubmissionVersion,
        bool requireActiveRound,
        CancellationToken cancellationToken)
    {
        var operation = await context.Set<AssessmentSubmission>()
            .Where(submission => submission.Id == submissionId)
            .Join(
                context.Set<GradingExecution>(),
                submission => submission.Id,
                execution => execution.AssessmentSubmissionId,
                (submission, execution) => new { submission, execution })
            .Join(
                context.Set<GradeRound>(),
                pair => expectedRoundId,
                round => round.Id,
                (pair, round) => new { pair.submission, pair.execution, round })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("The submission or grade round was not found.");
        if (!operation.submission.TenantId.HasValue)
        {
            throw new InvalidOperationException("Grade release scheduling requires a tenant-scoped submission.");
        }

        if (requireActiveRound && operation.submission.Version != expectedSubmissionVersion)
        {
            throw new InvalidOperationException("The submission version is stale.");
        }

        if (operation.execution.ExecutionContext != ReviewExecutionContext.OfficialSubmission ||
            (requireActiveRound && operation.execution.ActiveGradeRoundId != expectedRoundId) ||
            operation.round.GradingExecutionId != operation.execution.Id)
        {
            throw new InvalidOperationException("The expected round is not the active official result.");
        }

        if (operation.round.Status != PersistedGradeRoundStatus.Finalized)
        {
            throw new InvalidOperationException("Only a finalized grade round can be scheduled for release.");
        }

        return new OperationContext(operation.submission.TenantId.Value);
    }

    private async Task<GradeResultReleaseScheduleViewV1?> ReadReceiptAsync(
        Guid tenantId,
        Guid resourceId,
        string commandType,
        Guid actorId,
        string key,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var receipt = await context.Set<GradingCommandReceipt>()
            .AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.TenantId == tenantId &&
                value.ResourceId == resourceId &&
                value.CommandType == commandType &&
                value.ActorId == actorId &&
                value.IdempotencyKey == key,
                cancellationToken)
            .ConfigureAwait(false);
        if (receipt is null)
        {
            return null;
        }

        if (!string.Equals(receipt.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The idempotency key was used with a different release schedule request.");
        }

        var outcome = JsonSerializer.Deserialize<ScheduleOutcome>(receipt.OutcomeCanonicalJson, GradingJson.Options)
            ?? throw new InvalidOperationException("Stored release schedule outcome is invalid.");
        var schedule = await context.Set<GradeResultReleaseSchedule>()
            .AsNoTracking()
            .SingleAsync(value => value.Id == outcome.ScheduleId, cancellationToken)
            .ConfigureAwait(false);
        return ToView(schedule);
    }

    private void AddReceipt(
        Guid tenantId,
        Guid resourceId,
        string commandType,
        Guid actorId,
        string key,
        string requestHash,
        Guid scheduleId)
    {
        var outcome = new ScheduleOutcome(1, scheduleId, resourceId);
        context.Set<GradingCommandReceipt>().Add(GradingCommandReceipt.Create(
            tenantId,
            resourceId,
            commandType,
            actorId,
            key,
            requestHash,
            "1",
            CanonicalJson.Serialize(JsonSerializer.SerializeToElement(outcome, GradingJson.Options)),
            UtcNow.AddDays(90)));
    }

    private void Enqueue(Guid tenantId, string eventType, object payload)
    {
        var element = JsonSerializer.SerializeToElement(payload, GradingJson.Options);
        outbox.Enqueue(tenantId, eventType, "1", CanonicalJson.Serialize(element));
    }

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    private static GradeResultReleaseScheduleViewV1 ToView(GradeResultReleaseSchedule schedule) => new(
        schedule.Id,
        schedule.GradeRoundId,
        schedule.ScheduledFor,
        schedule.Status,
        schedule.Version,
        schedule.CancelledAt,
        schedule.ReleasedAt);

    private static string RequestHash(object request) =>
        CanonicalJson.Sha256(JsonSerializer.SerializeToElement(request, GradingJson.Options));

    private static void ValidateIds(Guid submissionId, Guid roundId, Guid actorId)
    {
        if (submissionId == Guid.Empty || roundId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new ArgumentException("Submission, round, and actor IDs are required.");
        }
    }

    private static string RequireIdempotencyKey(string value)
    {
        var normalized = RequireText(value, nameof(value));
        return normalized.Length <= 200
            ? normalized
            : throw new ArgumentException("Idempotency key cannot exceed 200 characters.", nameof(value));
    }

    private static string RequireText(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", parameterName) : value.Trim();

    private sealed record OperationContext(Guid TenantId);
    private sealed record ScheduleOutcome(int SchemaVersion, Guid ScheduleId, Guid GradeRoundId);
}

public sealed class ScheduledGradeReleaseRequestConsumer(
    IServiceScopeFactory scopeFactory) : IAcademicOutboxConsumer
{
    public string Key => "grading.scheduled-release-request.v1";
    public IReadOnlySet<string> EventTypes { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "grade-result-release-schedule-requested" };

    public async Task ConsumeAsync(AcademicOutboxEvent message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ScheduledReleaseRequestPayload>(
            message.PayloadCanonicalJson,
            GradingJson.Options) ?? throw new JsonException("Scheduled release request is invalid.");
        if (payload.SchemaVersion != 1)
        {
            throw new JsonException("Scheduled release request version is unsupported.");
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IGradeResultReleaseScheduleService>();
        await service.ScheduleByServiceAsync(
            payload.SubmissionId,
            payload.GradeRoundId,
            payload.ExpectedSubmissionVersion,
            payload.ScheduledFor,
            Key,
            message.Id.ToString("N"),
            payload.Reason,
            cancellationToken).ConfigureAwait(false);
    }

    private sealed record ScheduledReleaseRequestPayload(
        int SchemaVersion,
        Guid ExecutionId,
        Guid SubmissionId,
        Guid GradeRoundId,
        int ExpectedSubmissionVersion,
        DateTime ScheduledFor,
        string? Reason);
}

public sealed class ScheduledGradeReleaseBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledGradeReleaseBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IGradeResultReleaseScheduleService>();
                var released = await service.ReleaseDueBatchAsync(stoppingToken).ConfigureAwait(false);
                if (released == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled grade release cycle failed");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
