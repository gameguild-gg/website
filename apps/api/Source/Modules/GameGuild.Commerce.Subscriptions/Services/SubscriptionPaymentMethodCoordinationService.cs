using GameGuild.Commerce;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Coordinates subscriptions with the local payment-method registry (issue #414).
///     Implements the cross-module contract owned by <see cref="GameGuild.Commerce"/> so the
///     Payments module can guard deletes and re-point subscriptions without referencing this
///     module.
/// </summary>
public class SubscriptionPaymentMethodCoordinationService(
    ISubscriptionRepository repository,
    ILogger<SubscriptionPaymentMethodCoordinationService> logger) : ISubscriptionPaymentMethodCoordination
{
    /// <summary>
    ///     Statuses that either bill now or resume billing later — deleting their payment
    ///     method is refused (fail-closed).
    /// </summary>
    private static readonly SubscriptionStatus[] BillableStatuses =
    [
        SubscriptionStatus.Active,
        SubscriptionStatus.Trialing,
        SubscriptionStatus.PastDue,
        SubscriptionStatus.Suspended
    ];

    /// <summary>
    ///     Terminal statuses — subscriptions in them never bill again, so their payment
    ///     method can be removed and must not be re-pointed.
    /// </summary>
    private static readonly SubscriptionStatus[] TerminalStatuses =
    [
        SubscriptionStatus.Cancelled,
        SubscriptionStatus.Expired
    ];

    /// <inheritdoc />
    public async Task<int> CountBillableSubscriptionsOnPaymentMethodAsync(
        Guid tenantId,
        Guid paymentMethodId,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = await repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return subscriptions.Count(s => BillableStatuses.Contains(s.Status) && s.PaymentMethodId == paymentMethodId);
    }

    /// <inheritdoc />
    public async Task<int> RepointSubscriptionsAsync(
        Guid tenantId,
        Guid fromPaymentMethodId,
        Guid toPaymentMethodId,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = await repository.GetByTenantIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var rePointed = 0;

        foreach (var subscription in subscriptions.Where(s =>
                     s.PaymentMethodId == fromPaymentMethodId
                     && !TerminalStatuses.Contains(s.Status)))
        {
            subscription.SetPaymentMethod(toPaymentMethodId);
            await repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
            rePointed++;
        }

        if (rePointed > 0)
        {
            logger.LogInformation(
                "Re-pointed {RePointed} subscription(s) in tenant {TenantId} from payment method {FromId} to {ToId}.",
                rePointed, tenantId, fromPaymentMethodId, toPaymentMethodId);
        }

        return rePointed;
    }

    /// <inheritdoc />
    public async Task AssignSubscriptionPaymentMethodAsync(
        Guid subscriptionId,
        Guid paymentMethodId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await repository.GetByIdAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            logger.LogWarning(
                "Cannot assign payment method {PaymentMethodId}: subscription {SubscriptionId} not found.",
                paymentMethodId, subscriptionId);
            return;
        }

        subscription.SetPaymentMethod(paymentMethodId);
        await repository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Guid?> GetSubscriptionBillingUserIdAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await repository.GetByIdAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        return subscription?.CreatedByUserId;
    }
}
