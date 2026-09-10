using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Social.Feed;

[Microsoft.AspNetCore.Http.Tags("social/saved-posts")]
[ApiController]
[Authorize]
[Route("api/social/saved-posts")]
public sealed class SavedPostsController(
    ISender sender,
    IActorContextAccessor actorContextAccessor) : ControllerBase
{
    [HttpGet("{postId:guid}")]
    public async Task<ActionResult<SavedPostStateDto>> Get(Guid postId, CancellationToken cancellationToken)
    {
        if (ActorId is not { } actorId)
        {
            return Unauthorized();
        }

        return await sender.Send(new GetSavedPostStateQuery(actorId, postId), cancellationToken).ConfigureAwait(false);
    }

    [HttpPut("{postId:guid}")]
    public async Task<ActionResult<SavedPostStateDto>> Save(Guid postId, CancellationToken cancellationToken)
    {
        if (ActorId is not { } actorId)
        {
            return Unauthorized();
        }

        try
        {
            return await sender.Send(new SavePostCommand(actorId, postId), cancellationToken).ConfigureAwait(false);
        }
        catch (SavedPostUnavailableException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{postId:guid}")]
    public async Task<IActionResult> Unsave(Guid postId, CancellationToken cancellationToken)
    {
        if (ActorId is not { } actorId)
        {
            return Unauthorized();
        }

        await sender.Send(new UnsavePostCommand(actorId, postId), cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private Guid? ActorId => actorContextAccessor.ActorContext.SubjectIdAsGuid;
}
