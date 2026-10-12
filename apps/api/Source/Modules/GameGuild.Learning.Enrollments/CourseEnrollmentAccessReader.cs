using GameGuild.Learning.Courses;

namespace GameGuild.Learning.Enrollments;

public sealed class CourseEnrollmentAccessReader(
    IProgramEnrollmentService programEnrollmentService,
    IEnrollmentService enrollmentService)
    : ICourseEnrollmentAccessReader
{
    public async Task<bool> HasActiveEnrollmentAsync(
        Guid courseId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (await programEnrollmentService
                .IsUserEnrolledAsync(userId, courseId)
                .ConfigureAwait(false))
        {
            return true;
        }

        var enrollments = await enrollmentService
            .GetUserEnrollmentsAsync(userId, EnrollmentStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        return enrollments?.Any(enrollment => enrollment.CourseId == courseId) == true;
    }
}
