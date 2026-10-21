namespace GameGuild.Commerce.Payments;

/// <summary>Billing-period context of a subscription-linked payment (issue #419 prorated refunds).</summary>
/// <param name="PeriodStart">Inclusive start of the billing period the payment belongs to.</param>
/// <param name="PeriodEnd">Exclusive end of that billing period.</param>
/// <param name="OwnerUserId">User that owns the subscription, when known.</param>
public sealed record SubscriptionBillingPeriodContext(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    Guid? OwnerUserId = null);

/// <summary>
///     Synchronizes subscription state after payment success, and exposes the read-side
///     billing-period context plus the refund adjustment seam used by the Payments module
///     (issue #419 proration and subscription adjustment on refund).
/// </summary>
public interface IPaymentSubscriptionSyncService
{
    Task SyncSuccessfulPaymentAsync(
        Guid paymentId,
        Guid? subscriptionId,
        decimal amount,
        string currency,
        int? billingCycleNumber,
        DateTime processedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the billing period of a subscription-linked payment for refund proration
    ///     (issue #419). Null when the payment has no subscription link or the subscription no
    ///     longer exists — callers fail closed instead of guessing a period.
    /// </summary>
    Task<SubscriptionBillingPeriodContext?> GetBillingPeriodAsync(
        Guid paymentId,
        Guid? subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adjusts the subscription after a completed refund (issue #415 automatic subscription
    ///     adjustment on refund). A full refund suspends an active/past-due subscription with the
    ///     <see cref="PaymentDunningReasons.RefundIssued"/> reason so the customer no longer holds
    ///     access they were reimbursed for; partial refunds leave the subscription running.
    ///     Idempotent: already suspended or cancelled subscriptions are left untouched.
    /// </summary>
    Task AdjustForRefundAsync(
        Guid paymentId,
        Guid subscriptionId,
        decimal refundAmount,
        bool fullyRefunded,
        CancellationToken cancellationToken = default);
}
