namespace GameGuild.Commerce.Billing;

/// <summary>
///     Google Pay-specific payment webhook payload.
///     Concrete implementation of the abstract PaymentWebhookPayload.
/// </summary>
public sealed class GooglePayPaymentWebhookPayload : PaymentWebhookPayload
{
    /// <summary>
    ///     Google transaction identifier.
    /// </summary>
    public string? TransactionId { get; set; }

    /// <summary>
    ///     Provider reason code for failed payments.
    /// </summary>
    public string? FailureCode { get; set; }
}
