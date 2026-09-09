using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Service implementation for the instructor grading queue (SpeedGrader navigation bundle).
/// </summary>
public class GradingQueueService : IGradingQueueService
{
    private readonly IApplicationDbContext _context;
    private readonly IRubricService _rubricService;
    private readonly ILogger<GradingQueueService> _logger;

    public GradingQueueService(
        IApplicationDbContext context,
        IRubricService rubricService,
        ILogger<GradingQueueService> logger)
    {
        _context = context;
        _rubricService = rubricService;
        _logger = logger;
    }

    public async Task<Result<GradingQueueDto>> GetQueueAsync(Guid assessmentId)
    {
        // ponytail: single load, no pagination — add paging when a course exceeds ~500 items.
        var assessment = await _context.Set<Assessment>()
            .FirstOrDefaultAsync(a => a.Id == assessmentId && a.DeletedAt == null)
            .ConfigureAwait(false);
        if (assessment == null)
        {
            return Result.Failure<GradingQueueDto>(Error.NotFound("Assessment", "Assessment not found"));
        }

        RubricDto? rubric = null;
        if (assessment.RubricId != null)
        {
            var rubricResult = await _rubricService.GetAsync(assessmentId).ConfigureAwait(false);
            if (rubricResult.IsSuccess)
            {
                rubric = rubricResult.Value;
            }
        }

        var rows = await _context.Set<AssessmentSubmission>()
            .Where(s => s.AssessmentId == assessmentId && s.DeletedAt == null)
            .ToListAsync().ConfigureAwait(false);

        // ONE item per target (student | group) representing the target's LATEST gradeable
        // attempt — one grade per assignment: older attempts are history, regrades land on a
        // fresh submission, and targets whose only rows are InProgress stay out of the queue.
        var targets = new List<(List<AssessmentSubmission> TargetRows, Guid? GroupId)>();
        foreach (var userRows in rows.Where(r => r.CourseGroupId == null)
                     .GroupBy(r => r.UserId))
        {
            targets.Add((userRows.ToList(), null));
        }

        var groupIds = rows.Where(r => r.CourseGroupId != null)
            .Select(r => r.CourseGroupId!.Value)
            .Distinct()
            .ToList();
        var groups = groupIds.Count == 0
            ? []
            : await _context.Set<CourseGroup>()
                .Where(g => groupIds.Contains(g.Id) && g.DeletedAt == null)
                .ToDictionaryAsync(g => g.Id)
                .ConfigureAwait(false);

        foreach (var groupRows in rows.Where(r => r.CourseGroupId != null)
                     .GroupBy(r => r.CourseGroupId!.Value))
        {
            targets.Add((groupRows.ToList(), groupRows.Key));
        }

        // Group members are resolved at query time (active membership), matching GroupSetService.
        var members = groupIds.Count == 0
            ? []
            : await _context.Set<CourseGroupMember>()
                .Where(m => groupIds.Contains(m.GroupId) && m.DeletedAt == null)
                .ToListAsync().ConfigureAwait(false);

        // Batched display-name lookup for every user the queue can name (GroupSetService pattern).
        var userIds = targets
            .Where(t => t.GroupId == null)
            .SelectMany(t => t.TargetRows.Select(r => r.UserId))
            .Concat(members.Select(m => m.UserId))
            .Distinct()
            .ToList();
        var namesById = (await _context.Set<User>()
                .Where(u => userIds.Contains(u.Id) && u.DeletedAt == null)
                .ToListAsync().ConfigureAwait(false))
            .ToDictionary(u => u.Id, u => u.Name);
        string DisplayName(Guid userId) =>
            namesById.TryGetValue(userId, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : userId.ToString();

        var items = new List<GradingQueueItemDto>();
        foreach (var (targetRows, groupId) in targets)
        {
            // Gradeable attempts = attempts with at least one non-InProgress row.
            var gradeableAttempts = targetRows
                .GroupBy(r => r.AttemptNumber)
                .Where(g => g.Any(r => r.Status != SubmissionStatus.InProgress))
                .ToList();
            if (gradeableAttempts.Count == 0)
            {
                continue; // InProgress-only target: absent from the queue.
            }

            var latestAttempt = gradeableAttempts.Max(g => g.Key);
            var latestRows = gradeableAttempts.Single(g => g.Key == latestAttempt)
                .Where(r => r.Status != SubmissionStatus.InProgress)
                .ToList();

            // Assignment-level grade: canonical row of the target's LATEST GRADED attempt.
            // It persists across resubmissions until a newer attempt is graded.
            var gradedRows = targetRows.Where(r => r.Status == SubmissionStatus.Graded).ToList();
            AssessmentSubmission? gradedMeta = null;
            if (gradedRows.Count > 0)
            {
                var gradedAttempt = gradedRows.Max(r => r.AttemptNumber);
                gradedMeta = PeerReviewAssignmentService.CanonicalRow(
                    gradedRows.Where(r => r.AttemptNumber == gradedAttempt));
            }

            var attemptCount = targetRows.Select(r => r.AttemptNumber).Distinct().Count();
            if (groupId == null)
            {
                var row = latestRows[0];
                items.Add(new GradingQueueItemDto(
                    row.Id,
                    row.Id,
                    latestAttempt,
                    attemptCount,
                    AggregateStatus(latestRows),
                    latestRows.Any(r => r.IsLate),
                    latestRows.Max(r => r.SubmittedAt),
                    gradedMeta?.Score,
                    gradedMeta?.Passed,
                    IsGroup: false,
                    UserId: row.UserId,
                    DisplayName: DisplayName(row.UserId)));
            }
            else
            {
                // Canonical row rule shared with peer-review assignment: Min(Id) among the rows
                // sharing (CourseGroupId, AttemptNumber) — clones share timestamps, so Id is the
                // only deterministic tiebreak.
                var canonical = PeerReviewAssignmentService.CanonicalRow(latestRows);
                var group = groups.GetValueOrDefault(groupId.Value);
                items.Add(new GradingQueueItemDto(
                    canonical.Id,
                    canonical.Id,
                    latestAttempt,
                    attemptCount,
                    AggregateStatus(latestRows),
                    latestRows.Any(r => r.IsLate),
                    latestRows.Max(r => r.SubmittedAt),
                    gradedMeta?.Score,
                    gradedMeta?.Passed,
                    IsGroup: true,
                    GroupId: groupId,
                    GroupName: group?.Name ?? groupId.Value.ToString(),
                    MemberNames: members
                        .Where(m => m.GroupId == groupId)
                        .Select(m => m.UserId)
                        .Distinct()
                        .Select(DisplayName)
                        .ToList()));
            }
        }

        // DisplayName ASC: nav walks students/groups — attempts are no longer nav items,
        // each item carries its target's latest attempt and assignment-level grade.
        var sorted = items
            .OrderBy(i => i.DisplayName ?? i.GroupName, StringComparer.Ordinal)
            .ToList();

        return Result.Success(new GradingQueueDto(
            new GradingQueueAssessmentDto(
                assessment.Id,
                assessment.Title,
                assessment.Type,
                assessment.MaxScore,
                assessment.ReviewMethods,
                assessment.GroupSetId,
                assessment.RubricId != null,
                rubric),
            sorted,
            Total: sorted.Count,
            NeedsGrading: sorted.Count(i =>
                i.Status is SubmissionStatus.Submitted or SubmissionStatus.Late)));
    }

    /// <summary>
    /// Status precedence (deterministic, worst-pending-first): the latest attempt shows its
    /// least-advanced row status — Submitted &lt; Late &lt; Graded — so a group attempt with
    /// any ungraded row stays pending in the queue.
    /// </summary>
    private static SubmissionStatus AggregateStatus(IReadOnlyList<AssessmentSubmission> gradeable) =>
        gradeable.Any(r => r.Status == SubmissionStatus.Submitted) ? SubmissionStatus.Submitted
        : gradeable.Any(r => r.Status == SubmissionStatus.Late) ? SubmissionStatus.Late
        : SubmissionStatus.Graded;
}
