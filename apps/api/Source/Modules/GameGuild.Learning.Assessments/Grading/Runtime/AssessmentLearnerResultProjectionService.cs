using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Learning.Assessments.Grading.Runtime;

public sealed record LearnerReleasedAssessmentResultV1(
    Guid SubmissionId,
    Guid GradeRoundId,
    ScoreValue Score,
    ScoreValue MaxScore,
    bool Passed,
    string? Feedback,
    DateTime FinalizedAt,
    DateTime ReleasedAt);

public interface IAssessmentLearnerResultProjectionService
{
    Task<IReadOnlyDictionary<Guid, LearnerReleasedAssessmentResultV1>> GetLatestReleasedAsync(
        IEnumerable<Guid> submissionIds,
        CancellationToken cancellationToken = default);
}

public sealed class AssessmentLearnerResultProjectionService(IApplicationDbContext context)
    : IAssessmentLearnerResultProjectionService
{
    public async Task<IReadOnlyDictionary<Guid, LearnerReleasedAssessmentResultV1>> GetLatestReleasedAsync(
        IEnumerable<Guid> submissionIds,
        CancellationToken cancellationToken = default)
    {
        var ids = submissionIds.Where(value => value != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, LearnerReleasedAssessmentResultV1>();
        }

        var executions = await context.Set<GradingExecution>()
            .AsNoTracking()
            .Where(value =>
                value.ExecutionContext == ReviewExecutionContext.OfficialSubmission &&
                value.AssessmentSubmissionId.HasValue &&
                ids.Contains(value.AssessmentSubmissionId.Value))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (executions.Length == 0)
        {
            return new Dictionary<Guid, LearnerReleasedAssessmentResultV1>();
        }

        var executionIds = executions.Select(value => value.Id).ToArray();
        var releases = await context.Set<GradeResultRelease>()
            .AsNoTracking()
            .Where(value => executionIds.Contains(value.GradingExecutionId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (releases.Length == 0)
        {
            return new Dictionary<Guid, LearnerReleasedAssessmentResultV1>();
        }

        var releasedRoundIds = releases.Select(value => value.GradeRoundId).ToArray();
        var rounds = await context.Set<GradeRound>()
            .AsNoTracking()
            .Where(value =>
                releasedRoundIds.Contains(value.Id) &&
                value.Status == PersistedGradeRoundStatus.Finalized &&
                value.Score != null &&
                value.FinalizedAt != null)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var revisionIds = executions.Select(value => value.DefinitionRevisionId).Distinct().ToArray();
        var revisions = await context.Set<AssessmentDefinitionRevision>()
            .AsNoTracking()
            .Where(value => revisionIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken)
            .ConfigureAwait(false);
        var releaseByRound = releases.ToDictionary(value => value.GradeRoundId);
        var executionById = executions.ToDictionary(value => value.Id);
        var result = new Dictionary<Guid, LearnerReleasedAssessmentResultV1>();
        foreach (var round in rounds
                     .GroupBy(value => value.GradingExecutionId)
                     .Select(group => group.OrderByDescending(value => value.RoundNumber).First()))
        {
            var execution = executionById[round.GradingExecutionId];
            var submissionId = execution.AssessmentSubmissionId!.Value;
            var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revisions[execution.DefinitionRevisionId]);
            var passingScore = snapshot.AuthoringSource.Policy.PassingScore ?? ScoreValue.Zero;
            result[submissionId] = new LearnerReleasedAssessmentResultV1(
                submissionId,
                round.Id,
                round.Score!.Value,
                round.MaxScore,
                round.Score.Value.CompareTo(passingScore) >= 0,
                round.Feedback,
                round.FinalizedAt!.Value,
                releaseByRound[round.Id].ReleasedAt);
        }

        return result;
    }
}
