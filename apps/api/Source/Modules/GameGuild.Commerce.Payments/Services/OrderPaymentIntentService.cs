using GameGuild.Commerce;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

public sealed class OrderPaymentIntentService(
    IPaymentRepository payments,
    IStripePaymentService stripe,
    IOptions<PaymentRetryOptions> retryOptions) : IOrderPaymentIntentPreparer
{
    public async Task<OrderPaymentIntentPreparation> PrepareAsync(
        AuthoritativeOrderPaymentIntent intent,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = OrderPaymentService.CreateIdempotencyKey(intent.TenantId, intent.OrderId);
        var existing = await payments.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await ReplayAsync(existing, intent, cancellationToken).ConfigureAwait(false);
        }

        var proposed = Payment.Create(
            intent.TenantId,
            intent.Amount,
            intent.Currency,
            idempotencyKey,
            orderId: intent.OrderId,
            description: $"Payment for order {intent.OrderId}",
            maxRetries: retryOptions.Value.MaxRetries);
        var payment = await payments.AddAsync(proposed, cancellationToken).ConfigureAwait(false);
        if (payment.Id != proposed.Id)
        {
            return await ReplayAsync(payment, intent, cancellationToken).ConfigureAwait(false);
        }

        var setup = await stripe.CreatePaymentIntentAsync(new GatewayPaymentIntentSetupRequest(
            idempotencyKey,
            intent.Amount,
            intent.Currency,
            $"Payment for order {intent.OrderId}",
            new Dictionary<string, string>
            {
                ["order_id"] = intent.OrderId.ToString("N"),
                ["tenant_id"] = intent.TenantId.ToString("N"),
                ["payment_id"] = payment.Id.ToString("N")
            }), cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(setup.TransactionId) || string.IsNullOrWhiteSpace(setup.ClientSecret) || setup.ProviderMapping is null)
        {
            payment.MarkAsProcessing(setup.TransactionId);
            if (!setup.OutcomeUnknown)
            {
                payment.MarkAsFailed(
                    setup.ErrorMessage ?? "Stripe PaymentIntent setup failed.",
                    setup.ErrorCode,
                    maxRetries: retryOptions.Value.MaxRetries,
                    backoffBaseMinutes: retryOptions.Value.BackoffBaseMinutes,
                    backoffMultiplier: retryOptions.Value.BackoffMultiplier);
            }

            await payments.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);
            return new OrderPaymentIntentPreparation(
                false,
                payment.Id,
                null,
                setup.ErrorMessage ?? "Stripe PaymentIntent setup is unavailable.",
                setup.OutcomeUnknown ? OrderChargeState.RequiresReconciliation : OrderChargeState.Failed);
        }

        payment.MarkAsProcessing(setup.TransactionId);
        payment.BindProviderMapping(
            "stripe",
            setup.ProviderMapping.ProviderEnvironment,
            setup.ProviderMapping.ProviderAccountId,
            setup.ProviderMapping.ProviderObjectId,
            setup.ProviderMapping.ProviderObjectType,
            setup.ProviderMapping.ProviderMonetaryLeg);
        payment.MarkAsRequiresAction(setup.TransactionId);
        await payments.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);
        return new OrderPaymentIntentPreparation(true, payment.Id, setup.ClientSecret, null, OrderChargeState.RequiresAction);
    }

    private async Task<OrderPaymentIntentPreparation> ReplayAsync(
        Payment payment,
        AuthoritativeOrderPaymentIntent intent,
        CancellationToken cancellationToken)
    {
        if (payment.OrderId != intent.OrderId || payment.TenantId != intent.TenantId ||
            payment.Amount != intent.Amount || !string.Equals(payment.Currency, intent.Currency, StringComparison.Ordinal))
        {
            return new OrderPaymentIntentPreparation(false, payment.Id, null, "Existing payment does not match the order.", OrderChargeState.Failed);
        }

        if (string.IsNullOrWhiteSpace(payment.ExternalTransactionId))
        {
            return new OrderPaymentIntentPreparation(false, payment.Id, null, "PaymentIntent creation requires reconciliation.", OrderChargeState.RequiresReconciliation);
        }

        var provider = await stripe.GetPaymentAsync(payment.ExternalTransactionId, cancellationToken).ConfigureAwait(false);
        var state = provider.Status switch
        {
            PaymentStatus.Succeeded => OrderChargeState.Succeeded,
            PaymentStatus.RequiresAction => OrderChargeState.RequiresAction,
            PaymentStatus.Processing or PaymentStatus.Pending => OrderChargeState.Processing,
            _ => OrderChargeState.Failed
        };
        return new OrderPaymentIntentPreparation(
            provider.Status == PaymentStatus.RequiresAction && !string.IsNullOrWhiteSpace(provider.ClientActionToken),
            payment.Id,
            provider.ClientActionToken,
            provider.ErrorMessage,
            state);
    }
}
