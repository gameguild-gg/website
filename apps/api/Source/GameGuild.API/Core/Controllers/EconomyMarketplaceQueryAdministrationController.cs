using Asp.Versioning;
using GameGuild.Finance.Economy.Marketplace;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/economy/marketplace")]
[Tags("economy-administration")]
[Authorize]
public sealed class EconomyMarketplaceQueryAdministrationController(
    IMarketplaceOperationalQueryReader marketplace,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    [HttpGet("settlements")]
    [ProducesResponseType(
        typeof(EconomyOperationalPage<MarketplaceSettlementOperationalSummary>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSettlements(
        [FromQuery] MarketplaceSettlementStatus? status = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        return Ok(await marketplace.ListSettlementsAsync(
            tenantId, status, limit, cursor, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("settlements/{settlementId:guid}")]
    [ProducesResponseType(typeof(MarketplaceSettlementOperationalDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSettlement(Guid settlementId, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        var settlement = await marketplace.FindSettlementAsync(
            tenantId, settlementId, cancellationToken).ConfigureAwait(false);
        return settlement is null ? NotFound() : Ok(settlement);
    }

    [HttpGet("refunds")]
    [ProducesResponseType(
        typeof(EconomyOperationalPage<MarketplaceRefundOperationalStatus>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRefunds(
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        return Ok(await marketplace.ListRefundsAsync(
            tenantId, limit, cursor, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("refunds/{refundId:guid}")]
    [ProducesResponseType(typeof(MarketplaceRefundOperationalStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRefund(Guid refundId, CancellationToken cancellationToken)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        var refund = await marketplace.FindRefundAsync(
            tenantId, refundId, cancellationToken).ConfigureAwait(false);
        return refund is null ? NotFound() : Ok(refund);
    }

    [HttpGet("outbox")]
    [ProducesResponseType(
        typeof(EconomyOperationalPage<MarketplaceOutboxOperationalStatus>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> ListOutbox(
        [FromQuery] bool? published = null,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryTenant(out var tenantId)) return Forbid();
        return Ok(await marketplace.ListOutboxAsync(
            tenantId, published, limit, cursor, cancellationToken).ConfigureAwait(false));
    }

    private bool TryTenant(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue ||
            !actor.HasPermission(EconomyPermission.Keys.OperateMarketplace))
            return false;
        tenantId = actor.TenantId.Value;
        return true;
    }
}
