using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Orders;

/// <summary>
/// Controller for managing orders and purchases.
/// Dispatches all operations through CQRS commands/queries via <see cref="ISender"/>.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("commerce/orders")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/orders")]
[Authorize]
public class OrdersController(ISender sender, IActorContextAccessor actorContextAccessor) : BaseApiController
{
    // ── Result → ActionResult mapping (DRY) ─────────────────────────────

    /// <summary>
    ///     Maps a <see cref="Result{OrderOperationResult}"/> to an <see cref="ActionResult{OrderDto}"/>.
    /// </summary>
    private ActionResult<OrderDto> ToOrderActionResult(Result<OrderOperationResult> result)
    {
        if (result.IsFailure)
        {
            return BadRequest(CreateProblemDetails(result.Error.Description));
        }

        return Ok(MapToDto(result.Value.Order));
    }

    /// <summary>
    ///     Maps a <see cref="Result"/> to 204 NoContent or 400 BadRequest.
    /// </summary>
    private IActionResult ToResultActionResult(Result result)
    {
        if (result.IsFailure)
        {
            return BadRequest(CreateProblemDetails(result.Error.Description));
        }

        return NoContent();
    }

    /// <summary>
    ///     Creates a consistent ProblemDetails error response.
    /// </summary>
    private static ProblemDetails CreateProblemDetails(string? errorMessage) => new()
    {
        Title = "Order Operation Failed",
        Detail = errorMessage ?? "An unexpected error occurred.",
        Status = 400,
        Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
    };

    // ── Endpoints ────────────────────────────────────────────────────────

    /// <summary>
    /// Create a new order with idempotency protection
    /// </summary>
    [HttpPost]
    [MinimumOrderRoute]
    [RequirePermission(OrdersPermission.Keys.Create)]
    public async Task<ActionResult<OrderDto>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateOrderCommand(
            request.IdempotencyKey,
            GetIpAddress(),
            GetUserAgent());

        var result = await sender.Send<Result<OrderOperationResult>>(command, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return BadRequest(CreateProblemDetails(result.Error.Description));
        }

        var dto = MapToDto(result.Value.Order);

        return result.Value.WasDuplicate
            ? Ok(dto) // Return 200 for idempotent duplicate
            : CreatedAtAction(nameof(GetOrder), new { orderId = dto.Id }, dto);
    }

    /// <summary>
    /// Add a product to an existing order
    /// </summary>
    [HttpPost("{orderId:guid}/items")]
    [MinimumOrderRoute]
    [RequirePermission(OrdersPermission.Keys.Create)]
    public async Task<ActionResult<OrderDto>> AddProductToOrder(
        Guid orderId,
        [FromBody] AddOrderItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new AddProductToOrderCommand(
            orderId,
            request.ProductId,
            request.ProductPricingId,
            request.ProductPricingVersionId,
            request.Quantity,
            request.PromoCode);
        var result = await sender.Send<Result<Order>>(command, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return BadRequest(CreateProblemDetails(result.Error.Description));
        }

        return Ok(MapToDto(result.Value));
    }

    /// <summary>
    /// Complete an order (process payment, grant entitlements)
    /// </summary>
    [HttpPost("{orderId:guid}:complete")]
    [MinimumOrderRoute]
    [RequirePermission(OrdersPermission.Keys.Create)]
    public async Task<ActionResult<OrderDto>> CompleteOrder(
        Guid orderId,
        [FromBody] CompleteOrderRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new CompleteOrderCommand(
            orderId,
            request?.PaymentId,
            request?.PaymentProviderReference,
            request?.PaymentMethod,
            request?.MarketplaceSettlement);
        var result = await sender.Send<Result<OrderOperationResult>>(command, cancellationToken).ConfigureAwait(false);

        return ToOrderActionResult(result);
    }

    /// <summary>
    /// Cancel a pending order
    /// </summary>
    [HttpPost("{orderId:guid}:cancel")]
    [RequirePermission(OrdersPermission.Keys.Create)]
    public async Task<IActionResult> CancelOrder(
        Guid orderId,
        [FromBody] CancelOrderRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new CancelOrderCommand(orderId, request?.Reason);
        var result = await sender.Send<Result>(command, cancellationToken).ConfigureAwait(false);

        return ToResultActionResult(result);
    }

    /// <summary>
    /// Process a refund for a completed order
    /// </summary>
    [HttpPost("{orderId:guid}:refund")]
    [RequirePermission(OrdersPermission.Keys.Refund)]
    public async Task<ActionResult<OrderDto>> RefundOrder(
        Guid orderId,
        [FromBody] RefundOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new RefundOrderCommand(orderId, request.Amount, request.Reason ?? "");
        var result = await sender.Send<Result<OrderOperationResult>>(command, cancellationToken).ConfigureAwait(false);

        return ToOrderActionResult(result);
    }

    /// <summary>
    /// Get an order by ID
    /// </summary>
    [HttpGet("{orderId:guid}")]
    [MinimumOrderRoute]
    [RequirePermission(OrdersPermission.Keys.Read)]
    public async Task<ActionResult<OrderDto>> GetOrder(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await sender.Send<Order?>(new GetOrderQuery(orderId), cancellationToken).ConfigureAwait(false);

        if (order == null)
        {
            return NotFound();
        }

        var actor = actorContextAccessor.ActorContext;
        if (order.UserId != actor.SubjectIdAsGuid && !actor.HasPermission(OrdersPermission.Keys.ReadAll))
        {
            return NotFound();
        }

        return Ok(MapToDto(order));
    }

    /// <summary>
    /// List orders with optional filtering.
    /// Use owner=me to get current user's orders.
    /// Admin users can list all orders without owner filter.
    /// </summary>
    [HttpGet]
    [MinimumOrderRoute]
    [RequirePermission(OrdersPermission.Keys.Read)]
    public async Task<ActionResult<IEnumerable<OrderDto>>> ListOrders(
        [FromQuery] string? owner = null,
        [FromQuery] OrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } actorId || actorId == Guid.Empty)
        {
            return Forbid();
        }

        if (!string.Equals(owner, "me", StringComparison.OrdinalIgnoreCase) &&
            actor.HasPermission(OrdersPermission.Keys.ReadAll))
        {
            var allOrders = await sender.Send<IEnumerable<Order>>(
                new GetAllOrdersQuery(status),
                cancellationToken).ConfigureAwait(false);
            return Ok(allOrders.Select(MapToDto));
        }

        // owner=me resolves to current user's orders
        var userOrders = await sender.Send<IEnumerable<Order>>(
            new GetUserOrdersQuery(actorId, status),
            cancellationToken).ConfigureAwait(false);

        return Ok(userOrders.Select(MapToDto));
    }

    /// <summary>
    /// Check if an order exists
    /// </summary>
    [HttpHead("{orderId:guid}")]
    [RequirePermission(OrdersPermission.Keys.Read)]
    public async Task<IActionResult> OrderExists(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var exists = await sender.Send<bool>(new OrderExistsQuery(orderId), cancellationToken).ConfigureAwait(false);
        return exists ? Ok() : NotFound();
    }

    /// <summary>
    /// Update an order (partial update)
    /// </summary>
    [HttpPatch("{orderId:guid}")]
    [RequirePermission(OrdersPermission.Keys.Create)]
    public async Task<ActionResult<OrderDto>> UpdateOrder(
        Guid orderId,
        [FromBody] PatchOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateOrderCommand(orderId, request.Currency, request.Notes, request.Metadata);
        var result = await sender.Send<Result<OrderOperationResult>>(command, cancellationToken).ConfigureAwait(false);

        return ToOrderActionResult(result);
    }

    /// <summary>
    /// Delete an order (soft delete)
    /// </summary>
    [HttpDelete("{orderId:guid}")]
    [RequirePermission(OrdersPermission.Keys.Delete)]
    public async Task<IActionResult> DeleteOrder(
        Guid orderId,
        [FromQuery] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteOrderCommand(orderId, reason);
        var result = await sender.Send<Result>(command, cancellationToken).ConfigureAwait(false);

        return ToResultActionResult(result);
    }

    /// <summary>
    /// Capture payment for an authorized order
    /// </summary>
    [HttpPost("{orderId:guid}:capture")]
    [MinimumOrderRoute]
    [RequirePermission(OrdersPermission.Keys.Create)]
    public async Task<ActionResult<OrderCaptureDto>> CaptureOrder(
        Guid orderId,
        [FromBody] CaptureOrderRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new CaptureOrderCommand(orderId, request?.PaymentMethodId ?? string.Empty);
        var result = await sender.Send<Result<OrderOperationResult>>(command, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return BadRequest(CreateProblemDetails(result.Error.Description));
        }

        var value = result.Value;
        var order = MapToDto(value.Order);
        return Ok(new OrderCaptureDto(
            order.Id,
            order.UserId,
            order.IdempotencyKey,
            order.Status,
            order.Subtotal,
            order.DiscountTotal,
            order.TaxAmount,
            order.Total,
            order.Currency,
            order.PaymentProviderReference,
            order.PaymentMethod,
            order.PaidAt,
            order.RefundedAt,
            order.RefundAmount,
            order.RefundReason,
            order.CreatedAt,
            order.UpdatedAt,
            order.LineItems,
            value.PaymentState ?? (value.Order.Status == OrderStatus.Paid ? OrderChargeState.Succeeded : null),
            value.PaymentId ?? value.Order.PaymentId,
            value.ClientActionToken,
            value.PaymentMessage));
    }

    [HttpPost("{orderId:guid}:payment-intent")]
    [MinimumOrderRoute]
    [RequirePermission(OrdersPermission.Keys.Create)]
    public async Task<ActionResult<OrderPaymentIntentPreparation>> PreparePaymentIntent(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send<Result<OrderPaymentIntentPreparation>>(
            new PrepareOrderPaymentIntentCommand(orderId), cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return BadRequest(CreateProblemDetails(result.Error.Description));
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Place an order on hold
    /// </summary>
    [HttpPost("{orderId:guid}:hold")]
    [RequirePermission(OrdersPermission.Keys.Create)]
    public async Task<ActionResult<OrderDto>> HoldOrder(
        Guid orderId,
        [FromBody] HoldOrderRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var command = new HoldOrderCommand(orderId, request?.Reason);
        var result = await sender.Send<Result<OrderOperationResult>>(command, cancellationToken).ConfigureAwait(false);

        return ToOrderActionResult(result);
    }

    /// <summary>
    /// Release a held order
    /// </summary>
    [HttpPost("{orderId:guid}:release")]
    [RequirePermission(OrdersPermission.Keys.Create)]
    public async Task<ActionResult<OrderDto>> ReleaseOrder(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var command = new ReleaseOrderCommand(orderId);
        var result = await sender.Send<Result<OrderOperationResult>>(command, cancellationToken).ConfigureAwait(false);

        return ToOrderActionResult(result);
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private Guid GetUserId()
    {
        return actorContextAccessor.ActorContext.SubjectIdAsGuid ?? Guid.Empty;
    }

    private string? GetIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string GetUserAgent()
    {
        return HttpContext.Request.Headers.UserAgent.ToString();
    }

    private static OrderDto MapToDto(Order order) => new(
        order.Id,
        order.UserId,
        order.IdempotencyKey,
        order.Status,
        order.Subtotal,
        order.DiscountTotal,
        order.TaxAmount,
        order.Total,
        order.Currency,
        order.PaymentProviderReference,
        order.PaymentMethod,
        order.PaidAt,
        order.RefundedAt,
        order.RefundAmount,
        order.RefundReason,
        order.CreatedAt,
        order.UpdatedAt,
        order.LineItems.Select(li => new OrderLineItemDto(
            li.Id,
            li.ProductId,
            li.ProductPricingId,
            li.ProductPricingVersionId,
            li.PriceVersionSnapshot,
            li.ProductNameSnapshot,
            li.UnitPriceSnapshot,
            li.BasePriceSnapshot,
            li.SalePriceSnapshot,
            li.CurrencySnapshot,
            li.Quantity,
            li.DiscountAmount,
            li.PromoCodesApplied,
            li.LineTotal,
            li.IsSubscription
        )).ToList()
    );
}

/// <summary>Request to create a new order</summary>
public sealed record CreateOrderRequest(
    string IdempotencyKey);

/// <summary>Request to add an item to an order</summary>
public sealed record AddOrderItemRequest(
    Guid ProductId,
    Guid ProductPricingId,
    Guid ProductPricingVersionId,
    int Quantity = 1,
    string? PromoCode = null);

/// <summary>Request to complete an order</summary>
/// <param name="PaymentId">Optional internal Payment entity ID for Payment→Order linkage</param>
/// <param name="PaymentProviderReference">Optional external payment provider reference</param>
/// <param name="PaymentMethod">Optional payment method description (e.g., "card", "bank_transfer")</param>
/// <param name="MarketplaceSettlement">Economy Marketplace settlement intent; mutually exclusive with fiat payment references.</param>
public sealed record CompleteOrderRequest(
    Guid? PaymentId = null,
    string? PaymentProviderReference = null,
    string? PaymentMethod = null,
    CompleteOrderMarketplaceSettlement? MarketplaceSettlement = null);

/// <summary>Request to cancel an order</summary>
public sealed record CancelOrderRequest(string? Reason = null);

/// <summary>Request to refund an order</summary>
public sealed record RefundOrderRequest(decimal? Amount = null, string? Reason = null);

/// <summary>Order DTO</summary>
public sealed record OrderDto(
    Guid Id,
    Guid UserId,
    string IdempotencyKey,
    OrderStatus Status,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxAmount,
    decimal Total,
    string Currency,
    string? PaymentProviderReference,
    string? PaymentMethod,
    DateTime? PaidAt,
    DateTime? RefundedAt,
    decimal? RefundAmount,
    string? RefundReason,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<OrderLineItemDto> LineItems);

/// <summary>Order line item DTO</summary>
public sealed record OrderLineItemDto(
    Guid Id,
    Guid ProductId,
    Guid ProductPricingId,
    Guid ProductPricingVersionId,
    int PriceVersion,
    string ProductName,
    decimal UnitPrice,
    decimal BasePrice,
    decimal? SalePrice,
    string Currency,
    int Quantity,
    decimal DiscountAmount,
    string? PromoCodesApplied,
    decimal LineTotal,
    bool IsSubscription);

/// <summary>Request to partially update an order</summary>
public sealed record PatchOrderRequest(
    string? Currency = null,
    string? Notes = null,
    Dictionary<string, string>? Metadata = null);

/// <summary>Request to capture payment for an order</summary>
public sealed record CaptureOrderRequest(string PaymentMethodId);

/// <summary>Order capture result including any client-side payment action.</summary>
public sealed record OrderCaptureDto(
    Guid Id,
    Guid UserId,
    string IdempotencyKey,
    OrderStatus Status,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxAmount,
    decimal Total,
    string Currency,
    string? PaymentProviderReference,
    string? PaymentMethod,
    DateTime? PaidAt,
    DateTime? RefundedAt,
    decimal? RefundAmount,
    string? RefundReason,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<OrderLineItemDto> LineItems,
    OrderChargeState? PaymentState,
    Guid? PaymentId,
    string? ClientActionToken,
    string? PaymentMessage);

/// <summary>Request to hold an order</summary>
public sealed record HoldOrderRequest(string? Reason = null);
