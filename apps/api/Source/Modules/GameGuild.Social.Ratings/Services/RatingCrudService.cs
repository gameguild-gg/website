using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Social.Ratings;

/// <summary>
/// Handles core rating CRUD: create/update, get, and delete.
/// </summary>
public class RatingCrudService(
    IApplicationDbContext db,
    IActorContextAccessor actorContextAccessor,
    IRatingQueryService queryService,
    ILogger<RatingCrudService> logger) : IRatingCrudService
{
    private Guid GetCurrentUserId() => actorContextAccessor.ActorContext.SubjectIdAsGuid ?? throw new UnauthorizedAccessException("User not authenticated");

    public async Task<Result<Rating>> RateAsync(
        Guid entityId,
        string entityType,
        int value,
        string? reviewText = null,
        string? reviewTitle = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();

        // Check if user already rated this entity
        var existingRating = await db.Set<Rating>()
            .FirstOrDefaultAsync(r =>
                r.EntityId == entityId &&
                r.EntityType == entityType &&
                r.UserId == userId &&
                !r.IsDeleted, ct).ConfigureAwait(false);

        if (existingRating != null)
        {
            // Update existing rating
            existingRating.Update(value, reviewText, reviewTitle);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            logger.LogInformation("User {UserId} updated rating for {EntityType}:{EntityId}",
                userId, entityType, entityId);

            // Trigger summary recalculation
            await queryService.RecalculateSummaryAsync(entityId, entityType, ct).ConfigureAwait(false);

            return Result.Success(existingRating);
        }

        // Create new rating
        var rating = Rating.Create(userId, entityId, entityType, value, reviewText, reviewTitle);
        db.Set<Rating>().Add(rating);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation("User {UserId} created rating for {EntityType}:{EntityId} with value {Value}",
            userId, entityType, entityId, value);

        // Trigger summary recalculation
        await queryService.RecalculateSummaryAsync(entityId, entityType, ct).ConfigureAwait(false);

        return Result.Success(rating);
    }

    public async Task<Result<Rating>> GetByIdAsync(Guid ratingId, CancellationToken ct = default)
    {
        var rating = await db.Set<Rating>()
            .FirstOrDefaultAsync(r => r.Id == ratingId && !r.IsDeleted, ct).ConfigureAwait(false);

        if (rating == null)
        {
            return Result.Failure<Rating>(RatingErrors.NotFound);
        }

        return Result.Success(rating);
    }

    public async Task<Result<Rating>> GetUserRatingAsync(Guid entityId, string entityType, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();

        var rating = await db.Set<Rating>()
            .FirstOrDefaultAsync(r =>
                r.EntityId == entityId &&
                r.EntityType == entityType &&
                r.UserId == userId &&
                !r.IsDeleted, ct).ConfigureAwait(false);

        if (rating == null)
        {
            return Result.Failure<Rating>(RatingErrors.NotFound);
        }

        return Result.Success(rating);
    }

    public async Task<Result> DeleteAsync(Guid ratingId, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();

        var rating = await db.Set<Rating>()
            .FirstOrDefaultAsync(r => r.Id == ratingId && r.UserId == userId && !r.IsDeleted, ct).ConfigureAwait(false);

        if (rating == null)
        {
            return Result.Failure(RatingErrors.NotFound);
        }

        rating.SoftDelete();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation("User {UserId} deleted rating {RatingId}", userId, ratingId);

        // Trigger summary recalculation
        await queryService.RecalculateSummaryAsync(rating.EntityId, rating.EntityType, ct).ConfigureAwait(false);

        return Result.Success();
    }
}
