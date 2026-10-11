namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Subscriptions-side port for materializing the local invoice of a confirmed subscription billing cycle.
///     Implemented by the Commerce.Billing module (dependency inversion: Billing references Subscriptions,
///     so the port lives here exactly like <c>IPaymentSubscriptionSyncService</c> lives in Commerce.Payments).
///     The implementation is idempotent per <c>subscription:{{id}}:cycle:{{n}}:invoice</c>.
/// </summary>
public interface ISubscriptionInvoiceMaterializer
{
    /// <summary>
    ///     Materializes the invoice for a billing cycle whose payment was confirmed through the
    ///     first-party payment pipeline. Failures are logged, never thrown: the payment already
    ///     succeeded and subscription state is already persisted.
    /// </summary>
    /// <param name="subscriptionId">Local subscription identifier.</param>
    /// <param name="billingCycleNumber">1-based billing cycle the payment belongs to.</param>
    /// <param name="amount">Confirmed payment amount.</param>
    /// <param name="currency">ISO 4217 currency.</param>
    /// <param name="processedAtUtc">When the payment was confirmed.</param>
    /// <param name="paymentId">Local payment identifier.</param>
    /// <param name="providerInvoiceId">Provider invoice identifier when the cycle was provider-billed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MaterializeForConfirmedCycleAsync(
        Guid subscriptionId,
        int billingCycleNumber,
        decimal amount,
        string currency,
        DateTime processedAtUtc,
        Guid? paymentId = null,
        string? providerInvoiceId = null,
        CancellationToken cancellationToken = default);
}
