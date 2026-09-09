using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

public sealed record ResolveEconomyRiskReviewRequest(
    RiskManualDecisionCode DecisionCode,
    string Resolution);

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/economy/risk-reviews")]
[Tags("economy-risk-review-administration")]
[Authorize]
public sealed class EconomyRiskReviewAdministrationController(
    ISender sender,
    IRiskReviewStore reviews,
    IActorContextAccessor actorContextAccessor,
    TimeProvider timeProvider) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(RiskReviewPage), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] RiskReviewStatus? status = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryReviewer(out var tenantId, out _)) return Forbid();
        return Ok(await reviews.ListAsync(
            tenantId, status, limit, cursor, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("{reviewId:guid}")]
    [ProducesResponseType(typeof(RiskReviewCase), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid reviewId, CancellationToken cancellationToken)
    {
        if (!TryReviewer(out var tenantId, out _)) return Forbid();
        try
        {
            return Ok(await reviews.CurrentAsync(tenantId, reviewId, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{reviewId:guid}/audit")]
    [ProducesResponseType(typeof(IReadOnlyList<RiskReviewEvent>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Audit(Guid reviewId, CancellationToken cancellationToken)
    {
        if (!TryReviewer(out var tenantId, out _)) return Forbid();
        try
        {
            return Ok(await reviews.EventsAsync(tenantId, reviewId, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{reviewId:guid}:approve")]
    [ProducesResponseType(typeof(RiskReviewCase), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(
        Guid reviewId,
        [FromBody] ResolveEconomyRiskReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReviewer(out var tenantId, out var actorId)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        return await ResolveAsync(sender.Send(new ApproveEconomyRiskReviewEndpointCommand(
            tenantId, reviewId, actorId, request.DecisionCode, request.Resolution, timeProvider.GetUtcNow()),
            cancellationToken)).ConfigureAwait(false);
    }

    [HttpPost("{reviewId:guid}:reject")]
    [ProducesResponseType(typeof(RiskReviewCase), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        Guid reviewId,
        [FromBody] ResolveEconomyRiskReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryReviewer(out var tenantId, out var actorId)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        return await ResolveAsync(sender.Send(new RejectEconomyRiskReviewEndpointCommand(
            tenantId, reviewId, actorId, request.DecisionCode, request.Resolution, timeProvider.GetUtcNow()),
            cancellationToken)).ConfigureAwait(false);
    }

    private async Task<IActionResult> ResolveAsync(Task<RiskReviewCase> operation)
    {
        try
        {
            return Ok(await operation.ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    private bool TryReviewer(out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.TenantId is not { } resolvedTenant ||
            actor.SubjectIdAsGuid is not { } resolvedActor ||
            !actor.HasPermission(EconomyPermission.Keys.OperateCompliance))
            return false;
        tenantId = resolvedTenant;
        actorId = resolvedActor;
        return true;
    }
}
