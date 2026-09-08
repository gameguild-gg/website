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
/// API controller for course discussion operations
/// </summary>
[ApiController]
[Route("api/social")]
[Authorize]
[LxpCapabilityFilter]
[LxpCapability(LxpCapabilities.Social)]
public class DiscussionsController : LearningControllerBase
{
    private readonly IDiscussionService _discussionService;
    private readonly ISender _sender;

    public DiscussionsController(
        IDiscussionService discussionService,
        ISender sender,
        IActorContextAccessor actorContextAccessor) : base(actorContextAccessor)
    {
        _discussionService = discussionService;
        _sender = sender;
    }

    /// <summary>
    /// Creates a new discussion thread
    /// </summary>
    [HttpPost("discussions")]
    [ProducesResponseType(typeof(CourseDiscussionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDiscussion(
        [FromBody] CreateDiscussionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _sender.Send(new CreateDiscussionCommand(
            request.CourseId,
            userId,
            request.Title,
            request.Content,
            request.ContentId), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(nameof(GetDiscussion), new { id = result.Value.Id }, MapToDiscussionDto(result.Value));
    }

    /// <summary>
    /// Gets a discussion by ID
    /// </summary>
    [HttpGet("discussions/{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CourseDiscussionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDiscussion(Guid id, CancellationToken cancellationToken = default)
    {
        // Increment view count
        await _discussionService.IncrementDiscussionViewsAsync(id, cancellationToken).ConfigureAwait(false);

        var result = await _discussionService.GetDiscussionByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(MapToDiscussionDto(result.Value));
    }

    /// <summary>
    /// Gets discussions for a course
    /// </summary>
    [HttpGet("courses/{courseId:guid}/discussions")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<CourseDiscussionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCourseDiscussions(
        Guid courseId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] bool pinnedFirst = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _discussionService.GetCourseDiscussionsAsync(courseId, skip, take, pinnedFirst, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var dtos = result.Value.Select(MapToDiscussionDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Gets discussions for specific content within a course
    /// </summary>
    [HttpGet("courses/{courseId:guid}/content/{contentId:guid}/discussions")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<CourseDiscussionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContentDiscussions(
        Guid courseId,
        Guid contentId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _discussionService.GetContentDiscussionsAsync(courseId, contentId, skip, take, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var dtos = result.Value.Select(MapToDiscussionDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Pins a discussion (instructor/admin only)
    /// </summary>
    [HttpPost("discussions/{id:guid}/pin")]
    [ProducesResponseType(typeof(CourseDiscussionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PinDiscussion(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new PinDiscussionCommand(id), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(MapToDiscussionDto(result.Value));
    }

    /// <summary>
    /// Unpins a discussion
    /// </summary>
    [HttpPost("discussions/{id:guid}/unpin")]
    [ProducesResponseType(typeof(CourseDiscussionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnpinDiscussion(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new UnpinDiscussionCommand(id), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(MapToDiscussionDto(result.Value));
    }

    /// <summary>
    /// Marks a discussion as resolved
    /// </summary>
    [HttpPost("discussions/{id:guid}/resolve")]
    [ProducesResponseType(typeof(CourseDiscussionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkDiscussionResolved(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new MarkDiscussionResolvedCommand(id), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(MapToDiscussionDto(result.Value));
    }

    /// <summary>
    /// Deletes a discussion (owner only)
    /// </summary>
    [HttpDelete("discussions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDiscussion(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _sender.Send(new DeleteDiscussionCommand(id, userId), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return NoContent();
    }

    private static CourseDiscussionDto MapToDiscussionDto(CourseDiscussion discussion) =>
        new(discussion.Id,
            discussion.CourseId,
            discussion.ContentId,
            discussion.AuthorId,
            discussion.Title,
            discussion.Content,
            discussion.IsPinned,
            discussion.IsResolved,
            discussion.ReplyCount,
            discussion.ViewCount,
            discussion.LastActivityAt,
            discussion.CreatedAt);
}
