using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Social.Feed;

[Microsoft.AspNetCore.Http.Tags("social/stories")]
[ApiController]
[Authorize]
[Route("api/social/stories")]
public sealed class StoriesController(ISender sender, IActorContextAccessor actorContextAccessor) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StoryDto>>> GetActive(CancellationToken cancellationToken)
    {
        if (ActorId is not { } actorId)
        {
            return Unauthorized();
        }

        return Ok(await sender.Send(new GetActiveStoriesQuery(actorId), cancellationToken).ConfigureAwait(false));
    }

    [HttpPost]
    public async Task<ActionResult<StoryDto>> Create(CreateStoryRequest request, CancellationToken cancellationToken)
    {
        if (ActorId is not { } actorId)
        {
            return Unauthorized();
        }

        try
        {
            return await sender.Send(
                new CreateStoryCommand(actorId, request.AssetReferenceId, request.Caption),
                cancellationToken).ConfigureAwait(false);
        }
        catch (StoryAssetUnavailableException)
        {
            return NotFound();
        }
    }

    [HttpPost("{storyId:guid}/views")]
    public async Task<IActionResult> MarkViewed(Guid storyId, CancellationToken cancellationToken)
    {
        if (ActorId is not { } actorId)
        {
            return Unauthorized();
        }

        return await sender.Send(new MarkStoryViewedCommand(actorId, storyId), cancellationToken).ConfigureAwait(false)
            ? NoContent()
            : NotFound();
    }

    [HttpDelete("{storyId:guid}")]
    public async Task<IActionResult> Delete(Guid storyId, CancellationToken cancellationToken)
    {
        if (ActorId is not { } actorId)
        {
            return Unauthorized();
        }

        return await sender.Send(new DeleteStoryCommand(actorId, storyId), cancellationToken).ConfigureAwait(false)
            ? NoContent()
            : NotFound();
    }

    private Guid? ActorId => actorContextAccessor.ActorContext.SubjectIdAsGuid;
}
