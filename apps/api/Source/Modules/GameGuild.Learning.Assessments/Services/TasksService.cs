using System.Text.RegularExpressions;
using GameGuild.Identity.Authorization;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Enrollments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Cross-course task aggregation: grade items for managed courses, do/review items for active enrollments.
/// All counts are computed live per request from submission/review rows.
/// </summary>
public class TasksService(
    IApplicationDbContext context,
    IPermissionQueryService permissionQueryService,
    ILogger<TasksService> logger) : ITasksService
{
    // Managed-course permission names mirror AssessmentsController.CanManageCourseAsync exactly:
    // Program.{courseId}.{Edit|Create|Delete} in the actor's tenant, plus program creators and system admins.
    private static readonly Regex ProgramPermissionPattern = new(
        @"^Program\.(?<courseId>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\.(Edit|Create|Delete)$",
        RegexOptions.Compiled);

    public async Task<TasksDto> GetTasksAsync(Guid actorUserId, Guid? tenantId, bool isSystemAdmin)
    {
        var managedCourses = await GetManagedCoursesAsync(actorUserId, tenantId, isSystemAdmin).ConfigureAwait(false);

        var enrollments = await context.Set<Enrollment>()
            .Where(e => e.UserId == actorUserId &&
                        e.Status == GameGuild.Learning.Enrollments.EnrollmentStatus.Active &&
                        e.DeletedAt == null)
            .ToListAsync().ConfigureAwait(false);
        var enrolledCourseIds = enrollments.Select(e => e.CourseId).Distinct().ToList();

        var courseTitles = managedCourses.ToDictionary(kv => kv.Key, kv => kv.Value);
        var missingTitleIds = enrolledCourseIds.Where(id => !courseTitles.ContainsKey(id)).ToList();
        if (missingTitleIds.Count > 0)
        {
            var enrolledTitles = await context.Set<Program>()
                .Where(p => missingTitleIds.Contains(p.Id) && p.DeletedAt == null)
                .Select(p => new { p.Id, p.Title })
                .ToListAsync().ConfigureAwait(false);
            foreach (var program in enrolledTitles)
            {
                courseTitles[program.Id] = program.Title;
            }
        }

        var courseIds = managedCourses.Keys.Concat(enrolledCourseIds).Distinct().ToList();
        var assessments = courseIds.Count == 0
            ? []
            : await context.Set<Assessment>()
                .Where(a => courseIds.Contains(a.CourseId) && a.DeletedAt == null)
                .ToListAsync().ConfigureAwait(false);

        var assessmentIds = assessments.Select(a => a.Id).ToList();
        var submissions = assessmentIds.Count == 0
            ? []
            : await context.Set<AssessmentSubmission>()
                .Where(s => assessmentIds.Contains(s.AssessmentId) && s.DeletedAt == null)
                .ToListAsync().ConfigureAwait(false);

        var actorReviews = assessmentIds.Count == 0
            ? []
            : await context.Set<AssessmentPeerReview>()
                .Where(r => r.ReviewerUserId == actorUserId &&
                            r.DeletedAt == null &&
                            assessmentIds.Contains(r.AssessmentId))
                .ToListAsync().ConfigureAwait(false);

        var items = new List<TaskItemDto>();

        foreach (var assessment in assessments.Where(a => managedCourses.ContainsKey(a.CourseId)))
        {
            var pending = CountPendingGradeTargets(submissions.Where(s => s.AssessmentId == assessment.Id));
            if (pending > 0)
            {
                items.Add(new TaskItemDto(
                    "grade",
                    assessment.CourseId,
                    managedCourses[assessment.CourseId],
                    assessment.Id,
                    assessment.Title,
                    assessment.DueAt,
                    CountSubmitted: pending));
            }
        }

        var seenAssessmentIds = new HashSet<Guid>();
        foreach (var enrollment in enrollments)
        {
            foreach (var assessment in assessments.Where(a => a.CourseId == enrollment.CourseId))
            {
                if (!seenAssessmentIds.Add(assessment.Id))
                {
                    continue;
                }

                var own = submissions.Where(s => s.AssessmentId == assessment.Id && s.EnrollmentId == enrollment.Id).ToList();
                var latest = own.OrderByDescending(r => r.AttemptNumber).FirstOrDefault();
                var canContinueCurrentAttempt = latest?.Status == SubmissionStatus.InProgress;
                var canStartAttempt = latest is null && own.Count < assessment.MaxAttempts;
                if (assessment.IsAvailable() && (canContinueCurrentAttempt || canStartAttempt))
                {
                    items.Add(new TaskItemDto(
                        "do",
                        assessment.CourseId,
                        courseTitles.GetValueOrDefault(assessment.CourseId) ?? assessment.CourseId.ToString(),
                        assessment.Id,
                        assessment.Title,
                        assessment.DueAt));
                }

                var hasPeerReview = assessment.ReviewMethods.HasFlag(ReviewMethods.PeerReview);
                var reviewsRequired = assessment.GetRequiredPeerReviewCount();
                var reviews = actorReviews.Where(r => r.AssessmentId == assessment.Id).ToList();
                if (hasPeerReview &&
                    reviewsRequired > 0 &&
                    reviews.Count < reviewsRequired)
                {
                    items.Add(new TaskItemDto(
                        "review",
                        assessment.CourseId,
                        courseTitles.GetValueOrDefault(assessment.CourseId) ?? assessment.CourseId.ToString(),
                        assessment.Id,
                        assessment.Title,
                        // Reviews run to the assessment close — same asymmetry as the todo-8 read endpoints.
                        assessment.DueAt ?? assessment.AvailableUntil ?? assessment.LateSubmissionDeadline,
                        ReviewsCompleted: reviews.Count(r => r.Status == PeerReviewStatus.Submitted),
                        ReviewsRequired: reviewsRequired));
                }
            }
        }

        logger.LogDebug("Aggregated {Count} tasks for actor {ActorUserId}", items.Count, actorUserId);
        return new TasksDto(items);
    }

    /// <summary>
    /// Distinct targets awaiting a grade under the one-grade-per-assignment model: a target
    /// (user, or group collapsed to one) counts when its LATEST gradeable attempt has a
    /// Submitted/Late row — the same semantics as the grading queue's NeedsGrading. InProgress
    /// rows never count, and a stale ungraded attempt under a newer graded one does not either.
    /// Shared with the submit-notification hook (AssessmentService).
    /// </summary>
    internal static int CountPendingGradeTargets(IEnumerable<AssessmentSubmission> rows) =>
        rows.GroupBy(r => r.CourseGroupId.HasValue
                ? ("group", r.CourseGroupId.Value)
                : ("user", r.UserId))
            .Count(targetRows =>
            {
                var gradeableAttempts = targetRows
                    .GroupBy(r => r.AttemptNumber)
                    .Where(g => g.Any(r => r.Status != SubmissionStatus.InProgress))
                    .ToList();
                return gradeableAttempts.Count > 0 &&
                       gradeableAttempts.Single(g => g.Key == gradeableAttempts.Max(a => a.Key))
                           .Any(r => r.Status is SubmissionStatus.Submitted or SubmissionStatus.Late);
            });

    private async Task<Dictionary<Guid, string>> GetManagedCoursesAsync(Guid actorUserId, Guid? tenantId, bool isSystemAdmin)
    {
        var programs = await context.Set<Program>()
            .Where(p => p.DeletedAt == null)
            .ToListAsync().ConfigureAwait(false);

        // Tenant compatibility mirrors CanManageCourseAsync: system admins manage everything;
        // everyone else needs a tenant, and a program's tenant must be null or match the actor's.
        // Compatibility is a GUARD, not a grant — only creator or explicit permission manages.
        var managed = new Dictionary<Guid, string>();
        if (isSystemAdmin)
        {
            foreach (var program in programs)
            {
                managed.TryAdd(program.Id, program.Title);
            }
        }
        else if (tenantId.HasValue)
        {
            foreach (var program in programs.Where(p => p.CreatorId == actorUserId))
            {
                managed.TryAdd(program.Id, program.Title);
            }
        }

        if (!isSystemAdmin && tenantId.HasValue)
        {
            var permissions = await permissionQueryService
                .GetEffectivePermissionsAsync(actorUserId, tenantId)
                .ConfigureAwait(false);
            var permissionCourseIds = permissions
                .Select(p => ProgramPermissionPattern.Match(p))
                .Where(m => m.Success && Guid.TryParse(m.Groups["courseId"].Value, out _))
                .Select(m => Guid.Parse(m.Groups["courseId"].Value))
                .ToHashSet();
            foreach (var program in programs.Where(p => permissionCourseIds.Contains(p.Id)))
            {
                managed.TryAdd(program.Id, program.Title);
            }
        }

        return managed;
    }
}
