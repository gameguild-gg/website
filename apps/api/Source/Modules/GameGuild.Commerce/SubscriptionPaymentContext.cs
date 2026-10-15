namespace GameGuild.Commerce;

/// <summary>
///     Minimal subscription payment context shared across commerce modules.
///     Keeps payment-processing dependencies out of the Subscriptions project graph.
/// </summary>
/// <param name="SubscriptionId">Subscription the payment context describes.</param>
/// <param name="TenantId">Owning tenant.</param>
/// <param name="Amount">Authoritative subscription price for the cycle.</param>
/// <param name="Currency">ISO 4217 currency of the price.</param>
/// <param name="ExternalCustomerId">Provider customer reference, when bound.</param>
/// <param name="BillingCycleNumber">Cycle number the next payment settles.</param>
/// <param name="BillingPeriodStart">Start of the billing period being settled.</param>
/// <param name="BillingPeriodEnd">End of the billing period being settled.</param>
/// <param name="OwnerUserId">
///     User the subscription belongs to; lets the payment path resolve per-user state
///     (for example account-credit auto-apply, issue #410) without a Subscriptions reference.
/// </param>
public sealed record SubscriptionPaymentContext(
    Guid SubscriptionId,
    Guid TenantId,
    decimal Amount,
    string Currency,
    string? ExternalCustomerId,
    int BillingCycleNumber = 1,
    DateTime? BillingPeriodStart = null,
    DateTime? BillingPeriodEnd = null,
    Guid? OwnerUserId = null);

/// <summary>
///     Provides the payment-facing subscription data needed by the Payments module.
/// </summary>
public interface ISubscriptionPaymentContextService
{
    /// <summary>
    ///     Loads the payment-relevant context for a subscription.
    /// </summary>
    Task<SubscriptionPaymentContext?> GetPaymentContextAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Persists the Stripe customer linked to a subscription.
    /// </summary>
    Task SetExternalCustomerIdAsync(Guid subscriptionId, string externalCustomerId, CancellationToken cancellationToken = default);
}
