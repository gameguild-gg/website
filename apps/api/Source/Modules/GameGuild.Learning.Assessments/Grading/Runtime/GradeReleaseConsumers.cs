using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Enrollments;
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
            payload.AssessmentId == Guid.Empty ||
            payload.GradeRoundId == Guid.Empty)
        {
            throw new JsonException("Grade result release event version or identity is invalid.");
        }

        return payload;
    }

    internal static async Task<Guid[]> ResolveRecipientsAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> enrollmentIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = enrollmentIds.Distinct().ToArray();
        var enrollments = await context.Set<Enrollment>()
            .AsNoTracking()
            .Where(value => distinctIds.Contains(value.Id))
            .Select(value => new { value.Id, value.UserId })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (enrollments.Length != distinctIds.Length)
        {
            throw new InvalidOperationException("A released result references an unknown enrollment.");
        }

        return enrollments.Select(value => value.UserId).Distinct().ToArray();
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

        var recipients = await GradeReleaseNotificationConsumer.ResolveRecipientsAsync(
                context,
                payload.ParticipantEnrollmentIds,
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var recipientId in recipients)
        {
            await passback.PostScoreIfMappedAsync(
                    payload.AssessmentId,
                    recipientId,
                    round.Score.Value,
                    round.MaxScore,
                    cancellationToken)
                .ConfigureAwait(false);
        }
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
