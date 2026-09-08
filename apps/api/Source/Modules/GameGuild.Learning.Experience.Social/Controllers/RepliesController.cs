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
/// API controller for discussion reply operations
/// </summary>
[ApiController]
[Route("api/social")]
[Authorize]
[LxpCapabilityFilter]
[LxpCapability(LxpCapabilities.Social)]
public class RepliesController : LearningControllerBase
{
    private readonly IReplyService _replyService;
    private readonly ISender _sender;

    public RepliesController(
        IReplyService replyService,
        ISender sender,
        IActorContextAccessor actorContextAccessor) : base(actorContextAccessor)
    {
        _replyService = replyService;
        _sender = sender;
    }

    /// <summary>
    /// Creates a reply to a discussion
    /// </summary>
    [HttpPost("discussions/{discussionId:guid}/replies")]
    [ProducesResponseType(typeof(DiscussionReplyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReply(
        Guid discussionId,
        [FromBody] CreateReplyRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _sender.Send(new CreateReplyCommand(
            discussionId,
            userId,
            request.Content,
            request.ParentReplyId), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(nameof(GetDiscussionReplies), new { discussionId }, MapToReplyDto(result.Value));
    }

    /// <summary>
    /// Gets replies for a discussion
    /// </summary>
    [HttpGet("discussions/{discussionId:guid}/replies")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<DiscussionReplyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDiscussionReplies(
        Guid discussionId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _replyService.GetDiscussionRepliesAsync(discussionId, skip, take, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        var dtos = result.Value.Select(MapToReplyDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Accepts a reply as the answer (discussion author only)
    /// </summary>
    [HttpPost("replies/{id:guid}/accept")]
    [ProducesResponseType(typeof(DiscussionReplyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptReplyAsAnswer(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _sender.Send(new AcceptReplyAsAnswerCommand(id, userId), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(MapToReplyDto(result.Value));
    }

    /// <summary>
    /// Upvotes a reply
    /// </summary>
    [HttpPost("replies/{id:guid}/upvote")]
    [ProducesResponseType(typeof(DiscussionReplyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpvoteReply(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new UpvoteReplyCommand(id), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return Ok(MapToReplyDto(result.Value));
    }

    /// <summary>
    /// Deletes a reply (owner only)
    /// </summary>
    [HttpDelete("replies/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReply(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetRequiredUserId();
        var result = await _sender.Send(new DeleteReplyCommand(id, userId), cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return NotFound(result.Error);
        }

        return NoContent();
    }

    private static DiscussionReplyDto MapToReplyDto(DiscussionReply reply) =>
        new(reply.Id,
            reply.DiscussionId,
            reply.AuthorId,
            reply.ParentReplyId,
            reply.Content,
            reply.IsAcceptedAnswer,
            reply.UpvoteCount,
            reply.CreatedAt);
}
