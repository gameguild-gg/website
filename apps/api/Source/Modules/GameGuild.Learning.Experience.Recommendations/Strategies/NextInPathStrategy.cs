using GameGuild.Learning.Experience.LearningPaths;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Learning.Experience.Recommendations;

/// <summary>
/// Recommends the next course in an enrolled learning path
/// Highest priority as it represents committed learning goals
/// </summary>
public class NextInPathStrategy(IApplicationDbContext context) : IRecommendationStrategy
{
    public RecommendationType Type => RecommendationType.NextInPath;
    public int Priority => 100; // Highest priority

    public async Task<IEnumerable<RecommendationCandidate>> GenerateAsync(
        Guid userId,
        Guid? tenantId,
        IEnumerable<Guid> excludeCourseIds,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        var excludeSet = excludeCourseIds.ToHashSet();

        // Get user's active learning path enrollments
        var activeEnrollments = await context.Set<LearningPathEnrollment>()
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Where(e => e.Status == LearningPathEnrollmentStatus.InProgress)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (!activeEnrollments.Any())
        {
            return Enumerable.Empty<RecommendationCandidate>();
        }

        // Get the learning paths with their courses
        var pathIds = activeEnrollments.Select(e => e.LearningPathId).ToList();
        var learningPaths = await context.Set<LearningPath>()
            .AsNoTracking()
            .Where(lp => pathIds.Contains(lp.Id))
            .Include(lp => lp.Courses)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var recommendations = new List<RecommendationCandidate>();

        foreach (var enrollment in activeEnrollments)
        {
            var learningPath = learningPaths.FirstOrDefault(lp => lp.Id == enrollment.LearningPathId);
            if (learningPath?.Courses == null || !learningPath.Courses.Any())
            {
                continue;
            }

            // Find the next incomplete course in the path
            var orderedCourses = learningPath.Courses.OrderBy(c => c.Order).ToList();
            
            foreach (var pathCourse in orderedCourses)
            {
                // Skip if already excluded (completed/enrolled)
                if (excludeSet.Contains(pathCourse.CourseId))
                {
                    continue;
                }

                // This is the next course to recommend
                var position = orderedCourses.IndexOf(pathCourse) + 1;
                var totalCourses = orderedCourses.Count;
                var progressPercent = ((position - 1) * 100) / totalCourses;

                recommendations.Add(new RecommendationCandidate(
                    CourseId: pathCourse.CourseId,
                    Type: Type,
                    Score: CalculatePathScore(position, pathCourse.IsRequired),
                    Reason: $"Next in '{learningPath.Title}' ({progressPercent}% complete)"));

                break; // Only recommend one course per path
            }

            if (recommendations.Count >= maxResults)
            {
                break;
            }
        }

        return recommendations.Take(maxResults);
    }

    private static double CalculatePathScore(int position, bool isRequired)
    {
        // Base score: 0.9 (high because user is committed to the path)
        // Bonus for required courses
        // Slight penalty for later courses (user might not finish)
        
        var baseScore = 0.9;
        var requiredBonus = isRequired ? 0.05 : 0.0;
        var positionPenalty = (position - 1) * 0.02; // -2% per position

        return Math.Clamp(baseScore + requiredBonus - positionPenalty, 0.5, 1.0);
    }
}
