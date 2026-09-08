using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Commerce.Products;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Orders;

[ApiVersion("1.0")]
[Route("v{version:apiVersion}/marketplace/cart")]
[Microsoft.AspNetCore.Http.Tags("commerce/marketplace-cart")]
[Authorize]
public sealed class MarketplaceCartController(
    ISender sender) : ControllerBase
{
    [HttpGet]
    [RequirePermission(OrdersPermission.Keys.Read)]
    public async Task<ActionResult<MarketplaceCartDto>> Get(CancellationToken cancellationToken)
    {
        return await sender.Send(new GetMarketplaceCartQuery(), cancellationToken).ConfigureAwait(false);
    }

    [HttpPost("items")]
    [RequirePermission(OrdersPermission.Keys.Create)]
    public async Task<ActionResult<MarketplaceCartDto>> AddItem(
        [FromBody] AddMarketplaceCartItemInput input,
        CancellationToken cancellationToken)
    {
        return await sender.Send(new AddMarketplaceCartItemCommand(input), cancellationToken).ConfigureAwait(false);
    }

    [HttpPatch("items/{itemId:guid}")]
    [RequirePermission(OrdersPermission.Keys.Create)]
    public async Task<ActionResult<MarketplaceCartDto>> SetQuantity(
        Guid itemId,
        [FromBody] SetMarketplaceCartItemQuantityInput input,
        CancellationToken cancellationToken)
    {
        return await sender.Send(new SetMarketplaceCartItemQuantityCommand(itemId, input), cancellationToken).ConfigureAwait(false);
    }

    [HttpDelete("items/{itemId:guid}")]
    [RequirePermission(OrdersPermission.Keys.Create)]
    public async Task<ActionResult<MarketplaceCartDto>> RemoveItem(
        Guid itemId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
    {
        return await sender.Send(new RemoveMarketplaceCartItemCommand(itemId, expectedVersion), cancellationToken).ConfigureAwait(false);
    }

    [HttpPost("checkout")]
    [RequirePermission(OrdersPermission.Keys.Create)]
    public async Task<ActionResult<MarketplaceCheckoutDto>> Checkout(
        [FromBody] CheckoutMarketplaceCartInput input,
        CancellationToken cancellationToken)
    {
        return await sender.Send(new CheckoutMarketplaceCartCommand(input), cancellationToken).ConfigureAwait(false);
    }


}

public sealed record AddMarketplaceCartItemInput(
    Guid ProductId,
    Guid ProductPricingId,
    Guid ProductPricingVersionId,
    int Quantity,
    string IdempotencyKey);
public sealed record SetMarketplaceCartItemQuantityInput(int Quantity, int ExpectedVersion);
public sealed record CheckoutMarketplaceCartInput(int ExpectedVersion, string IdempotencyKey);
public sealed record MarketplaceCartDto(
    Guid? Id, Guid TenantId, Guid UserId, int Version, MarketplaceCartState State, IReadOnlyList<MarketplaceCartItemDto> Items);
public sealed record MarketplaceCartItemDto(
    Guid Id, Guid ProductId, Guid ProductPricingId, Guid ProductPricingVersionId, int Quantity);
public sealed record MarketplaceCheckoutDto(Guid CartId, IReadOnlyList<MarketplaceCheckoutOrderDto> Orders);
public sealed record MarketplaceCheckoutOrderDto(Guid OrderId, decimal Total, string Currency);
