using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GameGuild.API.Eventing;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;

namespace GameGuild.API;

[Microsoft.AspNetCore.Http.Tags("admin/events")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/admin/events")]
[Authorize(Policy = Policies.SystemAdmin)]
public sealed class EventTransportAdminController(IEventReplayService replayService, ISender sender) : BaseApiController
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(EventTransportStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<EventTransportStatus>> GetStatus(CancellationToken cancellationToken = default) =>
        Ok(await replayService.GetStatusAsync(cancellationToken).ConfigureAwait(false));

    [HttpGet("dead-letters")]
    [ProducesResponseType(typeof(IReadOnlyList<DeadLetterEvent>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DeadLetterEvent>>> GetDeadLetters(
        CancellationToken cancellationToken = default) =>
        Ok(await replayService.GetDeadLettersAsync(cancellationToken).ConfigureAwait(false));

    [HttpPost("{eventId:guid}:replay")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Replay(
        Guid eventId,
        [FromQuery] string? consumerName = null,
        CancellationToken cancellationToken = default) =>
        await sender.Send(new ReplayOutboxEventCommand(eventId, consumerName), cancellationToken).ConfigureAwait(false)
            ? Accepted()
            : NotFound();
}
