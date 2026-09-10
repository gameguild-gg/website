using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Social.Feed;

[ApiController]
[Authorize]
[Route("api/social/feed")]
public sealed class SocialFeedController(
    ISocialFeedQueryService feed,
    IActorContextAccessor actorContextAccessor) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SocialFeedPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SocialFeedPageDto>> Get(
        [FromQuery] string scope = "for-you",
        [FromQuery] string? cursor = null,
        [FromQuery] int take = 20,
        [FromQuery] string? tag = null,
        CancellationToken cancellationToken = default)
    {
        var actorId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!actorId.HasValue || actorId.Value == Guid.Empty)
            return Unauthorized();

        if (!TryParseScope(scope, out var parsedScope))
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid feed scope",
                Detail = "Use for-you, following, community, or saved.",
                Status = StatusCodes.Status400BadRequest
            });

        try
        {
            return await feed.GetAsync(
                actorId.Value,
                parsedScope,
                cursor,
                take,
                tag,
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidFeedCursorException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid feed cursor",
                Detail = exception.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    internal static bool TryParseScope(string? value, out FeedScope scope)
    {
        scope = value?.Trim().ToLowerInvariant() switch
        {
            "for-you" or "foryou" => FeedScope.ForYou,
            "following" => FeedScope.Following,
            "community" => FeedScope.Community,
            "saved" => FeedScope.Saved,
            _ => (FeedScope)(-1)
        };
        return Enum.IsDefined(scope);
    }
}
