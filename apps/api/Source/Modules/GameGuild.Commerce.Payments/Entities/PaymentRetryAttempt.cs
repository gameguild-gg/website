using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Persisted history entry for one automatic or manual payment retry attempt (issue #415).
///     Written by <c>RetryPaymentCommandHandler</c> every time a retry attempt is executed,
///     recording when the attempt was scheduled and executed, its outcome, the gateway failure
///     code and the gateway transaction reference when one exists.
/// </summary>
[Table("payment_retry_attempts")]
[Index(nameof(PaymentId), nameof(AttemptNumber), IsUnique = true)]
public class PaymentRetryAttempt : EntityBase
{
    /// <summary>Default constructor for EF Core</summary>
    public PaymentRetryAttempt() { }

    /// <summary>Payment the attempt belongs to.</summary>
    [Required]
    public Guid PaymentId { get; private set; }

    /// <summary>1-based retry attempt number (matches <see cref="Payment.RetryCount" /> after the attempt).</summary>
    [Required]
    public int AttemptNumber { get; private set; }

    /// <summary>When this attempt was scheduled to run (the payment's next-retry time when claimed); null for immediate manual retries.</summary>
    public DateTime? ScheduledForUtc { get; private set; }

    /// <summary>When the attempt was executed.</summary>
    [Required]
    public DateTime ExecutedAtUtc { get; private set; }

    /// <summary>Outcome status of the attempt (Succeeded, Failed, RequiresAction, ...).</summary>
    [Required]
    public PaymentStatus Outcome { get; private set; }

    /// <summary>Gateway failure code when the attempt failed.</summary>
    [MaxLength(100)]
    public string? FailureCode { get; private set; }

    /// <summary>Gateway failure message when the attempt failed.</summary>
    [MaxLength(1000)]
    public string? FailureMessage { get; private set; }

    /// <summary>Gateway that executed the attempt (stripe, paypal, ...).</summary>
    [Required]
    [MaxLength(50)]
    public string Gateway { get; private set; } = "stripe";

    /// <summary>Gateway transaction reference for the attempt when one exists.</summary>
    [MaxLength(255)]
    public string? GatewayTransactionId { get; private set; }

    /// <summary>Navigation to the retried payment.</summary>
    public Payment? Payment { get; private set; }

    /// <summary>Creates a retry-attempt history entry with validation.</summary>
    public static PaymentRetryAttempt Create(
        Guid paymentId,
        int attemptNumber,
        DateTime? scheduledForUtc,
        DateTime executedAtUtc,
        PaymentStatus outcome,
        string gateway,
        string? failureCode = null,
        string? failureMessage = null,
        string? gatewayTransactionId = null)
    {
        if (paymentId == Guid.Empty)
        {
            throw new ArgumentException("PaymentId is required for retry attempt entries", nameof(paymentId));
        }

        if (attemptNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber), "Attempt numbers are 1-based");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(gateway);

        return new PaymentRetryAttempt
        {
            PaymentId = paymentId,
            AttemptNumber = attemptNumber,
            ScheduledForUtc = scheduledForUtc,
            ExecutedAtUtc = executedAtUtc,
            Outcome = outcome,
            Gateway = gateway,
            FailureCode = failureCode,
            FailureMessage = failureMessage,
            GatewayTransactionId = gatewayTransactionId
        };
    }
}
