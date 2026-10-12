using GameGuild.Commerce.Products;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Orders;

/// <summary>
/// Handler for cancelling a pending order
/// </summary>
public sealed class CancelOrderCommandHandler(
    IOrderRepository orderRepository,
    IProductInventoryService? inventoryService = null)
    : ICommandHandler<CancelOrderCommand, Result>
{
    public async Task<Result> Handle(
        CancelOrderCommand request,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetWithLineItemsAsync(request.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return Result.Failure(Error.NotFound("Orders.NotFound", $"Order {request.OrderId} not found"));
        }

        if (order.Status != OrderStatus.Pending)
        {
            return Result.Failure(Error.Failure("Orders.InvalidStatus", "Cannot cancel order in current state"));
        }

        // Inventory release (issue #401): give back units reserved by pending lines.
        // Untracked products are skipped; releases ride the same save as the cancel.
        if (inventoryService is not null && order.LineItems.Count > 0)
        {
            await inventoryService.ReleaseAsync(
                order.LineItems.Select(lineItem => (lineItem.ProductId, lineItem.Quantity)).ToList(),
                cancellationToken).ConfigureAwait(false);
        }

        order.Cancel(request.Reason);
        order.Touch();

        await orderRepository.UpdateAsync(order, cancellationToken).ConfigureAwait(false);
        await orderRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
