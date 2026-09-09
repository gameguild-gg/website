using GameGuild.CQRS;
using GameGuild.Learning.Assessments;
using GameGuild.Learning.Certificates;
using GameGuild.Learning.Cohorts;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Experience.Social;
using Microsoft.EntityFrameworkCore;
using LearningEnrollment = GameGuild.Learning.Enrollments.Enrollment;
using Program = GameGuild.Learning.Courses.Program;

namespace GameGuild.Learning.Workspaces;

public sealed record GetLearnerDashboardQuery(Guid UserId) : IQuery<LearnerDashboardDto>;

public sealed class GetLearnerDashboardQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetLearnerDashboardQuery, LearnerDashboardDto>
{
    public async Task<LearnerDashboardDto> Handle(
        GetLearnerDashboardQuery request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            throw new ArgumentException("An authenticated user is required.", nameof(request));
        }

        var enrollments = await context.Set<ProgramEnrollment>()
            .AsNoTracking()
            .Where(enrollment =>
                enrollment.UserId == request.UserId &&
                enrollment.DeletedAt == null &&
                enrollment.EnrollmentStatus != GameGuild.Learning.Courses.EnrollmentStatus.Cancelled &&
                enrollment.EnrollmentStatus != GameGuild.Learning.Courses.EnrollmentStatus.Expired)
            .OrderByDescending(enrollment => enrollment.EnrolledAt)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        if (enrollments.Length == 0)
        {
            return EmptyDashboard();
        }

        var courseIds = enrollments.Select(enrollment => enrollment.ProgramId).Distinct().ToArray();
        var enrollmentIds = enrollments.Select(enrollment => enrollment.Id).ToArray();
        var courses = await context.Set<Program>()
            .AsNoTracking()
            .Where(course => courseIds.Contains(course.Id) && course.DeletedAt == null)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var content = await context.Set<ProgramContent>()
            .AsNoTracking()
            .Where(item => courseIds.Contains(item.ProgramId) && item.DeletedAt == null)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var progress = await context.Set<ContentProgress>()
            .AsNoTracking()
            .Where(item =>
                item.UserId == request.UserId &&
                enrollmentIds.Contains(item.ProgramEnrollmentId) &&
                item.DeletedAt == null)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var learningEnrollments = await context.Set<LearningEnrollment>()
            .AsNoTracking()
            .Where(enrollment =>
                enrollment.UserId == request.UserId &&
                courseIds.Contains(enrollment.CourseId) &&
                enrollment.DeletedAt == null)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var cohortIds = learningEnrollments
            .Where(enrollment => enrollment.CohortId.HasValue)
            .Select(enrollment => enrollment.CohortId!.Value)
            .Distinct()
            .ToArray();
        var cohorts = cohortIds.Length == 0
            ? []
            : await context.Set<Cohort>()
                .AsNoTracking()
                .Where(cohort => cohortIds.Contains(cohort.Id) && cohort.DeletedAt == null)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        var schedule = cohortIds.Length == 0
            ? []
            : await context.Set<CohortScheduleItem>()
                .AsNoTracking()
                .Where(item => cohortIds.Contains(item.CohortId) && item.DeletedAt == null)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        var assessments = await context.Set<Assessment>()
            .AsNoTracking()
            .Where(assessment => courseIds.Contains(assessment.CourseId) && assessment.DeletedAt == null)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var assessmentGroups = await context.Set<AssessmentGroup>()
            .AsNoTracking()
            .Where(group => courseIds.Contains(group.CourseId) && group.DeletedAt == null)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var assessmentIds = assessments.Select(assessment => assessment.Id).ToArray();
        var submissions = assessmentIds.Length == 0
            ? []
            : await context.Set<AssessmentSubmission>()
                .AsNoTracking()
                .Where(submission =>
                    submission.UserId == request.UserId &&
                    assessmentIds.Contains(submission.AssessmentId) &&
                    submission.DeletedAt == null)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        var certificates = await context.Set<Certificate>()
            .AsNoTracking()
            .Where(certificate =>
                certificate.UserId == request.UserId &&
                courseIds.Contains(certificate.CourseId) &&
                certificate.DeletedAt == null)
            .OrderByDescending(certificate => certificate.IssuedAt)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var announcements = await context.Set<CourseDiscussion>()
            .AsNoTracking()
            .Where(discussion =>
                courseIds.Contains(discussion.CourseId) &&
                discussion.IsPinned &&
                discussion.DeletedAt == null)
            .OrderByDescending(discussion => discussion.LastActivityAt ?? discussion.CreatedAt)
            .Take(12)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var courseById = courses.ToDictionary(course => course.Id);
        var contentByCourse = content.ToLookup(item => item.ProgramId);
        var progressByEnrollment = progress.ToLookup(item => item.ProgramEnrollmentId);
        var courseSummaries = enrollments
            .Where(enrollment => courseById.ContainsKey(enrollment.ProgramId))
            .Select(enrollment => LearnerWorkspaceMapper.MapCourse(
                courseById[enrollment.ProgramId],
                enrollment,
                contentByCourse[enrollment.ProgramId].ToArray(),
                progressByEnrollment[enrollment.Id].ToArray()))
            .ToArray();
        var cohortsById = cohorts.ToDictionary(cohort => cohort.Id);
        var courseByCohortId = learningEnrollments
            .Where(enrollment => enrollment.CohortId.HasValue)
            .GroupBy(enrollment => enrollment.CohortId!.Value)
            .ToDictionary(group => group.Key, group => group.First().CourseId);
        var now = DateTime.UtcNow;
        var upcoming = schedule
            .Where(item =>
                cohortsById.ContainsKey(item.CohortId) &&
                courseByCohortId.TryGetValue(item.CohortId, out var courseId) &&
                courseById.ContainsKey(courseId) &&
                NextDate(item) >= now)
            .OrderBy(NextDate)
            .Take(12)
            .Select(item =>
            {
                var courseId = courseByCohortId[item.CohortId];
                return LearnerWorkspaceMapper.MapSchedule(item, cohortsById[item.CohortId], courseById[courseId]);
            })
            .ToArray();
        var latestSubmissionByAssessment = submissions
            .GroupBy(submission => submission.AssessmentId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.AttemptNumber).First());
        var deadlines = assessments
            .Where(assessment =>
                courseById.ContainsKey(assessment.CourseId) &&
                assessment.DueAt.HasValue &&
                assessment.DueAt.Value >= now)
            .OrderBy(assessment => assessment.DueAt)
            .Take(12)
            .Select(assessment => new LearnerAssessmentDeadlineDto(
                assessment.Id,
                assessment.CourseId,
                courseById[assessment.CourseId].Title,
                courseById[assessment.CourseId].Slug ?? assessment.CourseId.ToString(),
                assessment.ContentId,
                assessment.AssessmentGroupId,
                assessment.Title,
                assessment.Type.ToString(),
                assessment.MaxScore,
                assessment.AvailableFrom,
                assessment.AvailableUntil,
                assessment.DueAt,
                latestSubmissionByAssessment.TryGetValue(assessment.Id, out var submission)
                    ? submission.Status.ToString()
                    : "NotStarted"))
            .ToArray();
        var grades = courseSummaries
            .Select(course => MapGrade(
                course,
                assessments.Where(assessment => assessment.CourseId == course.CourseId).ToArray(),
                assessmentGroups.Where(group => group.CourseId == course.CourseId).ToArray(),
                latestSubmissionByAssessment))
            .ToArray();
        var mappedCertificates = certificates.Select(LearnerWorkspaceMapper.MapCertificate).ToArray();
        var mappedAnnouncements = announcements
            .Where(discussion => courseById.ContainsKey(discussion.CourseId))
            .Select(discussion => new LearnerAnnouncementDto(
                discussion.Id,
                discussion.CourseId,
                courseById[discussion.CourseId].Title,
                courseById[discussion.CourseId].Slug ?? discussion.CourseId.ToString(),
                discussion.Title,
                discussion.Content,
                discussion.CreatedAt,
                discussion.LastActivityAt))
            .ToArray();

        return new LearnerDashboardDto(
            courseSummaries,
            upcoming,
            deadlines,
            grades,
            mappedCertificates,
            mappedAnnouncements);
    }

    private static DateTime? NextDate(CohortScheduleItem item)
    {
        return item.StartsAt ?? item.AvailableFrom ?? item.DueAt;
    }

    private static LearnerGradeSummaryDto MapGrade(
        LearnerCourseSummaryDto course,
        IReadOnlyList<Assessment> assessments,
        IReadOnlyList<AssessmentGroup> groups,
        IReadOnlyDictionary<Guid, AssessmentSubmission> submissions)
    {
        var graded = assessments
            .Where(assessment =>
                submissions.TryGetValue(assessment.Id, out var submission) &&
                submission.Score.HasValue)
            .Select(assessment => new
            {
                Assessment = assessment,
                Submission = submissions[assessment.Id],
            })
            .ToArray();
        var earned = graded.Length > 0
            ? GameGuild.Learning.Grading.Contracts.ScoreValue.Sum(graded.Select(item => item.Submission.Score!.Value))
            : (GameGuild.Learning.Grading.Contracts.ScoreValue?)null;
        var possible = graded.Length > 0
            ? GameGuild.Learning.Grading.Contracts.ScoreValue.Sum(graded.Select(item => item.Assessment.MaxScore))
            : (GameGuild.Learning.Grading.Contracts.ScoreValue?)null;
        var percentage = earned.HasValue && possible.HasValue
            ? GameGuild.Learning.Grading.Contracts.PercentValue.FromScores(earned.Value, possible.Value)
            : (GameGuild.Learning.Grading.Contracts.PercentValue?)null;

        return new LearnerGradeSummaryDto(
            course.CourseId,
            course.Title,
            course.Slug,
            course.FinalGrade,
            graded.Length,
            assessments.Count,
            earned,
            possible,
            percentage,
            groups
                .OrderBy(group => group.Order)
                .Select(group => new LearnerAssessmentGroupDto(
                    group.Id,
                    group.Name,
                    group.Description,
                    group.WeightPercent,
                    group.Order))
                .ToArray(),
            assessments
                .OrderBy(assessment => assessment.Order)
                .Select(assessment =>
                {
                    submissions.TryGetValue(assessment.Id, out var submission);
                    return new LearnerGradeItemDto(
                        assessment.Id,
                        assessment.ContentId,
                        assessment.AssessmentGroupId,
                        assessment.Title,
                        assessment.Type.ToString(),
                        assessment.MaxScore,
                        assessment.AvailableFrom,
                        assessment.AvailableUntil,
                        assessment.DueAt,
                        submission?.Status.ToString() ?? "NotStarted",
                        submission?.Score,
                        submission?.Passed,
                        submission?.Feedback,
                        submission?.GradedAt);
                })
                .ToArray());
    }

    private static LearnerDashboardDto EmptyDashboard()
    {
        return new LearnerDashboardDto([], [], [], [], [], []);
    }
}
