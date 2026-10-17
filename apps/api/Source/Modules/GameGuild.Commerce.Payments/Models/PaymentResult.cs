
namespace GameGuild.Commerce.Payments;

/// <summary>
///     Result of payment processing
/// </summary>
public class PaymentResult
{
    /// <summary>
    ///     Tenant that owns the payment. Required for authorization at API boundaries.
    /// </summary>
    public Guid TenantId { get; init; }

    public bool Success { get; init; }

    public string? TransactionId { get; init; }

    /// <summary>
    ///     Internal or provider payment identifier
    /// </summary>
    public string? PaymentId { get; init; }

    public Money? Amount { get; init; }

    public DateTime? ProcessedAt { get; init; }

    public string? FailureReason { get; init; }

    public string? PaymentMethodId { get; init; }

    public PaymentStatus Status { get; init; }

    /// <summary>
    ///     Account-credit tender applied automatically before the payment method was charged
    ///     (issue #410 auto-apply); the provider charge is <c>Amount - CreditApplied</c>.
    ///     Null when no credits were applied.
    /// </summary>
    public decimal? CreditApplied { get; init; }

    /// <summary>
    ///     Invoice ID that this payment was applied to.
    ///     Links payment to specific invoice for audit trail and preventing duplicate applications.
    /// </summary>
    public Guid? InvoiceId { get; init; }

    /// <summary>
    ///     Creates a successful payment result
    /// </summary>
    public static PaymentResult CreateSuccess(Money amount, string? paymentId = null, string? transactionId = null, Guid? invoiceId = null)
    {
        return new PaymentResult
        {
            Success = true,
            Status = PaymentStatus.Succeeded,
            Amount = amount,
            PaymentId = paymentId,
            TransactionId = transactionId,
            InvoiceId = invoiceId,
            ProcessedAt = SystemClock.UtcNow
        };
    }

    /// <summary>
    ///     Creates a failed payment result
    /// </summary>
    public static PaymentResult Failed(string failureReason, Guid? invoiceId = null)
    {
        return new PaymentResult
        {
            Success = false,
            Status = PaymentStatus.Failed,
            FailureReason = failureReason,
            InvoiceId = invoiceId,
            ProcessedAt = SystemClock.UtcNow
        };
    }

    /// <summary>
    ///     Creates a pending payment result
    /// </summary>
    public static PaymentResult Pending(Money amount, string? paymentId = null, Guid? invoiceId = null)
    {
        return new PaymentResult
        {
            Success = false,
            Status = PaymentStatus.Pending,
            Amount = amount,
            PaymentId = paymentId,
            InvoiceId = invoiceId,
            ProcessedAt = SystemClock.UtcNow
        };
    }
}
