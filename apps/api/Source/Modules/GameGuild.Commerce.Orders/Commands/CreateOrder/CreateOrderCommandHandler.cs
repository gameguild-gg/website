using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Commerce.Orders;

/// <summary>
/// Handler for creating a new order with idempotency protection
/// </summary>
public sealed class CreateOrderCommandHandler(
    IOrderRepository orderRepository,
    IActorContextAccessor actorContextAccessor)
    : ICommandHandler<CreateOrderCommand, Result<OrderOperationResult>>
{
    public async Task<Result<OrderOperationResult>> Handle(
        CreateOrderCommand request,
        CancellationToken cancellationToken)
    {
        if (!OrderActorContext.TryResolve(actorContextAccessor, out var actor, out var actorError))
        {
            return Result.Failure<OrderOperationResult>(actorError);
        }

        // Check for existing order with same idempotency key
        var existingOrder = await orderRepository.GetByIdempotencyKeyAsync(
            request.IdempotencyKey, cancellationToken).ConfigureAwait(false);

        if (existingOrder != null)
        {
            if (existingOrder.UserId != actor.UserId || existingOrder.TenantId != actor.TenantId)
            {
                return Result.Failure<OrderOperationResult>(
                    Error.Forbidden("Orders.Forbidden", "The idempotency key belongs to another actor context."));
            }

            return Result.Success(OrderOperationResult.FromOrder(existingOrder, wasDuplicate: true));
        }

        // Create new order
        var order = Order.Create(
            actor.UserId,
            request.IdempotencyKey,
            actor.TenantId,
            "USD",
            request.IpAddress,
            request.UserAgent);

        await orderRepository.AddAsync(order, cancellationToken).ConfigureAwait(false);
        await orderRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(OrderOperationResult.FromOrder(order));
    }
}
