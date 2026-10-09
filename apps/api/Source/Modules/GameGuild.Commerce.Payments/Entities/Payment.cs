using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Entity representing a payment transaction
/// </summary>
[Table("payments")]
[Index(nameof(TenantId), nameof(Status))]
[Index(nameof(SubscriptionId))]
[Index(nameof(ExternalPaymentId), IsUnique = true)]
[Index(nameof(IdempotencyKey), IsUnique = true)]
public class Payment : EntityBase
{
    private static readonly Dictionary<PaymentStatus, HashSet<PaymentStatus>> ValidTransitions = new()
    {
        { PaymentStatus.Pending, new() { PaymentStatus.Processing, PaymentStatus.Cancelled, PaymentStatus.Failed } },
        { PaymentStatus.Processing, new() { PaymentStatus.Succeeded, PaymentStatus.Failed, PaymentStatus.RequiresAction, PaymentStatus.Cancelled } },
        { PaymentStatus.RequiresAction, new() { PaymentStatus.Processing, PaymentStatus.Succeeded, PaymentStatus.Failed, PaymentStatus.Cancelled } },
        { PaymentStatus.Succeeded, new() { PaymentStatus.Refunded, PaymentStatus.Disputed } },
        { PaymentStatus.Failed, new() { PaymentStatus.Pending } }, // Retry allowed
        { PaymentStatus.Cancelled, new HashSet<PaymentStatus>() }, // Terminal state
        { PaymentStatus.Refunded, new HashSet<PaymentStatus>() }, // Terminal state
        { PaymentStatus.Disputed, new() { PaymentStatus.Succeeded, PaymentStatus.Refunded } }
    };

    /// <summary>Default constructor for EF Core</summary>
    public Payment() { }

    /// <summary>Tenant ID for multi-tenant isolation</summary>
    [Required]
    public new Guid TenantId { get; private set; }

    /// <summary>Optional subscription reference</summary>
    public Guid? SubscriptionId { get; private set; }

    /// <summary>Optional order reference</summary>
    public Guid? OrderId { get; private set; }

    /// <summary>Optional invoice reference</summary>
    public Guid? InvoiceId { get; private set; }

    /// <summary>Payment amount in smallest currency unit</summary>
    [Column(TypeName = "decimal(18,2)")]
    [Required]
    public decimal Amount { get; private set; }

    /// <summary>ISO 4217 currency code (e.g., USD, EUR)</summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; private set; } = "USD";

    /// <summary>Current payment status</summary>
    [Required]
    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;

    /// <summary>Payment gateway provider (stripe, paypal, apple, google)</summary>
    [Required]
    [MaxLength(50)]
    public string Provider { get; private set; } = "stripe";

    /// <summary>External payment ID from payment gateway</summary>
    [MaxLength(255)]
    public string? ExternalPaymentId { get; private set; }

    /// <summary>External transaction ID from payment gateway</summary>
    [MaxLength(255)]
    public string? ExternalTransactionId { get; private set; }

    /// <summary>External customer ID from payment gateway</summary>
    [MaxLength(255)]
    public string? ExternalCustomerId { get; private set; }

    /// <summary>Provider environment associated with the external payment object.</summary>
    [MaxLength(32)]
    public string? ProviderEnvironment { get; private set; }

    /// <summary>Connected or merchant account that owns the external payment object.</summary>
    [MaxLength(255)]
    public string? ProviderAccountId { get; private set; }

    /// <summary>Canonical provider object ID once a scoped provider mapping has been verified.</summary>
    [MaxLength(255)]
    public string? ProviderObjectId { get; private set; }

    /// <summary>Provider object kind, such as payment_intent or charge.</summary>
    [MaxLength(100)]
    public string? ProviderObjectType { get; private set; }

    /// <summary>Monetary leg represented by the provider object, such as capture or refund.</summary>
    [MaxLength(100)]
    public string? ProviderMonetaryLeg { get; private set; }

    /// <summary>Payment method ID used for this payment</summary>
    [MaxLength(255)]
    public string? PaymentMethodId { get; private set; }

    /// <summary>Idempotency key to prevent duplicate payments</summary>
    [Required]
    [MaxLength(255)]
    public string IdempotencyKey { get; private set; } = null!;

    /// <summary>Description or memo for the payment</summary>
    [MaxLength(500)]
    public string? Description { get; private set; }

    /// <summary>Failure reason if payment failed</summary>
    [MaxLength(1000)]
    public string? FailureReason { get; private set; }

    /// <summary>Error code from payment gateway</summary>
    [MaxLength(100)]
    public string? ErrorCode { get; private set; }

    /// <summary>When the payment was processed</summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>When the payment was cancelled</summary>
    public DateTime? CancelledAt { get; private set; }

    /// <summary>Reason for cancellation</summary>
    [MaxLength(500)]
    public string? CancellationReason { get; private set; }

    /// <summary>Who cancelled the payment</summary>
    public Guid? CancelledByUserId { get; private set; }

    /// <summary>Number of retry attempts</summary>
    public int RetryCount { get; private set; }

    /// <summary>Maximum retry attempts allowed</summary>
    public int MaxRetries { get; private set; } = 3;

    /// <summary>Next retry date if failed</summary>
    public DateTime? NextRetryAt { get; private set; }

    /// <summary>Refund amount if partially or fully refunded</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal RefundedAmount { get; private set; }

    /// <summary>External refund ID</summary>
    [MaxLength(255)]
    public string? RefundId { get; private set; }

    /// <summary>Reason for refund</summary>
    [MaxLength(500)]
    public string? RefundReason { get; private set; }

    /// <summary>When the refund was processed</summary>
    public DateTime? RefundedAt { get; private set; }

    /// <summary>JSON metadata for additional payment details</summary>
    [Column(TypeName = "jsonb")]
    public string? Metadata { get; private set; }

    /// <summary>Creates a new payment with proper validation</summary>
    public static Payment Create(
        Guid tenantId,
        decimal amount,
        string currency,
        string idempotencyKey,
        string provider = "stripe",
        Guid? subscriptionId = null,
        Guid? orderId = null,
        Guid? invoiceId = null,
        string? externalCustomerId = null,
        string? paymentMethodId = null,
        string? description = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required for payment entities", nameof(tenantId));
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be positive", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("Idempotency key is required", nameof(idempotencyKey));
        }

        return new Payment
        {
            TenantId = tenantId,
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            IdempotencyKey = idempotencyKey,
            Provider = provider,
            SubscriptionId = subscriptionId,
            OrderId = orderId,
            InvoiceId = invoiceId,
            ExternalCustomerId = externalCustomerId,
            PaymentMethodId = paymentMethodId,
            Description = description,
            Status = PaymentStatus.Pending
        };
    }

    /// <summary>Binds this payment to one immutable provider object and monetary leg.</summary>
    public void BindProviderMapping(
        string provider,
        string providerEnvironment,
        string providerAccountId,
        string providerObjectId,
        string providerObjectType,
        string providerMonetaryLeg)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEnvironment);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerObjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerObjectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerMonetaryLeg);

        var isUnbound = ProviderEnvironment is null
                        && ProviderAccountId is null
                        && ProviderObjectId is null
                        && ProviderObjectType is null
                        && ProviderMonetaryLeg is null;

        if (isUnbound && string.Equals(Provider, provider, StringComparison.Ordinal))
        {
            ProviderEnvironment = providerEnvironment;
            ProviderAccountId = providerAccountId;
            ProviderObjectId = providerObjectId;
            ProviderObjectType = providerObjectType;
            ProviderMonetaryLeg = providerMonetaryLeg;
            Touch();
            return;
        }

        var isIdentical = string.Equals(Provider, provider, StringComparison.Ordinal)
                          && string.Equals(ProviderEnvironment, providerEnvironment, StringComparison.Ordinal)
                          && string.Equals(ProviderAccountId, providerAccountId, StringComparison.Ordinal)
                          && string.Equals(ProviderObjectId, providerObjectId, StringComparison.Ordinal)
                          && string.Equals(ProviderObjectType, providerObjectType, StringComparison.Ordinal)
                          && string.Equals(ProviderMonetaryLeg, providerMonetaryLeg, StringComparison.Ordinal);

        if (!isIdentical)
        {
            throw new InvalidOperationException("Payment provider mapping is already bound to a different identity");
        }
    }

    /// <summary>Validates cumulative provider amounts without changing payment state.</summary>
    public void ValidateProviderMonetaryBounds(
        decimal cumulativeConfirmedAmount,
        decimal cumulativeRefundedAmount,
        decimal cumulativeDisputedAmount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cumulativeConfirmedAmount);
        ArgumentOutOfRangeException.ThrowIfNegative(cumulativeRefundedAmount);
        ArgumentOutOfRangeException.ThrowIfNegative(cumulativeDisputedAmount);

        if (cumulativeConfirmedAmount > Amount)
        {
            throw new InvalidOperationException("Cumulative provider confirmed amount cannot exceed payment amount");
        }

        if (cumulativeRefundedAmount > cumulativeConfirmedAmount)
        {
            throw new InvalidOperationException("Cumulative provider refunded amount cannot exceed confirmed amount");
        }

        if (cumulativeDisputedAmount > cumulativeConfirmedAmount)
        {
            throw new InvalidOperationException("Cumulative provider disputed amount cannot exceed confirmed amount");
        }

        if (cumulativeRefundedAmount + cumulativeDisputedAmount > cumulativeConfirmedAmount)
        {
            throw new InvalidOperationException("Combined provider refunded and disputed amounts cannot exceed confirmed amount");
        }
    }

    /// <summary>Checks if transition to the specified status is valid</summary>
    public bool CanTransitionTo(PaymentStatus newStatus)
    {
        return ValidTransitions.TryGetValue(Status, out var allowed) && allowed.Contains(newStatus);
    }

    /// <summary>Transitions to a new status with validation</summary>
    private void TransitionTo(PaymentStatus newStatus)
    {
        if (!CanTransitionTo(newStatus))
        {
            throw new InvalidOperationException($"Cannot transition payment from {Status} to {newStatus}");
        }

        Status = newStatus;
        Touch();
    }

    /// <summary>Marks the payment as processing</summary>
    public void MarkAsProcessing(string? externalTransactionId = null)
    {
        TransitionTo(PaymentStatus.Processing);
        BindExternalTransactionId(externalTransactionId);
    }

    /// <summary>Marks the payment as succeeded</summary>
    public void MarkAsSucceeded(string externalPaymentId, string? externalTransactionId = null)
    {
        TransitionTo(PaymentStatus.Succeeded);
        ExternalPaymentId = externalPaymentId;
        ExternalTransactionId = externalTransactionId ?? ExternalTransactionId;
        ProcessedAt = SystemClock.UtcNow;

        Raise(new PaymentSucceededEvent(
            Id,
            TenantId,
            SubscriptionId,
            Amount,
            Currency,
            ProcessedAt.Value));
    }

    /// <summary>Marks the payment as failed</summary>
    public void MarkAsFailed(string failureReason, string? errorCode = null)
    {
        TransitionTo(PaymentStatus.Failed);
        FailureReason = failureReason;
        ErrorCode = errorCode;
        ProcessedAt = SystemClock.UtcNow;

        // Calculate next retry if retries remaining
        if (RetryCount < MaxRetries)
        {
            // Exponential backoff: 1 min, 5 min, 30 min, etc.
            var delayMinutes = Math.Pow(5, RetryCount) * 1;
            NextRetryAt = SystemClock.UtcNow.AddMinutes(delayMinutes);
        }
    }

    /// <summary>Marks the payment as requiring additional action (e.g., 3DS)</summary>
    public void MarkAsRequiresAction(string? externalTransactionId = null)
    {
        TransitionTo(PaymentStatus.RequiresAction);
        BindExternalTransactionId(externalTransactionId);
    }

    /// <summary>Binds the immutable provider transaction reference when it becomes available.</summary>
    public bool BindExternalTransactionId(string? externalTransactionId)
    {
        if (string.IsNullOrWhiteSpace(externalTransactionId))
        {
            return false;
        }

        if (ExternalTransactionId is null)
        {
            ExternalTransactionId = externalTransactionId;
            Touch();
            return true;
        }

        if (string.Equals(ExternalTransactionId, externalTransactionId, StringComparison.Ordinal))
        {
            return false;
        }

        throw new InvalidOperationException("A payment cannot be rebound to a different provider transaction.");
    }

    /// <summary>Cancels the payment</summary>
    public void Cancel(string reason, Guid? cancelledByUserId = null)
    {
        TransitionTo(PaymentStatus.Cancelled);
        CancellationReason = reason;
        CancelledByUserId = cancelledByUserId;
        CancelledAt = SystemClock.UtcNow;
    }

    /// <summary>Increments retry count and resets for retry</summary>
    public void PrepareForRetry(string? paymentMethodId = null)
    {
        if (Status != PaymentStatus.Failed)
        {
            throw new InvalidOperationException("Can only retry failed payments");
        }

        if (RetryCount >= MaxRetries)
        {
            throw new InvalidOperationException($"Maximum retry attempts ({MaxRetries}) reached");
        }

        RetryCount++;
        Status = PaymentStatus.Pending;
        FailureReason = null;
        ErrorCode = null;
        NextRetryAt = null;
        ExternalTransactionId = null;
        if (!string.IsNullOrWhiteSpace(paymentMethodId))
        {
            PaymentMethodId = paymentMethodId;
        }

        Touch();
    }

    /// <summary>Processes a full or partial refund</summary>
    public void ProcessRefund(decimal refundAmount, string refundId, string reason)
    {
        if (Status != PaymentStatus.Succeeded && Status != PaymentStatus.Disputed)
        {
            throw new InvalidOperationException($"Can only refund succeeded or disputed payments, current status: {Status}");
        }

        if (refundAmount <= 0)
        {
            throw new ArgumentException("Refund amount must be positive", nameof(refundAmount));
        }

        if (RefundedAmount + refundAmount > Amount)
        {
            throw new InvalidOperationException("Total refund amount cannot exceed payment amount");
        }

        RefundedAmount += refundAmount;
        RefundId = refundId;
        RefundReason = reason;
        RefundedAt = SystemClock.UtcNow;

        // If fully refunded, transition to Refunded status
        if (RefundedAmount >= Amount)
        {
            TransitionTo(PaymentStatus.Refunded);
        }
    }

    /// <summary>Marks the payment as disputed</summary>
    public void MarkAsDisputed()
    {
        TransitionTo(PaymentStatus.Disputed);
    }

    /// <summary>Sets metadata JSON</summary>
    public void SetMetadata(string metadata)
    {
        Metadata = metadata;
        Touch();
    }

    /// <summary>
    ///     Returns a legacy external reference for compatibility reads only.
    ///     This value is not a scoped provider identity and must never authorize value movement.
    /// </summary>
    public string? ResolveUnverifiedLegacyProviderObjectId() => ExternalPaymentId ?? ExternalTransactionId;

    /// <summary>Whether the payment can be retried</summary>
    public bool CanRetry => Status == PaymentStatus.Failed && RetryCount < MaxRetries;

    /// <summary>Whether max retries have been reached</summary>
    public bool MaxRetriesReached => RetryCount >= MaxRetries;

    /// <summary>Whether the payment is in a terminal state</summary>
    public bool IsTerminal => Status is PaymentStatus.Cancelled or PaymentStatus.Refunded;

    /// <summary>Net amount after refunds</summary>
    public decimal NetAmount => Amount - RefundedAmount;
}
