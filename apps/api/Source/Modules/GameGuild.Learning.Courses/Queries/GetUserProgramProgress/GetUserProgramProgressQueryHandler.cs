using GameGuild.CQRS;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Query handler for GetUserProgramProgressQuery
/// Gets detailed progress information for a user in a specific program
/// </summary>
public sealed class GetUserProgramProgressQueryHandler(
    IApplicationDbContext context,
    ILogger<GetUserProgramProgressQueryHandler> logger)
    : IQueryHandler<GetUserProgramProgressQuery, ProgramUserProgress?>
{
    public async Task<ProgramUserProgress?> Handle(
        GetUserProgramProgressQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Getting user program progress for user: {UserId}, program: {ProgramId}",
            request.UserId, request.ProgramId);

        // Get the enrollment
        var enrollment = await context.Set<ProgramEnrollment>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.UserId == request.UserId &&
                e.ProgramId == request.ProgramId &&
                e.DeletedAt == null,
                cancellationToken);

        if (enrollment == null)
        {
            logger.LogWarning(
                "No enrollment found for user {UserId} in program {ProgramId}",
                request.UserId, request.ProgramId);
            return null;
        }

        // Get total content count for the program
        var totalContent = await context.Set<ProgramContent>()
            .AsNoTracking()
            .CountAsync(c => c.ProgramId == request.ProgramId && c.DeletedAt == null, cancellationToken).ConfigureAwait(false);

        // Get completed content count
        var completedContent = await context.Set<ContentProgress>()
            .AsNoTracking()
            .CountAsync(cp =>
                cp.ProgramEnrollmentId == enrollment.Id &&
                cp.CompletionStatus == ContentCompletionStatus.Completed,
                cancellationToken).ConfigureAwait(false);

        // Calculate progress percentage
        var progressPercentage = totalContent > 0
            ? PercentValue.FromRatio(completedContent, totalContent)
            : PercentValue.Zero;

        // Get time spent from content progress records
        var timeSpentSeconds = await context.Set<ContentProgress>()
            .AsNoTracking()
            .Where(cp => cp.ProgramEnrollmentId == enrollment.Id)
            .SumAsync(cp => cp.TimeSpentSeconds, cancellationToken).ConfigureAwait(false);

        // Get last activity
        var lastActivity = await context.Set<ContentProgress>()
            .AsNoTracking()
            .Where(cp => cp.ProgramEnrollmentId == enrollment.Id)
            .MaxAsync(cp => (DateTime?)cp.LastAccessedAt, cancellationToken).ConfigureAwait(false);

        var isCompleted = enrollment.CompletionStatus == CompletionStatus.Completed ||
                         enrollment.CompletionStatus == CompletionStatus.CompletedWithCertificate;

        logger.LogInformation(
            "User {UserId} progress in program {ProgramId}: {CompletedContent}/{TotalContent} ({Progress}%)",
            request.UserId, request.ProgramId, completedContent, totalContent, progressPercentage);

        return new ProgramUserProgress(
            ProgramId: request.ProgramId,
            UserId: request.UserId,
            CompletedContent: completedContent,
            TotalContent: totalContent,
            ProgressPercentage: progressPercentage,
            TimeSpent: TimeSpan.FromSeconds(timeSpentSeconds),
            LastActivityAt: lastActivity,
            IsCompleted: isCompleted,
            CompletedAt: enrollment.CompletedAt);
    }
}
