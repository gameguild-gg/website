using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>How a refund amount was determined (issue #419).</summary>
public enum RefundKind
{
    /// <summary>The full remaining refundable balance of the payment.</summary>
    Full = 0,

    /// <summary>An explicitly specified partial amount.</summary>
    Partial = 1,

    /// <summary>Computed from the unused time of the subscription billing period (issue #419 proration).</summary>
    Prorated = 2
}

/// <summary>
///     A single refund against a payment (issue #419). Each refund attempt is one row with its
///     own lifecycle (Pending → Approved/Rejected → Processing → Completed/Failed); the payment's
///     <see cref="Payment.RefundedAmount"/> remains the cumulative aggregate derived from the
///     completed rows. The idempotency key is retry-safe: it is derived from the payment and the
///     refund row identity only, never from a timestamp, so replaying the same request can never
///     create a duplicate gateway refund.
/// </summary>
[Table("refunds")]
[Index(nameof(PaymentId))]
[Index(nameof(Status))]
[Index(nameof(TenantId), nameof(Status))]
[Index(nameof(CreatedAt))]
[Index(nameof(PaymentId), nameof(IdempotencyKey), IsUnique = true)]
public class Refund : EntityBase
{
    private static readonly Dictionary<RefundStatus, HashSet<RefundStatus>> ValidTransitions = new()
    {
        { RefundStatus.Pending, new() { RefundStatus.Approved, RefundStatus.Rejected, RefundStatus.Processing } },
        { RefundStatus.Approved, new() { RefundStatus.Processing, RefundStatus.Rejected } },
        { RefundStatus.Rejected, new HashSet<RefundStatus>() }, // Terminal
        { RefundStatus.Processing, new() { RefundStatus.Completed, RefundStatus.Failed } },
        { RefundStatus.Completed, new HashSet<RefundStatus>() }, // Terminal
        { RefundStatus.Failed, new() { RefundStatus.Processing } } // Retry allowed
    };

    /// <summary>Default constructor for EF Core</summary>
    public Refund() { }

    /// <summary>Tenant that owns the refunded payment</summary>
    [Required]
    public new Guid TenantId { get; private set; }

    /// <summary>Payment this refund is issued against</summary>
    [Required]
    public Guid PaymentId { get; private set; }

    /// <summary>Refund amount in major currency units; must not exceed the remaining refundable amount</summary>
    [Column(TypeName = "decimal(18,2)")]
    [Required]
    public decimal Amount { get; private set; }

    /// <summary>How <see cref="Amount"/> was determined: full remaining, explicit partial, or computed proration.</summary>
    public RefundKind Kind { get; private set; } = RefundKind.Full;

    /// <summary>ISO 4217 currency copied from the payment at request time</summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; private set; } = "USD";

    /// <summary>Short machine-readable reason (e.g. <c>duplicate</c>, <c>chargeback_accepted</c>)</summary>
    [Required]
    [MaxLength(100)]
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Optional human-readable details accompanying the reason</summary>
    [MaxLength(1000)]
    public string? ReasonDetails { get; private set; }

    /// <summary>Current lifecycle status</summary>
    [Required]
    public RefundStatus Status { get; private set; } = RefundStatus.Pending;

    /// <summary>Whether this refund waited for (or is waiting on) a manual approval decision</summary>
    public bool RequiresApproval { get; private set; }

    /// <summary>Retry-safe idempotency key used for the gateway refund call; unique per (payment, refund)</summary>
    [Required]
    [MaxLength(255)]
    public string IdempotencyKey { get; private set; } = null!;

    /// <summary>Identity of the user who requested the refund</summary>
    [Required]
    public Guid RequestedByUserId { get; private set; }

    /// <summary>When the refund was requested</summary>
    public DateTime RequestedAt { get; private set; } = SystemClock.UtcNow;

    /// <summary>Administrator who approved or rejected the refund; null while pending or auto-approved</summary>
    public Guid? DecidedByUserId { get; private set; }

    /// <summary>When the approval decision was recorded</summary>
    public DateTime? DecidedAt { get; private set; }

    /// <summary>Notes recorded with the approval or rejection decision</summary>
    [MaxLength(1000)]
    public string? DecisionNotes { get; private set; }

    /// <summary>Gateway refund identifier once the gateway accepted the refund</summary>
    [MaxLength(255)]
    public string? GatewayRefundId { get; private set; }

    /// <summary>Gateway name that executed the refund (e.g. stripe)</summary>
    [MaxLength(50)]
    public string? GatewayName { get; private set; }

    /// <summary>When the gateway confirmed the refund</summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>When the gateway rejected the refund attempt</summary>
    public DateTime? FailedAt { get; private set; }

    /// <summary>Bounded failure reason recorded on a failed attempt</summary>
    [MaxLength(1000)]
    public string? FailureReason { get; private set; }

    /// <summary>Number of gateway execution attempts for this refund</summary>
    public int AttemptCount { get; private set; }

    /// <summary>Dispute that triggered this refund through the accept-dispute flow, when applicable</summary>
    public Guid? DisputeId { get; private set; }

    /// <summary>Creates a new refund request in the <see cref="RefundStatus.Pending"/> state.</summary>
    public static Refund Create(
        Guid tenantId,
        Guid paymentId,
        decimal amount,
        string currency,
        string reason,
        string? reasonDetails,
        Guid requestedByUserId,
        bool requiresApproval,
        string idempotencyKey,
        Guid? disputeId = null,
        RefundKind kind = RefundKind.Full)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required for refund entities", nameof(tenantId));
        }

        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("PaymentId is required for refund entities", nameof(paymentId));
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Refund amount must be positive", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A refund reason is required", nameof(reason));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return new Refund
        {
            TenantId = tenantId,
            PaymentId = paymentId,
            Amount = amount,
            Kind = kind,
            Currency = currency.ToUpperInvariant(),
            Reason = reason.Trim(),
            ReasonDetails = reasonDetails,
            RequestedByUserId = requestedByUserId,
            RequiresApproval = requiresApproval,
            IdempotencyKey = idempotencyKey,
            Status = RefundStatus.Pending,
            RequestedAt = SystemClock.UtcNow,
            DisputeId = disputeId
        };
    }

    /// <summary>Whether the refund may move to the specified status.</summary>
    public bool CanTransitionTo(RefundStatus newStatus) =>
        ValidTransitions.TryGetValue(Status, out var allowed) && allowed.Contains(newStatus);

    /// <summary>Records the manual approval decision and stamps the approver.</summary>
    public void MarkApproved(Guid decidedByUserId, string? notes = null)
    {
        if (decidedByUserId == Guid.Empty)
        {
            throw new ArgumentException("An approver identity is required.", nameof(decidedByUserId));
        }

        TransitionTo(RefundStatus.Approved);
        DecidedByUserId = decidedByUserId;
        DecidedAt = SystemClock.UtcNow;
        DecisionNotes = Bound(notes);
    }

    /// <summary>Records the manual rejection decision; terminal.</summary>
    public void MarkRejected(Guid decidedByUserId, string? notes = null)
    {
        if (decidedByUserId == Guid.Empty)
        {
            throw new ArgumentException("A reviewer identity is required.", nameof(decidedByUserId));
        }

        TransitionTo(RefundStatus.Rejected);
        DecidedByUserId = decidedByUserId;
        DecidedAt = SystemClock.UtcNow;
        DecisionNotes = Bound(notes);
    }

    /// <summary>Moves the refund into the in-flight gateway execution state.</summary>
    public void MarkProcessing()
    {
        TransitionTo(RefundStatus.Processing);
        AttemptCount++;
    }

    /// <summary>Marks the refund as confirmed by the gateway and records the gateway refund id.</summary>
    public void MarkCompleted(string gatewayRefundId, string gatewayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayRefundId);
        ArgumentException.ThrowIfNullOrWhiteSpace(gatewayName);

        TransitionTo(RefundStatus.Completed);
        GatewayRefundId = gatewayRefundId;
        GatewayName = gatewayName;
        ProcessedAt = SystemClock.UtcNow;
        FailureReason = null;
    }

    /// <summary>Marks the refund as rejected by the gateway; may be re-executed later.</summary>
    public void MarkFailed(string failureReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        TransitionTo(RefundStatus.Failed);
        FailedAt = SystemClock.UtcNow;
        FailureReason = Bound(failureReason);
    }

    /// <summary>Whether this refund is in a terminal state.</summary>
    public bool IsTerminal => Status is RefundStatus.Completed or RefundStatus.Rejected;

    private void TransitionTo(RefundStatus newStatus)
    {
        if (!CanTransitionTo(newStatus))
        {
            throw new InvalidOperationException($"Cannot transition refund from {Status} to {newStatus}");
        }

        Status = newStatus;
        Touch();
    }

    private static string? Bound(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : (value.Length <= 1000 ? value : value[..1000]);
}
