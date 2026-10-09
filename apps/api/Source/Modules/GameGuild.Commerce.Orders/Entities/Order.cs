using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Orders;

/// <summary>
/// Represents a purchase order containing one or more line items
/// </summary>
[Table("orders")]
[Index(nameof(UserId))]
[Index(nameof(Status))]
[Index(nameof(IdempotencyKey), IsUnique = true)]
[Index(nameof(CreatedAt))]
[Index(nameof(TenantId))]
public class Order : StatefulEntity<OrderStatus>
{
    /// <summary>Default constructor for EF Core</summary>
    public Order() { }

    /// <summary>User who placed the order</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>Navigation property to user</summary>
    public virtual User User { get; set; } = null!;

    /// <summary>Idempotency key for preventing duplicate orders</summary>
    [Required]
    [MaxLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Current order status</summary>
    public override OrderStatus Status { get; protected set; } = OrderStatus.Pending;

    /// <summary>
    ///     Valid state transitions for orders (monotonic state machine).
    ///     Economic invariant: No backward transitions that would reverse economic effects.
    ///     Extended to support Paid→Fulfilled flow for explicit fulfillment tracking.
    /// </summary>
    protected override IReadOnlyDictionary<OrderStatus, IReadOnlySet<OrderStatus>> ValidTransitions { get; } =
        new Dictionary<OrderStatus, IReadOnlySet<OrderStatus>>
        {
            { OrderStatus.Pending, new HashSet<OrderStatus> { OrderStatus.Processing, OrderStatus.Paid, OrderStatus.Completed, OrderStatus.Failed, OrderStatus.Cancelled, OrderStatus.OnHold } },
            { OrderStatus.Processing, new HashSet<OrderStatus> { OrderStatus.Paid, OrderStatus.Completed, OrderStatus.Failed, OrderStatus.Cancelled, OrderStatus.OnHold } },
            { OrderStatus.OnHold, new HashSet<OrderStatus> { OrderStatus.Pending, OrderStatus.Processing, OrderStatus.Cancelled } }, // Can be released back to processing or cancelled
            { OrderStatus.Paid, new HashSet<OrderStatus> { OrderStatus.Fulfilled, OrderStatus.Failed } }, // Paid but not yet fulfilled
            { OrderStatus.Fulfilled, new HashSet<OrderStatus> { OrderStatus.Refunded, OrderStatus.PartiallyRefunded, OrderStatus.Disputed } }, // Terminal success state
            { OrderStatus.Completed, new HashSet<OrderStatus> { OrderStatus.Refunded, OrderStatus.PartiallyRefunded, OrderStatus.Disputed } }, // Legacy: treated as Fulfilled
            { OrderStatus.Failed, new HashSet<OrderStatus>() }, // Terminal state
            { OrderStatus.Cancelled, new HashSet<OrderStatus>() }, // Terminal state
            { OrderStatus.Refunded, new HashSet<OrderStatus>() }, // Terminal state
            { OrderStatus.PartiallyRefunded, new HashSet<OrderStatus> { OrderStatus.Refunded, OrderStatus.Disputed } },
            { OrderStatus.Disputed, new HashSet<OrderStatus> { OrderStatus.Fulfilled, OrderStatus.Completed, OrderStatus.Refunded } }
        };

    /// <summary>Subtotal before discounts and taxes</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal Subtotal { get; private set; }

    /// <summary>Total discount amount applied</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal DiscountTotal { get; private set; }

    /// <summary>Tax amount</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal TaxAmount { get; private set; }

    /// <summary>Final total charged</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal Total { get; private set; }

    /// <summary>Currency code (ISO 4217)</summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; private set; } = "USD";

    /// <summary>External payment provider reference</summary>
    [MaxLength(200)]
    public string? PaymentProviderReference { get; set; }

    /// <summary>
    ///     External payment ID from payment gateway (Stripe charge ID, PayPal transaction ID, etc.).
    ///     Used for reconciliation with payment provider records.
    /// </summary>
    [MaxLength(200)]
    public string? ExternalPaymentId { get; set; }

    /// <summary>Payment method used</summary>
    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    /// <summary>When payment was completed</summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>When order was refunded (if applicable)</summary>
    public DateTime? RefundedAt { get; set; }

    /// <summary>Refund amount if partially refunded</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? RefundAmount { get; set; }

    /// <summary>Reason for refund</summary>
    [MaxLength(500)]
    public string? RefundReason { get; set; }

    /// <summary>IP address of the purchaser</summary>
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    /// <summary>User agent string</summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>Additional metadata as JSON</summary>
    [Column(TypeName = "jsonb")]
    public string? Metadata { get; set; }

    // ═══════════════════════════════════════════════════════════════════════
    // ECONOMIC MODEL ALIGNMENT - Added for unified commerce flow
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Type of order - determines fulfillment logic and validation rules.
    ///     Subscribe/Upgrade/Downgrade orders require TargetSubscriptionId.
    /// </summary>
    public OrderType OrderType { get; set; } = OrderType.OneTimePurchase;

    /// <summary>
    ///     Target subscription ID for upgrade/downgrade/renewal orders.
    ///     Required when OrderType is Upgrade, Downgrade, AddOn, or Renewal.
    /// </summary>
    public Guid? TargetSubscriptionId { get; set; }

    /// <summary>
    ///     When fulfillment was completed (entitlements granted).
    ///     Distinct from PaidAt - payment can succeed before fulfillment completes.
    ///     Only set when Status transitions to Fulfilled.
    /// </summary>
    public DateTime? FulfilledAt { get; private set; }

    /// <summary>
    ///     Foreign key to Payment entity for reconciliation.
    ///     Links order to the payment transaction that settled it.
    /// </summary>
    public Guid? PaymentId { get; private set; }

    /// <summary>Order line items</summary>
    public virtual ICollection<OrderLineItem> LineItems { get; set; } = new List<OrderLineItem>();

    /// <summary>Create a new order with idempotency key (TenantId required - fail-closed)</summary>
    /// <exception cref="ArgumentException">Thrown when tenantId is null or empty</exception>
    public static Order Create(
        Guid userId,
        string idempotencyKey,
        Guid tenantId,
        string currency = "USD",
        string? ipAddress = null,
        string? userAgent = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required for financial entities (fail-closed)", nameof(tenantId));
        }

        return new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            IdempotencyKey = idempotencyKey,
            Currency = currency,
            TenantId = tenantId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Status = OrderStatus.Pending
        };
    }

    /// <summary>
    ///     Transitions to a new status with validation and raises domain event for audit trail
    /// </summary>
    /// <param name="newStatus">The new status to transition to</param>
    /// <param name="reason">Optional reason for the transition (e.g., failure reason, cancellation reason)</param>
    /// <param name="externalPaymentId">Optional external payment ID from payment gateway</param>
    /// <exception cref="InvalidOperationException">Thrown when transition is not allowed</exception>
    private void TransitionToWithReason(OrderStatus newStatus, string? reason = null, string? externalPaymentId = null)
    {
        var previousStatus = Status;
        TransitionTo(newStatus); // Base class validation
        
        // Raise domain event for audit trail
        Raise(new OrderStateChangedEvent(
            Id, 
            TenantId ?? Guid.Empty, 
            previousStatus, 
            newStatus, 
            reason,
            externalPaymentId));
    }

    /// <summary>
    ///     Called after a successful state transition.
    ///     Raises domain event for audit trail.
    /// </summary>
    protected override void OnStatusChanged(OrderStatus oldStatus, OrderStatus newStatus)
    {
        Raise(new OrderStateChangedEvent(
            Id,
            TenantId ?? Guid.Empty,
            oldStatus,
            newStatus,
            reason: null,
            externalPaymentId: null));
    }

    /// <summary>Add a line item to the order</summary>
    public OrderLineItem AddLineItem(
        Guid productId,
        string productName,
        OrderLineItemPricingSnapshot pricing,
        int quantity = 1,
        decimal discountAmount = 0,
        string? promoCodesApplied = null,
        string pricingTierName = "",
        bool isSubscription = false)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot add line items to an order in {Status} status.");
        }

        if (TenantId is null || TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("A tenant is required before adding order line items.");
        }

        if (productId == Guid.Empty || pricing.ProductPricingId == Guid.Empty || pricing.ProductPricingVersionId == Guid.Empty)
        {
            throw new ArgumentException("Product and pricing identifiers are required.", nameof(pricing));
        }

        if (pricing.PriceVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pricing), "Price version must be positive.");
        }

        if (pricing.BasePrice <= 0 || pricing.UnitPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pricing), "Authoritative prices must be positive.");
        }

        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (discountAmount < 0 || discountAmount >= pricing.UnitPrice * quantity)
        {
            throw new ArgumentOutOfRangeException(nameof(discountAmount), "Discount must leave a positive line total.");
        }

        if (pricing.Currency.Length != 3 || !pricing.Currency.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException("Currency must be a three-letter uppercase code.", nameof(pricing));
        }

        if (LineItems.Count == 0)
        {
            Currency = pricing.Currency;
        }
        else if (Currency != pricing.Currency || LineItems.Any(item => item.CurrencySnapshot != pricing.Currency))
        {
            throw new InvalidOperationException("All order line items must use the same currency.");
        }

        var lineItem = OrderLineItem.Create(
            Id,
            TenantId.Value,
            productId,
            productName,
            pricingTierName,
            pricing,
            quantity,
            discountAmount,
            promoCodesApplied,
            isSubscription);

        lineItem.Touch();

        LineItems.Add(lineItem);
        RecalculateTotals();
        return lineItem;
    }

    /// <summary>Recalculate order totals from line items (only allowed before completion)</summary>
    /// <exception cref="InvalidOperationException">Thrown when order is already completed</exception>
    public void RecalculateTotals()
    {
        if (Status != OrderStatus.Pending && Status != OrderStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot modify order totals in {Status} status. Financial amounts are immutable after processing.");
        }

        Subtotal = LineItems.Sum(li => li.UnitPriceSnapshot * li.Quantity);
        DiscountTotal = LineItems.Sum(li => li.DiscountAmount);
        Total = Subtotal - DiscountTotal + TaxAmount;
        Touch();
    }

    /// <summary>Mark order as paid (with state machine validation and audit trail)</summary>
    /// <param name="paymentProviderReference">Optional payment provider reference</param>
    /// <param name="paymentMethod">Optional payment method description</param>
    /// <param name="externalPaymentId">External payment ID from payment gateway for reconciliation</param>
    public void MarkAsPaid(string? paymentProviderReference = null, string? paymentMethod = null, string? externalPaymentId = null)
    {
        TransitionToWithReason(OrderStatus.Completed, reason: null, externalPaymentId: externalPaymentId);
        PaidAt = SystemClock.UtcNow;
        PaymentProviderReference = paymentProviderReference;
        PaymentMethod = paymentMethod;
        ExternalPaymentId = externalPaymentId;
        Touch();
    }

    /// <summary>
    ///     Mark order as paid without fulfillment (explicit Paid state).
    ///     Use this when payment succeeds but fulfillment is async.
    ///     Call MarkAsFulfilled() after entitlements are granted.
    /// </summary>
    /// <param name="paymentId">Internal Payment entity ID</param>
    /// <param name="externalPaymentId">External payment gateway reference</param>
    public void MarkAsPaidPendingFulfillment(Guid paymentId, string? externalPaymentId = null)
    {
        TransitionToWithReason(OrderStatus.Paid, reason: null, externalPaymentId: externalPaymentId);
        PaymentId = paymentId;
        PaidAt = SystemClock.UtcNow;
        ExternalPaymentId = externalPaymentId;
        Touch();
    }

    /// <summary>
    /// Reserves a pending order for an authoritative payment attempt.
    /// Concurrent item, cancellation, and capture updates are rejected by the order version token.
    /// </summary>
    public void StartPaymentProcessing()
    {
        if (Status == OrderStatus.Processing)
        {
            return;
        }

        TransitionToWithReason(OrderStatus.Processing, reason: "Authoritative payment capture started");
        Touch();
    }

    public void ReleasePaymentReservation()
    {
        if (Status != OrderStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot release payment reservation for order in {Status} status");
        }

        Status = OrderStatus.Pending;
        Touch();
    }

    /// <summary>
    ///     Mark order as fulfilled after entitlements are granted.
    ///     Must be in Paid or Completed status.
    ///     Economic invariant: FulfilledAt is set exactly once.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when order is not in Paid status</exception>
    public void MarkAsFulfilled()
    {
        if (FulfilledAt.HasValue)
        {
            return; // Idempotent - already fulfilled
        }

        // Allow transition from Paid or legacy Completed
        if (Status == OrderStatus.Completed)
        {
            // Legacy order already marked as Completed - just set FulfilledAt
            FulfilledAt = SystemClock.UtcNow;
            Touch();
            return;
        }

        TransitionToWithReason(OrderStatus.Fulfilled, reason: "Entitlements granted");
        FulfilledAt = SystemClock.UtcNow;
        Touch();
    }

    /// <summary>
    ///     Associates a Payment entity with this order.
    ///     Single payment per order invariant enforced.
    /// </summary>
    /// <param name="paymentId">The Payment entity ID</param>
    /// <exception cref="InvalidOperationException">Thrown when order already has a different payment</exception>
    public void AssociatePayment(Guid paymentId)
    {
        if (PaymentId is Guid existingPaymentId && existingPaymentId != paymentId)
        {
            throw new InvalidOperationException($"Order {Id} already has payment {PaymentId}. Single payment per order enforced.");
        }

        PaymentId = paymentId;
        Touch();
    }

    /// <summary>Mark order as failed (with state machine validation and audit trail)</summary>
    /// <param name="reason">Reason for the failure</param>
    public void MarkAsFailed(string? reason = null)
    {
        TransitionToWithReason(OrderStatus.Failed, reason: reason);
        Metadata = reason;
        Touch();
    }

    /// <summary>Cancel the order (with state machine validation and audit trail)</summary>
    /// <param name="reason">Reason for cancellation</param>
    public void Cancel(string? reason = null)
    {
        TransitionToWithReason(OrderStatus.Cancelled, reason: reason);
        Metadata = reason;
        Touch();
    }

    /// <summary>Place order on hold for review (with state machine validation and audit trail)</summary>
    /// <param name="reason">Reason for holding the order</param>
    public void PlaceOnHold(string? reason = null)
    {
        TransitionToWithReason(OrderStatus.OnHold, reason: reason);
        Metadata = reason;
        Touch();
    }

    /// <summary>Release held order back to processing (with state machine validation and audit trail)</summary>
    public void Release()
    {
        TransitionToWithReason(OrderStatus.Pending, reason: "Released from hold");
        Touch();
    }

    /// <summary>Soft delete the order</summary>
    public new void SoftDelete()
    {
        DeletedAt = SystemClock.UtcNow;
        Touch();
    }

    /// <summary>Process refund (with state machine validation and audit trail)</summary>
    /// <param name="amount">Amount to refund</param>
    /// <param name="reason">Reason for the refund</param>
    public void ProcessRefund(decimal amount, string reason)
    {
        var newStatus = amount >= Total ? OrderStatus.Refunded : OrderStatus.PartiallyRefunded;
        TransitionToWithReason(newStatus, reason: reason);
        RefundAmount = amount;
        RefundReason = reason;
        RefundedAt = SystemClock.UtcNow;
        Touch();
    }

    /// <summary>
    ///     Whether the order is in a terminal success state (fulfilled or legacy completed)
    /// </summary>
    public bool IsSuccessfullyCompleted => 
        Status == OrderStatus.Fulfilled || 
        (Status == OrderStatus.Completed && FulfilledAt.HasValue);
}
