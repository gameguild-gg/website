using GameGuild.CQRS;
using GameGuild.Commerce.Products;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Orders;

public sealed record GetMarketplaceCartQuery() : IQuery<ActionResult<MarketplaceCartDto>>;
public sealed record AddMarketplaceCartItemCommand(AddMarketplaceCartItemInput Input) : ICommand<ActionResult<MarketplaceCartDto>>;
public sealed record SetMarketplaceCartItemQuantityCommand(Guid ItemId, SetMarketplaceCartItemQuantityInput Input) : ICommand<ActionResult<MarketplaceCartDto>>;
public sealed record RemoveMarketplaceCartItemCommand(Guid ItemId, int ExpectedVersion) : ICommand<ActionResult<MarketplaceCartDto>>;
public sealed record CheckoutMarketplaceCartCommand(CheckoutMarketplaceCartInput Input) : ICommand<ActionResult<MarketplaceCheckoutDto>>;

public sealed class MarketplaceCartHandler(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor) :
    IQueryHandler<GetMarketplaceCartQuery, ActionResult<MarketplaceCartDto>>,
    ICommandHandler<AddMarketplaceCartItemCommand, ActionResult<MarketplaceCartDto>>,
    ICommandHandler<SetMarketplaceCartItemQuantityCommand, ActionResult<MarketplaceCartDto>>,
    ICommandHandler<RemoveMarketplaceCartItemCommand, ActionResult<MarketplaceCartDto>>,
    ICommandHandler<CheckoutMarketplaceCartCommand, ActionResult<MarketplaceCheckoutDto>>
{
    public async Task<ActionResult<MarketplaceCartDto>> Handle(GetMarketplaceCartQuery command, CancellationToken cancellationToken)
    {
        if (!TryActor(out var actor)) return new ForbidResult();
        var cart = await ActiveCart(actor, tracking: false, cancellationToken);
        return new OkObjectResult(Map(cart, actor.TenantId, actor.UserId));

    }

    public async Task<ActionResult<MarketplaceCartDto>> Handle(AddMarketplaceCartItemCommand command, CancellationToken cancellationToken)
    {
        var input = command.Input;
        if (!TryActor(out var actor)) return new ForbidResult();
        if (input.Quantity is < 1 or > 100 || string.IsNullOrWhiteSpace(input.IdempotencyKey))
            return new BadRequestObjectResult("Quantity and idempotency key are required.");
        if (!await IsCurrentPublishedPrice(input, actor.TenantId, cancellationToken))
            return new ConflictObjectResult("The product or immutable price version is unavailable or stale.");

        var cart = await ActiveCart(actor, tracking: true, cancellationToken)
            ?? MarketplaceCart.Create(actor.TenantId, actor.UserId);
        if (cart.Version == 0) context.Set<MarketplaceCart>().Add(cart);
        cart.AddItem(input.ProductId, input.ProductPricingId, input.ProductPricingVersionId, input.Quantity, input.IdempotencyKey);
        await context.SaveChangesAsync(cancellationToken);
        return new OkObjectResult(Map(cart, actor.TenantId, actor.UserId));

    }

    public async Task<ActionResult<MarketplaceCartDto>> Handle(SetMarketplaceCartItemQuantityCommand command, CancellationToken cancellationToken)
    {
        var itemId = command.ItemId;
        var input = command.Input;
        if (!TryActor(out var actor)) return new ForbidResult();
        var cart = await ActiveCart(actor, tracking: true, cancellationToken);
        if (cart is null) return new NotFoundResult();
        if (cart.Version != input.ExpectedVersion) return new ConflictObjectResult("The cart changed on another device.");
        try { cart.SetQuantity(itemId, input.Quantity); }
        catch (KeyNotFoundException) { return new NotFoundResult(); }
        catch (ArgumentOutOfRangeException exception) { return new BadRequestObjectResult(exception.Message); }
        await context.SaveChangesAsync(cancellationToken);
        return new OkObjectResult(Map(cart, actor.TenantId, actor.UserId));

    }

    public async Task<ActionResult<MarketplaceCartDto>> Handle(RemoveMarketplaceCartItemCommand command, CancellationToken cancellationToken)
    {
        var itemId = command.ItemId;
        var expectedVersion = command.ExpectedVersion;
        if (!TryActor(out var actor)) return new ForbidResult();
        var cart = await ActiveCart(actor, tracking: true, cancellationToken);
        if (cart is null) return new NotFoundResult();
        if (cart.Version != expectedVersion) return new ConflictObjectResult("The cart changed on another device.");
        try { cart.RemoveItem(itemId); }
        catch (KeyNotFoundException) { return new NotFoundResult(); }
        await context.SaveChangesAsync(cancellationToken);
        return new OkObjectResult(Map(cart, actor.TenantId, actor.UserId));

    }

    public async Task<ActionResult<MarketplaceCheckoutDto>> Handle(CheckoutMarketplaceCartCommand command, CancellationToken cancellationToken)
    {
        var input = command.Input;
        if (!TryActor(out var actor)) return new ForbidResult();
        var cart = await ActiveCart(actor, tracking: true, cancellationToken);
        if (cart is null || cart.Items.Count == 0) return new BadRequestObjectResult("The cart is empty.");
        if (cart.Version != input.ExpectedVersion) return new ConflictObjectResult("The cart changed on another device.");
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey)) return new BadRequestObjectResult("Idempotency key is required.");

        var snapshots = new List<MarketplaceCheckoutSnapshot>();
        foreach (var item in cart.Items.OrderBy(item => item.Id))
        {
            var snapshot = await PriceSnapshot(
                item.ProductId, item.ProductPricingId, item.ProductPricingVersionId, actor.TenantId, cancellationToken);
            if (snapshot is null) return new ConflictObjectResult("A cart price changed before checkout.");
            snapshots.Add(new MarketplaceCheckoutSnapshot(item, snapshot.Value));
        }

        var orders = new List<Order>();
        foreach (var currencyGroup in snapshots.GroupBy(snapshot => snapshot.Price.Version.Currency, StringComparer.Ordinal))
        {
            var order = Order.Create(
                actor.UserId,
                $"{input.IdempotencyKey}:{currencyGroup.Key}",
                actor.TenantId,
                currencyGroup.Key);
            foreach (var snapshot in currencyGroup)
            {
                order.AddLineItem(
                    snapshot.Item.ProductId,
                    snapshot.Price.Product.Name,
                    new OrderLineItemPricingSnapshot(
                        snapshot.Item.ProductPricingId,
                        snapshot.Item.ProductPricingVersionId,
                        snapshot.Price.Version.PriceVersion,
                        snapshot.Price.Version.BasePrice,
                        snapshot.Price.Version.SalePrice,
                        snapshot.Price.UnitPrice,
                        snapshot.Price.Version.Currency),
                    snapshot.Item.Quantity,
                    pricingTierName: snapshot.Price.Pricing.Name,
                    isSubscription: snapshot.Price.Product.Type == ProductType.Subscription);
            }
            orders.Add(order);
        }

        context.Set<Order>().AddRange(orders);
        cart.MarkCheckedOut(SystemClock.UtcNow);
        await context.SaveChangesAsync(cancellationToken);
        return new OkObjectResult(new MarketplaceCheckoutDto(
            cart.Id,
            orders.Select(order => new MarketplaceCheckoutOrderDto(order.Id, order.Total, order.Currency)).ToArray()));

    }

    private async Task<bool> IsCurrentPublishedPrice(
        AddMarketplaceCartItemInput input,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await PriceSnapshot(
            input.ProductId, input.ProductPricingId, input.ProductPricingVersionId, tenantId, cancellationToken) is not null;

    private async Task<(Product Product, ProductPricing Pricing, ProductPricingVersion Version, decimal UnitPrice)?> PriceSnapshot(
        Guid productId,
        Guid productPricingId,
        Guid productPricingVersionId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var product = await context.Set<Product>().AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == productId && candidate.TenantId == tenantId && candidate.IsPublished && candidate.DeletedAt == null,
            cancellationToken);
        var pricing = await context.Set<ProductPricing>().AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == productPricingId && candidate.ProductId == productId && candidate.TenantId == tenantId && candidate.DeletedAt == null,
            cancellationToken);
        var version = await context.Set<ProductPricingVersion>().AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == productPricingVersionId && candidate.ProductPricingId == productPricingId &&
                         candidate.TenantId == tenantId && candidate.IsActive && candidate.DeletedAt == null,
            cancellationToken);
        if (product is null || pricing is null || version is null || version.PriceVersion != pricing.CurrentVersion ||
            version.Currency != pricing.Currency || version.BasePrice != pricing.BasePrice || version.SalePrice != pricing.SalePrice)
            return null;
        var unitPrice = pricing.IsSaleActive() && version.SalePrice.HasValue ? version.SalePrice.Value : version.BasePrice;
        return unitPrice > 0 ? (product, pricing, version, unitPrice) : null;
    }

    private Task<MarketplaceCart?> ActiveCart(
        MarketplaceActor actor,
        bool tracking,
        CancellationToken cancellationToken)
    {
        IQueryable<MarketplaceCart> query = context.Set<MarketplaceCart>().Include(cart => cart.Items);
        if (!tracking) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(
            cart => cart.TenantId == actor.TenantId && cart.UserId == actor.UserId && cart.State == MarketplaceCartState.Active && cart.DeletedAt == null,
            cancellationToken);
    }

    private bool TryActor(out MarketplaceActor actor)
    {
        var context = actorContextAccessor.ActorContext;
        if (!context.IsAuthenticated || context.TenantId is not { } tenantId || tenantId == Guid.Empty ||
            context.SubjectIdAsGuid is not { } userId || userId == Guid.Empty)
        {
            actor = default;
            return false;
        }
        actor = new MarketplaceActor(tenantId, userId);
        return true;
    }

    private static MarketplaceCartDto Map(MarketplaceCart? cart, Guid tenantId, Guid userId) => cart is null
        ? new MarketplaceCartDto(null, tenantId, userId, 0, MarketplaceCartState.Active, [])
        : new MarketplaceCartDto(
            cart.Id, tenantId, userId, cart.Version, cart.State,
            cart.Items.OrderBy(item => item.CreatedAt).Select(item => new MarketplaceCartItemDto(
                item.Id, item.ProductId, item.ProductPricingId, item.ProductPricingVersionId, item.Quantity)).ToArray());

    private readonly record struct MarketplaceActor(Guid TenantId, Guid UserId);
    private readonly record struct MarketplaceCheckoutSnapshot(
        MarketplaceCartItem Item,
        (Product Product, ProductPricing Pricing, ProductPricingVersion Version, decimal UnitPrice) Price);
}
