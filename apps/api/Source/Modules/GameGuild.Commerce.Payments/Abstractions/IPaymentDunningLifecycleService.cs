namespace GameGuild.Commerce.Payments;

/// <summary>
///     Well-known dunning suspension reasons recorded on subscriptions suspended by the
///     payment recovery lifecycle (issue #415).
/// </summary>
public static class PaymentDunningReasons
{
    /// <summary>The subscription was suspended because its payment exhausted the retry budget.</summary>
    public const string RetryExhausted = "payment_retry_exhausted";

    /// <summary>The subscription was suspended because its payment was fully refunded (issue #415).</summary>
    public const string RefundIssued = "payment_refund_issued";
}

/// <summary>
///     Bridges the payment recovery lifecycle into subscription lifecycle operations
///     (issue #415). Implemented by the Subscriptions module so the Payments module stays
///     independent of subscription internals: when a payment's retries are exhausted the
///     subscription is suspended through the standard lifecycle with a dunning reason, and
///     when a recovery payment later succeeds the dunning suspension is lifted.
/// </summary>
public interface IPaymentDunningLifecycleService
{
    /// <summary>
    ///     Suspends the subscription tied to a payment whose retries were exhausted, using the
    ///     standard subscription lifecycle with the <see cref="PaymentDunningReasons.RetryExhausted" />
    ///     reason, and triggers the final-notice dunning communication. Idempotent: subscriptions
    ///     already suspended or cancelled are left untouched.
    /// </summary>
    /// <param name="subscriptionId">Subscription to suspend.</param>
    /// <param name="tenantId">Owning tenant (for logging and the dunning notification).</param>
    /// <param name="reason">Human-readable failure context recorded alongside the suspension.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when a suspension was applied by this call.</returns>
    Task<bool> SuspendForRetryExhaustionAsync(
        Guid subscriptionId,
        Guid tenantId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reactivates a subscription that was suspended for payment dunning after a successful
    ///     recovery payment. Guarded by the suspension reason: subscriptions suspended manually
    ///     or for other causes are never reactivated automatically.
    /// </summary>
    /// <param name="subscriptionId">Subscription that may be dunning-suspended.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when a dunning suspension was lifted by this call.</returns>
    Task<bool> TryReactivateAfterRecoveryAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default);
}
