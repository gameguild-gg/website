using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Social.Ratings;

/// <summary>
/// Handles rating queries, batch lookups, summary aggregation, and statistics.
/// </summary>
public class RatingQueryService(
    IApplicationDbContext db,
    IActorContextAccessor actorContextAccessor,
    ILogger<RatingQueryService> _logger) : IRatingQueryService
{
    private Guid GetCurrentUserId() => actorContextAccessor.ActorContext.SubjectIdAsGuid ?? throw new UnauthorizedAccessException("User not authenticated");

    public async Task<Result<IEnumerable<Rating>>> GetRatingsAsync(
        Guid entityId,
        string entityType,
        int? minValue = null,
        int? maxValue = null,
        bool? withReviewOnly = null,
        bool? verifiedOnly = null,
        RatingSortOrder sortOrder = RatingSortOrder.MostRecent,
        int skip = 0,
        int take = 20,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Querying ratings: EntityId={EntityId}, EntityType={EntityType}, SortOrder={SortOrder}", entityId, LogRedaction.Sanitize(entityType), sortOrder);

        var query = db.Set<Rating>()
            .Where(r => r.EntityId == entityId && r.EntityType == entityType && !r.IsDeleted)
            .Where(r => r.ModerationStatus == RatingModerationStatus.Approved);

        if (minValue.HasValue)
            query = query.Where(r => r.Value >= minValue.Value);

        if (maxValue.HasValue)
            query = query.Where(r => r.Value <= maxValue.Value);

        if (withReviewOnly == true)
            query = query.Where(r => r.ReviewText != null && r.ReviewText != "");

        if (verifiedOnly == true)
            query = query.Where(r => r.IsVerified);

        query = sortOrder switch
        {
            RatingSortOrder.Oldest => query.OrderBy(r => r.CreatedAt),
            RatingSortOrder.HighestRating => query.OrderByDescending(r => r.Value).ThenByDescending(r => r.CreatedAt),
            RatingSortOrder.LowestRating => query.OrderBy(r => r.Value).ThenByDescending(r => r.CreatedAt),
            RatingSortOrder.MostHelpful => query.OrderByDescending(r => r.HelpfulCount).ThenByDescending(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };

        var ratings = await query.Skip(skip).Take(take).ToListAsync(ct).ConfigureAwait(false);
        return Result.Success<IEnumerable<Rating>>(ratings);
    }

    public async Task<Result<RatingSummary>> GetSummaryAsync(Guid entityId, string entityType, CancellationToken ct = default)
    {
        var summary = await db.Set<RatingSummary>()
            .FirstOrDefaultAsync(s => s.EntityId == entityId && s.EntityType == entityType && !s.IsDeleted, ct).ConfigureAwait(false);

        if (summary == null)
        {
            // Create initial summary on-demand
            summary = RatingSummary.Create(entityId, entityType);
            db.Set<RatingSummary>().Add(summary);

            // Calculate from existing ratings
            var ratings = await db.Set<Rating>()
                .Where(r => r.EntityId == entityId && r.EntityType == entityType && !r.IsDeleted)
                .ToListAsync(ct).ConfigureAwait(false);

            summary.Recalculate(ratings);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return Result.Success(summary);
    }

    public async Task<Result<bool>> HasUserRatedAsync(Guid entityId, string entityType, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();

        var hasRated = await db.Set<Rating>()
            .AnyAsync(r =>
                r.EntityId == entityId &&
                r.EntityType == entityType &&
                r.UserId == userId &&
                !r.IsDeleted, ct).ConfigureAwait(false);

        return Result.Success(hasRated);
    }

    public async Task<Result<IEnumerable<Rating>>> GetUserRatingsAsync(
        Guid userId,
        string? entityType = null,
        int skip = 0,
        int take = 20,
        CancellationToken ct = default)
    {
        var query = db.Set<Rating>()
            .Where(r => r.UserId == userId && !r.IsDeleted)
            .Where(r => r.ModerationStatus == RatingModerationStatus.Approved);

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(r => r.EntityType == entityType);

        var ratings = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct).ConfigureAwait(false);

        return Result.Success<IEnumerable<Rating>>(ratings);
    }

    public async Task<Result<int>> GetCountAsync(Guid entityId, string entityType, CancellationToken ct = default)
    {
        var count = await db.Set<Rating>()
            .CountAsync(r =>
                r.EntityId == entityId &&
                r.EntityType == entityType &&
                !r.IsDeleted &&
                r.ModerationStatus == RatingModerationStatus.Approved, ct).ConfigureAwait(false);

        return Result.Success(count);
    }

    public async Task<Result<Dictionary<Guid, RatingSummary>>> GetSummariesBatchAsync(
        IEnumerable<Guid> entityIds,
        string entityType,
        CancellationToken ct = default)
    {
        var ids = entityIds.ToList();

        var summaries = await db.Set<RatingSummary>()
            .Where(s => ids.Contains(s.EntityId) && s.EntityType == entityType && !s.IsDeleted)
            .ToListAsync(ct).ConfigureAwait(false);

        var result = summaries.ToDictionary(s => s.EntityId, s => s);

        // Create empty summaries for entities without ratings
        foreach (var id in ids.Where(id => !result.ContainsKey(id)))
        {
            result[id] = RatingSummary.Create(id, entityType);
        }

        return Result.Success(result);
    }

    public async Task<Result<Dictionary<Guid, Rating>>> GetUserRatingsBatchAsync(
        IEnumerable<Guid> entityIds,
        string entityType,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var ids = entityIds.ToList();

        var ratings = await db.Set<Rating>()
            .Where(r =>
                ids.Contains(r.EntityId) &&
                r.EntityType == entityType &&
                r.UserId == userId &&
                !r.IsDeleted)
            .ToListAsync(ct).ConfigureAwait(false);

        var result = ratings.ToDictionary(r => r.EntityId, r => r);
        return Result.Success(result);
    }

    public async Task<Result> RecalculateSummaryAsync(Guid entityId, string entityType, CancellationToken ct = default)
    {
        var summary = await db.Set<RatingSummary>()
            .FirstOrDefaultAsync(s => s.EntityId == entityId && s.EntityType == entityType && !s.IsDeleted, ct).ConfigureAwait(false);

        if (summary == null)
        {
            summary = RatingSummary.Create(entityId, entityType);
            db.Set<RatingSummary>().Add(summary);
        }

        var ratings = await db.Set<Rating>()
            .Where(r => r.EntityId == entityId && r.EntityType == entityType && !r.IsDeleted)
            .ToListAsync(ct).ConfigureAwait(false);

        summary.Recalculate(ratings);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<IEnumerable<RatingSummary>>> GetTopRatedAsync(
        string entityType,
        int minRatings = 5,
        int take = 10,
        CancellationToken ct = default)
    {
        var summaries = await db.Set<RatingSummary>()
            .Where(s => s.EntityType == entityType && !s.IsDeleted)
            .Where(s => s.TotalRatings >= minRatings)
            .OrderByDescending(s => s.AverageRating)
            .ThenByDescending(s => s.TotalRatings)
            .Take(take)
            .ToListAsync(ct).ConfigureAwait(false);

        return Result.Success<IEnumerable<RatingSummary>>(summaries);
    }

    public async Task<Result<IEnumerable<Rating>>> GetRecentReviewsAsync(
        string? entityType = null,
        int take = 20,
        CancellationToken ct = default)
    {
        var query = db.Set<Rating>()
            .Where(r => !r.IsDeleted)
            .Where(r => r.ModerationStatus == RatingModerationStatus.Approved)
            .Where(r => r.ReviewText != null && r.ReviewText != "");

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(r => r.EntityType == entityType);

        var reviews = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .ToListAsync(ct).ConfigureAwait(false);

        return Result.Success<IEnumerable<Rating>>(reviews);
    }
}
