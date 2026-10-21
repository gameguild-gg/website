using GameGuild.Commerce.Payments;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Bridges successful payment events into subscription billing state, and exposes the
///     billing-period context + refund-adjustment seams the Payments module needs for prorated
///     refunds and post-refund subscription adjustment (issues #415/#419).
/// </summary>
public sealed class PaymentSubscriptionSyncService(
    ISubscriptionRepository subscriptionRepository,
    ISubscriptionInvoiceMaterializer invoiceMaterializer,
    ISubscriptionLifecycleService lifecycleService,
    ILogger<PaymentSubscriptionSyncService> logger) : IPaymentSubscriptionSyncService
{
    public Task SyncSuccessfulPaymentAsync(
        Guid paymentId,
        Guid? subscriptionId,
        decimal amount,
        string currency,
        DateTime processedAt,
        CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "Payment {PaymentId} cannot be synced to subscription {SubscriptionId} without a billing cycle identity",
            paymentId,
            subscriptionId);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Synchronizes a provider-confirmed payment for one explicit subscription billing cycle.
    /// </summary>
    public async Task SyncSuccessfulPaymentAsync(
        Guid paymentId,
        Guid? subscriptionId,
        decimal amount,
        string currency,
        int? billingCycleNumber,
        DateTime processedAt,
        CancellationToken cancellationToken = default)
    {
        if (!billingCycleNumber.HasValue)
        {
            logger.LogWarning(
                "Payment {PaymentId} cannot be synced to subscription {SubscriptionId} without a billing cycle identity",
                paymentId,
                subscriptionId);
            return;
        }

        if (!subscriptionId.HasValue)
        {
            logger.LogDebug("Payment {PaymentId} has no subscription link; skipping subscription sync", paymentId);
            return;
        }

        var subscription = await subscriptionRepository.GetByIdAsync(subscriptionId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            logger.LogWarning(
                "Payment {PaymentId} references missing subscription {SubscriptionId}; skipping subscription sync",
                paymentId,
                subscriptionId.Value);
            return;
        }

        var idempotencyKey = $"payment:{paymentId}";
        var result = subscription.RecordPayment(
            amount,
            currency,
            processedAt,
            idempotencyKey,
            billingCycleNumber.Value);

        if (result.IsSuccess)
        {
            if (subscription.Status == SubscriptionStatus.PendingActivation)
            {
                subscription.Activate();
            }

            await subscriptionRepository.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Payment {PaymentId} synced to subscription {SubscriptionId}",
                paymentId,
                subscriptionId.Value);

            await MaterializeInvoiceAsync(
                subscription.Id,
                billingCycleNumber.Value,
                amount,
                currency,
                processedAt,
                paymentId,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        if (result.IsAlreadyProcessed)
        {
            logger.LogInformation(
                "Payment {PaymentId} was already synced to subscription {SubscriptionId}",
                paymentId,
                subscriptionId.Value);

            // Idempotent replays re-attempt materialization: a cycle can be payment-synced before its
            // invoice write succeeds, and the materializer itself is keyed per cycle.
            await MaterializeInvoiceAsync(
                subscription.Id,
                billingCycleNumber.Value,
                amount,
                currency,
                processedAt,
                paymentId,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        logger.LogWarning(
            "Payment {PaymentId} could not be synced to subscription {SubscriptionId}: {Reason}",
            paymentId,
            subscriptionId.Value,
            result.Message);
    }

    /// <summary>
    ///     Materializes the cycle invoice on top of an already-persisted subscription payment.
    ///     Best effort by contract: the payment-success flow must never be rolled back by a
    ///     materialization failure (the materializer is idempotent per cycle, so later replays
    ///     re-attempt the write through the same key). Cancellation is the only propagation.
    /// </summary>
    private async Task MaterializeInvoiceAsync(
        Guid subscriptionId,
        int billingCycleNumber,
        decimal amount,
        string currency,
        DateTime processedAt,
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        try
        {
            await invoiceMaterializer.MaterializeForConfirmedCycleAsync(
                subscriptionId,
                billingCycleNumber,
                amount,
                currency,
                processedAt,
                paymentId,
                providerInvoiceId: null,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Invoice materialization failed for subscription {SubscriptionId} cycle {Cycle} after payment {PaymentId}; the payment sync itself stays applied",
                subscriptionId,
                billingCycleNumber,
                paymentId);
        }
    }

    /// <summary>
    ///     Billing-period context for prorated refunds (issue #419): the subscription's current
    ///     period and its owning user. Null when the payment has no subscription link or the
    ///     subscription no longer exists — the refund path fails closed instead of guessing.
    /// </summary>
    public async Task<SubscriptionBillingPeriodContext?> GetBillingPeriodAsync(
        Guid paymentId,
        Guid? subscriptionId,
        CancellationToken cancellationToken = default)
    {
        if (!subscriptionId.HasValue)
        {
            logger.LogDebug("Payment {PaymentId} has no subscription link; no billing-period context for proration", paymentId);
            return null;
        }

        var subscription = await subscriptionRepository.GetByIdAsync(subscriptionId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            logger.LogWarning(
                "Payment {PaymentId} references missing subscription {SubscriptionId}; no billing-period context for proration",
                paymentId,
                subscriptionId.Value);
            return null;
        }

        return new SubscriptionBillingPeriodContext(
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.CreatedByUserId);
    }

    /// <summary>
    ///     Post-refund subscription adjustment (issue #415): a fully refunded payment suspends an
    ///     active/past-due subscription through the standard lifecycle with the
    ///     <see cref="PaymentDunningReasons.RefundIssued" /> reason; partial refunds leave the
    ///     subscription running. Idempotent: suspended/cancelled subscriptions are untouched.
    /// </summary>
    public async Task AdjustForRefundAsync(
        Guid paymentId,
        Guid subscriptionId,
        decimal refundAmount,
        bool fullyRefunded,
        CancellationToken cancellationToken = default)
    {
        if (!fullyRefunded)
        {
            logger.LogInformation(
                "Partial refund {Amount} for payment {PaymentId}; subscription {SubscriptionId} stays active",
                refundAmount,
                paymentId,
                subscriptionId);
            return;
        }

        var subscription = await subscriptionRepository.GetByIdAsync(subscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            logger.LogWarning(
                "Payment {PaymentId} references missing subscription {SubscriptionId}; refund adjustment skipped",
                paymentId,
                subscriptionId);
            return;
        }

        if (subscription.Status is SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled)
        {
            logger.LogInformation(
                "Subscription {SubscriptionId} is already {Status}; refund adjustment skipped",
                subscriptionId,
                subscription.Status);
            return;
        }

        await lifecycleService.SuspendAsync(subscriptionId, PaymentDunningReasons.RefundIssued, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Subscription {SubscriptionId} suspended after a full refund of payment {PaymentId}",
            subscriptionId,
            paymentId);
    }
}
