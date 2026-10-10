using System.ComponentModel.DataAnnotations;
using GameGuild.Learning.Assessments.Grading.Authoring;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Learning.Assessments.QuizAdapter;

/// <summary>
/// Keeps the assessment projection used by the learner experience synchronized
/// with quiz content published through the generic lesson authoring workflow.
/// </summary>
public sealed class QuizProgramContentPublicationParticipant(
    IApplicationDbContext context,
    QuizAssessmentTypeAdapter adapter,
    IAssessmentAuthoringService authoringService,
    IActorContextAccessor actorContextAccessor) : IProgramContentPublicationParticipant
{
    private const string DefaultInstructorReviewConfiguration =
        "{\"schemaVersion\":1,\"instructor\":{\"requireOverrideReason\":false}}";

    public bool CanHandle(ProgramContent content) =>
        content.Type == ProgramContentType.Questionnaire;

    public async Task PreparePublishAsync(
        ProgramContent content,
        AuthoringContentPayload payload,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (!payload.JsonBody.HasValue)
        {
            throw new ValidationException("Quiz content is required before publication.");
        }

        var projection = adapter.ProjectAuthoring(payload.JsonBody.Value);
        var assessment = await context.Set<Assessment>()
            .SingleOrDefaultAsync(
                candidate => candidate.ContentId == content.Id && candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);

        if (projection.Grading is null)
        {
            if (assessment is null)
            {
                return;
            }

            if (assessment.PublishedDefinitionRevisionId.HasValue)
            {
                assessment.UnpublishRevision(
                    assessment.PublishedDefinitionRevisionId.Value,
                    assessment.Version);
            }
            assessment.SoftDelete();
            context.Set<Assessment>().Update(assessment);
            return;
        }

        if (projection.Items.Count == 0)
        {
            throw new ValidationException("Graded quiz content requires at least one assessable item.");
        }

        if (assessment is not null && assessment.Type != AssessmentType.Quiz)
        {
            throw new InvalidOperationException("The linked assessment type does not match quiz content.");
        }

        var tenantId = assessment?.TenantId ?? content.TenantId ?? ResolveActorTenant(actorId);

        if (assessment is null)
        {
            const ReviewMethods reviewMethods =
                ReviewMethods.AutomatedReview | ReviewMethods.InstructorReview;
            assessment = Assessment.Create(
                content.ProgramId,
                payload.Title,
                AssessmentType.Quiz,
                projection.MaxScore,
                payload.IsRequired,
                contentId: content.Id,
                reviewMethods: reviewMethods,
                slug: payload.Slug);
            assessment.TenantId = tenantId;
            assessment.SetDescription(payload.Description);
            assessment.SetDeliveryContract(
                SubmissionModality.StructuredAnswer,
                AssessmentPresentationMode.Continuous);
            assessment.SetReviewPolicy(
                reviewMethods,
                DefaultInstructorReviewConfiguration,
                assessment.AttemptContributionMode,
                assessment.ContentCompletionMode,
                assessment.ResultReleaseMode,
                assessment.ResultReleaseScheduledFor);
            context.Set<Assessment>().Add(assessment);
            return;
        }

        if (!assessment.TenantId.HasValue)
        {
            assessment.TenantId = tenantId;
        }

        var reviewConfiguration = EnsureInstructorReviewConfiguration(
            assessment.ReviewMethods,
            assessment.ReviewConfigurationCanonicalJson);
        var passingScore = assessment.PassingScore.CompareTo(projection.MaxScore) <= 0
            ? assessment.PassingScore
            : ScoreValue.Zero;
        assessment.Update(
            title: payload.Title,
            description: payload.Description,
            clearDescription: payload.Description is null,
            maxScore: projection.MaxScore,
            passingScore: passingScore,
            timeLimitMinutes: assessment.TimeLimitMinutes,
            clearTimeLimitMinutes: false,
            maxAttempts: assessment.MaxAttempts,
            isRequired: payload.IsRequired,
            availableFrom: assessment.AvailableFrom,
            clearAvailableFrom: false,
            availableUntil: assessment.AvailableUntil,
            clearAvailableUntil: false,
            contentId: content.Id,
            submissionModalities: SubmissionModality.StructuredAnswer,
            presentationMode: AssessmentPresentationMode.Continuous,
            dueAt: assessment.DueAt,
            allowLateSubmissions: assessment.AllowLateSubmissions,
            lateSubmissionDeadline: assessment.LateSubmissionDeadline,
            reviewMethods: assessment.ReviewMethods,
            reviewConfigurationCanonicalJson: reviewConfiguration,
            attemptContributionMode: assessment.AttemptContributionMode,
            contentCompletionMode: assessment.ContentCompletionMode,
            resultReleaseMode: assessment.ResultReleaseMode,
            resultReleaseScheduledFor: assessment.ResultReleaseScheduledFor,
            slug: payload.Slug);
        context.Set<Assessment>().Update(assessment);
    }

    private Guid ResolveActorTenant(Guid actorId)
    {
        var actor = actorContextAccessor.ActorContext;
        if (actor.SubjectIdAsGuid != actorId || !actor.TenantId.HasValue)
        {
            throw new ValidationException(
                "A tenant-scoped actor is required to publish graded quiz content.");
        }

        return actor.TenantId.Value;
    }

    private static string? EnsureInstructorReviewConfiguration(
        ReviewMethods methods,
        string? currentConfiguration)
    {
        if (!string.IsNullOrWhiteSpace(currentConfiguration) ||
            !methods.HasFlag(ReviewMethods.InstructorReview))
        {
            return currentConfiguration;
        }

        var methodsRequiringExplicitConfiguration =
            ReviewMethods.PeerReview | ReviewMethods.AIReview | ReviewMethods.SelfReview;
        return (methods & methodsRequiringExplicitConfiguration) == ReviewMethods.None
            ? DefaultInstructorReviewConfiguration
            : currentConfiguration;
    }

    public async Task FinalizePublishAsync(
        ProgramContent content,
        AuthoringContentPayload payload,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var assessment = await context.Set<Assessment>()
            .SingleOrDefaultAsync(
                candidate => candidate.ContentId == content.Id && candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (assessment is null)
        {
            return;
        }

        var prepared = await authoringService.PrepareAsync(
                assessment.Id,
                actorId,
                new PrepareAssessmentRevisionRequest(assessment.Version),
                cancellationToken)
            .ConfigureAwait(false);
        if (!prepared.IsSuccess)
        {
            throw new ValidationException($"{prepared.Error.Code}: {prepared.Error.Description}");
        }

        var published = await authoringService.PublishAsync(
                assessment.Id,
                actorId,
                new PublishAssessmentRevisionRequest(
                    prepared.Value.RevisionId,
                    assessment.Version),
                cancellationToken)
            .ConfigureAwait(false);
        if (!published.IsSuccess)
        {
            throw new ValidationException($"{published.Error.Code}: {published.Error.Description}");
        }
    }
}
