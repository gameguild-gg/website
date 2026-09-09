using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Learning.Courses;

[ApiVersion("1.0")]
[Route("v{version:apiVersion}/courses/{programId:guid}/interactions/{interactionId:guid}/events")]
[Authorize]
public sealed class LessonInteractionEventsController(ISender sender) : BaseApiController
{
    [HttpPost]
    [RequireResourcePermission<PermissionType, Program>(PermissionType.Read, "programId")]
    public async Task<ActionResult<ContentInteractionEventDto>> Record(
        Guid programId,
        Guid interactionId,
        [FromBody] RecordContentInteractionEventRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RecordContentInteractionEventCommand(
                programId,
                interactionId,
                request.Type,
                request.DurationSeconds,
                request.PositionSeconds,
                request.ProgressPercentage,
                request.Payload,
                request.IdempotencyKey,
                request.OccurredAt),
            cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet]
    [RequireResourcePermission<PermissionType, Program>(PermissionType.Read, "programId")]
    public async Task<ActionResult<IReadOnlyList<ContentInteractionEventDto>>> List(
        Guid programId,
        Guid interactionId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new GetContentInteractionEventsQuery(programId, interactionId),
            cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}

public sealed record RecordContentInteractionEventRequest(
    ContentInteractionEventType Type,
    [Range(1, int.MaxValue)] int? DurationSeconds = null,
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal? PositionSeconds = null,
    PercentValue? ProgressPercentage = null,
    string? Payload = null,
    [MaxLength(128)] string? IdempotencyKey = null,
    DateTime? OccurredAt = null);
