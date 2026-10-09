using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Learning.Assessments.Grading.Runtime;

public sealed class OfficialGradingFinalizationSink(
    IApplicationDbContext context,
    IAcademicOutboxWriter outbox,
    IAssessmentGradebookProjectionService gradebookProjection) : IGradingFinalizationSink
{
    public async Task OnFinalizedAsync(
        GradingExecution execution,
        GradeRound round,
        GradeResultV1 result,
        AssessmentExecutionSnapshotV1 snapshot,
        CancellationToken cancellationToken)
    {
        if (execution.ExecutionContext != ReviewExecutionContext.OfficialSubmission ||
            !execution.AssessmentSubmissionId.HasValue)
        {
            throw new InvalidOperationException("Only an official submission can produce academic effects.");
        }
        if (!execution.TenantId.HasValue)
        {
            throw new InvalidOperationException("Official grading requires a tenant-scoped execution.");
        }

        var submission = await context.Set<AssessmentSubmission>()
            .SingleAsync(value => value.Id == execution.AssessmentSubmissionId.Value, cancellationToken)
            .ConfigureAwait(false);
        var assessment = await context.Set<Assessment>()
            .SingleAsync(value => value.Id == submission.AssessmentId, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Score.HasValue)
        {
            throw new InvalidOperationException("An official finalized result requires a score.");
        }

        var passingScore = snapshot.AuthoringSource.Policy.PassingScore ?? ScoreValue.Zero;
        submission.ApplyRuntimeGrade(result.Score.Value, passingScore, result.MaxScore, result.Feedback);

        var enrollmentIds = await ResolveEnrollmentIdsAsync(submission, cancellationToken).ConfigureAwait(false);
        await gradebookProjection.ProjectFinalizedResultAsync(
            submission,
            assessment,
            round,
            result,
            enrollmentIds,
            cancellationToken).ConfigureAwait(false);

        if (snapshot.AuthoringSource.Policy.Completion.Mode == ContentCompletionMode.OnFinalize)
        {
            await ProjectCompletionAsync(
                submission,
                assessment,
                round.Id,
                enrollmentIds,
                "finalize",
                cancellationToken).ConfigureAwait(false);
        }

        Enqueue(execution.TenantId.Value, "grade-result-finalized", new
        {
            schemaVersion = 1,
            executionId = execution.Id,
            submissionId = submission.Id,
            assessmentId = assessment.Id,
            gradeRoundId = round.Id,
            score = result.Score,
            maxScore = result.MaxScore,
            passed = submission.Passed,
            collective = submission.IsCollective,
            participantEnrollmentIds = enrollmentIds,
        });
        if (snapshot.AuthoringSource.Policy.ResultRelease.Mode == ResultReleaseMode.Immediate)
        {
            Enqueue(execution.TenantId.Value, "grade-result-release-requested", new
            {
                schemaVersion = 1,
                executionId = execution.Id,
                submissionId = submission.Id,
                gradeRoundId = round.Id,
                expectedSubmissionVersion = checked(submission.Version + 1),
                reason = "immediate-release-policy",
            });
        }
    }

    internal async Task ProjectCompletionAsync(
        AssessmentSubmission submission,
        Assessment assessment,
        Guid? roundId,
        IReadOnlyList<Guid> enrollmentIds,
        string transition,
        CancellationToken cancellationToken)
    {
        if (!assessment.ContentId.HasValue)
        {
            return;
        }

        var existing = await context.Set<AssessmentContentCompletionProjection>()
            .Where(value => value.AssessmentId == assessment.Id &&
                            value.ContentId == assessment.ContentId.Value &&
                            enrollmentIds.Contains(value.EnrollmentId))
            .Select(value => value.EnrollmentId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var existingIds = existing.ToHashSet();
        foreach (var enrollmentId in enrollmentIds.Where(value => !existingIds.Contains(value)))
        {
            context.Set<AssessmentContentCompletionProjection>().Add(
                AssessmentContentCompletionProjection.Create(
                    submission.TenantId,
                    assessment.Id,
                    assessment.ContentId.Value,
                    enrollmentId,
                    submission.Id,
                    roundId,
                    transition));
        }
    }

    internal async Task<IReadOnlyList<Guid>> ResolveEnrollmentIdsAsync(
        AssessmentSubmission submission,
        CancellationToken cancellationToken)
    {
        if (submission.EnrollmentId.HasValue)
        {
            return [submission.EnrollmentId.Value];
        }

        return await context.Set<AssessmentSubmissionParticipant>()
            .AsNoTracking()
            .Where(value => value.SubmissionId == submission.Id)
            .OrderBy(value => value.EnrollmentId)
            .Select(value => value.EnrollmentId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private void Enqueue(Guid tenantId, string eventType, object payload)
    {
        var element = JsonSerializer.SerializeToElement(payload, GradingJson.Options);
        outbox.Enqueue(tenantId, eventType, "1", CanonicalJson.Serialize(element));
    }
}
