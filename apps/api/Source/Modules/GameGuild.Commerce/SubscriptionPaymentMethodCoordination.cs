namespace GameGuild.Commerce;

/// <summary>
///     Coordinates the local payment-method registry (Payments module, issue #414) with the
///     subscriptions that bill through it. Implemented by the Subscriptions module so the
///     Payments project graph keeps its dependency direction (Subscriptions → Payments).
/// </summary>
public interface ISubscriptionPaymentMethodCoordination
{
    /// <summary>
    ///     Counts the billable-or-resumable subscriptions in a tenant that are billed with the
    ///     given payment method. Used as the fail-closed delete guard.
    /// </summary>
    Task<int> CountBillableSubscriptionsOnPaymentMethodAsync(
        Guid tenantId,
        Guid paymentMethodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Moves every non-terminal subscription in the tenant from one payment method to
    ///     another (used when the default payment method changes).
    /// </summary>
    /// <returns>The number of subscriptions that were re-pointed.</returns>
    Task<int> RepointSubscriptionsAsync(
        Guid tenantId,
        Guid fromPaymentMethodId,
        Guid toPaymentMethodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Assigns a payment method to a single subscription (used when a checkout completes
    ///     with a confirmed gateway payment method).
    /// </summary>
    Task AssignSubscriptionPaymentMethodAsync(
        Guid subscriptionId,
        Guid paymentMethodId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Returns the user that manages billing for a subscription (its creator), used to
    ///     attribute payment methods registered during checkout by service actors.
    /// </summary>
    Task<Guid?> GetSubscriptionBillingUserIdAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default);
}
