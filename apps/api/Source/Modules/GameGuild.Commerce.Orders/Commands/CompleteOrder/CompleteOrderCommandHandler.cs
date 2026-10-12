using GameGuild.Commerce.Products;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Commerce.Orders;

/// <summary>
/// Handler for completing an order with payment and entitlement granting
/// </summary>
public sealed class CompleteOrderCommandHandler(
    IOrderRepository orderRepository,
    IEntitlementService entitlementService,
    IApplicationDbContext dbContext,
    IOrderPaymentAuthority paymentAuthority,
    IActorContextAccessor actorContextAccessor,
    IOrderMarketplaceSettlementAuthority? marketplaceSettlementAuthority = null,
    IProductInventoryService? inventoryService = null)
    : ICommandHandler<CompleteOrderCommand, Result<OrderOperationResult>>
{
    public async Task<Result<OrderOperationResult>> Handle(
        CompleteOrderCommand request,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetWithLineItemsAsync(request.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return Result.Failure<OrderOperationResult>(Error.NotFound("Orders.NotFound", $"Order {request.OrderId} not found"));
        }

        var authorizationError = OrderActorContext.Authorize(order, actorContextAccessor);
        if (authorizationError is not null)
            return Result.Failure<OrderOperationResult>(authorizationError);

        if (order.Status == OrderStatus.Fulfilled)
        {
            return Result.Success(OrderOperationResult.FromOrder(order, wasDuplicate: true));
        }

        if (request.MarketplaceSettlement is not null)
        {
            if (request.PaymentId.HasValue ||
                !string.IsNullOrWhiteSpace(request.PaymentProviderReference) ||
                !string.IsNullOrWhiteSpace(request.PaymentMethod))
                return Result.Failure<OrderOperationResult>(
                    Error.Validation(
                        "Orders.MixedSettlementAuthority",
                        "Fiat payment references cannot be combined with Economy Marketplace settlement."));
            if (order.Status is not (OrderStatus.Pending or OrderStatus.Processing) ||
                !order.TenantId.HasValue)
                return Result.Failure<OrderOperationResult>(
                    Error.Forbidden(
                        "Orders.EconomyMarketplaceIneligible",
                        "The order is not eligible for Economy Marketplace settlement."));

            var settlement = request.MarketplaceSettlement;
            var authority = marketplaceSettlementAuthority ?? new DenyOrderMarketplaceSettlementAuthority();
            var decision = await authority.SettleAsync(
                new OrderMarketplaceSettlementRequest(
                    order.Id,
                    settlement.CurrencyChoice,
                    settlement.IdempotencyKey),
                cancellationToken).ConfigureAwait(false);
            if (!decision.IsAccepted || !decision.SettlementId.HasValue)
                return Result.Failure<OrderOperationResult>(
                    Error.Forbidden(
                        decision.ErrorCode ?? "Orders.EconomyMarketplaceRejected",
                        decision.ErrorDescription ?? "Economy Marketplace rejected the settlement."));

            return Result.Success(OrderOperationResult.FromMarketplaceSettlement(
                order,
                decision.SettlementId.Value,
                decision.IsDuplicate));
        }

        if (request.PaymentId.HasValue ||
            !string.IsNullOrWhiteSpace(request.PaymentProviderReference) ||
            !string.IsNullOrWhiteSpace(request.PaymentMethod) ||
            order.Status != OrderStatus.Paid ||
            !order.PaymentId.HasValue)
        {
            return Result.Failure<OrderOperationResult>(
                Error.Forbidden(
                    "Orders.PaymentAuthorityRequired",
                    "Only an order already bound and marked paid by Payments can be completed."));
        }

        if (order.Total <= 0 ||
            order.LineItems.Count == 0 ||
            order.LineItems.Any(lineItem =>
                lineItem.ProductPricingId == Guid.Empty ||
                lineItem.ProductPricingVersionId == Guid.Empty ||
                lineItem.PriceVersionSnapshot < 1 ||
                lineItem.UnitPriceSnapshot <= 0 ||
                lineItem.LineTotal <= 0 ||
                lineItem.CurrencySnapshot != order.Currency))
        {
            return Result.Failure<OrderOperationResult>(
                Error.Validation(
                    "Orders.InvalidPayableOrder",
                    "The paid order does not contain authoritative payable line-item snapshots."));
        }

        if (order.LineItems.Any(lineItem => lineItem.IsSubscription))
        {
            return Result.Failure<OrderOperationResult>(
                Error.Forbidden(
                    "Orders.SubscriptionAuthorityRequired",
                    "Subscription fulfillment requires an authoritative Subscriptions binding."));
        }

        var binding = new OrderPaymentBinding(
            order.Id,
            order.PaymentId.Value,
            order.TenantId!.Value,
            order.Total,
            order.Currency);
        if (!await paymentAuthority.IsSettledAsync(binding, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<OrderOperationResult>(
                Error.Forbidden(
                    "Orders.PaymentAuthorityRequired",
                    "Payments did not confirm an authoritative settlement binding for this order."));
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Inventory decrement (issue #401): convert reservations into definitive stock
            // decrements inside the same transaction as entitlement fulfillment. Untracked
            // products (the digital-goods default) are skipped by the service.
            if (inventoryService is not null)
            {
                var inventoryLines = order.LineItems
                    .Select(lineItem => (lineItem.ProductId, lineItem.Quantity))
                    .ToList();
                if (!await inventoryService.TryCommitSaleAsync(inventoryLines, cancellationToken).ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Result.Failure<OrderOperationResult>(
                        Error.Conflict(
                            "Orders.OutOfStockAtFulfillment",
                            "Tracked inventory can no longer satisfy one or more order lines."));
                }
            }

            foreach (var lineItem in order.LineItems)
            {
                var acquisitionType = lineItem.IsSubscription
                    ? ProductAcquisitionType.Subscription
                    : ProductAcquisitionType.Purchase;

                var entitlementResult = await entitlementService.GrantEntitlementAsync(
                    order.UserId,
                    lineItem.ProductId,
                    acquisitionType,
                    lineItem.LineTotal,
                    lineItem.CurrencySnapshot,
                    expiresAt: null,
                    orderId: order.Id,
                    cancellationToken).ConfigureAwait(false);

                if (!entitlementResult.Success)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return Result.Failure<OrderOperationResult>(
                        Error.Failure(
                            "Orders.FulfillmentFailed",
                            entitlementResult.ErrorMessage ?? "Entitlement fulfillment failed."));
                }

                if (entitlementResult.UserProduct != null)
                    lineItem.AttachEntitlement(entitlementResult.UserProduct.Id);
            }

            order.MarkAsFulfilled();

            await orderRepository.UpdateAsync(order, cancellationToken).ConfigureAwait(false);
            await orderRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return Result.Success(OrderOperationResult.FromOrder(order));
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
