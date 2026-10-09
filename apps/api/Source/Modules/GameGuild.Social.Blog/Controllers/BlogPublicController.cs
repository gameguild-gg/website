using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Social.Blog.Queries;
using GameGuild.Social.Blog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Social.Blog.Controllers;

/// <summary>
/// Public blog reading surface. Every action is anonymous (published content only — the queries
/// filter <c>Status == Published &amp;&amp; DeletedAt == null</c>, so drafts and unknown routes are
/// indistinguishable 404s); an authenticated viewer additionally gets muted-author filtering on
/// the comments read (invariant #10).
/// </summary>
[ApiController]
[Authorize]
[Route("api/social/blog/public")]
public sealed class BlogPublicController(
    ISender sender,
    IActorContextAccessor actorContextAccessor,
    IBlogViewCounterService viewCounter) : ControllerBase
{
    /// <summary>Public author listing by handle (published only), keyset-paged newest-first.</summary>
    [HttpGet("authors/{handle}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BlogPostSummaryPage), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuthorPosts(
        string handle,
        [FromQuery] DateTime? beforePublishedAt = null,
        [FromQuery] Guid? beforeId = null,
        CancellationToken cancellationToken = default)
        => Ok(await sender.Send(
            new ListAuthorBlogSummariesQuery(handle, beforePublishedAt, beforeId),
            cancellationToken).ConfigureAwait(false));

    /// <summary>Public post detail by (handle, slug); unpublished/missing → indistinguishable 404.</summary>
    [HttpGet("authors/{handle}/{slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BlogPostDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPostDetail(string handle, string slug, CancellationToken cancellationToken)
        => await sender.Send(new GetPublicBlogPostDetailQuery(handle, slug), cancellationToken).ConfigureAwait(false)
            is { } detail
            ? Ok(detail)
            : NotFound();

    /// <summary>Resolves a possibly-stale (handle, slug) route to the canonical route, or 404.</summary>
    [HttpGet("resolve/{handle}/{slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BlogRouteResolutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveRoute(string handle, string slug, CancellationToken cancellationToken)
        => await sender.Send(new ResolveBlogRouteForRedirectQuery(handle, slug), cancellationToken).ConfigureAwait(false)
            is { } resolution
            ? Ok(resolution)
            : NotFound();

    /// <summary>Global public blog index (published only), keyset-paged newest-first.</summary>
    [HttpGet("posts")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BlogPostSummaryPage), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIndex(
        [FromQuery] DateTime? beforePublishedAt = null,
        [FromQuery] Guid? beforeId = null,
        CancellationToken cancellationToken = default)
        => Ok(await sender.Send(
            new ListPublicBlogSummariesQuery(beforePublishedAt, beforeId),
            cancellationToken).ConfigureAwait(false));

    /// <summary>View beacon: atomically increments the published post's counter; PerIp rate-limited.</summary>
    [HttpPost("posts/{id:guid}/views")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PerIp)]
    [NoBusinessMutationEndpoint("Atomic view-counter increment over a published post; no business state transition.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordView(Guid id, CancellationToken cancellationToken)
        => await viewCounter.IncrementAsync(id, cancellationToken).ConfigureAwait(false)
            ? NoContent()
            : NotFound();

    /// <summary>Public comments of a published post, oldest-first; unpublished/missing post → 404.</summary>
    [HttpGet("posts/{id:guid}/comments")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(BlogCommentPage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComments(
        Guid id,
        [FromQuery] DateTime? afterCreatedAt = null,
        [FromQuery] Guid? afterId = null,
        CancellationToken cancellationToken = default)
    {
        if (!await sender.Send(new IsBlogPostPublishedQuery(id), cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        var viewerId = ViewerIdOrNull();
        return Ok(await sender.Send(
            new ListBlogCommentsQuery(id, viewerId, afterCreatedAt, afterId),
            cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    ///     Muted-author filtering applies only to authenticated viewers (actor context);
    ///     anonymous viewers see all non-deleted comments.
    /// </summary>
    private Guid? ViewerIdOrNull()
    {
        var actor = actorContextAccessor.ActorContext;
        return actor is { IsAuthenticated: true, SubjectIdAsGuid: { } actorId } && actorId != Guid.Empty
            ? actorId
            : null;
    }
}
