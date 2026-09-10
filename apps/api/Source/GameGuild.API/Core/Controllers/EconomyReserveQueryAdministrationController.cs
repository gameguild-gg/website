using Asp.Versioning;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/economy")]
[Tags("economy-administration")]
[Authorize]
public sealed class EconomyReserveQueryAdministrationController(
    IEconomyReserveQueryReader reserves,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    [HttpGet("custody/observations")]
    [ProducesResponseType(
        typeof(EconomyOperationalPage<EconomyCustodyObservationOperationalStatus>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCustody(
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        return Ok(await reserves.ListCustodyAsync(
            tenantId, limit, cursor, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("custody/observations/{observationId:guid}")]
    [ProducesResponseType(typeof(EconomyCustodyObservationOperationalStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCustody(Guid observationId, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        var observation = await reserves.FindCustodyAsync(
            tenantId, observationId, cancellationToken).ConfigureAwait(false);
        return observation is null ? NotFound() : Ok(observation);
    }

    [HttpGet("reserves/proposals")]
    [ProducesResponseType(
        typeof(EconomyOperationalPage<EconomyReserveProposalOperationalStatus>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProposals(
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        return Ok(await reserves.ListProposalsAsync(
            tenantId, limit, cursor, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("reserves/proposals/{proposalId:guid}")]
    [ProducesResponseType(typeof(EconomyReserveProposalOperationalStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProposal(Guid proposalId, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        var proposal = await reserves.FindProposalAsync(
            tenantId, proposalId, cancellationToken).ConfigureAwait(false);
        return proposal is null ? NotFound() : Ok(proposal);
    }

    [HttpGet("reserves/active")]
    [ProducesResponseType(typeof(EconomyActiveReserveOperationalDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveHead(CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        var head = await reserves.ReadActiveHeadAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return head is null ? NotFound() : Ok(head);
    }

    private bool TryTenant(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue ||
            !actor.HasPermission(EconomyPermission.Keys.ManageReserves))
            return false;
        tenantId = actor.TenantId.Value;
        return true;
    }
}
