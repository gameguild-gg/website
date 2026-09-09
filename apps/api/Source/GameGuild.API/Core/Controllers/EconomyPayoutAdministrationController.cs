using Asp.Versioning;
using GameGuild.API.Setup;
using GameGuild.CQRS;
using GameGuild.Finance.Economy.Payouts;
using GameGuild.Finance.Economy.Payouts.Commands;
using GameGuild.Finance.Economy.Payouts.Queries;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/economy/payout-requests")]
[Tags("economy")]
[Authorize]
public sealed class EconomyPayoutAdministrationController(
    ISender sender,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    [HttpGet]
    [EndpointSummary("List payout requests awaiting administrative review")]
    [ProducesResponseType(typeof(IReadOnlyList<EconomyPayoutRequestReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListForReview(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (!CanAdministerWallets())
        {
            return Forbid();
        }
        if (take is < 1 or > 100)
        {
            return BadRequest("Take must be between 1 and 100.");
        }

        var requests = await sender.Send(new ListPayoutRequestsForReviewQuery(take), cancellationToken)
            .ConfigureAwait(false);
        return Ok(requests);
    }

    [HttpGet("{requestId:guid}/audit")]
    [EndpointSummary("Get the immutable administrative review trail for a payout request")]
    [ProducesResponseType(typeof(IReadOnlyList<EconomyPayoutRequestReviewAuditDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListReviewAudit(Guid requestId, CancellationToken cancellationToken = default)
    {
        if (!CanAdministerWallets())
        {
            return Forbid();
        }

        var audit = await sender.Send(new ListPayoutRequestReviewAuditQuery(requestId), cancellationToken)
            .ConfigureAwait(false);
        return Ok(audit);
    }

    [HttpPost("{requestId:guid}/approve")]
    [EndpointSummary("Record one independent payout approval")]
    [EndpointDescription("The first approval waits for a different tenant administrator. Final approval records a decision only and does not reserve or dispatch value.")]
    [ProducesResponseType(typeof(EconomyPayoutRequestReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Approve(
        Guid requestId,
        [FromBody] ReviewPayoutRequestRequest request,
        CancellationToken cancellationToken = default) =>
        Review(requestId, PayoutRequestState.Approved, request, cancellationToken);

    [HttpPost("{requestId:guid}/reject")]
    [EndpointSummary("Reject a payout request with an immutable reason")]
    [ProducesResponseType(typeof(EconomyPayoutRequestReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> Reject(
        Guid requestId,
        [FromBody] ReviewPayoutRequestRequest request,
        CancellationToken cancellationToken = default) =>
        Review(requestId, PayoutRequestState.Rejected, request, cancellationToken);

    private async Task<IActionResult> Review(
        Guid requestId,
        PayoutRequestState outcome,
        ReviewPayoutRequestRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanAdministerWallets())
        {
            return Forbid();
        }
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var reviewed = await sender.Send(
                    new ReviewPayoutRequestCommand(requestId, outcome, request),
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(reviewed);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (PayoutRequestStaleCommandException exception)
        {
            return Conflict(exception.Message);
        }
        catch (PayoutRequestTransitionException exception)
        {
            return Conflict(exception.Message);
        }
    }

    private bool CanAdministerWallets()
    {
        var actor = actorContextAccessor.ActorContext;
        return actor.IsAuthenticated && actor.SubjectIdAsGuid.HasValue && actor.TenantId.HasValue &&
               actor.HasPermission(EconomyPermission.Keys.ReviewPayouts);
    }
}
