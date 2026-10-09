using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Finance.Economy.Integrations.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace GameGuild.Learning.Courses;

[ApiVersion("1.0")]
[Route("v{version:apiVersion}/courses/{programId:guid}/content/{contentId:guid}/authoring")]
[Authorize]
public sealed class ProgramContentAuthoringController(
    IProgramContentAuthoringService authoring,
    IActorContextAccessor actorContextAccessor,
    ISender sender) : BaseApiController
{
    [HttpGet]
    [RequireCourseCapability(CourseCapability.Edit, "programId")]
    [ProducesResponseType<AuthoringDraftDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthoringDraftDto>> GetDraft(
        Guid programId,
        Guid contentId,
        CancellationToken cancellationToken)
    {
        var actorId = RequireActorId();
        try
        {
            var draft = await authoring.GetOrCreateDraft(programId, contentId, actorId, cancellationToken).ConfigureAwait(false);
            SetEtag(draft.ETag);
            return Ok(draft);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut]
    [RequireCourseCapability(CourseCapability.Edit, "programId")]
    [ProducesResponseType<AuthoringDraftDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthoringDraftDto>> SaveDraft(
        Guid programId,
        Guid contentId,
        [FromBody] SaveAuthoringDraftRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = RequireActorId();
        try
        {
            var draft = await sender.Send(new SaveProgramContentDraftCommand(
                programId,
                contentId,
                request.Revision,
                request.Payload,
                actorId), cancellationToken).ConfigureAwait(false);
            SetEtag(draft.ETag);
            return Ok(draft);
        }
        catch (AuthoringRevisionConflictException conflict)
        {
            return ConflictResponse("AUTHORING_REVISION_CONFLICT", conflict.Message, conflict.CurrentRevision);
        }
        catch (ValidationException validation)
        {
            return UnprocessableEntity(ErrorResponse(
                StatusCodes.Status422UnprocessableEntity,
                "AUTHORING_ASSET_VALIDATION_FAILED",
                validation.Message));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("publish")]
    [RequireCourseCapability(CourseCapability.Publish, "programId")]
    [ProducesResponseType<PublishAuthoringResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PublishAuthoringResult>> Publish(
        Guid programId,
        Guid contentId,
        [FromBody] PublishAuthoringDraftRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = RequireActorId();
        try
        {
            var result = await sender.Send(new PublishProgramContentDraftCommand(
                programId,
                contentId,
                request.Revision,
                actorId), cancellationToken).ConfigureAwait(false);
            SetEtag(result.Draft.ETag);
            return Ok(result);
        }
        catch (AuthoringRevisionConflictException conflict)
        {
            return ConflictResponse("AUTHORING_REVISION_CONFLICT", conflict.Message, conflict.CurrentRevision);
        }
        catch (AuthoringPublishedVersionConflictException conflict)
        {
            return ConflictResponse("PUBLISHED_VERSION_CONFLICT", conflict.Message, conflict.CurrentVersion);
        }
        catch (ValidationException validation)
        {
            return UnprocessableEntity(ErrorResponse(
                StatusCodes.Status422UnprocessableEntity,
                "AUTHORING_ASSET_VALIDATION_FAILED",
                validation.Message));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("ai/entitlement")]
    [RequireCourseCapability(CourseCapability.Edit, "programId")]
    [ProducesResponseType<AiEntitlementDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiEntitlementDto>> GetAiEntitlement(
        Guid programId,
        Guid contentId,
        [FromServices] IAuthoringAiService aiAuthoring,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        try
        {
            return Ok(await aiAuthoring.GetEntitlement(actor.TenantId, actor.ActorId, cancellationToken).ConfigureAwait(false));
        }
        catch (InsufficientAiCreditsException)
        {
            return Ok(new AiEntitlementDto(0, 0, 0));
        }
    }

    [HttpGet("ai/conversations")]
    [RequireCourseCapability(CourseCapability.Edit, "programId")]
    [ProducesResponseType<IReadOnlyList<AiAuthoringConversationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AiAuthoringConversationDto>>> GetAiConversations(
        Guid programId,
        Guid contentId,
        [FromServices] IAuthoringAiService aiAuthoring,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        return Ok(await aiAuthoring.GetConversations(actor.TenantId, actor.ActorId, programId, contentId, cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("ai/runs")]
    [RequireCourseCapability(CourseCapability.Edit, "programId")]
    [ProducesResponseType<AiAuthoringRunDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AiAuthoringRunDto>> CreateAiRun(
        Guid programId,
        Guid contentId,
        [FromBody] AiAuthoringRunRequest request,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        try
        {
            var run = await sender.Send(new CreateAiAuthoringRunCommand(
                actor.TenantId,
                actor.ActorId,
                programId,
                contentId,
                request), cancellationToken).ConfigureAwait(false);
            return AcceptedAtAction(nameof(GetAiRun), new { programId, contentId, runId = run.Id, version = "1.0" }, run);
        }
        catch (InsufficientAiCreditsException exception)
        {
            return StatusCode(StatusCodes.Status402PaymentRequired, ErrorResponse(
                StatusCodes.Status402PaymentRequired,
                "INSUFFICIENT_AI_CREDITS",
                exception.Message));
        }
        catch (AiAuthoringExecutionException exception) when (exception.Code.Contains("Quota", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, ErrorResponse(
                StatusCodes.Status429TooManyRequests,
                "AI_QUOTA_EXCEEDED",
                exception.Message));
        }
        catch (AuthoringRevisionConflictException conflict)
        {
            return ConflictResponse("AUTHORING_REVISION_CONFLICT", conflict.Message, conflict.CurrentRevision);
        }
        catch (AiAuthoringIdempotencyConflictException conflict)
        {
            return Conflict(ErrorResponse(
                StatusCodes.Status409Conflict,
                "AI_IDEMPOTENCY_CONFLICT",
                conflict.Message));
        }
        catch (AiProposalKindNotAllowedException exception)
        {
            return BadRequest(ErrorResponse(
                StatusCodes.Status400BadRequest,
                "AI_PROPOSAL_KIND_NOT_ALLOWED",
                exception.Message));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("ai/runs/{runId:guid}")]
    [RequireCourseCapability(CourseCapability.Edit, "programId")]
    [ProducesResponseType<AiAuthoringRunDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiAuthoringRunDto>> GetAiRun(
        Guid programId,
        Guid contentId,
        Guid runId,
        [FromServices] IAuthoringAiService aiAuthoring,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        try
        {
            return Ok(await aiAuthoring.GetRun(actor.TenantId, actor.ActorId, programId, contentId, runId, cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("ai/runs/{runId:guid}/cancel")]
    [RequireCourseCapability(CourseCapability.Edit, "programId")]
    [ProducesResponseType<AiAuthoringRunDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiAuthoringRunDto>> CancelAiRun(
        Guid programId,
        Guid contentId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        try
        {
            return Ok(await sender.Send(new CancelAiAuthoringRunCommand(
                actor.TenantId,
                actor.ActorId,
                programId,
                contentId,
                runId), cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("ai/runs/{runId:guid}/stream")]
    [RequireCourseCapability(CourseCapability.Edit, "programId")]
    [Produces("text/event-stream")]
    public async Task StreamAiRun(
        Guid programId,
        Guid contentId,
        Guid runId,
        [FromHeader(Name = "Last-Event-ID")] long? lastEventId,
        [FromServices] IAuthoringAiService aiAuthoring,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers.Append("X-Accel-Buffering", "no");
        await foreach (var streamEvent in aiAuthoring.StreamRun(
                           actor.TenantId,
                           actor.ActorId,
                           programId,
                           contentId,
                           runId,
                           lastEventId ?? 0,
                           cancellationToken).ConfigureAwait(false))
        {
            await Response.WriteAsync($"id: {streamEvent.Sequence}\n", cancellationToken).ConfigureAwait(false);
            await Response.WriteAsync($"event: {streamEvent.Type}\n", cancellationToken).ConfigureAwait(false);
            await Response.WriteAsync($"data: {SerializeStreamEvent(streamEvent)}\n\n", cancellationToken).ConfigureAwait(false);
            await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    [HttpPost("ai/proposals/{proposalId:guid}/apply")]
    [RequireCourseCapability(CourseCapability.Edit, "programId")]
    [ProducesResponseType<AuthoringDraftDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthoringDraftDto>> ApplyAiProposal(
        Guid programId,
        Guid contentId,
        Guid proposalId,
        [FromBody] ApplyAiProposalRequest request,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        try
        {
            var draft = await sender.Send(new ApplyAiAuthoringProposalCommand(
                actor.TenantId,
                actor.ActorId,
                programId,
                contentId,
                proposalId,
                request), cancellationToken).ConfigureAwait(false);
            SetEtag(draft.ETag);
            return Ok(draft);
        }
        catch (AuthoringRevisionConflictException conflict)
        {
            return ConflictResponse("AUTHORING_REVISION_CONFLICT", conflict.Message, conflict.CurrentRevision);
        }
        catch (AiProposalStateConflictException conflict)
        {
            return Conflict(ErrorResponse(
                StatusCodes.Status409Conflict,
                "AI_PROPOSAL_ALREADY_RESOLVED",
                conflict.Message));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("ai/proposals/{proposalId:guid}")]
    [RequireCourseCapability(CourseCapability.Edit, "programId")]
    [ProducesResponseType<AiProposalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AiProposalDto>> DiscardAiProposal(
        Guid programId,
        Guid contentId,
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        var actor = RequireActor();
        try
        {
            return Ok(await sender.Send(new DiscardAiAuthoringProposalCommand(
                actor.TenantId,
                actor.ActorId,
                programId,
                contentId,
                proposalId), cancellationToken).ConfigureAwait(false));
        }
        catch (AiProposalStateConflictException conflict)
        {
            return Conflict(ErrorResponse(
                StatusCodes.Status409Conflict,
                "AI_PROPOSAL_ALREADY_RESOLVED",
                conflict.Message));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private Guid RequireActorId()
    {
        var actor = actorContextAccessor.ActorContext;
        return actor is { IsAuthenticated: true, ActorKind: ActorKind.User, SubjectIdAsGuid: { } actorId }
            ? actorId
            : throw new UnauthorizedAccessException("An authenticated user actor is required.");
    }

    private (Guid TenantId, Guid ActorId) RequireActor()
    {
        var actor = actorContextAccessor.ActorContext;
        return actor is { IsAuthenticated: true, ActorKind: ActorKind.User, TenantId: { } tenantId, SubjectIdAsGuid: { } actorId }
            ? (tenantId, actorId)
            : throw new UnauthorizedAccessException("An authenticated tenant user actor is required.");
    }

    private static ProblemDetails ErrorResponse(int status, string code, string detail) => new()
    {
        Status = status,
        Title = "AI authoring request rejected",
        Detail = detail,
        Extensions = { ["code"] = code },
    };

    private void SetEtag(string etag) => Response.Headers.ETag = etag;

    internal static string SerializeStreamEvent(AiStreamEvent streamEvent) =>
        JsonSerializer.Serialize(streamEvent, JsonSerializerOptions.Web);

    private ConflictObjectResult ConflictResponse(string code, string detail, int currentRevision) => Conflict(new ProblemDetails
    {
        Status = StatusCodes.Status409Conflict,
        Title = "Authoring conflict",
        Detail = detail,
        Extensions =
        {
            ["code"] = code,
            ["currentRevision"] = currentRevision,
        },
    });
}
