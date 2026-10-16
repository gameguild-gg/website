using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Enrollments;
using GameGuild.Learning.Grading.Contracts;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Learning.Assessments.Grading.Runtime;

public sealed class GradeReleaseNotificationConsumer(
    IApplicationDbContext context,
    IServiceProvider services) : IAcademicOutboxConsumer
{
    public string Key => "grading.grade-result-released.notification.v1";

    public IReadOnlySet<string> EventTypes { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "grade-result-released" };

    public async Task ConsumeAsync(AcademicOutboxEvent message, CancellationToken cancellationToken)
    {
        var notifications = services.GetService<INotificationService>();
        if (notifications is null)
        {
            return;
        }

        var payload = Parse(message);
        var recipients = await ResolveRecipientsAsync(context, payload.ParticipantEnrollmentIds, cancellationToken)
            .ConfigureAwait(false);
        foreach (var recipientId in recipients)
        {
            var exists = await context.Set<Notification>()
                .AsNoTracking()
                .AnyAsync(
                    value => value.RecipientId == recipientId &&
                             value.Type == NotificationType.AssessmentGraded &&
                             value.ReferenceEntityId == message.Id &&
                             value.ReferenceEntityType == "GradeResultReleased",
                    cancellationToken)
                .ConfigureAwait(false);
            if (exists)
            {
                continue;
            }

            var result = await notifications.SendAsync(
                    recipientId,
                    NotificationType.AssessmentGraded,
                    "Assessment result available",
                    "Your assessment score and feedback are now available.",
                    NotificationChannel.InApp,
                    message.TenantId,
                    "/learn",
                    NotificationPriority.Normal,
                    message.Id,
                    "GradeResultReleased",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (result.IsFailure)
            {
                throw new InvalidOperationException(result.Error.Description);
            }
        }
    }

    internal static GradeResultReleasedPayload Parse(AcademicOutboxEvent message)
    {
        var payload = JsonSerializer.Deserialize<GradeResultReleasedPayload>(
            message.PayloadCanonicalJson,
            GradingJson.Options) ?? throw new JsonException("Grade result release event is invalid.");
        if (payload.SchemaVersion != 1 ||
            payload.ReleaseId == Guid.Empty ||
            payload.SubmissionId == Guid.Empty ||
            payload.AssessmentId == Guid.Empty ||
            payload.ExecutionId == Guid.Empty ||
            payload.GradeRoundId == Guid.Empty)
        {
            throw new JsonException("Grade result release event version or identity is invalid.");
        }

        return payload;
    }

    internal static async Task<Guid[]> ResolveRecipientsAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> enrollmentIds,
        CancellationToken cancellationToken) =>
        (await ResolveRecipientEnrollmentsAsync(context, enrollmentIds, cancellationToken).ConfigureAwait(false))
        .Select(value => value.UserId)
        .Distinct()
        .ToArray();

    internal static async Task<GradeReleaseRecipient[]> ResolveRecipientEnrollmentsAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> enrollmentIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = enrollmentIds.Distinct().ToArray();
        var enrollments = await context.Set<Enrollment>()
            .AsNoTracking()
            .Where(value => distinctIds.Contains(value.Id))
            .Select(value => new GradeReleaseRecipient(value.Id, value.UserId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (enrollments.Length != distinctIds.Length)
        {
            throw new InvalidOperationException("A released result references an unknown enrollment.");
        }

        return enrollments;
    }
}

public sealed class GradeReleasePassbackConsumer(
    IApplicationDbContext context,
    IServiceProvider services) : IAcademicOutboxConsumer
{
    public string Key => "grading.grade-result-released.passback.v1";

    public IReadOnlySet<string> EventTypes { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "grade-result-released" };

    public async Task ConsumeAsync(AcademicOutboxEvent message, CancellationToken cancellationToken)
    {
        var passback = services.GetService<ILtiScorePassback>();
        if (passback is null)
        {
            return;
        }

        var payload = GradeReleaseNotificationConsumer.Parse(message);
        var round = await context.Set<GradeRound>()
            .AsNoTracking()
            .SingleAsync(value => value.Id == payload.GradeRoundId, cancellationToken)
            .ConfigureAwait(false);
        if (round.Status != PersistedGradeRoundStatus.Finalized || !round.Score.HasValue)
        {
            throw new InvalidOperationException("Only a finalized grade round can be passed back.");
        }

        var recipients = await GradeReleaseNotificationConsumer.ResolveRecipientEnrollmentsAsync(
                context,
                payload.ParticipantEnrollmentIds,
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var recipient in recipients.GroupBy(value => value.UserId).OrderBy(value => value.Key))
        {
            await DeliverCanonicalScoreAsync(
                passback,
                payload,
                recipient.Key,
                recipient.Select(value => value.EnrollmentId).ToArray(),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private Task<bool> DeliverCanonicalScoreAsync(
        ILtiScorePassback passback,
        GradeResultReleasedPayload payload,
        Guid recipientId,
        IReadOnlyCollection<Guid> enrollmentIds,
        CancellationToken cancellationToken) =>
        GradingRuntimeDatabaseLock.ExecuteWithStrategyAsync(context, async () =>
        {
            await using var transaction = await GradingRuntimeDatabaseLock.AcquireAsync(
                context,
                "grade-release-recipient",
                payload.AssessmentId,
                recipientId,
                cancellationToken).ConfigureAwait(false);
            var canonical = await ResolveCanonicalReleasedRoundAsync(
                payload,
                enrollmentIds,
                cancellationToken).ConfigureAwait(false);
            if (canonical.GradeRoundId == payload.GradeRoundId)
            {
                await passback.PostScoreIfMappedAsync(
                        payload.AssessmentId,
                        recipientId,
                        canonical.Score,
                        canonical.MaxScore,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await GradingRuntimeDatabaseLock.CommitAsync(transaction, cancellationToken).ConfigureAwait(false);
            return true;
        });

    private async Task<CanonicalReleasedRound> ResolveCanonicalReleasedRoundAsync(
        GradeResultReleasedPayload payload,
        IReadOnlyCollection<Guid> enrollmentIds,
        CancellationToken cancellationToken)
    {
        var enrollmentIdArray = enrollmentIds.Distinct().ToArray();
        var candidates = await (
                from release in context.Set<GradeResultRelease>().AsNoTracking()
                join round in context.Set<GradeRound>().AsNoTracking()
                    on release.GradeRoundId equals round.Id
                join execution in context.Set<GradingExecution>().AsNoTracking()
                    on round.GradingExecutionId equals execution.Id
                join submission in context.Set<AssessmentSubmission>().AsNoTracking()
                    on execution.AssessmentSubmissionId equals (Guid?)submission.Id
                where submission.AssessmentId == payload.AssessmentId &&
                      execution.ExecutionContext == ReviewExecutionContext.OfficialSubmission &&
                      round.Status == PersistedGradeRoundStatus.Finalized &&
                      round.Score != null
                select new CanonicalReleasedRound(
                    submission.Id,
                    execution.Id,
                    submission.EnrollmentId,
                    submission.AttemptNumber,
                    round.Id,
                    round.RoundNumber,
                    round.Score!.Value,
                    round.MaxScore,
                    release.ReleasedAt))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var submissionIds = candidates.Select(value => value.SubmissionId).Distinct().ToArray();
        var participantSubmissionIds = await context.Set<AssessmentSubmissionParticipant>()
            .AsNoTracking()
            .Where(value => submissionIds.Contains(value.SubmissionId) && enrollmentIdArray.Contains(value.EnrollmentId))
            .Select(value => value.SubmissionId)
            .Distinct()
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var participantSet = participantSubmissionIds.ToHashSet();
        var enrollmentSet = enrollmentIdArray.ToHashSet();
        var eligible = candidates
            .Where(value =>
                (value.EnrollmentId.HasValue && enrollmentSet.Contains(value.EnrollmentId.Value)) ||
                participantSet.Contains(value.SubmissionId))
            .ToArray();
        if (!eligible.Any(value =>
                value.GradeRoundId == payload.GradeRoundId &&
                value.SubmissionId == payload.SubmissionId &&
                value.ExecutionId == payload.ExecutionId))
        {
            throw new InvalidOperationException("A released result does not belong to the declared learner and submission.");
        }

        return eligible
            .OrderByDescending(value => value.AttemptNumber)
            .ThenByDescending(value => value.RoundNumber)
            .ThenByDescending(value => value.ReleasedAt)
            .First();
    }
}

internal sealed record GradeResultReleasedPayload(
    int SchemaVersion,
    Guid ReleaseId,
    Guid SubmissionId,
    Guid AssessmentId,
    Guid ExecutionId,
    Guid GradeRoundId,
    DateTime ReleasedAt,
    Guid[] ParticipantEnrollmentIds);

internal sealed record GradeReleaseRecipient(Guid EnrollmentId, Guid UserId);

internal sealed record CanonicalReleasedRound(
    Guid SubmissionId,
    Guid ExecutionId,
    Guid? EnrollmentId,
    int AttemptNumber,
    Guid GradeRoundId,
    int RoundNumber,
    ScoreValue Score,
    ScoreValue MaxScore,
    DateTime ReleasedAt);
