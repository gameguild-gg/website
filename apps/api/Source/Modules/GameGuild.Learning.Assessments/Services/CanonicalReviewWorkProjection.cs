using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Learning.Assessments;

internal sealed record CanonicalReviewWorkState(
    Guid GradeRoundId,
    GradeRoundStatusV1 GradeRoundStatus,
    Guid? ReviewStageId,
    ReviewMethod? ReviewMethod,
    ReviewStageStatusV1? ReviewStageStatus,
    bool RequiresStaffReview);

/// <summary>
/// Projects operational review work from the canonical execution graph. Submission status
/// remains delivery metadata and must not decide which asynchronous review stage owns work.
/// </summary>
internal static class CanonicalReviewWorkProjection
{
    public static async Task<IReadOnlyDictionary<Guid, CanonicalReviewWorkState>> LoadAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> submissionIds,
        CancellationToken cancellationToken = default)
    {
        if (submissionIds.Count == 0)
        {
            return new Dictionary<Guid, CanonicalReviewWorkState>();
        }

        var ids = submissionIds.Distinct().ToArray();
        var executions = await context.Set<GradingExecution>()
            .AsNoTracking()
            .Where(value =>
                value.ExecutionContext == ReviewExecutionContext.OfficialSubmission &&
                value.AssessmentSubmissionId.HasValue &&
                ids.Contains(value.AssessmentSubmissionId.Value) &&
                value.ActiveGradeRoundId.HasValue)
            .Select(value => new
            {
                SubmissionId = value.AssessmentSubmissionId!.Value,
                RoundId = value.ActiveGradeRoundId!.Value,
            })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var duplicate = executions
            .GroupBy(value => value.SubmissionId)
            .FirstOrDefault(group => group.Count() != 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Submission {duplicate.Key} has more than one canonical official grading execution.");
        }

        var roundIds = executions.Select(value => value.RoundId).Distinct().ToArray();
        var rounds = await context.Set<GradeRound>()
            .AsNoTracking()
            .Where(value => roundIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken)
            .ConfigureAwait(false);
        var stages = await context.Set<ReviewStage>()
            .AsNoTracking()
            .Where(value => roundIds.Contains(value.GradeRoundId))
            .OrderBy(value => value.Sequence)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var stagesByRound = stages
            .GroupBy(value => value.GradeRoundId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var result = new Dictionary<Guid, CanonicalReviewWorkState>(executions.Length);
        foreach (var execution in executions)
        {
            if (!rounds.TryGetValue(execution.RoundId, out var round))
            {
                throw new InvalidOperationException(
                    $"Canonical grading execution references missing active round {execution.RoundId}.");
            }

            stagesByRound.TryGetValue(round.Id, out var roundStages);
            var activeStage = roundStages?.FirstOrDefault(value =>
                                  value.Status != PersistedReviewStageStatus.Completed) ??
                              roundStages?.LastOrDefault();
            result.Add(
                execution.SubmissionId,
                new CanonicalReviewWorkState(
                    round.Id,
                    MapRoundStatus(round.Status),
                    activeStage?.Id,
                    activeStage?.ReviewMethod,
                    activeStage is null ? null : MapStageStatus(activeStage.Status),
                    round.Status == PersistedGradeRoundStatus.AwaitingInstructorResolution));
        }

        return result;
    }

    private static GradeRoundStatusV1 MapRoundStatus(PersistedGradeRoundStatus status) => status switch
    {
        PersistedGradeRoundStatus.Pending => GradeRoundStatusV1.Pending,
        PersistedGradeRoundStatus.Running => GradeRoundStatusV1.Running,
        PersistedGradeRoundStatus.AwaitingEvidence => GradeRoundStatusV1.AwaitingEvidence,
        PersistedGradeRoundStatus.AwaitingInstructorResolution => GradeRoundStatusV1.AwaitingInstructorResolution,
        PersistedGradeRoundStatus.Failed => GradeRoundStatusV1.Failed,
        PersistedGradeRoundStatus.Finalized => GradeRoundStatusV1.Finalized,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    private static ReviewStageStatusV1 MapStageStatus(PersistedReviewStageStatus status) => status switch
    {
        PersistedReviewStageStatus.Pending => ReviewStageStatusV1.Pending,
        PersistedReviewStageStatus.Running => ReviewStageStatusV1.Running,
        PersistedReviewStageStatus.AwaitingEvidence => ReviewStageStatusV1.AwaitingEvidence,
        PersistedReviewStageStatus.AwaitingInstructorResolution => ReviewStageStatusV1.AwaitingInstructorResolution,
        PersistedReviewStageStatus.Completed => ReviewStageStatusV1.Completed,
        PersistedReviewStageStatus.Failed => ReviewStageStatusV1.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
