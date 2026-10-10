using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Social.Ratings;

/// <summary>
/// Handles review interactions (helpful votes, reports) and admin moderation.
/// </summary>
public class RatingModerationService(
    IApplicationDbContext db,
    IActorContextAccessor actorContextAccessor,
    IRatingQueryService queryService,
    ILogger<RatingModerationService> logger) : IRatingModerationService
{
    private Guid GetCurrentUserId() => actorContextAccessor.ActorContext.SubjectIdAsGuid ?? throw new UnauthorizedAccessException("User not authenticated");

    // ─── Review Interactions ─────────────────────────────────────────────────────

    public async Task<Result> VoteHelpfulAsync(Guid ratingId, bool isHelpful, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();

        var rating = await db.Set<Rating>()
            .FirstOrDefaultAsync(r => r.Id == ratingId && !r.IsDeleted, ct).ConfigureAwait(false);

        if (rating == null)
        {
            return Result.Failure(RatingErrors.NotFound);
        }

        if (rating.UserId == userId)
        {
            return Result.Failure(RatingErrors.CannotVoteOwnRating);
        }

        var existingVote = await db.Set<RatingHelpfulVote>()
            .FirstOrDefaultAsync(v => v.RatingId == ratingId && v.UserId == userId && !v.IsDeleted, ct).ConfigureAwait(false);

        if (existingVote != null)
        {
            // Update existing vote
            var wasHelpful = existingVote.IsHelpful;
            existingVote.UpdateVote(isHelpful);

            if (wasHelpful && !isHelpful)
            {
                rating.DecrementHelpful();
            }
            else if (!wasHelpful && isHelpful)
            {
                rating.IncrementHelpful();
            }
        }
        else
        {
            // Create new vote
            var vote = RatingHelpfulVote.Create(ratingId, userId, isHelpful);
            db.Set<RatingHelpfulVote>().Add(vote);

            if (isHelpful)
            {
                rating.IncrementHelpful();
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> RemoveHelpfulVoteAsync(Guid ratingId, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();

        var vote = await db.Set<RatingHelpfulVote>()
            .FirstOrDefaultAsync(v => v.RatingId == ratingId && v.UserId == userId && !v.IsDeleted, ct).ConfigureAwait(false);

        if (vote == null)
        {
            return Result.Failure(RatingErrors.VoteNotFound);
        }

        var rating = await db.Set<Rating>()
            .FirstOrDefaultAsync(r => r.Id == ratingId && !r.IsDeleted, ct).ConfigureAwait(false);

        if (rating != null && vote.IsHelpful)
        {
            rating.DecrementHelpful();
        }

        vote.SoftDelete();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> ReportAsync(Guid ratingId, string reason, CancellationToken ct = default)
    {
        var rating = await db.Set<Rating>()
            .FirstOrDefaultAsync(r => r.Id == ratingId && !r.IsDeleted, ct).ConfigureAwait(false);

        if (rating == null)
        {
            return Result.Failure(RatingErrors.NotFound);
        }

        rating.IncrementReport();

        // Auto-flag for moderation if too many reports
        if (rating.ReportCount >= 3)
        {
            rating.SetModerationStatus(RatingModerationStatus.Flagged);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogWarning("Rating {RatingId} reported for: {Reason}. Total reports: {ReportCount}",
            ratingId, reason, rating.ReportCount);

        return Result.Success();
    }

    // ─── Moderation (Admin) ──────────────────────────────────────────────────────

    public async Task<Result<IEnumerable<Rating>>> GetPendingModerationAsync(
        int skip = 0,
        int take = 20,
        CancellationToken ct = default)
    {
        var ratings = await db.Set<Rating>()
            .Where(r => !r.IsDeleted)
            .Where(r => r.ModerationStatus == RatingModerationStatus.Pending ||
                        r.ModerationStatus == RatingModerationStatus.Flagged)
            .OrderByDescending(r => r.ReportCount)
            .ThenBy(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct).ConfigureAwait(false);

        return Result.Success<IEnumerable<Rating>>(ratings);
    }

    public async Task<Result> ApproveAsync(Guid ratingId, CancellationToken ct = default)
    {
        var rating = await db.Set<Rating>()
            .FirstOrDefaultAsync(r => r.Id == ratingId && !r.IsDeleted, ct).ConfigureAwait(false);

        if (rating == null)
        {
            return Result.Failure(RatingErrors.NotFound);
        }

        rating.SetModerationStatus(RatingModerationStatus.Approved);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Recalculate summary since this rating is now visible
        await queryService.RecalculateSummaryAsync(rating.EntityId, rating.EntityType, ct).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> RejectAsync(Guid ratingId, CancellationToken ct = default)
    {
        var rating = await db.Set<Rating>()
            .FirstOrDefaultAsync(r => r.Id == ratingId && !r.IsDeleted, ct).ConfigureAwait(false);

        if (rating == null)
        {
            return Result.Failure(RatingErrors.NotFound);
        }

        rating.SetModerationStatus(RatingModerationStatus.Rejected);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Recalculate summary since this rating is now hidden
        await queryService.RecalculateSummaryAsync(rating.EntityId, rating.EntityType, ct).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result> AdminDeleteAsync(Guid ratingId, CancellationToken ct = default)
    {
        var rating = await db.Set<Rating>()
            .FirstOrDefaultAsync(r => r.Id == ratingId && !r.IsDeleted, ct).ConfigureAwait(false);

        if (rating == null)
        {
            return Result.Failure(RatingErrors.NotFound);
        }

        rating.SoftDelete();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogWarning("Admin deleted rating {RatingId}", ratingId);

        // Recalculate summary
        await queryService.RecalculateSummaryAsync(rating.EntityId, rating.EntityType, ct).ConfigureAwait(false);

        return Result.Success();
    }
}
