using Asp.Versioning;
using GameGuild.Finance.Economy.AdRewards;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/economy/ad-rewards")]
[Tags("economy-administration")]
[Authorize]
public sealed class EconomyAdRewardQueryAdministrationController(
    IAdRewardOperationalQueryReader adRewards,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    [HttpGet("sessions")]
    [ProducesResponseType(
        typeof(EconomyOperationalPage<AdRewardSessionOperationalSummary>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSessions(
        [FromQuery] DurableAdRewardSessionState? state = null,
        [FromQuery] string? network = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        return Ok(await adRewards.ListSessionsAsync(
            tenantId, state, network, limit, cursor, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("sessions/{sessionId:guid}")]
    [ProducesResponseType(typeof(AdRewardSessionOperationalDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSession(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        var session = await adRewards.FindSessionAsync(
            tenantId, sessionId, cancellationToken).ConfigureAwait(false);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpGet("pending-claims")]
    [ProducesResponseType(
        typeof(EconomyOperationalPage<AdRewardPendingClaimOperationalStatus>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPendingClaims(
        [FromQuery] bool? confirmed = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        return Ok(await adRewards.ListPendingClaimsAsync(
            tenantId, confirmed, limit, cursor, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("reconciliations")]
    [ProducesResponseType(
        typeof(EconomyOperationalPage<AdRewardReconciliationOperationalStatus>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ListReconciliations(
        [FromQuery] string? network = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        return Ok(await adRewards.ListReconciliationsAsync(
            tenantId, network, limit, cursor, cancellationToken).ConfigureAwait(false));
    }

    private bool TryTenant(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue ||
            !actor.HasPermission(EconomyPermission.Keys.OperateAdRewards))
            return false;
        tenantId = actor.TenantId.Value;
        return true;
    }
}
