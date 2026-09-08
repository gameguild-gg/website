using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Abstractions;
using GameGuild.Learning.Attributes;
using GameGuild.Learning.Experience.Social.Services;
using GameGuild.Learning.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Learning.Experience.Social.Controllers;

/// <summary>
/// API controller for course wishlist (bookmark) operations
/// </summary>
[ApiController]
[Route("api/social")]
[Authorize]
[LxpCapabilityFilter]
[LxpCapability(LxpCapabilities.Social)]
public class WishlistsController : LearningControllerBase
{
    private readonly IWishlistService _wishlistService;
    private readonly ISender _sender;

    public WishlistsController(
        IWishlistService wishlistService,
        ISender sender,
        IActorContextAccessor actorContextAccessor) : base(actorContextAccessor)
    {
        _wishlistService = wishlistService;
        _sender = sender;
    }

    /// <summary>
    /// Adds a course to the current user's wishlist
    /// </summary>
    [HttpPost("wishlist/{courseId:guid}")]
    [ProducesResponseType(typeof(CourseWishlistDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddToWishlist(
        Guid courseId,
        [FromQuery] bool notifyOnSale = true,
        [FromQuery] bool notifyOnUpdate = false,
        CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _sender.Send(new AddToWishlistCommand(courseId, userId, notifyOnSale, notifyOnUpdate), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(nameof(GetMyWishlist), null, MapToWishlistDto(result.Value));
    }

    /// <summary>
    /// Removes a course from the current user's wishlist
    /// </summary>
    [HttpDelete("wishlist/{courseId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFromWishlist(Guid courseId, CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _sender.Send(new RemoveFromWishlistCommand(courseId, userId), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Gets the current user's wishlist
    /// </summary>
    [HttpGet("wishlist/me")]
    [ProducesResponseType(typeof(IEnumerable<CourseWishlistDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyWishlist(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _wishlistService.GetUserWishlistAsync(userId, skip, take, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var dtos = result.Value.Select(MapToWishlistDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Checks if a course is in the current user's wishlist
    /// </summary>
    [HttpGet("wishlist/{courseId:guid}/check")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> IsInWishlist(Guid courseId, CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _wishlistService.IsInWishlistAsync(courseId, userId, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return Ok(new { isInWishlist = result.Value });
    }

    /// <summary>
    /// Updates wishlist notification preferences
    /// </summary>
    [HttpPut("wishlist/{courseId:guid}/preferences")]
    [ProducesResponseType(typeof(CourseWishlistDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWishlistPreferences(
        Guid courseId,
        [FromBody] WishlistPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _sender.Send(new UpdateWishlistPreferencesCommand(
            courseId, userId, request.NotifyOnSale, request.NotifyOnUpdate), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(MapToWishlistDto(result.Value));
    }

    private static CourseWishlistDto MapToWishlistDto(CourseWishlist wishlist) =>
        new(wishlist.Id,
            wishlist.CourseId,
            wishlist.UserId,
            wishlist.NotifyOnSale,
            wishlist.NotifyOnUpdate,
            wishlist.CreatedAt);
}
