using GameGuild.CQRS;




using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Query handlers for Program statistics (single, global, creator) and discovery
/// operations (popular, recent, featured, recommended).
/// </summary>
public sealed class ProgramStatisticsAndDiscoveryQueryHandlers(IApplicationDbContext context, ILogger<ProgramStatisticsAndDiscoveryQueryHandlers> logger) : IRequestHandler<GetProgramStatisticsQuery, ProgramStatistics>,
                                                                                                                                                      IRequestHandler<GetGlobalProgramStatisticsQuery, GlobalProgramStatistics>,
                                                                                                                                                      IRequestHandler<GetCreatorProgramStatisticsQuery, CreatorProgramStatistics>,
                                                                                                                                                      IRequestHandler<GetPopularProgramsQuery, IEnumerable<Program>>,
                                                                                                                                                      IRequestHandler<GetRecentProgramsQuery, IEnumerable<Program>>,
                                                                                                                                                      IRequestHandler<GetFeaturedProgramsQuery, IEnumerable<Program>>,
                                                                                                                                                      IRequestHandler<GetRecommendedProgramsQuery, IEnumerable<Program>> {
  public async Task<ProgramStatistics> Handle(GetProgramStatisticsQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting statistics for program: {ProgramId}", request.ProgramId);

    var totalEnrollments = await context.Set<ProgramUser>().Where(pu => pu.ProgramId == request.ProgramId).CountAsync(cancellationToken);

    var activeEnrollments = await context.Set<ProgramUser>().Where(pu => pu.ProgramId == request.ProgramId && pu.IsActive).CountAsync(cancellationToken);

    var completedEnrollments = await context.Set<ProgramUser>().Where(pu => pu.ProgramId == request.ProgramId && pu.CompletedAt.HasValue) // Fixed: use CompletedAt instead of IsCompleted
                                            .CountAsync(cancellationToken);

    var ratings = await context.Set<ProgramRating>().Where(pr => pr.ProgramId == request.ProgramId).ToListAsync(cancellationToken);

    var averageRating = ratings.Count > 0 ? ratings.Average(r => r.Rating) : 0;
    var totalRatings = ratings.Count;
    var completionRate = totalEnrollments > 0 ? (decimal)completedEnrollments / totalEnrollments : 0;

    // Note: Average completion time would need actual time tracking
    var averageCompletionTime = TimeSpan.Zero;

    var statistics = new ProgramStatistics(request.ProgramId, totalEnrollments, activeEnrollments, completedEnrollments, averageRating, totalRatings, completionRate, averageCompletionTime);

    return statistics;
  }

  public async Task<GlobalProgramStatistics> Handle(GetGlobalProgramStatisticsQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting global program statistics");

    var query = context.Set<Program>().Where(p => p.DeletedAt == null);

    if (request.FromDate.HasValue)
        {
            query = query.Where(p => p.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(p => p.CreatedAt <= request.ToDate.Value);
        }

        var totalPrograms = await query.CountAsync(cancellationToken).ConfigureAwait(false);
    var publishedPrograms = await query.Where(p => p.Status == ContentStatus.Published).CountAsync(cancellationToken);

    var enrollmentQuery = context.Set<ProgramUser>().AsQueryable();
    if (request.FromDate.HasValue)
        {
            enrollmentQuery = enrollmentQuery.Where(pu => pu.JoinedAt >= request.FromDate.Value); // Fixed property name
        }

        if (request.ToDate.HasValue)
        {
            enrollmentQuery = enrollmentQuery.Where(pu => pu.JoinedAt <= request.ToDate.Value); // Fixed property name
        }

        var totalEnrollments = await enrollmentQuery.CountAsync(cancellationToken).ConfigureAwait(false);
    var activeEnrollments = await enrollmentQuery.Where(pu => pu.IsActive).CountAsync(cancellationToken);

    var allRatings = await context.Set<ProgramRating>().ToListAsync(cancellationToken).ConfigureAwait(false);
    var averageRating = allRatings.Count > 0 ? allRatings.Average(r => r.Rating) : 0;
    var totalRatings = allRatings.Count;

    // Most popular category and difficulty (simplified)
    var mostPopularCategory = await context.Set<Program>().Where(p => p.DeletedAt == null && p.Status == ContentStatus.Published)
                                           .GroupBy(p => p.Category)
                                           .OrderByDescending(g => g.Count())
                                           .Select(g => g.Key)
                                           .FirstOrDefaultAsync(cancellationToken);

    var mostPopularDifficulty = await context.Set<Program>().Where(p => p.DeletedAt == null && p.Status == ContentStatus.Published)
                                             .GroupBy(p => p.Difficulty)
                                             .OrderByDescending(g => g.Count())
                                             .Select(g => g.Key)
                                             .FirstOrDefaultAsync(cancellationToken);

    var statistics = new GlobalProgramStatistics(totalPrograms, publishedPrograms, totalEnrollments, activeEnrollments, averageRating, totalRatings, mostPopularCategory, mostPopularDifficulty);

    return statistics;
  }

  public async Task<CreatorProgramStatistics> Handle(GetCreatorProgramStatisticsQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting program statistics for creator: {CreatorId}", request.CreatorId);

    // CreatorId property doesn't exist in current Program model, return empty statistics for now
    var query = context.Set<Program>().Where(p => false && p.DeletedAt == null); // Return empty until CreatorId is added to model

    if (request.FromDate.HasValue)
        {
            query = query.Where(p => p.CreatedAt >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(p => p.CreatedAt <= request.ToDate.Value);
        }

        var totalPrograms = await query.CountAsync(cancellationToken).ConfigureAwait(false);
    var publishedPrograms = await query.Where(p => p.Status == ContentStatus.Published).CountAsync(cancellationToken);

    var programIds = await query.Select(p => p.Id).ToListAsync(cancellationToken);

    var enrollmentQuery = context.Set<ProgramUser>().Where(pu => programIds.Contains(pu.ProgramId));
    if (request.FromDate.HasValue)
        {
            enrollmentQuery = enrollmentQuery.Where(pu => pu.JoinedAt >= request.FromDate.Value); // Fixed property name
        }

        if (request.ToDate.HasValue)
        {
            enrollmentQuery = enrollmentQuery.Where(pu => pu.JoinedAt <= request.ToDate.Value); // Fixed property name
        }

        var totalEnrollments = await enrollmentQuery.CountAsync(cancellationToken).ConfigureAwait(false);
    var activeEnrollments = await enrollmentQuery.Where(pu => pu.IsActive).CountAsync(cancellationToken);

    var ratings = await context.Set<ProgramRating>().Where(pr => programIds.Contains(pr.ProgramId)).ToListAsync(cancellationToken);

    var averageRating = ratings.Count > 0 ? ratings.Average(r => r.Rating) : 0;
    var totalRatings = ratings.Count;

    // Note: Average completion rate would need actual completion tracking
    var averageCompletionRate = 0m;

    var statistics = new CreatorProgramStatistics(request.CreatorId, totalPrograms, publishedPrograms, totalEnrollments, activeEnrollments, averageRating, totalRatings, averageCompletionRate);

    return statistics;
  }

  public async Task<IEnumerable<Program>> Handle(GetPopularProgramsQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting popular programs");

    var sinceDate = SystemClock.UtcNow.AddDays(-request.DaysBack);

    var programs = await context.Set<Program>().Where(p => p.DeletedAt == null && p.Status == ContentStatus.Published && p.Visibility == ContentVisibility.Public)
                                .OrderByDescending(p => p.ProgramUsers.Count(pu => pu.JoinedAt >= sinceDate)) // Fixed property name
                                .ThenByDescending(p => p.ProgramRatings.Count > 0 ? p.ProgramRatings.Average(pr => pr.Rating) : 0)
                                .Skip(request.Skip)
                                .Take(request.Take)
                                .ToListAsync(cancellationToken);

    logger.LogInformation("Found {Count} popular programs", programs.Count);

    return programs;
  }

  public async Task<IEnumerable<Program>> Handle(GetRecentProgramsQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting recent programs");

    var sinceDate = SystemClock.UtcNow.AddDays(-request.DaysBack);

    var programs = await context.Set<Program>().Where(p => p.DeletedAt == null && p.Status == ContentStatus.Published && p.Visibility == ContentVisibility.Public && p.CreatedAt >= sinceDate // Fixed: use CreatedAt instead of PublishedAt
                                )
                                .OrderByDescending(p => p.CreatedAt) // Fixed: use CreatedAt instead of PublishedAt
                                .Skip(request.Skip)
                                .Take(request.Take)
                                .ToListAsync(cancellationToken);

    logger.LogInformation("Found {Count} recent programs", programs.Count);

    return programs;
  }

  public async Task<IEnumerable<Program>> Handle(GetFeaturedProgramsQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting featured programs");

    // Note: This is a simplified implementation. You might want to add a "Featured" flag to programs
    var programs = await context.Set<Program>().Where(p => p.DeletedAt == null && p.Status == ContentStatus.Published && p.Visibility == ContentVisibility.Public)
                                .OrderByDescending(p => p.ProgramRatings.Count > 0 ? p.ProgramRatings.Average(pr => pr.Rating) : 0)
                                .ThenByDescending(p => p.ProgramUsers.Count)
                                .Skip(request.Skip)
                                .Take(request.Take)
                                .ToListAsync(cancellationToken);

    logger.LogInformation("Found {Count} featured programs", programs.Count);

    return programs;
  }

  public async Task<IEnumerable<Program>> Handle(GetRecommendedProgramsQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting recommended programs for user: {UserId}", request.UserId);

    var userGuid = Guid.Parse(request.UserId); // Convert string UserId to Guid

    // Note: This is a simplified recommendation system. A real implementation would use more sophisticated algorithms
    var userEnrollments = await context.Set<ProgramUser>().Where(pu => pu.UserId == userGuid && pu.IsActive) // Fixed: use converted Guid
                                       .Select(pu => pu.Program)
                                       .ToListAsync(cancellationToken);

    var userCategories = userEnrollments.Select(p => p.Category).Distinct().ToList();
    var userDifficulties = userEnrollments.Select(p => p.Difficulty).Distinct().ToList();

    var enrolledProgramIds = userEnrollments.Select(p => p.Id).ToList();

    var recommendations = await context.Set<Program>()
                                       .Where(p => p.DeletedAt == null &&
                                                   p.Status == ContentStatus.Published &&
                                                   p.Visibility == ContentVisibility.Public &&
                                                   !enrolledProgramIds.Contains(p.Id) &&
                                                   (userCategories.Contains(p.Category) || userDifficulties.Contains(p.Difficulty))
                                       )
                                       .OrderByDescending(p => p.ProgramRatings.Count > 0 ? p.ProgramRatings.Average(pr => pr.Rating) : 0)
                                       .Take(request.Take)
                                       .ToListAsync(cancellationToken);

    logger.LogInformation("Found {Count} recommended programs for user {UserId}", recommendations.Count, request.UserId);

    return recommendations;
  }
}
