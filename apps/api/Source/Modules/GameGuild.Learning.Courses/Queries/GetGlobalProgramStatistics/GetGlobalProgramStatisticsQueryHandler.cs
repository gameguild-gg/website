using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Query handler for GetGlobalProgramStatisticsQuery
/// Calculates platform-wide program statistics
/// </summary>
public sealed class GetGlobalProgramStatisticsQueryHandler(
    IApplicationDbContext context,
    ILogger<GetGlobalProgramStatisticsQueryHandler> logger)
    : IQueryHandler<GetGlobalProgramStatisticsQuery, GlobalProgramStatistics>
{
    public async Task<GlobalProgramStatistics> Handle(
        GetGlobalProgramStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting global program statistics");

        // Build base query with date filters
        var programsQuery = context.Set<Program>()
            .AsNoTracking()
            .Where(p => p.DeletedAt == null);

        if (request.FromDate.HasValue)
        {
            programsQuery = programsQuery.Where(p => p.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            programsQuery = programsQuery.Where(p => p.CreatedAt <= request.ToDate.Value);
        }

        var totalPrograms = await programsQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var publishedPrograms = await programsQuery
            .CountAsync(p => p.Status == ContentStatus.Published, cancellationToken).ConfigureAwait(false);

        // Get enrollment statistics
        var enrollmentsQuery = context.Set<ProgramEnrollment>()
            .AsNoTracking()
            .Where(e => e.DeletedAt == null);

        if (request.FromDate.HasValue)
        {
            enrollmentsQuery = enrollmentsQuery.Where(e => e.EnrolledAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            enrollmentsQuery = enrollmentsQuery.Where(e => e.EnrolledAt <= request.ToDate.Value);
        }

        var totalEnrollments = await enrollmentsQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var activeEnrollments = await enrollmentsQuery
            .CountAsync(e => e.EnrollmentStatus == EnrollmentStatus.Active, cancellationToken).ConfigureAwait(false);

        // Get rating statistics
        var ratingsQuery = context.Set<ProgramRating>()
            .AsNoTracking();

        var totalRatings = await ratingsQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var averageRating = totalRatings > 0
            ? await ratingsQuery.AverageAsync(r => r.Rating, cancellationToken)
            : 0m;

        // Find most popular category
        var mostPopularCategory = await programsQuery
            .GroupBy(p => p.Category)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        // Find most popular difficulty
        var mostPopularDifficulty = await programsQuery
            .GroupBy(p => p.Difficulty)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Global statistics: {TotalPrograms} programs, {TotalEnrollments} enrollments, {AvgRating} avg rating",
            totalPrograms, totalEnrollments, Math.Round(averageRating, 2));

        return new GlobalProgramStatistics(
            TotalPrograms: totalPrograms,
            PublishedPrograms: publishedPrograms,
            TotalEnrollments: totalEnrollments,
            ActiveEnrollments: activeEnrollments,
            AverageRating: Math.Round(averageRating, 2),
            TotalRatings: totalRatings,
            MostPopularCategory: mostPopularCategory,
            MostPopularDifficulty: mostPopularDifficulty);
    }
}
