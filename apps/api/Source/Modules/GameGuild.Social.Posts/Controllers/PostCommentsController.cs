using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Social.Posts.Commands;
using GameGuild.Social.Posts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Social.Posts.Controllers;

/// <summary>
/// REST API for post comments and tags.
/// </summary>
[Route("api/v1/posts")]
[Authorize]
public class PostCommentsController(
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

    #region Comments

    /// <summary>Get comments for a post</summary>
    [HttpGet("{postId:guid}/comments")]
    [AllowAnonymous]
    public async Task<IActionResult> GetComments(Guid postId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var result = await postService.GetPostCommentsAsync(postId, skip, take, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result.Value!.Select(PostMappings.MapCommentToDto))
            : BadRequest(result.Error);
    }

    /// <summary>Add a comment to a post</summary>
    [HttpPost("{postId:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid postId, [FromBody] AddCommentRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await sender.Send(
            new AddPostCommentEndpointCommand(postId, userId, request.Content, request.ParentCommentId),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Created($"/api/v1/posts/{postId}/comments/{result.Value!.Id}", PostMappings.MapCommentToDto(result.Value))
            : BadRequest(result.Error);
    }

    /// <summary>Update a comment</summary>
    [HttpPut("{postId:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> UpdateComment(Guid postId, Guid commentId, [FromBody] UpdateCommentRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await sender.Send(
            new UpdatePostCommentEndpointCommand(commentId, userId, request.Content),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(PostMappings.MapCommentToDto(result.Value!))
            : MapMutationFailure(result.Error);
    }

    /// <summary>Delete a comment</summary>
    [HttpDelete("{postId:guid}/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid postId, Guid commentId, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await sender.Send(
            new DeletePostCommentEndpointCommand(commentId, userId),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? NoContent()
            : MapMutationFailure(result.Error);
    }

    #endregion

    #region Tags

    /// <summary>Get popular tags</summary>
    [HttpGet("tags/popular")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPopularTags([FromQuery] int count = 20, CancellationToken cancellationToken = default)
    {
        var result = await postService.GetPopularTagsAsync(count, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result.Value!.Select(PostMappings.MapTagToDto))
            : BadRequest(result.Error);
    }

    /// <summary>Get tags for a post</summary>
    [HttpGet("{postId:guid}/tags")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPostTags(Guid postId, CancellationToken cancellationToken = default)
    {
        var result = await postService.GetPostTagsAsync(postId, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result.Value!.Select(PostMappings.MapTagToDto))
            : BadRequest(result.Error);
    }

    /// <summary>Search posts by tags</summary>
    [HttpGet("tags/search")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchByTags([FromQuery] string[] tags, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var result = await postService.GetPostsByTagsAsync(tags, skip, take, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result.Value!.Select(PostMappings.MapToDto))
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

public sealed record AddCommentRequest
{
    public string Content { get; init; } = string.Empty;
    public Guid? ParentCommentId { get; init; }
}

public sealed record UpdateCommentRequest
{
    public string Content { get; init; } = string.Empty;
}

#endregion
