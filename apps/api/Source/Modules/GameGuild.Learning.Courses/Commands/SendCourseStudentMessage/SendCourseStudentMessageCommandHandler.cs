using GameGuild.CQRS;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Learning.Courses;

public sealed class SendCourseStudentMessageCommandHandler(
    IApplicationDbContext context,
    INotificationService notifications) : ICommandHandler<SendCourseStudentMessageCommand, int>
{
    public async Task<int> Handle(SendCourseStudentMessageCommand request, CancellationToken cancellationToken)
    {
        var requestedUserIds = request.UserIds.Distinct().ToArray();
        if (requestedUserIds.Length == 0)
        {
            return 0;
        }

        var enrolledUserIds = await context.Set<ProgramUser>()
            .Where(enrollment =>
                enrollment.ProgramId == request.CourseId &&
                enrollment.IsActive &&
                enrollment.DeletedAt == null &&
                requestedUserIds.Contains(enrollment.UserId))
            .Select(enrollment => enrollment.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        if (enrolledUserIds.Length == 0)
        {
            return 0;
        }

        var result = await notifications.SendBulkAsync(
                enrolledUserIds,
                NotificationType.DirectMessage,
                request.Subject,
                request.Message,
                NotificationChannel.InApp,
                request.TenantId,
                $"/dashboard/learning/courses/{request.CourseId}/students",
                NotificationPriority.Normal,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Description);
        }

        return enrolledUserIds.Length;
    }
}
