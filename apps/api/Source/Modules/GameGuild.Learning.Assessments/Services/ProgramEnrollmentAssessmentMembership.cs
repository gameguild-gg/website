using GameGuild.Learning.Courses;
using Microsoft.EntityFrameworkCore;
using CourseProgram = GameGuild.Learning.Courses.Program;

namespace GameGuild.Learning.Assessments;

/// <summary>Shared persisted membership predicate for assessment transport and grading runtime.</summary>
internal static class ProgramEnrollmentAssessmentMembership
{
    public static IQueryable<ProgramEnrollment> ActiveForCourse(IApplicationDbContext context, Guid courseId) =>
        from enrollment in context.Set<ProgramEnrollment>().AsNoTracking()
        join course in context.Set<CourseProgram>().AsNoTracking() on enrollment.ProgramId equals course.Id
        where enrollment.ProgramId == courseId && enrollment.DeletedAt == null && course.DeletedAt == null &&
              enrollment.EnrollmentStatus == EnrollmentStatus.Active &&
              (enrollment.TenantId == null || enrollment.TenantId == course.TenantId)
        select enrollment;
}
