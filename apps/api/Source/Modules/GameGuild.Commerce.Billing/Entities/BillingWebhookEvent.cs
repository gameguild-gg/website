using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Represents a webhook event received from a billing provider
/// </summary>
[Table("BillingWebhookEvents")]
public class BillingWebhookEvent : EntityBase
{
    /// <summary>
    ///     Payment provider that sent the webhook (stripe, paypal, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    ///     External event ID from the provider
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string ExternalEventId { get; set; } = string.Empty;

    /// <summary>
    ///     Provider environment that emitted the event, such as test or live.
    ///     Nullable while legacy inbox rows are backfilled.
    /// </summary>
    [MaxLength(32)]
    public string? ProviderEnvironment { get; set; }

    /// <summary>
    ///     Connected or merchant account that owns the provider event.
    /// </summary>
    [MaxLength(255)]
    public string? ProviderAccountId { get; set; }

    /// <summary>
    ///     Provider webhook endpoint that accepted the event.
    /// </summary>
    [MaxLength(255)]
    public string? WebhookEndpointId { get; set; }

    /// <summary>
    ///     Immutable provider object referenced by the event.
    /// </summary>
    [MaxLength(255)]
    public string? ProviderObjectId { get; set; }

    /// <summary>
    ///     Provider object kind, such as payment_intent or charge.
    /// </summary>
    [MaxLength(100)]
    public string? ProviderObjectType { get; set; }

    /// <summary>
    ///     Monetary leg represented by the provider object, such as capture or refund.
    /// </summary>
    [MaxLength(100)]
    public string? ProviderMonetaryLeg { get; set; }

    /// <summary>
    ///     Provider livemode marker. Nullable for providers or legacy rows without the signal.
    /// </summary>
    public bool? IsLiveMode { get; set; }

    /// <summary>
    ///     Version of the provider event schema used to parse this event.
    /// </summary>
    [MaxLength(50)]
    public string? EventSchemaVersion { get; set; }

    /// <summary>
    ///     Type of webhook event (subscription.created, payment.succeeded, etc.)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    ///     Raw webhook payload as received
    /// </summary>
    [Required]
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    ///     Headers received with the webhook
    /// </summary>
    public string? Headers { get; set; }

    /// <summary>
    ///     Whether the webhook has been processed successfully
    /// </summary>
    public bool IsProcessed { get; set; }

    /// <summary>
    ///     Whether the webhook processing failed
    /// </summary>
    public bool IsFailed { get; set; }

    /// <summary>
    ///     Number of processing attempts
    /// </summary>
    public int ProcessingAttempts { get; set; }

    /// <summary>
    ///     Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    ///     When the webhook was processed
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    ///     Whether the provider signature was verified for this inbox row (issue #408).
    ///     Nullable for legacy rows accepted before the flag existed. Rows that failed
    ///     signature verification never flip this to true and are never replayed.
    /// </summary>
    public bool? IsSignatureVerified { get; set; }

    /// <summary>
    ///     When the provider signature was verified (issue #408).
    /// </summary>
    public DateTime? SignatureVerifiedAt { get; set; }

    /// <summary>
    ///     When the event was dead-lettered after exhausting its retry budget (issue #408).
    ///     Null while the event is still being retried or was processed successfully.
    /// </summary>
    public DateTime? DeadLetteredAt { get; set; }

    /// <summary>
    ///     Bounded reason recorded when the event was dead-lettered (issue #408).
    /// </summary>
    [MaxLength(1000)]
    public string? DeadLetterReason { get; set; }

    /// <summary>
    ///     Related tenant ID if applicable
    /// </summary>
    public new Guid? TenantId { get; set; }

    /// <summary>
    ///     Related subscription ID if applicable
    /// </summary>
    public Guid? SubscriptionId { get; set; }

    /// <summary>
    ///     Mark webhook as processed
    /// </summary>
    public void MarkAsProcessed()
    {
        IsProcessed = true;
        ProcessedAt = SystemClock.UtcNow;
        IsFailed = false;
        Touch();
    }

    /// <summary>
    ///     Records that the provider signature was verified for this delivery (issue #408).
    ///     Only ever transitions to true; verification is immutable once established.
    /// </summary>
    public void MarkSignatureVerified()
    {
        if (IsSignatureVerified == true)
        {
            return;
        }

        IsSignatureVerified = true;
        SignatureVerifiedAt = SystemClock.UtcNow;
        Touch();
    }

    /// <summary>
    ///     Marks the event as dead-lettered after its retry budget was exhausted (issue #408).
    ///     A dead-lettered row keeps its failure evidence for inspection and is never
    ///     requeued by the retry worker again.
    /// </summary>
    public void MarkAsDeadLettered(string reason)
    {
        DeadLetteredAt = SystemClock.UtcNow;
        DeadLetterReason = string.IsNullOrWhiteSpace(reason)
            ? "Retry budget exhausted"
            : (reason.Length <= 1000 ? reason : reason[..1000]);
        Touch();
    }

    /// <summary>
    ///     Mark webhook as failed
    /// </summary>
    public void MarkAsFailed(string errorMessage)
    {
        IsFailed = true;
        ErrorMessage = errorMessage;
        Touch();
    }

    /// <summary>
    ///     Increment processing attempts
    /// </summary>
    public void IncrementAttempts()
    {
        ProcessingAttempts++;
        Touch();
    }

    /// <summary>
    ///     Attempts to acquire the processing lease for this durable inbox row.
    ///     Failed or stale attempts may be reclaimed; active attempts may not.
    /// </summary>
    public bool TryBeginProcessing(DateTime staleBefore)
    {
        if (IsProcessed)
        {
            return false;
        }

        var hasActiveLease = ProcessingAttempts > 0 &&
                             !IsFailed &&
                             UpdatedAt > staleBefore;
        if (hasActiveLease)
        {
            return false;
        }

        ProcessingAttempts++;
        IsFailed = false;
        ErrorMessage = null;
        Touch();
        return true;
    }
}
