namespace GameGuild.Commerce.Billing;

/// <summary>
///     Google Pay-specific subscription webhook payload.
///     Concrete implementation of the abstract SubscriptionWebhookPayload.
/// </summary>
public sealed class GooglePaySubscriptionWebhookPayload : SubscriptionWebhookPayload
{
    /// <summary>
    ///     Google Play subscription purchase token.
    /// </summary>
    public string? PurchaseToken { get; set; }

    /// <summary>
    ///     Google Cloud project the notification was issued to.
    /// </summary>
    public string? Audience { get; set; }
}
