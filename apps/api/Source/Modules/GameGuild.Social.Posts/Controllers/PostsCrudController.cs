using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Social.Posts.Commands;
using GameGuild.Social.Posts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Social.Posts.Controllers;

/// <summary>
/// REST API for post CRUD operations, feed, and search.
/// </summary>
[Route("api/v1/posts")]
[Authorize]
public class PostsCrudController(
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

    #region Listing & Feed

    /// <summary>Get paginated list of public posts</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPosts([FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var result = await postService.GetPublicPostsAsync(skip, take, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result.Value!.Select(PostMappings.MapToDto))
            : BadRequest(result.Error);
    }

    /// <summary>Get a single post by ID</summary>
    [HttpGet("{postId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPost(Guid postId, CancellationToken cancellationToken = default)
    {
        var result = await postService.GetPostByIdAsync(postId, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(PostMappings.MapToDto(result.Value!))
            : NotFound(result.Error);
    }

    /// <summary>Get posts for the current user's feed</summary>
    [HttpGet("feed")]
    public async Task<IActionResult> GetFeed([FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await postService.GetFeedPostsAsync(userId, skip, take, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result.Value!.Select(PostMappings.MapToDto))
            : BadRequest(result.Error);
    }

    /// <summary>Get trending posts</summary>
    [HttpGet("trending")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTrending([FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var result = await postService.GetTrendingPostsAsync(skip, take, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result.Value!.Select(PostMappings.MapToDto))
            : BadRequest(result.Error);
    }

    /// <summary>Get posts by a specific author</summary>
    [HttpGet("author/{authorId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByAuthor(Guid authorId, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var result = await postService.GetPostsByAuthorAsync(authorId, skip, take, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result.Value!.Select(PostMappings.MapToDto))
            : BadRequest(result.Error);
    }

    /// <summary>Get current user's posts</summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMyPosts([FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await postService.GetPostsByAuthorAsync(userId, skip, take, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result.Value!.Select(PostMappings.MapToDto))
            : BadRequest(result.Error);
    }

    /// <summary>Search posts by content</summary>
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Search query is required");

        var result = await postService.SearchPostsAsync(q, skip, take, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result.Value!.Select(PostMappings.MapToDto))
            : BadRequest(result.Error);
    }

    #endregion

    #region Create / Update / Delete

    /// <summary>Create a new post</summary>
    [HttpPost]
    public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await sender.Send(new CreatePostEndpointCommand(
            userId,
            request.Content,
            request.Visibility,
            request.MediaUrl,
            request.MediaType,
            request.TenantId,
            request.Tags), cancellationToken).ConfigureAwait(false);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetPost), new { postId = result.Value!.Id }, PostMappings.MapToDto(result.Value))
            : BadRequest(result.Error);
    }

    /// <summary>Update a post</summary>
    [HttpPut("{postId:guid}")]
    public async Task<IActionResult> UpdatePost(Guid postId, [FromBody] UpdatePostRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await sender.Send(
            new UpdatePostEndpointCommand(postId, userId, request.Content),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(PostMappings.MapToDto(result.Value!))
            : MapMutationFailure(result.Error);
    }

    /// <summary>Delete a post</summary>
    [HttpDelete("{postId:guid}")]
    public async Task<IActionResult> DeletePost(Guid postId, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized();

        var result = await sender.Send(
            new DeletePostEndpointCommand(postId, userId),
            cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? NoContent()
            : MapMutationFailure(result.Error);
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

public sealed record CreatePostRequest
{
    public string Content { get; init; } = string.Empty;
    public PostVisibility Visibility { get; init; } = PostVisibility.Public;
    public string? MediaUrl { get; init; }
    public MediaType? MediaType { get; init; }
    public Guid? TenantId { get; init; }
    public string[]? Tags { get; init; }
}

public sealed record UpdatePostRequest
{
    public string Content { get; init; } = string.Empty;
}

#endregion
