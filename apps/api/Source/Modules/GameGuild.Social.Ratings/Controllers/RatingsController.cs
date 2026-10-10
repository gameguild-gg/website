using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Social.Ratings;

/// <summary>
/// REST API controller for the polymorphic rating system
/// </summary>
[Route("api/[controller]")]
[Authorize]
public class RatingsController : BaseApiController
{
    private readonly IRatingService _ratingService;
    private readonly ISender _sender;

    public RatingsController(IRatingService ratingService, ISender sender)
    {
        _ratingService = ratingService;
        _sender = sender;
    }

    /// <summary>
    /// Rate an entity (create or update)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RatingDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Rate([FromBody] CreateRatingRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new RateRatingEndpointCommand(
            request.EntityId,
            request.EntityType,
            request.Value,
            request.ReviewText,
            request.ReviewTitle), ct).ConfigureAwait(false);

        return result.IsSuccess 
            ? Ok(RatingDto.FromEntity(result.Value)) 
            : BadRequest(result.Error);
    }

    /// <summary>
    /// Get a specific rating by ID
    /// </summary>
    [HttpGet("{ratingId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RatingDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid ratingId, CancellationToken ct)
    {
        var result = await _ratingService.GetByIdAsync(ratingId, ct).ConfigureAwait(false);
        return result.IsSuccess 
            ? Ok(RatingDto.FromEntity(result.Value)) 
            : NotFound(result.Error);
    }

    /// <summary>
    /// Get the current user's rating for an entity
    /// </summary>
    [HttpGet("my/{entityType}/{entityId:guid}")]
    [ProducesResponseType(typeof(RatingDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMyRating(string entityType, Guid entityId, CancellationToken ct)
    {
        var result = await _ratingService.GetUserRatingAsync(entityId, entityType, ct).ConfigureAwait(false);
        return result.IsSuccess 
            ? Ok(RatingDto.FromEntity(result.Value)) 
            : NotFound(result.Error);
    }

    /// <summary>
    /// Check if current user has rated an entity
    /// </summary>
    [HttpGet("has-rated/{entityType}/{entityId:guid}")]
    [ProducesResponseType(typeof(bool), 200)]
    public async Task<IActionResult> HasUserRated(string entityType, Guid entityId, CancellationToken ct)
    {
        var result = await _ratingService.HasUserRatedAsync(entityId, entityType, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    /// <summary>
    /// Delete the current user's rating
    /// </summary>
    [HttpDelete("{ratingId:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid ratingId, CancellationToken ct)
    {
        var result = await _sender.Send(new DeleteRatingEndpointCommand(ratingId), ct).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : NotFound(result.Error);
    }

    /// <summary>
    /// Get ratings for an entity
    /// </summary>
    [HttpGet("entity/{entityType}/{entityId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<RatingDto>), 200)]
    public async Task<IActionResult> GetRatings(
        string entityType, 
        Guid entityId,
        [FromQuery] int? minValue = null,
        [FromQuery] int? maxValue = null,
        [FromQuery] bool? withReviewOnly = null,
        [FromQuery] bool? verifiedOnly = null,
        [FromQuery] RatingSortOrder sortOrder = RatingSortOrder.MostRecent,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var result = await _ratingService.GetRatingsAsync(
            entityId, entityType, minValue, maxValue, withReviewOnly, verifiedOnly, sortOrder, skip, take, ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var dtos = result.Value.Select(RatingDto.FromEntity);
        return Ok(dtos);
    }

    /// <summary>
    /// Get rating summary for an entity
    /// </summary>
    [HttpGet("summary/{entityType}/{entityId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RatingSummaryDto), 200)]
    public async Task<IActionResult> GetSummary(string entityType, Guid entityId, CancellationToken ct)
    {
        var result = await _ratingService.GetSummaryAsync(entityId, entityType, ct).ConfigureAwait(false);
        return result.IsSuccess 
            ? Ok(RatingSummaryDto.FromEntity(result.Value)) 
            : BadRequest(result.Error);
    }

    /// <summary>
    /// Get summaries for multiple entities (batch)
    /// </summary>
    [HttpPost("summaries/batch")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Dictionary<Guid, RatingSummaryDto>), 200)]
    public async Task<IActionResult> GetSummariesBatch([FromBody] BatchSummaryRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new GetRatingSummariesBatchEndpointCommand(
            request.EntityIds.ToArray(), request.EntityType), ct).ConfigureAwait(false);
        
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var dtos = result.Value.ToDictionary(
            kvp => kvp.Key, 
            kvp => RatingSummaryDto.FromEntity(kvp.Value));

        return Ok(dtos);
    }

    /// <summary>
    /// Get current user's ratings for multiple entities (batch)
    /// </summary>
    [HttpPost("my/batch")]
    [ProducesResponseType(typeof(Dictionary<Guid, RatingDto>), 200)]
    public async Task<IActionResult> GetMyRatingsBatch([FromBody] BatchSummaryRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new GetUserRatingsBatchEndpointCommand(
            request.EntityIds.ToArray(), request.EntityType), ct).ConfigureAwait(false);
        
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var dtos = result.Value.ToDictionary(
            kvp => kvp.Key, 
            kvp => RatingDto.FromEntity(kvp.Value));

        return Ok(dtos);
    }

    /// <summary>
    /// Get all ratings by a specific user
    /// </summary>
    [HttpGet("user/{userId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<RatingDto>), 200)]
    public async Task<IActionResult> GetUserRatings(
        Guid userId,
        [FromQuery] string? entityType = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var result = await _ratingService.GetUserRatingsAsync(userId, entityType, skip, take, ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var dtos = result.Value.Select(RatingDto.FromEntity);
        return Ok(dtos);
    }

    /// <summary>
    /// Get ratings count for an entity
    /// </summary>
    [HttpGet("count/{entityType}/{entityId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(int), 200)]
    public async Task<IActionResult> GetCount(string entityType, Guid entityId, CancellationToken ct)
    {
        var result = await _ratingService.GetCountAsync(entityId, entityType, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    /// <summary>
    /// Mark a review as helpful or not helpful
    /// </summary>
    [HttpPost("{ratingId:guid}/helpful")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> VoteHelpful(Guid ratingId, [FromBody] VoteHelpfulRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new VoteRatingHelpfulEndpointCommand(
            ratingId, request.IsHelpful), ct).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    /// <summary>
    /// Remove helpful vote
    /// </summary>
    [HttpDelete("{ratingId:guid}/helpful")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RemoveHelpfulVote(Guid ratingId, CancellationToken ct)
    {
        var result = await _sender.Send(new RemoveRatingHelpfulVoteEndpointCommand(ratingId), ct).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : NotFound(result.Error);
    }

    /// <summary>
    /// Report a review
    /// </summary>
    [HttpPost("{ratingId:guid}/report")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Report(Guid ratingId, [FromBody] ReportRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new ReportRatingEndpointCommand(
            ratingId, request.Reason), ct).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : NotFound(result.Error);
    }

    /// <summary>
    /// Get top-rated entities of a type
    /// </summary>
    [HttpGet("top/{entityType}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<RatingSummaryDto>), 200)]
    public async Task<IActionResult> GetTopRated(
        string entityType,
        [FromQuery] int minRatings = 5,
        [FromQuery] int take = 10,
        CancellationToken ct = default)
    {
        var result = await _ratingService.GetTopRatedAsync(entityType, minRatings, take, ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var dtos = result.Value.Select(RatingSummaryDto.FromEntity);
        return Ok(dtos);
    }

    /// <summary>
    /// Get recent reviews
    /// </summary>
    [HttpGet("recent-reviews")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<RatingDto>), 200)]
    public async Task<IActionResult> GetRecentReviews(
        [FromQuery] string? entityType = null,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var result = await _ratingService.GetRecentReviewsAsync(entityType, take, ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var dtos = result.Value.Select(RatingDto.FromEntity);
        return Ok(dtos);
    }

    // ─── Admin Endpoints ─────────────────────────────────────────────────────────

    /// <summary>
    /// Get ratings pending moderation (Admin)
    /// </summary>
    [HttpGet("admin/moderation")]
    [Authorize(Policy = "TenantAdmin")]
    [ProducesResponseType(typeof(IEnumerable<RatingDto>), 200)]
    public async Task<IActionResult> GetPendingModeration(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var result = await _ratingService.GetPendingModerationAsync(skip, take, ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var dtos = result.Value.Select(RatingDto.FromEntity);
        return Ok(dtos);
    }

    /// <summary>
    /// Approve a rating (Admin)
    /// </summary>
    [HttpPost("admin/{ratingId:guid}/approve")]
    [Authorize(Policy = "TenantAdmin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Approve(Guid ratingId, CancellationToken ct)
    {
        var result = await _sender.Send(new ApproveRatingEndpointCommand(ratingId), ct).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : NotFound(result.Error);
    }

    /// <summary>
    /// Reject a rating (Admin)
    /// </summary>
    [HttpPost("admin/{ratingId:guid}/reject")]
    [Authorize(Policy = "TenantAdmin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Reject(Guid ratingId, CancellationToken ct)
    {
        var result = await _sender.Send(new RejectRatingEndpointCommand(ratingId), ct).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : NotFound(result.Error);
    }

    /// <summary>
    /// Admin delete a rating
    /// </summary>
    [HttpDelete("admin/{ratingId:guid}")]
    [Authorize(Policy = "TenantAdmin")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AdminDelete(Guid ratingId, CancellationToken ct)
    {
        var result = await _sender.Send(new AdminDeleteRatingEndpointCommand(ratingId), ct).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : NotFound(result.Error);
    }

    /// <summary>
    /// Force recalculate rating summary (Admin)
    /// </summary>
    [HttpPost("admin/recalculate/{entityType}/{entityId:guid}")]
    [Authorize(Policy = "TenantAdmin")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RecalculateSummary(string entityType, Guid entityId, CancellationToken ct)
    {
        var result = await _sender.Send(new RecalculateRatingSummaryEndpointCommand(
            entityId, entityType), ct).ConfigureAwait(false);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────

public sealed record CreateRatingRequest(
    Guid EntityId,
    string EntityType,
    int Value,
    string? ReviewText = null,
    string? ReviewTitle = null
);

public sealed record BatchSummaryRequest(
    IEnumerable<Guid> EntityIds,
    string EntityType
);

public sealed record VoteHelpfulRequest(bool IsHelpful);

public sealed record ReportRequest(string Reason);

public sealed record RatingDto(
    Guid Id,
    Guid UserId,
    Guid EntityId,
    string EntityType,
    int Value,
    string? ReviewTitle,
    string? ReviewText,
    bool IsVerified,
    int HelpfulCount,
    RatingModerationStatus ModerationStatus,
    DateTime CreatedAt,
    DateTime? EditedAt
)
{
    public static RatingDto FromEntity(Rating r) => new(
        r.Id,
        r.UserId,
        r.EntityId,
        r.EntityType,
        r.Value,
        r.ReviewTitle,
        r.ReviewText,
        r.IsVerified,
        r.HelpfulCount,
        r.ModerationStatus,
        r.CreatedAt,
        r.EditedAt
    );
}

public sealed record RatingSummaryDto(
    Guid EntityId,
    string EntityType,
    decimal AverageRating,
    int TotalRatings,
    int OneStar,
    int TwoStar,
    int ThreeStar,
    int FourStar,
    int FiveStar,
    int TotalReviews,
    Dictionary<int, double> Distribution
)
{
    public static RatingSummaryDto FromEntity(RatingSummary s) => new(
        s.EntityId,
        s.EntityType,
        s.AverageRating,
        s.TotalRatings,
        s.OneStar,
        s.TwoStar,
        s.ThreeStar,
        s.FourStar,
        s.FiveStar,
        s.TotalReviews,
        s.GetDistributionPercentages()
    );
}
