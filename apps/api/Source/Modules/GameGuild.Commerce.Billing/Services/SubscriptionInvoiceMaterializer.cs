using GameGuild.Commerce.Subscriptions;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Commerce.Billing implementation of the Subscriptions-side
///     <see cref="ISubscriptionInvoiceMaterializer"/> port. Thin adapter over
///     <see cref="IInvoiceGenerationService"/> that never throws: it runs after a payment has
///     already succeeded and subscription state is already persisted, so materialization failures
///     are logged for ops instead of failing the payment-success flow. Later idempotent replays
/// (command retries, UpdatePaymentStatus) re-attempt materialization through the same key.
/// </summary>
public sealed class SubscriptionInvoiceMaterializer(
    IInvoiceGenerationService invoiceGeneration,
    ILogger<SubscriptionInvoiceMaterializer> logger) : ISubscriptionInvoiceMaterializer
{
    /// <inheritdoc />
    public async Task MaterializeForConfirmedCycleAsync(
        Guid subscriptionId,
        int billingCycleNumber,
        decimal amount,
        string currency,
        DateTime processedAtUtc,
        Guid? paymentId = null,
        string? providerInvoiceId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await invoiceGeneration
                .MaterializeForConfirmedCycleAsync(
                    new ConfirmedCycleInvoiceRequest(
                        subscriptionId,
                        billingCycleNumber,
                        amount,
                        currency,
                        processedAtUtc,
                        paymentId,
                        providerInvoiceId),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!result.Created)
            {
                logger.LogInformation(
                    "Invoice for subscription {SubscriptionId} cycle {Cycle} already materialized; idempotent replay",
                    subscriptionId,
                    billingCycleNumber);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Invoice materialization failed for subscription {SubscriptionId} cycle {Cycle}; payment success is unaffected",
                subscriptionId,
                billingCycleNumber);
        }
    }
}
