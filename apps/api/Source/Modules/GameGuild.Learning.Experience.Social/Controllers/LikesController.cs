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
/// API controller for course like (social proof) operations
/// </summary>
[ApiController]
[Route("api/social")]
[Authorize]
[LxpCapabilityFilter]
[LxpCapability(LxpCapabilities.Social)]
public class LikesController : LearningControllerBase
{
    private readonly ILikeService _likeService;
    private readonly ISender _sender;

    public LikesController(
        ILikeService likeService,
        ISender sender,
        IActorContextAccessor actorContextAccessor) : base(actorContextAccessor)
    {
        _likeService = likeService;
        _sender = sender;
    }

    /// <summary>
    /// Likes a course
    /// </summary>
    [HttpPost("courses/{courseId:guid}/like")]
    [ProducesResponseType(typeof(CourseLikeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LikeCourse(Guid courseId, CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _sender.Send(new LikeCourseCommand(courseId, userId, null), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(nameof(GetLikedCourses), null, MapToLikeDto(result.Value));
    }

    /// <summary>
    /// Unlikes a course
    /// </summary>
    [HttpDelete("courses/{courseId:guid}/like")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlikeCourse(Guid courseId, CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _sender.Send(new UnlikeCourseCommand(courseId, userId), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Checks if the current user has liked a course
    /// </summary>
    [HttpGet("courses/{courseId:guid}/like/check")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> HasLikedCourse(Guid courseId, CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _likeService.HasUserLikedCourseAsync(courseId, userId, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return Ok(new { hasLiked = result.Value });
    }

    /// <summary>
    /// Gets the like count for a course
    /// </summary>
    [HttpGet("courses/{courseId:guid}/like/count")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourseLikeCount(Guid courseId, CancellationToken cancellationToken = default)
    {
        var result = await _likeService.GetCourseLikeCountAsync(courseId, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return Ok(new { likeCount = result.Value });
    }

    /// <summary>
    /// Gets the current user's liked courses
    /// </summary>
    [HttpGet("likes/me")]
    [ProducesResponseType(typeof(IEnumerable<CourseLikeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLikedCourses(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _likeService.GetUserLikedCoursesAsync(userId, skip, take, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var dtos = result.Value.Select(MapToLikeDto);
        return Ok(dtos);
    }

    private static CourseLikeDto MapToLikeDto(CourseLike like) =>
        new(like.Id,
            like.CourseId,
            like.UserId,
            like.CreatedAt);
}
