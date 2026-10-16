using FraudDecisionCode = GameGuild.Commerce.Payments.FraudDecision;
using FraudReviewStateCode = GameGuild.Commerce.Payments.FraudReviewStatus;
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
[Index(nameof(Status), nameof(NextRetryAt))]
[Index(nameof(ExternalPaymentId), IsUnique = true)]
[Index(nameof(IdempotencyKey), IsUnique = true)]
public class Payment : EntityBase
{
    private const int DefaultMaxRetries = 4;
    private const double DefaultBackoffBaseMinutes = 1.0;
    private const double DefaultBackoffMultiplier = 5.0;

    private static readonly Dictionary<PaymentStatus, HashSet<PaymentStatus>> ValidTransitions = new()
    {
        { PaymentStatus.Pending, new() { PaymentStatus.Processing, PaymentStatus.Cancelled, PaymentStatus.Failed } },
        { PaymentStatus.Processing, new() { PaymentStatus.Succeeded, PaymentStatus.Failed, PaymentStatus.RequiresAction, PaymentStatus.Cancelled } },
        { PaymentStatus.RequiresAction, new() { PaymentStatus.Processing, PaymentStatus.Succeeded, PaymentStatus.Failed, PaymentStatus.Cancelled } },
        { PaymentStatus.Succeeded, new() { PaymentStatus.Refunded, PaymentStatus.PartiallyRefunded, PaymentStatus.Disputed } },
        { PaymentStatus.Failed, new() { PaymentStatus.Pending } }, // Retry allowed
        { PaymentStatus.Cancelled, new HashSet<PaymentStatus>() }, // Terminal state
        { PaymentStatus.Refunded, new HashSet<PaymentStatus>() }, // Terminal state
        { PaymentStatus.Disputed, new() { PaymentStatus.Succeeded, PaymentStatus.Refunded, PaymentStatus.PartiallyRefunded } },
        // Further partial refunds allowed until the cumulative total reaches the original amount.
        { PaymentStatus.PartiallyRefunded, new() { PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded } }
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

    /// <summary>
    ///     Account-credit tender of this payment (issue #410 auto-apply): the portion of
    ///     <see cref="Amount" /> settled by consuming the user's account credits before the
    ///     payment method was charged. The provider charge is <c>Amount - CreditAppliedAmount</c>.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditAppliedAmount { get; private set; }

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

    /// <summary>Maximum retry attempts allowed; sourced from <c>Payments:Retry</c> at creation, legacy default as fallback</summary>
    public int MaxRetries { get; private set; } = DefaultMaxRetries;

    /// <summary>Next retry date if failed</summary>
    public DateTime? NextRetryAt { get; private set; }

    /// <summary>
    ///     Whether the recorded failure is eligible for further retry attempts (issue #415).
    ///     Hard declines (expired card, incorrect CVC, fraud, ...) are marked non-retriable
    ///     immediately by the failure-code retry policy; soft declines and transient errors
    ///     stay retriable until the retry budget is exhausted.
    /// </summary>
    public bool IsRetriable { get; private set; } = true;

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

    /// <summary>Aggregate fraud score (0-100) from the pre-gateway assessment; null when never assessed.</summary>
    public int? FraudScore { get; private set; }

    /// <summary>Fraud gate decision recorded for this payment; null when never assessed.</summary>
    public FraudDecision? FraudDecision { get; private set; }

    /// <summary>Bounded human-readable reason for the recorded fraud decision.</summary>
    [MaxLength(500)]
    public string? FraudDecisionReason { get; private set; }

    /// <summary>
    ///     Bounded JSON evidence bundle (score, risk level, signals) backing the fraud
    ///     decision. Internal assessment evidence only — never provider tokens.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? FraudEvidence { get; private set; }

    /// <summary>Manual review state when the fraud decision was <see cref="FraudDecision.Review"/>.</summary>
    public FraudReviewStatus? FraudReviewStatus { get; private set; }

    /// <summary>Administrator who resolved the manual fraud review.</summary>
    public Guid? FraudReviewedByUserId { get; private set; }

    /// <summary>When the manual fraud review was resolved.</summary>
    public DateTime? FraudReviewedAt { get; private set; }

    /// <summary>Administrator notes recorded with the fraud review resolution.</summary>
    [MaxLength(500)]
    public string? FraudReviewNotes { get; private set; }

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
        string? description = null,
        int? maxRetries = null)
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
            MaxRetries = maxRetries ?? DefaultMaxRetries,
            Status = PaymentStatus.Pending
        };
    }

    /// <summary>
    ///     Re-attributes the payment to the gateway that actually processed it when
    ///     failover routed the charge away from the primary gateway (issue #413). No-op
    ///     when the provider mapping is already bound (the mapped provider stays
    ///     authoritative) and when the payment is terminal.
    /// </summary>
    public void RecordProcessingProvider(string provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        var normalized = provider.ToLowerInvariant();
        if (string.Equals(Provider, normalized, StringComparison.Ordinal))
        {
            return;
        }

        if (ProviderObjectId is not null || IsTerminal)
        {
            // A bound provider mapping is the authoritative identity of this payment's
            // provider object; attribution must never drift away from it.
            return;
        }

        Provider = normalized;
        Touch();
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

    /// <summary>
    ///     Records the account-credit tender of this payment (issue #410 auto-apply): the
    ///     amount actually consumed from the user's credit grants before the payment method
    ///     was charged. Fails closed on non-positive or amount-exceeding values and on
    ///     terminal payments.
    /// </summary>
    public void RecordCreditApplied(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Applied credit amount must be positive.", nameof(amount));
        }

        if (amount > Amount)
        {
            throw new InvalidOperationException("Applied credit cannot exceed the payment amount.");
        }

        if (Status is PaymentStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot apply credit to a payment in status {Status}.");
        }

        CreditAppliedAmount = amount;
        Touch();
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

    /// <summary>
    ///     Marks the payment as failed. Retry-policy parameters may be supplied by the caller from
    ///     <c>Payments:Retry</c> options (issue #403); when omitted the legacy entity defaults apply
    ///     (max 3 retries, exponential backoff of <c>Math.Pow(5, RetryCount)</c> minutes).
    ///     Callers that resolve a failure-code retry policy (issue #415) pass an explicit
    ///     <paramref name="nextRetryAt" /> computed by <c>PaymentRetryPolicyResolver</c> (which may
    ///     be <c>null</c> when the failure class is non-retriable or the budget is exhausted) and
    ///     <paramref name="isRetriable" />; the entity itself stays free of policy logic.
    /// </summary>
    public void MarkAsFailed(
        string failureReason,
        string? errorCode = null,
        int? maxRetries = null,
        double? backoffBaseMinutes = null,
        double? backoffMultiplier = null,
        DateTime? nextRetryAt = null,
        bool? isRetriable = null)
    {
        TransitionTo(PaymentStatus.Failed);
        FailureReason = failureReason;
        ErrorCode = errorCode;
        ProcessedAt = SystemClock.UtcNow;

        // Calculate next retry if retries remaining
        var effectiveMaxRetries = maxRetries ?? MaxRetries;
        if (maxRetries.HasValue && maxRetries.Value != MaxRetries)
        {
            MaxRetries = maxRetries.Value;
        }

        if (isRetriable.HasValue)
        {
            IsRetriable = isRetriable.Value;
        }

        if (nextRetryAt.HasValue)
        {
            // Failure-code policy already computed the schedule (issue #415): honor it as-is,
            // including a deliberate null for non-retriable or exhausted payments.
            NextRetryAt = nextRetryAt.Value;
            return;
        }

        if (IsRetriable && RetryCount < effectiveMaxRetries)
        {
            // Exponential backoff: legacy default is 1 min, 5 min, 25 min, etc.
            var baseMinutes = backoffBaseMinutes ?? DefaultBackoffBaseMinutes;
            var multiplier = backoffMultiplier ?? DefaultBackoffMultiplier;
            var delayMinutes = baseMinutes * Math.Pow(multiplier, RetryCount);
            NextRetryAt = SystemClock.UtcNow.AddMinutes(delayMinutes);
        }
        else
        {
            NextRetryAt = null;
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

        if (!CanRetry)
        {
            if (MaxRetriesReached)
            {
                throw new InvalidOperationException($"Maximum retry attempts ({MaxRetries}) reached");
            }

            if (FraudBlocksRetry)
            {
                throw new InvalidOperationException(
                    "A fraud-held payment cannot be retried; resolve the fraud review first.");
            }

            throw new InvalidOperationException(
                "Payment is marked non-retriable for its recorded failure code");
        }

        RetryCount++;
        Status = PaymentStatus.Pending;
        FailureReason = null;
        ErrorCode = null;
        NextRetryAt = null;
        ExternalTransactionId = null;
        IsRetriable = true; // The new attempt is classified afresh when its outcome is recorded.
        if (!string.IsNullOrWhiteSpace(paymentMethodId))
        {
            PaymentMethodId = paymentMethodId;
        }

        Touch();
    }

    /// <summary>
    ///     Pauses automatic retries for this failed payment (issue #415): the scheduled next-retry
    ///     time is cleared so the retry queue stops picking the payment up until it is resumed.
    /// </summary>
    public void PauseRetryQueue()
    {
        if (Status != PaymentStatus.Failed)
        {
            throw new InvalidOperationException("Only failed payments can have their retry schedule paused");
        }

        if (NextRetryAt is null)
        {
            return; // Already paused, exhausted or non-retriable — idempotent.
        }

        NextRetryAt = null;
        Touch();
    }

    /// <summary>
    ///     Resumes automatic retries that were previously paused for a payment-method change
    ///     (issue #415): the payment becomes eligible again on the next retry-queue pass.
    ///     Only payments that still have retry budget and are retriable can be resumed.
    /// </summary>
    public void ResumeRetryQueue(DateTime? eligibleAtUtc = null)
    {
        if (Status != PaymentStatus.Failed)
        {
            throw new InvalidOperationException("Only failed payments can have their retry schedule resumed");
        }

        if (!IsRetriable || RetryCount >= MaxRetries)
        {
            return; // Non-retriable or exhausted payments stay out of the queue.
        }

        NextRetryAt = eligibleAtUtc ?? SystemClock.UtcNow;
        Touch();
    }

    /// <summary>
    ///     Re-points the payment at a newly confirmed payment method (issue #415). Used when a
    ///     customer replaces their payment method mid-dunning so queued retries charge the new
    ///     instrument instead of the failed one. Terminal payments cannot change methods.
    /// </summary>
    public void ApplyPaymentMethodChange(string paymentMethodId)
    {
        if (string.IsNullOrWhiteSpace(paymentMethodId))
        {
            throw new ArgumentException("Payment method id is required", nameof(paymentMethodId));
        }

        if (IsTerminal)
        {
            throw new InvalidOperationException($"Cannot change the payment method of a payment in status {Status}");
        }

        if (string.Equals(PaymentMethodId, paymentMethodId, StringComparison.Ordinal))
        {
            return;
        }

        PaymentMethodId = paymentMethodId;
        Touch();
    }

    /// <summary>Processes a full or partial refund</summary>
    public void ProcessRefund(decimal refundAmount, string refundId, string reason)
    {
        if (Status is not (PaymentStatus.Succeeded or PaymentStatus.Disputed or PaymentStatus.PartiallyRefunded))
        {
            throw new InvalidOperationException($"Can only refund succeeded, disputed or partially refunded payments, current status: {Status}");
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

        // If fully refunded, transition to Refunded status; otherwise PartiallyRefunded (issue #419)
        if (RefundedAmount >= Amount)
        {
            TransitionTo(PaymentStatus.Refunded);
        }
        else if (Status != PaymentStatus.PartiallyRefunded)
        {
            TransitionTo(PaymentStatus.PartiallyRefunded);
        }
        else
        {
            Touch();
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
    ///     Records the pre-gateway fraud assessment (issue #409). For
    ///     <see cref="FraudDecision.Review"/> and <see cref="FraudDecision.Block"/> the
    ///     payment transitions to <see cref="PaymentStatus.Failed"/> without scheduling an
    ///     automatic retry, and Review decisions additionally enter the manual review queue.
    /// </summary>
    /// <param name="decision">Gate decision to record.</param>
    /// <param name="score">Aggregate fraud score (0-100).</param>
    /// <param name="reason">Bounded decision reason.</param>
    /// <param name="evidenceJson">Bounded JSON evidence bundle; never provider tokens.</param>
    public void RecordFraudAssessment(FraudDecision decision, int score, string reason, string? evidenceJson)
    {
        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Fraud score must be between 0 and 100.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        FraudScore = score;
        FraudDecision = decision;
        FraudDecisionReason = reason.Length <= 500 ? reason : reason[..500];
        FraudEvidence = evidenceJson;
        FraudReviewStatus = decision == FraudDecisionCode.Review ? FraudReviewStateCode.PendingReview : null;

        if (decision != FraudDecisionCode.Allow)
        {
            TransitionTo(PaymentStatus.Failed);
            FailureReason = $"Fraud gate {decision.ToString().ToLowerInvariant()}: {FraudDecisionReason}";
            ErrorCode = $"fraud_{decision.ToString().ToLowerInvariant()}";
            ProcessedAt = SystemClock.UtcNow;
            NextRetryAt = null; // Fraud-held payments are never auto-retried.
        }

        Touch();
    }

    /// <summary>
    ///     Resolves a pending manual fraud review (issue #409). Approving releases the fraud
    ///     hold so the payment may be retried through the existing (manual) retry flow;
    ///     declining keeps it failed and permanently non-retryable.
    /// </summary>
    /// <param name="approved">Whether the review is approved.</param>
    /// <param name="reviewedByUserId">Administrator resolving the review.</param>
    /// <param name="notes">Optional administrator notes (bounded).</param>
    public void ResolveFraudReview(bool approved, Guid reviewedByUserId, string? notes = null)
    {
        if (reviewedByUserId == Guid.Empty)
        {
            throw new ArgumentException("A reviewer identity is required to resolve a fraud review.", nameof(reviewedByUserId));
        }

        if (FraudDecision != FraudDecisionCode.Review || FraudReviewStatus != FraudReviewStateCode.PendingReview)
        {
            throw new InvalidOperationException(
                $"Cannot resolve a fraud review for a payment with decision {FraudDecision} and review status {FraudReviewStatus}.");
        }

        FraudReviewStatus = approved ? FraudReviewStateCode.Approved : FraudReviewStateCode.Declined;
        FraudReviewedByUserId = reviewedByUserId;
        FraudReviewedAt = SystemClock.UtcNow;
        FraudReviewNotes = string.IsNullOrWhiteSpace(notes)
            ? null
            : (notes.Length <= 500 ? notes : notes[..500]);

        Touch();
    }

    /// <summary>
    ///     Returns a legacy external reference for compatibility reads only.
    ///     This value is not a scoped provider identity and must never authorize value movement.
    /// </summary>
    public string? ResolveUnverifiedLegacyProviderObjectId() => ExternalPaymentId ?? ExternalTransactionId;

    /// <summary>Whether the payment can be retried. Non-retriable failure classifications
    /// (hard declines, issue #415) and fraud-held payments are excluded: blocked payments
    /// never retry, and review-held payments only retry once the fraud review has been
    /// approved (issue #409).</summary>
    public bool CanRetry => Status == PaymentStatus.Failed && RetryCount < MaxRetries && IsRetriable && !FraudBlocksRetry;

    /// <summary>Whether the recorded fraud decision currently blocks retrying this payment.</summary>
    public bool FraudBlocksRetry =>
        FraudDecision == FraudDecisionCode.Block
        || (FraudDecision == FraudDecisionCode.Review && FraudReviewStatus != FraudReviewStateCode.Approved);

    /// <summary>Whether the retry budget is exhausted (attempts reached the persisted maximum)</summary>
    public bool MaxRetriesReached => RetryCount >= MaxRetries;

    /// <summary>Whether the payment is in a terminal state</summary>
    public bool IsTerminal => Status is PaymentStatus.Cancelled or PaymentStatus.Refunded;

    /// <summary>Net amount after refunds</summary>
    public decimal NetAmount => Amount - RefundedAmount;
}
