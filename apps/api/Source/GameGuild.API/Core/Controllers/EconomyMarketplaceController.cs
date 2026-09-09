using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Economy.Contracts;
using GameGuild.Economy.Marketplace;
using GameGuild.Economy.Risk;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

public sealed record SettleMyMarketplaceOrderRequest(
    MarketplaceCurrencyChoice CurrencyChoice,
    string IdempotencyKey);

public sealed record RefundMarketplaceSettlementRequest(
    int Quantity,
    string ReasonCode,
    string IdempotencyKey);

public sealed record MarketplaceProtectedOperationFailureResponse(
    EconomyProtectedOperationState State,
    Guid? ReviewId,
    IReadOnlyList<string> Diagnostics);

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/economy/marketplace")]
[Tags("economy-marketplace")]
[Authorize]
public sealed class EconomyMarketplaceController(
    ISender sender,
    IActorContextAccessor actorContextAccessor,
    TimeProvider timeProvider) : BaseApiController
{
    [HttpPost("orders/{orderId:guid}:settle")]
    [ProducesResponseType(typeof(DurableMarketplaceSettlementResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MarketplaceProtectedOperationFailureResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MarketplaceProtectedOperationFailureResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(MarketplaceProtectedOperationFailureResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Settle(
        Guid orderId,
        [FromBody] SettleMyMarketplaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out _, out _)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        return await MarketplaceProtectedOperationResponse.ExecuteAsync(() =>
            sender.Send(new SettleMarketplaceOrderEndpointCommand(new SettleAuthoritativeMarketplaceOrderRequest(
                orderId,
                request.CurrencyChoice,
                new IdempotencyKey(request.IdempotencyKey),
                timeProvider.GetUtcNow())), cancellationToken));
    }

    [HttpPost("settlements/{settlementId:guid}:refund")]
    [ProducesResponseType(typeof(DurableMarketplaceRefundResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MarketplaceProtectedOperationFailureResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MarketplaceProtectedOperationFailureResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(MarketplaceProtectedOperationFailureResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Refund(
        Guid settlementId,
        [FromBody] RefundMarketplaceSettlementRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryActor(out _, out _)) return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        return await MarketplaceProtectedOperationResponse.ExecuteAsync(() =>
            sender.Send(new RefundMarketplaceOrderEndpointCommand(new RefundAuthoritativeMarketplaceOrderRequest(
                MarketplaceRefundAuthority.SelfService,
                settlementId,
                request.Quantity,
                request.ReasonCode,
                new IdempotencyKey(request.IdempotencyKey),
                timeProvider.GetUtcNow())), cancellationToken));
    }

    private bool TryActor(out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue || !actor.SubjectIdAsGuid.HasValue)
            return false;
        tenantId = actor.TenantId.Value;
        actorId = actor.SubjectIdAsGuid.Value;
        return true;
    }
}

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/economy/marketplace")]
[Tags("economy-administration")]
[Authorize]
public sealed class EconomyMarketplaceAdministrationController(
    ISender sender,
    IActorContextAccessor actorContextAccessor,
    TimeProvider timeProvider) : BaseApiController
{
    [HttpPost("settlements/{settlementId:guid}:refund")]
    [ProducesResponseType(typeof(DurableMarketplaceRefundResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MarketplaceProtectedOperationFailureResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MarketplaceProtectedOperationFailureResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(MarketplaceProtectedOperationFailureResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Refund(
        Guid settlementId,
        [FromBody] RefundMarketplaceSettlementRequest request,
        CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue || !actor.SubjectIdAsGuid.HasValue ||
            !actor.HasPermission(EconomyPermission.Keys.OperateMarketplace))
            return Forbid();
        ArgumentNullException.ThrowIfNull(request);
        return await MarketplaceProtectedOperationResponse.ExecuteAsync(() =>
            sender.Send(new RefundMarketplaceOrderAdministrationEndpointCommand(new RefundAuthoritativeMarketplaceOrderRequest(
                MarketplaceRefundAuthority.Operations,
                settlementId,
                request.Quantity,
                request.ReasonCode,
                new IdempotencyKey(request.IdempotencyKey),
                timeProvider.GetUtcNow())), cancellationToken));
    }
}

internal static class MarketplaceProtectedOperationResponse
{
    public static async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return new OkObjectResult(await operation().ConfigureAwait(false));
        }
        catch (EconomyProtectedOperationException exception)
        {
            var status = exception.State switch
            {
                EconomyProtectedOperationState.Denied => StatusCodes.Status403Forbidden,
                EconomyProtectedOperationState.ReviewRequired or EconomyProtectedOperationState.Hold or
                    EconomyProtectedOperationState.Challenge => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status503ServiceUnavailable
            };
            return new ObjectResult(new MarketplaceProtectedOperationFailureResponse(
                exception.State, exception.ReviewId, exception.Diagnostics)) { StatusCode = status };
        }
    }
}
