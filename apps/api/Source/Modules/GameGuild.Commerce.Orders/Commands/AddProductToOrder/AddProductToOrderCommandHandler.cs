using GameGuild.Commerce.Products;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Orders;

/// <summary>
/// Handler for adding a product to an existing order. Promo redemptions on the line item
/// are recorded atomically (idempotent per order line, fail-closed on usage limits) via
/// <see cref="IPromoCodeUsageRecorder"/>; the usage row carries the order/line linkage and
/// the actual discount applied.
/// </summary>
public sealed class AddProductToOrderCommandHandler(
    IOrderRepository orderRepository,
    IProductRepository productRepository,
    IProductPricingRepository pricingRepository,
    IPromoCodeService promoCodeService,
    IPromoCodeUsageRecorder promoUsageRecorder,
    IApplicationDbContext dbContext,
    IActorContextAccessor actorContextAccessor)
    : ICommandHandler<AddProductToOrderCommand, Result<Order>>
{
    public async Task<Result<Order>> Handle(
        AddProductToOrderCommand request,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetWithLineItemsAsync(request.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return Result.Failure<Order>(Error.NotFound("Orders.NotFound", $"Order {request.OrderId} not found"));
        }

        var authorizationError = OrderActorContext.Authorize(order, actorContextAccessor);
        if (authorizationError is not null)
            return Result.Failure<Order>(authorizationError);

        if (order.Status != OrderStatus.Pending)
        {
            return Result.Failure<Order>(Error.Failure("Orders.InvalidStatus", $"Cannot add items to order in {order.Status} status"));
        }

        var product = await productRepository.GetByIdAsync(
            request.ProductId,
            cancellationToken,
            isPublished: true).ConfigureAwait(false);
        if (product is null || !product.IsPublished)
        {
            return Result.Failure<Order>(Error.NotFound("Products.Unavailable", $"Product {request.ProductId} is unavailable"));
        }

        var pricing = await pricingRepository.GetByIdAsync(request.ProductPricingId, cancellationToken).ConfigureAwait(false);
        if (pricing is null || pricing.ProductId != request.ProductId)
        {
            return Result.Failure<Order>(
                Error.NotFound("Orders.PricingNotFound", "The requested pricing does not belong to the product."));
        }

        var tenantId = order.TenantId!.Value;
        if (product.TenantId != tenantId || pricing.TenantId != tenantId)
        {
            return Result.Failure<Order>(
                Error.Forbidden("Orders.PricingTenantMismatch", "Product pricing is outside the order tenant."));
        }

        var pricingVersion = await dbContext.Set<ProductPricingVersion>()
            .FirstOrDefaultAsync(
                version => version.Id == request.ProductPricingVersionId && version.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);

        if (pricingVersion is null || pricingVersion.ProductPricingId != pricing.Id)
        {
            return Result.Failure<Order>(
                Error.NotFound("Orders.PricingVersionNotFound", "The requested pricing version does not belong to the pricing tier."));
        }

        if (pricingVersion.TenantId != tenantId)
        {
            return Result.Failure<Order>(
                Error.Forbidden("Orders.PricingTenantMismatch", "Product pricing is outside the order tenant."));
        }

        if (!pricingVersion.IsActive ||
            pricingVersion.PriceVersion != pricing.CurrentVersion ||
            pricingVersion.EffectiveFrom > SystemClock.UtcNow ||
            pricingVersion.EffectiveTo.HasValue ||
            pricingVersion.Currency != pricing.Currency ||
            pricingVersion.BasePrice != pricing.BasePrice ||
            pricingVersion.SalePrice != pricing.SalePrice)
        {
            return Result.Failure<Order>(
                Error.Conflict("Orders.StalePricing", "The requested pricing version is no longer current."));
        }

        var price = pricing.IsSaleActive() && pricingVersion.SalePrice.HasValue
            ? pricingVersion.SalePrice.Value
            : pricingVersion.BasePrice;
        if (pricingVersion.BasePrice <= 0 || price <= 0)
        {
            return Result.Failure<Order>(
                Error.Validation("Orders.InvalidPrice", "Order line items require a positive authoritative price."));
        }

        if (order.LineItems.Count > 0 &&
            (order.Currency != pricingVersion.Currency ||
             order.LineItems.Any(item => item.CurrencySnapshot != pricingVersion.Currency)))
        {
            return Result.Failure<Order>(
                Error.Validation("Orders.MixedCurrency", "All order line items must use the same currency."));
        }

        // Calculate discount if promo code provided (all monetary discount types supported).
        decimal discountAmount = 0;
        PromoCode? appliedPromo = null;
        string? promoCodesApplied = null;
        if (!string.IsNullOrEmpty(request.PromoCode))
        {
            var promo = await promoCodeService.GetPromoCodeByCodeAsync(request.PromoCode).ConfigureAwait(false);
            if (promo?.TenantId == order.TenantId &&
                await promoCodeService.ValidatePromoCodeAsync(promo.Code, order.UserId, request.ProductId).ConfigureAwait(false))
            {
                if (promo.Type == PromoCodeType.FixedAmountOff && promo.Currency != pricingVersion.Currency)
                {
                    return Result.Failure<Order>(
                        Error.Validation("Orders.MixedCurrency", "Fixed discounts must use the line-item currency."));
                }

                if (promo.Type is PromoCodeType.FreeTrialExtension or PromoCodeType.FreeMonths)
                {
                    return Result.Failure<Order>(
                        Error.Validation("Orders.UnsupportedPromoType",
                            "Free trial extension and free months codes apply at subscription billing, not order line items."));
                }

                discountAmount = promo.CalculateDiscount(price * request.Quantity);
                promoCodesApplied = System.Text.Json.JsonSerializer.Serialize(new[] { promo.Code });
                appliedPromo = promo;
            }
        }

        if (discountAmount >= price * request.Quantity)
        {
            return Result.Failure<Order>(
                Error.Validation("Orders.InvalidPrice", "Discounts must leave a positive authoritative line total."));
        }

        // Add line item with price snapshot
        var lineItem = order.AddLineItem(
            request.ProductId,
            product.Name,
            new OrderLineItemPricingSnapshot(
                pricing.Id,
                pricingVersion.Id,
                pricingVersion.PriceVersion,
                pricingVersion.BasePrice,
                pricingVersion.SalePrice,
                price,
                pricingVersion.Currency),
            request.Quantity,
            discountAmount,
            promoCodesApplied,
            pricing.Name,
            product.Type == ProductType.Subscription);

        // Record the redemption atomically before persisting the order so that a rejected
        // (limit-exhausted) code fails the whole add. Usage is idempotent per order line.
        if (appliedPromo is not null)
        {
            var idempotencyKey = $"order:{order.Id}:line:{lineItem.Id}:code:{appliedPromo.Code}";
            var (status, _) = await promoUsageRecorder
                .RecordAsync(
                    appliedPromo,
                    order.UserId,
                    discountAmount,
                    pricingVersion.Currency,
                    idempotencyKey,
                    order.Id,
                    lineItem.Id,
                    cancellationToken)
                .ConfigureAwait(false);

            if (status is PromoUsageRecordingStatus.RejectedTotalLimit or PromoUsageRecordingStatus.RejectedPerUserLimit)
            {
                return Result.Failure<Order>(
                    Error.Conflict("Orders.PromoLimitReached", "The promo code has reached its usage limit."));
            }
        }

        await orderRepository.UpdateAsync(order, cancellationToken).ConfigureAwait(false);
        await orderRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(order);
    }
}
