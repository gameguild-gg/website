using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Learning.Assessments.Grading.Runtime;

public interface IGradeReleaseService
{
    Task<GradeResultRelease> ReleaseByActorAsync(
        Guid submissionId,
        Guid expectedRoundId,
        int expectedSubmissionVersion,
        Guid actorId,
        string idempotencyKey,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<GradeResultRelease> ReleaseByServiceAsync(
        Guid submissionId,
        Guid expectedRoundId,
        int expectedSubmissionVersion,
        string service,
        string idempotencyKey,
        string? reason,
        CancellationToken cancellationToken = default);
}

public sealed class GradeReleaseService(
    IApplicationDbContext context,
    IAcademicOutboxWriter outbox,
    OfficialGradingFinalizationSink finalizationSink) : IGradeReleaseService
{
    private static readonly Guid ServiceActorId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Task<GradeResultRelease> ReleaseByActorAsync(
        Guid submissionId,
        Guid expectedRoundId,
        int expectedSubmissionVersion,
        Guid actorId,
        string idempotencyKey,
        string? reason,
        CancellationToken cancellationToken = default) =>
        ReleaseAsync(
            submissionId,
            expectedRoundId,
            expectedSubmissionVersion,
            actorId,
            null,
            idempotencyKey,
            reason,
            cancellationToken);

    public Task<GradeResultRelease> ReleaseByServiceAsync(
        Guid submissionId,
        Guid expectedRoundId,
        int expectedSubmissionVersion,
        string service,
        string idempotencyKey,
        string? reason,
        CancellationToken cancellationToken = default) =>
        ReleaseAsync(
            submissionId,
            expectedRoundId,
            expectedSubmissionVersion,
            ServiceActorId,
            string.IsNullOrWhiteSpace(service) ? throw new ArgumentException("Service is required.", nameof(service)) : service.Trim(),
            idempotencyKey,
            reason,
            cancellationToken);

    private async Task<GradeResultRelease> ReleaseAsync(
        Guid submissionId,
        Guid expectedRoundId,
        int? expectedSubmissionVersion,
        Guid receiptActorId,
        string? service,
        string idempotencyKey,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (submissionId == Guid.Empty || expectedRoundId == Guid.Empty || receiptActorId == Guid.Empty)
        {
            throw new ArgumentException("Submission, round, and actor IDs are required.");
        }

        var key = RequireIdempotencyKey(idempotencyKey);
        await using var transaction = await GradingRuntimeDatabaseLock.AcquireAsync(
            context,
            "release-grade-result",
            submissionId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var submission = await context.Set<AssessmentSubmission>()
            .SingleOrDefaultAsync(value => value.Id == submissionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Assessment submission was not found.");
        if (!submission.TenantId.HasValue)
        {
            throw new InvalidOperationException("Grade release requires a tenant-scoped submission.");
        }

        var request = new
        {
            schemaVersion = 1,
            submissionId,
            expectedRoundId,
            expectedSubmissionVersion,
            reason,
            service,
        };
        var requestHash = CanonicalJson.Sha256(JsonSerializer.SerializeToElement(request, GradingJson.Options));
        const string commandType = "release-grade-result";
        var receipt = await context.Set<GradingCommandReceipt>()
            .AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.TenantId == submission.TenantId.Value &&
                value.ResourceId == submissionId &&
                value.CommandType == commandType &&
                value.ActorId == receiptActorId &&
                value.IdempotencyKey == key,
                cancellationToken)
            .ConfigureAwait(false);
        if (receipt is not null)
        {
            if (!string.Equals(receipt.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The idempotency key was used with a different release request.");
            }

            var replayOutcome = JsonSerializer.Deserialize<ReleaseOutcome>(receipt.OutcomeCanonicalJson, GradingJson.Options)
                ?? throw new InvalidOperationException("Stored release outcome is invalid.");
            var replay = await context.Set<GradeResultRelease>()
                .SingleAsync(value => value.Id == replayOutcome.ReleaseId, cancellationToken)
                .ConfigureAwait(false);
            await CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
            return replay;
        }

        if (expectedSubmissionVersion.HasValue && submission.Version != expectedSubmissionVersion.Value)
        {
            throw new InvalidOperationException("The submission version is stale.");
        }

        var execution = await context.Set<GradingExecution>()
            .SingleOrDefaultAsync(value => value.AssessmentSubmissionId == submissionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Submission does not have a grading execution.");
        if (execution.ExecutionContext != ReviewExecutionContext.OfficialSubmission || execution.ActiveGradeRoundId != expectedRoundId)
        {
            throw new InvalidOperationException("The expected round is not the active official result.");
        }

        var round = await context.Set<GradeRound>()
            .SingleAsync(value => value.Id == expectedRoundId && value.GradingExecutionId == execution.Id, cancellationToken)
            .ConfigureAwait(false);
        if (round.Status != PersistedGradeRoundStatus.Finalized)
        {
            throw new InvalidOperationException("Only a finalized grade round can be released.");
        }

        var existing = await context.Set<GradeResultRelease>()
            .SingleOrDefaultAsync(value => value.GradeRoundId == round.Id, cancellationToken)
            .ConfigureAwait(false);
        var release = existing ?? (service is null
            ? GradeResultRelease.CreateByActor(
                submission.TenantId.Value,
                round.Id,
                execution.Id,
                receiptActorId,
                reason)
            : GradeResultRelease.CreateByService(
                submission.TenantId.Value,
                round.Id,
                execution.Id,
                service,
                reason));
        if (existing is null)
        {
            context.Set<GradeResultRelease>().Add(release);
        }

        var revision = await context.Set<AssessmentDefinitionRevision>()
            .AsNoTracking()
            .SingleAsync(value => value.Id == execution.DefinitionRevisionId, cancellationToken)
            .ConfigureAwait(false);
        var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
        var assessment = await context.Set<Assessment>()
            .SingleAsync(value => value.Id == submission.AssessmentId, cancellationToken)
            .ConfigureAwait(false);
        var enrollmentIds = await finalizationSink.ResolveEnrollmentIdsAsync(submission, cancellationToken).ConfigureAwait(false);
        if (existing is null &&
            (snapshot.AuthoringSource.Policy.Completion.Mode is ContentCompletionMode.OnRelease or ContentCompletionMode.OnReleaseAndPass) &&
            (snapshot.AuthoringSource.Policy.Completion.Mode != ContentCompletionMode.OnReleaseAndPass || submission.Passed == true))
        {
            await finalizationSink.ProjectCompletionAsync(
                submission,
                assessment,
                round.Id,
                enrollmentIds,
                snapshot.AuthoringSource.Policy.Completion.Mode == ContentCompletionMode.OnRelease
                    ? "release"
                    : "release-and-pass",
                cancellationToken).ConfigureAwait(false);
        }

        if (existing is null)
        {
            var releasedEvent = JsonSerializer.SerializeToElement(new
            {
                schemaVersion = 1,
                releaseId = release.Id,
                submissionId,
                assessmentId = submission.AssessmentId,
                executionId = execution.Id,
                gradeRoundId = round.Id,
                releasedAt = release.ReleasedAt,
                participantEnrollmentIds = enrollmentIds,
            }, GradingJson.Options);
            outbox.Enqueue(
                submission.TenantId.Value,
                "grade-result-released",
                "1",
                CanonicalJson.Serialize(releasedEvent));
        }

        var outcome = new ReleaseOutcome(1, release.Id, round.Id);
        context.Set<GradingCommandReceipt>().Add(GradingCommandReceipt.Create(
            submission.TenantId.Value,
            submissionId,
            commandType,
            receiptActorId,
            key,
            requestHash,
            "1",
            CanonicalJson.Serialize(JsonSerializer.SerializeToElement(outcome, GradingJson.Options)),
            SystemClock.UtcNow.AddDays(90)));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
        return release;
    }

    private Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) =>
        context is DbContext dbContext && dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is null
            ? BeginRelationalTransactionAsync(context, cancellationToken)
            : Task.FromResult<IDbContextTransaction?>(null);

    private static async Task<IDbContextTransaction?> BeginRelationalTransactionAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken) =>
        await context.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken) =>
        transaction is null ? Task.CompletedTask : transaction.CommitAsync(cancellationToken);

    private static string RequireIdempotencyKey(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Idempotency key is required.", nameof(value))
            : value.Trim();
        return normalized.Length <= 200
            ? normalized
            : throw new ArgumentException("Idempotency key cannot exceed 200 characters.", nameof(value));
    }

    private sealed record ReleaseOutcome(int SchemaVersion, Guid ReleaseId, Guid GradeRoundId);
}

public sealed class ImmediateGradeReleaseConsumer(
    IServiceScopeFactory scopeFactory) : IAcademicOutboxConsumer
{
    public string Key => "grading.immediate-release.v1";
    public IReadOnlySet<string> EventTypes { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "grade-result-release-requested" };

    public async Task ConsumeAsync(AcademicOutboxEvent message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<ImmediateReleasePayload>(message.PayloadCanonicalJson, GradingJson.Options)
            ?? throw new JsonException("Immediate release request is invalid.");
        if (payload.SchemaVersion != 1)
        {
            throw new JsonException("Immediate release request version is unsupported.");
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var releaseService = scope.ServiceProvider.GetRequiredService<IGradeReleaseService>();
        await releaseService.ReleaseByServiceAsync(
            payload.SubmissionId,
            payload.GradeRoundId,
            payload.ExpectedSubmissionVersion,
            "grading.immediate-release.v1",
            message.Id.ToString("N"),
            payload.Reason,
            cancellationToken).ConfigureAwait(false);
    }

    private sealed record ImmediateReleasePayload(
        int SchemaVersion,
        Guid ExecutionId,
        Guid SubmissionId,
        Guid GradeRoundId,
        int ExpectedSubmissionVersion,
        string? Reason);
}
