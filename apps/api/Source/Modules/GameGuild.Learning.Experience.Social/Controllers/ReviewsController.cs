using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Authorization;
using GameGuild.Learning.Abstractions;
using GameGuild.Learning.Attributes;
using GameGuild.Learning.Experience.Social.Services;
using GameGuild.Learning.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Learning.Experience.Social.Controllers;

/// <summary>
/// API controller for course review operations
/// </summary>
[ApiController]
[Route("api/social")]
[Authorize]
[LxpCapabilityFilter]
[LxpCapability(LxpCapabilities.Social)]
public class ReviewsController : LearningControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ISender _sender;

    public ReviewsController(
        IReviewService reviewService,
        ISender sender,
        IActorContextAccessor actorContextAccessor) : base(actorContextAccessor)
    {
        _reviewService = reviewService;
        _sender = sender;
    }

    /// <summary>
    /// Creates a new course review
    /// </summary>
    [HttpPost("reviews")]
    [ProducesResponseType(typeof(CourseReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReview(
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _sender.Send(new CreateReviewCommand(
            request.CourseId,
            userId,
            request.Rating,
            request.Title,
            request.Content,
            request.EnrollmentId), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(nameof(GetReview), new { id = result.Value.Id }, MapToReviewDto(result.Value));
    }

    /// <summary>
    /// Gets a review by ID
    /// </summary>
    [HttpGet("reviews/{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CourseReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReview(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _reviewService.GetReviewByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(MapToReviewDto(result.Value));
    }

    /// <summary>
    /// Gets all reviews for a course
    /// </summary>
    [HttpGet("courses/{courseId:guid}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<CourseReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourseReviews(
        Guid courseId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] bool approvedOnly = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _reviewService.GetCourseReviewsAsync(courseId, skip, take, approvedOnly, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var dtos = result.Value.Select(MapToReviewDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Gets the current user's reviews
    /// </summary>
    [HttpGet("reviews/me")]
    [ProducesResponseType(typeof(IEnumerable<CourseReviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyReviews(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _reviewService.GetUserReviewsAsync(userId, skip, take, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var dtos = result.Value.Select(MapToReviewDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Marks a review as helpful
    /// </summary>
    [HttpPost("reviews/{id:guid}/helpful")]
    [ProducesResponseType(typeof(CourseReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkReviewHelpful(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new MarkReviewHelpfulCommand(id), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(MapToReviewDto(result.Value));
    }

    /// <summary>
    /// Deletes a review (owner only)
    /// </summary>
    [HttpDelete("reviews/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReview(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _sender.Send(new DeleteReviewCommand(id, userId), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Gets rating statistics for a course
    /// </summary>
    [HttpGet("courses/{courseId:guid}/rating-stats")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CourseRatingStats), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourseRatingStats(Guid courseId, CancellationToken cancellationToken = default)
    {
        var result = await _reviewService.GetCourseRatingStatsAsync(courseId, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Approves a review (admin only)
    /// </summary>
    [HttpPost("reviews/{id:guid}/approve")]
    [RequireContentTypePermission<CourseReview>(PermissionType.Edit)]
    [ProducesResponseType(typeof(CourseReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveReview(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new ApproveReviewCommand(id), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(MapToReviewDto(result.Value));
    }

    /// <summary>
    /// Features a review (admin only)
    /// </summary>
    [HttpPost("reviews/{id:guid}/feature")]
    [RequireContentTypePermission<CourseReview>(PermissionType.Edit)]
    [ProducesResponseType(typeof(CourseReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FeatureReview(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new FeatureReviewCommand(id), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(MapToReviewDto(result.Value));
    }

    /// <summary>
    /// Updates review approval and storefront featured state.
    /// </summary>
    [HttpPatch("reviews/{id:guid}/moderation")]
    [RequireContentTypePermission<CourseReview>(PermissionType.Edit)]
    [ProducesResponseType(typeof(CourseReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReviewModeration(
        Guid id,
        [FromBody] UpdateReviewModerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new UpdateReviewModerationCommand(
            id,
            request.IsApproved,
            request.IsFeatured), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(MapToReviewDto(result.Value));
    }

    private static CourseReviewDto MapToReviewDto(CourseReview review) =>
        new(review.Id,
            review.CourseId,
            review.UserId,
            review.Rating,
            review.Title,
            review.Content,
            review.IsVerifiedPurchase,
            review.HelpfulCount,
            review.IsApproved,
            review.IsFeatured,
            review.CreatedAt);
}

public sealed record UpdateReviewModerationRequest(bool IsApproved, bool IsFeatured);
