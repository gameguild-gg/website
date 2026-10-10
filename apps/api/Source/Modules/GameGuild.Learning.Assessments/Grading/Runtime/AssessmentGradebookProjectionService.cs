using System.Numerics;
using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Learning.Assessments.Grading.Runtime;

public sealed record GradebookAssessmentProjectionV1(
    Guid AssessmentId,
    Guid SubmissionId,
    Guid GradeRoundId,
    ScoreValue EffectiveScore,
    ScoreValue CapturedMaxScore,
    bool Released);

public sealed record GradebookGroupProjectionV1(
    Guid AssessmentGroupId,
    PercentValue WeightPercent,
    PercentValue GroupRatio,
    PercentValue ContributionPercent,
    IReadOnlyList<GradebookAssessmentProjectionV1> Assessments);

public sealed record GradebookCourseProjectionV1(
    Guid CourseId,
    Guid EnrollmentId,
    bool LearnerVisible,
    bool HasWithheldContribution,
    int? CoursePercentUnits,
    IReadOnlyList<GradebookGroupProjectionV1> Groups);

public interface IAssessmentGradebookProjectionService
{
    Task ProjectFinalizedResultAsync(
        AssessmentSubmission submission,
        Assessment assessment,
        GradeRound round,
        GradeResultV1 result,
        IReadOnlyList<Guid> enrollmentIds,
        CancellationToken cancellationToken = default);

    Task ReprojectAssessmentPlacementAsync(
        Guid assessmentId,
        Guid actorId,
        Guid? previousGroupId,
        PercentValue? previousWeight,
        CancellationToken cancellationToken = default);

    Task ReprojectGroupWeightAsync(
        Guid assessmentGroupId,
        Guid actorId,
        PercentValue previousWeight,
        CancellationToken cancellationToken = default);

    Task RemoveAssessmentGroupPlacementAsync(
        Guid assessmentGroupId,
        Guid actorId,
        PercentValue previousWeight,
        CancellationToken cancellationToken = default);

    Task<GradebookCourseProjectionV1> GetCourseProjectionAsync(
        Guid courseId,
        Guid enrollmentId,
        bool learnerView,
        CancellationToken cancellationToken = default);
}

public sealed class AssessmentGradebookProjectionService(
    IApplicationDbContext context,
    IAcademicOutboxWriter outbox) : IAssessmentGradebookProjectionService
{
    public async Task ProjectFinalizedResultAsync(
        AssessmentSubmission submission,
        Assessment assessment,
        GradeRound round,
        GradeResultV1 result,
        IReadOnlyList<Guid> enrollmentIds,
        CancellationToken cancellationToken = default)
    {
        if (!result.Score.HasValue)
        {
            throw new InvalidOperationException("A finalized result requires a score before gradebook projection.");
        }

        if (!assessment.AssessmentGroupId.HasValue)
        {
            return;
        }

        var group = await context.Set<AssessmentGroup>()
            .SingleAsync(value => value.Id == assessment.AssessmentGroupId.Value && value.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        var existing = await context.Set<AssessmentGradebookEntry>()
            .Where(value => value.AssessmentId == assessment.Id && enrollmentIds.Contains(value.EnrollmentId))
            .ToDictionaryAsync(value => value.EnrollmentId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var enrollmentId in enrollmentIds.Distinct())
        {
            if (existing.TryGetValue(enrollmentId, out var entry))
            {
                entry.Reproject(group.Id, submission.Id, round.Id, result.Score.Value, result.MaxScore, group.WeightPercent);
            }
            else
            {
                context.Set<AssessmentGradebookEntry>().Add(AssessmentGradebookEntry.Create(
                    submission.TenantId,
                    assessment.CourseId,
                    enrollmentId,
                    assessment.Id,
                    group.Id,
                    submission.Id,
                    round.Id,
                    result.Score.Value,
                    result.MaxScore,
                    group.WeightPercent));
            }
        }
    }

    public async Task ReprojectAssessmentPlacementAsync(
        Guid assessmentId,
        Guid actorId,
        Guid? previousGroupId,
        PercentValue? previousWeight,
        CancellationToken cancellationToken = default)
    {
        RequireActor(actorId);
        var assessment = await context.Set<Assessment>()
            .SingleAsync(value => value.Id == assessmentId && value.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        var nextGroup = assessment.AssessmentGroupId.HasValue
            ? await context.Set<AssessmentGroup>()
                .SingleAsync(value => value.Id == assessment.AssessmentGroupId.Value && value.DeletedAt == null, cancellationToken)
                .ConfigureAwait(false)
            : null;
        var entries = await context.Set<AssessmentGradebookEntry>()
            .Where(value => value.AssessmentId == assessmentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (nextGroup is null)
        {
            context.Set<AssessmentGradebookEntry>().RemoveRange(entries);
        }
        else
        {
            foreach (var entry in entries)
            {
                entry.Reproject(
                    nextGroup.Id,
                    entry.SubmissionId,
                    entry.GradeRoundId,
                    entry.EffectiveScore,
                    entry.CapturedMaxScore,
                    nextGroup.WeightPercent);
            }

            await AddMissingEntriesAsync(assessment, nextGroup, entries, cancellationToken).ConfigureAwait(false);
        }

        EnqueuePlacementChanged(
            assessment,
            actorId,
            previousGroupId,
            previousWeight,
            nextGroup?.Id,
            nextGroup?.WeightPercent);
    }

    public async Task ReprojectGroupWeightAsync(
        Guid assessmentGroupId,
        Guid actorId,
        PercentValue previousWeight,
        CancellationToken cancellationToken = default)
    {
        RequireActor(actorId);
        var group = await context.Set<AssessmentGroup>()
            .SingleAsync(value => value.Id == assessmentGroupId && value.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        var entries = await context.Set<AssessmentGradebookEntry>()
            .Where(value => value.AssessmentGroupId == assessmentGroupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var entry in entries)
        {
            entry.Reproject(
                group.Id,
                entry.SubmissionId,
                entry.GradeRoundId,
                entry.EffectiveScore,
                entry.CapturedMaxScore,
                group.WeightPercent);
        }

        EnqueueGroupWeightChanged(group, actorId, previousWeight, group.WeightPercent);
    }

    public async Task RemoveAssessmentGroupPlacementAsync(
        Guid assessmentGroupId,
        Guid actorId,
        PercentValue previousWeight,
        CancellationToken cancellationToken = default)
    {
        RequireActor(actorId);
        var group = await context.Set<AssessmentGroup>()
            .SingleAsync(value => value.Id == assessmentGroupId, cancellationToken)
            .ConfigureAwait(false);
        var entries = await context.Set<AssessmentGradebookEntry>()
            .Where(value => value.AssessmentGroupId == assessmentGroupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        context.Set<AssessmentGradebookEntry>().RemoveRange(entries);
        EnqueueGroupWeightChanged(group, actorId, previousWeight, null);
    }

    public async Task<GradebookCourseProjectionV1> GetCourseProjectionAsync(
        Guid courseId,
        Guid enrollmentId,
        bool learnerView,
        CancellationToken cancellationToken = default)
    {
        var entries = await context.Set<AssessmentGradebookEntry>()
            .AsNoTracking()
            .Where(value => value.CourseId == courseId && value.EnrollmentId == enrollmentId)
            .OrderBy(value => value.AssessmentGroupId)
            .ThenBy(value => value.AssessmentId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var currentReleasedRoundIds = entries.Length == 0
            ? new HashSet<Guid>()
            : (await context.Set<GradeResultRelease>()
                .AsNoTracking()
                .Where(value => entries.Select(entry => entry.GradeRoundId).Contains(value.GradeRoundId))
                .Select(value => value.GradeRoundId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false)).ToHashSet();
        var withheld = entries.Any(entry =>
            entry.CapturedWeightPercent is { } weight &&
            weight.CompareTo(PercentValue.Zero) > 0 &&
            !currentReleasedRoundIds.Contains(entry.GradeRoundId));
        var projectedEntries = entries
            .Select(entry => GradebookEntrySnapshot.From(entry, currentReleasedRoundIds.Contains(entry.GradeRoundId)))
            .ToArray();
        var learnerHasUnavailableContribution = false;
        if (learnerView)
        {
            (projectedEntries, learnerHasUnavailableContribution) = await ResolveLearnerEntriesAsync(
                    entries,
                    currentReleasedRoundIds,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (learnerView && learnerHasUnavailableContribution)
        {
            return new GradebookCourseProjectionV1(courseId, enrollmentId, false, withheld, null, []);
        }

        var groups = projectedEntries
            .Where(entry => entry.AssessmentGroupId.HasValue && entry.CapturedWeightPercent.HasValue)
            .GroupBy(entry => new { Id = entry.AssessmentGroupId!.Value, Weight = entry.CapturedWeightPercent!.Value })
            .OrderBy(group => group.Key.Id)
            .Select(group => BuildGroupProjection(group.Key.Id, group.Key.Weight, group.ToArray()))
            .ToArray();
        var coursePercentUnits = groups.Aggregate(0, (sum, group) => checked(sum + group.ContributionPercent.Units));
        return new GradebookCourseProjectionV1(
            courseId,
            enrollmentId,
            true,
            withheld,
            coursePercentUnits,
            groups);
    }

    private async Task<(GradebookEntrySnapshot[] Entries, bool HasUnavailableContribution)> ResolveLearnerEntriesAsync(
        IReadOnlyList<AssessmentGradebookEntry> entries,
        IReadOnlySet<Guid> currentReleasedRoundIds,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return ([], false);
        }

        var submissionIds = entries.Select(value => value.SubmissionId).Distinct().ToArray();
        var executionOwners = await context.Set<GradingExecution>()
            .AsNoTracking()
            .Where(value =>
                value.ExecutionContext == ReviewExecutionContext.OfficialSubmission &&
                value.AssessmentSubmissionId.HasValue &&
                submissionIds.Contains(value.AssessmentSubmissionId.Value))
            .Select(value => new { SubmissionId = value.AssessmentSubmissionId!.Value, ExecutionId = value.Id })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var executionBySubmission = executionOwners.ToDictionary(value => value.SubmissionId, value => value.ExecutionId);
        var executionIds = executionOwners.Select(value => value.ExecutionId).ToArray();
        var releasedRoundIds = executionIds.Length == 0
            ? new HashSet<Guid>()
            : (await context.Set<GradeResultRelease>()
                .AsNoTracking()
                .Where(value => executionIds.Contains(value.GradingExecutionId))
                .Select(value => value.GradeRoundId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false)).ToHashSet();
        var releasedRounds = executionIds.Length == 0
            ? []
            : await context.Set<GradeRound>()
                .AsNoTracking()
                .Where(value =>
                    executionIds.Contains(value.GradingExecutionId) &&
                    value.Status == PersistedGradeRoundStatus.Finalized &&
                    value.Score != null)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        var latestReleasedByExecution = releasedRounds
            .Where(value => releasedRoundIds.Contains(value.Id))
            .GroupBy(value => value.GradingExecutionId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(value => value.RoundNumber).First());

        var visible = new List<GradebookEntrySnapshot>(entries.Count);
        var hasUnavailableContribution = false;
        foreach (var entry in entries)
        {
            if (currentReleasedRoundIds.Contains(entry.GradeRoundId))
            {
                visible.Add(GradebookEntrySnapshot.From(entry, true));
                continue;
            }

            if (executionBySubmission.TryGetValue(entry.SubmissionId, out var executionId) &&
                latestReleasedByExecution.TryGetValue(executionId, out var previousRound))
            {
                visible.Add(GradebookEntrySnapshot.FromReleasedRound(entry, previousRound));
                continue;
            }

            if (entry.CapturedWeightPercent is { } weight && weight.CompareTo(PercentValue.Zero) > 0)
            {
                hasUnavailableContribution = true;
            }
        }

        return (visible.ToArray(), hasUnavailableContribution);
    }

    private async Task AddMissingEntriesAsync(
        Assessment assessment,
        AssessmentGroup group,
        IReadOnlyCollection<AssessmentGradebookEntry> existing,
        CancellationToken cancellationToken)
    {
        var existingEnrollments = existing.Select(value => value.EnrollmentId).ToHashSet();
        var finalized = await (
                from submission in context.Set<AssessmentSubmission>()
                join execution in context.Set<GradingExecution>()
                    on submission.Id equals execution.AssessmentSubmissionId
                join round in context.Set<GradeRound>()
                    on execution.ActiveGradeRoundId equals (Guid?)round.Id
                where submission.AssessmentId == assessment.Id &&
                      execution.ExecutionContext == ReviewExecutionContext.OfficialSubmission &&
                      round.Status == PersistedGradeRoundStatus.Finalized &&
                      round.Score != null
                select new { Submission = submission, Round = round })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var candidate in finalized.OrderBy(value => value.Submission.AttemptNumber))
        {
            var enrollmentIds = candidate.Submission.EnrollmentId.HasValue
                ? [candidate.Submission.EnrollmentId.Value]
                : await context.Set<AssessmentSubmissionParticipant>()
                    .AsNoTracking()
                    .Where(value => value.SubmissionId == candidate.Submission.Id)
                    .Select(value => value.EnrollmentId)
                    .ToArrayAsync(cancellationToken)
                    .ConfigureAwait(false);
            foreach (var enrollmentId in enrollmentIds)
            {
                if (!existingEnrollments.Add(enrollmentId))
                {
                    continue;
                }

                context.Set<AssessmentGradebookEntry>().Add(AssessmentGradebookEntry.Create(
                    candidate.Submission.TenantId,
                    assessment.CourseId,
                    enrollmentId,
                    assessment.Id,
                    group.Id,
                    candidate.Submission.Id,
                    candidate.Round.Id,
                    candidate.Round.Score!.Value,
                    candidate.Round.MaxScore,
                    group.WeightPercent));
            }
        }
    }

    private static GradebookGroupProjectionV1 BuildGroupProjection(
        Guid groupId,
        PercentValue weight,
        IReadOnlyList<GradebookEntrySnapshot> entries)
    {
        var earned = entries.Aggregate(BigInteger.Zero, (sum, entry) => sum + entry.EffectiveScore.Units);
        var possible = entries.Aggregate(BigInteger.Zero, (sum, entry) => sum + entry.CapturedMaxScore.Units);
        var ratio = PercentValue.FromRatio(earned, possible);
        var contributionUnits = DivideRoundHalfUp(
            earned * weight.Units,
            possible);
        var contribution = PercentValue.FromUnits((int)contributionUnits);
        return new GradebookGroupProjectionV1(
            groupId,
            weight,
            ratio,
            contribution,
            entries.Select(entry => new GradebookAssessmentProjectionV1(
                    entry.AssessmentId,
                    entry.SubmissionId,
                    entry.GradeRoundId,
                    entry.EffectiveScore,
                    entry.CapturedMaxScore,
                    entry.Released))
                .ToArray());
    }

    private sealed record GradebookEntrySnapshot(
        Guid AssessmentId,
        Guid SubmissionId,
        Guid GradeRoundId,
        Guid? AssessmentGroupId,
        ScoreValue EffectiveScore,
        ScoreValue CapturedMaxScore,
        PercentValue? CapturedWeightPercent,
        bool Released)
    {
        public static GradebookEntrySnapshot From(AssessmentGradebookEntry entry, bool released) =>
            new(
                entry.AssessmentId,
                entry.SubmissionId,
                entry.GradeRoundId,
                entry.AssessmentGroupId,
                entry.EffectiveScore,
                entry.CapturedMaxScore,
                entry.CapturedWeightPercent,
                released);

        public static GradebookEntrySnapshot FromReleasedRound(AssessmentGradebookEntry entry, GradeRound round) =>
            new(
                entry.AssessmentId,
                entry.SubmissionId,
                round.Id,
                entry.AssessmentGroupId,
                round.Score!.Value,
                round.MaxScore,
                entry.CapturedWeightPercent,
                true);
    }

    private void EnqueuePlacementChanged(
        Assessment assessment,
        Guid actorId,
        Guid? previousGroupId,
        PercentValue? previousWeight,
        Guid? nextGroupId,
        PercentValue? nextWeight)
    {
        if (!assessment.TenantId.HasValue)
        {
            return;
        }

        Enqueue(assessment.TenantId.Value, new
        {
            schemaVersion = 1,
            assessmentId = assessment.Id,
            courseId = assessment.CourseId,
            actorId,
            before = new { assessmentGroupId = previousGroupId, weightPercent = previousWeight },
            after = new { assessmentGroupId = nextGroupId, weightPercent = nextWeight },
        });
    }

    private void EnqueueGroupWeightChanged(
        AssessmentGroup group,
        Guid actorId,
        PercentValue previousWeight,
        PercentValue? nextWeight)
    {
        if (!group.TenantId.HasValue)
        {
            return;
        }

        Enqueue(group.TenantId.Value, new
        {
            schemaVersion = 1,
            assessmentGroupId = group.Id,
            courseId = group.CourseId,
            actorId,
            before = new { weightPercent = previousWeight },
            after = new { weightPercent = nextWeight },
        });
    }

    private void Enqueue(Guid tenantId, object payload)
    {
        var element = JsonSerializer.SerializeToElement(payload, GradingJson.Options);
        outbox.Enqueue(tenantId, "gradebook-placement-reprojected", "1", CanonicalJson.Serialize(element));
    }

    private static BigInteger DivideRoundHalfUp(BigInteger numerator, BigInteger denominator) =>
        (numerator + denominator / 2) / denominator;

    private static void RequireActor(Guid actorId)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Actor ID is required.", nameof(actorId));
        }
    }
}
