using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Social.Posts.Commands;
using GameGuild.Social.Posts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Social.Posts.Controllers;

/// <summary>
/// REST API for post interactions: likes, pins, shares, views, statistics, and following.
/// </summary>
[Route("api/v1/posts")]
[Authorize]
public class PostInteractionsController(
    IPostService postService,
    IActorContextAccessor actorContextAccessor,
    ISender sender)
    : BaseApiController
{
    private Guid GetCurrentUserId()
    {
        var actor = actorContextAccessor.ActorContext;
        return actor?.SubjectIdAsGuid ?? Guid.Empty;
    }

    #region Reactions

    /// <summary>Toggle like on a post</summary>
    [HttpPost("{postId:guid}/like")]
    public async Task<IActionResult> ToggleLike(Guid postId, [FromQuery] string reactionType = "like", CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await sender.Send(
            new TogglePostLikeEndpointCommand(postId, userId, reactionType),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(new { Liked = result.Value })
            : BadRequest(result.Error);
    }

    /// <summary>Toggle pin on a post (author only)</summary>
    [HttpPost("{postId:guid}/pin")]
    public async Task<IActionResult> TogglePin(Guid postId, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await sender.Send(
            new TogglePostPinEndpointCommand(postId, userId),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(new { Pinned = result.Value })
            : MapMutationFailure(result.Error);
    }

    /// <summary>Repost a public post once for the current user</summary>
    [HttpPost("{postId:guid}/reposts")]
    public async Task<IActionResult> CreateRepost(
        Guid postId,
        [FromBody] CreateRepostRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await sender.Send(
            new CreateRepostEndpointCommand(postId, userId, request?.Content),
            cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Created($"/api/v1/posts/{result.Value!.Id}", PostMappings.MapToDto(result.Value))
            : MapMutationFailure(result.Error);
    }

    /// <summary>Record a share of the post</summary>
    [HttpPost("{postId:guid}/share")]
    [AllowAnonymous]
    public async Task<IActionResult> Share(Guid postId, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new SharePostEndpointCommand(postId),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok()
            : BadRequest(result.Error);
    }

    /// <summary>Record a view of the post</summary>
    [HttpPost("{postId:guid}/view")]
    [AllowAnonymous]
    public async Task<IActionResult> RecordView(
        Guid postId,
        [FromHeader(Name = "X-Forwarded-For")] string? ipAddress = null,
        [FromHeader(Name = "User-Agent")] string? userAgent = null,
        [FromHeader(Name = "Referer")] string? referrer = null,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await sender.Send(new RecordPostViewEndpointCommand(
            postId,
            userId == Guid.Empty ? null : userId,
            ipAddress,
            userAgent,
            referrer), cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Ok()
            : BadRequest(result.Error);
    }

    /// <summary>Get statistics for a post</summary>
    [HttpGet("{postId:guid}/statistics")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStatistics(Guid postId, CancellationToken cancellationToken = default)
    {
        var result = await postService.GetPostStatisticsAsync(postId, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(PostMappings.MapStatisticsToDto(result.Value!))
            : NotFound(result.Error);
    }

    #endregion

    #region Following

    /// <summary>Follow a post for notifications</summary>
    [HttpPost("{postId:guid}/follow")]
    public async Task<IActionResult> FollowPost(Guid postId, [FromBody] FollowPostRequest? request = null, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await sender.Send(new FollowPostEndpointCommand(
            postId,
            userId,
            request?.NotifyOnComments ?? true,
            request?.NotifyOnLikes ?? false,
            request?.NotifyOnShares ?? false,
            request?.NotifyOnUpdates ?? true), cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? Ok(PostMappings.MapFollowerToDto(result.Value!))
            : BadRequest(result.Error);
    }

    /// <summary>Unfollow a post</summary>
    [HttpDelete("{postId:guid}/follow")]
    public async Task<IActionResult> UnfollowPost(Guid postId, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await sender.Send(
            new UnfollowPostEndpointCommand(postId, userId),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? NoContent()
            : BadRequest(result.Error);
    }

    /// <summary>Check if current user is following a post</summary>
    [HttpGet("{postId:guid}/follow")]
    public async Task<IActionResult> IsFollowing(Guid postId, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await postService.IsFollowingPostAsync(postId, userId, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(new { IsFollowing = result.Value })
            : BadRequest(result.Error);
    }

    #endregion

    private IActionResult MapMutationFailure(Error error) => error.Type switch
    {
        ErrorType.NotFound => NotFound(error),
        ErrorType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, error),
        _ => BadRequest(error)
    };
}

#region Request DTOs

public sealed record FollowPostRequest
{
    public bool NotifyOnComments { get; init; } = true;
    public bool NotifyOnLikes { get; init; } = false;
    public bool NotifyOnShares { get; init; } = false;
    public bool NotifyOnUpdates { get; init; } = true;
}

public sealed record CreateRepostRequest(string? Content);

#endregion
