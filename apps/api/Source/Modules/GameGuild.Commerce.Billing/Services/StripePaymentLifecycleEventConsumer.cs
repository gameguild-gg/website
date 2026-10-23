using System.Text.Json;
using GameGuild.Commerce.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Reconciles Stripe payment-lifecycle webhooks into the local <see cref="Payment"/> and
///     <see cref="Refund"/> aggregates (issue #415): <c>payment_intent.succeeded</c> and
///     <c>payment_intent.payment_failed</c> confirm outcomes the synchronous charge path never
///     observed (lost gateway responses, asynchronous 3DS completions), putting the payment back
///     on its retry schedule when the gateway reports the failure; <c>refund.created</c> and
///     <c>refund.updated</c> complete or fail the local refund row awaiting its gateway outcome;
///     <c>charge.refunded</c> coalesces with the per-refund events and only closes the gap when
///     the per-refund delivery was missed and exactly one in-flight local refund remains.
///     Runs through the <see cref="IStripeVerifiedEventConsumer"/> extension point, so every
///     event arrives from the same verified, idempotent inbox as the dispute reconciliation.
///     Events whose payment intent resolves to no local payment are released (<c>false</c>) so
///     sibling consumers — e.g. the economy top-up consumer — can claim them.
/// </summary>
public sealed class StripePaymentLifecycleEventConsumer(
    IPaymentRepository paymentRepository,
    IRefundRepository refundRepository,
    IRefundProcessingService refundProcessingService,
    IPaymentRetryPolicyResolver retryPolicyResolver,
    IOptions<PaymentRetryOptions> retryOptions,
    ILogger<StripePaymentLifecycleEventConsumer> logger) : IStripeVerifiedEventConsumer
{
    private const string StripeProvider = "stripe";

    /// <inheritdoc />
    public async ValueTask<bool> TryConsumeAsync(
        VerifiedStripeWebhookEvent verifiedEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedEvent);

        switch (verifiedEvent.EventType)
        {
            case "payment_intent.succeeded":
            case "payment_intent.payment_failed":
                return await HandlePaymentIntentEventAsync(verifiedEvent, cancellationToken).ConfigureAwait(false);

            case "refund.created":
            case "refund.updated":
                return await HandleRefundEventAsync(verifiedEvent, cancellationToken).ConfigureAwait(false);

            case "charge.refunded":
                return await HandleChargeRefundedEventAsync(verifiedEvent, cancellationToken).ConfigureAwait(false);

            default:
                return false;
        }
    }

    /// <summary>
    ///     Payment-intent outcome reconciliation: a succeeded intent completes a payment still
    ///     pending/processing locally; a failed intent records the failure and re-arms the
    ///     failure-code retry schedule so the payment re-enters the retry queue (issue #415).
    ///     Both paths are idempotent — a payment already in a settled state is left untouched.
    /// </summary>
    private async Task<bool> HandlePaymentIntentEventAsync(
        VerifiedStripeWebhookEvent verifiedEvent,
        CancellationToken cancellationToken)
    {
        var payload = ParseLifecyclePayload(verifiedEvent.VerifiedPayload);
        if (payload is null)
        {
            logger.LogWarning(
                "Stripe payment lifecycle event {EventId} could not be parsed; skipping payment reconciliation",
                verifiedEvent.EventId);
            return true;
        }

        var payment = await ResolvePaymentAsync(verifiedEvent, cancellationToken).ConfigureAwait(false);
        if (payment is null)
        {
            // Not a Commerce payment (e.g. an economy top-up intent): release the event to the
            // sibling consumers instead of failing the inbox delivery.
            logger.LogDebug(
                "Stripe payment lifecycle event {EventId} references no local payment; released",
                verifiedEvent.EventId);
            return false;
        }

        if (verifiedEvent.EventType == "payment_intent.succeeded")
        {
            if (payment.Status is PaymentStatus.Pending
                or PaymentStatus.Processing
                or PaymentStatus.RequiresAction)
            {
                payment.MarkAsSucceeded(verifiedEvent.ProviderObjectId, payload.ChargeId);
                await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);
                logger.LogInformation(
                    "Payment {PaymentId} reconciled to Succeeded from Stripe event {EventId}",
                    payment.Id,
                    verifiedEvent.EventId);
            }
            else
            {
                logger.LogDebug(
                    "Stripe succeeded event {EventId} for payment {PaymentId} in status {Status} is idempotently ignored",
                    verifiedEvent.EventId,
                    payment.Id,
                    payment.Status);
            }

            return true;
        }

        // payment_intent.payment_failed: record the failure on payments that never observed a
        // terminal outcome locally and schedule the retry the same way the synchronous path
        // does (issue #415). The minimized webhook payload retains no decline code, so the
        // default exponential-backoff schedule applies.
        if (payment.Status is PaymentStatus.Pending
            or PaymentStatus.Processing
            or PaymentStatus.RequiresAction)
        {
            var schedule = retryPolicyResolver.ComputeSchedule(failureCode: null, payment.RetryCount);
            payment.MarkAsFailed(
                $"Stripe reported the payment failed at the gateway (event {verifiedEvent.EventId}).",
                errorCode: null,
                maxRetries: schedule.EffectiveMaxRetries,
                backoffBaseMinutes: retryOptions.Value.BackoffBaseMinutes,
                backoffMultiplier: retryOptions.Value.BackoffMultiplier,
                nextRetryAt: schedule.NextRetryAt,
                isRetriable: schedule.IsRetriable);
            await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Payment {PaymentId} reconciled to Failed from Stripe event {EventId}; next retry {NextRetryAt} (retriable: {IsRetriable})",
                payment.Id,
                verifiedEvent.EventId,
                schedule.NextRetryAt,
                schedule.IsRetriable);
        }
        else
        {
            logger.LogDebug(
                "Stripe failed event {EventId} for payment {PaymentId} in status {Status} is idempotently ignored",
                verifiedEvent.EventId,
                payment.Id,
                payment.Status);
        }

        return true;
    }

    /// <summary>
    ///     Refund webhook reconciliation (issue #415 payment.refunded events): a gateway refund
    ///     reported as succeeded completes the local refund row through the accounting-aware
    ///     core; one reported as failed/canceled fails the in-flight row so the automated refund
    ///     workflow can retry it. Gateway refunds without a local row (initiated directly in the
    ///     gateway dashboard) are released: the platform never records financial rows it cannot
    ///     attribute to a tenant.
    /// </summary>
    private async Task<bool> HandleRefundEventAsync(
        VerifiedStripeWebhookEvent verifiedEvent,
        CancellationToken cancellationToken)
    {
        var payload = ParseLifecyclePayload(verifiedEvent.VerifiedPayload);
        if (payload is null || string.IsNullOrWhiteSpace(payload.ObjectId))
        {
            logger.LogWarning(
                "Stripe refund event {EventId} could not be parsed; skipping refund reconciliation",
                verifiedEvent.EventId);
            return true;
        }

        var refund = await refundRepository
            .GetByGatewayRefundIdAsync(payload.ObjectId, cancellationToken)
            .ConfigureAwait(false);
        if (refund is null)
        {
            logger.LogDebug(
                "Stripe refund event {EventId} references no local refund row; released",
                verifiedEvent.EventId);
            return false;
        }

        switch (payload.Status)
        {
            case "succeeded":
                if (refund.IsTerminal)
                {
                    logger.LogDebug(
                        "Stripe refund event {EventId} replays the terminal state of refund {RefundId}; ignored",
                        verifiedEvent.EventId,
                        refund.Id);
                    return true;
                }

                var completion = await refundProcessingService
                    .CompleteFromGatewayAsync(refund, payload.ObjectId, StripeProvider, cancellationToken)
                    .ConfigureAwait(false);
                if (completion.Success)
                {
                    logger.LogInformation(
                        "Refund {RefundId} completed from Stripe refund event {EventId}",
                        refund.Id,
                        verifiedEvent.EventId);
                }
                else
                {
                    logger.LogWarning(
                        "Stripe refund event {EventId} could not complete refund {RefundId}: {Reason}",
                        verifiedEvent.EventId,
                        refund.Id,
                        completion.FailureReason);
                }

                return true;

            case "failed":
            case "canceled":
                if (refund.Status == RefundStatus.Processing)
                {
                    refund.MarkFailed(
                        $"Stripe reported the refund as {payload.Status} (event {verifiedEvent.EventId}).");
                    await refundRepository.UpdateAsync(refund, cancellationToken).ConfigureAwait(false);
                    logger.LogInformation(
                        "Refund {RefundId} marked failed from Stripe refund event {EventId}",
                        refund.Id,
                        verifiedEvent.EventId);
                }

                return true;

            default:
                // pending / requires_action: the refund is still in flight at the gateway;
                // nothing to reconcile yet.
                return true;
        }
    }

    /// <summary>
    ///     <c>charge.refunded</c> coalesces the per-refund Stripe events (<c>refund.created</c> /
    ///     <c>refund.updated</c>) and only reconciles when those were missed: exactly one
    ///     in-flight local refund for the payment is completed with a derived gateway refund id.
    ///     Ambiguous or external refunds (no in-flight local row) are logged and skipped.
    ///     Infrastructure failures from the completion core propagate to the caller (the
    ///     verified-event inbox routing) so the failed delivery is redelivered by the inbox
    ///     retry machinery instead of leaving the refund stuck in Processing.
    /// </summary>
    private async Task<bool> HandleChargeRefundedEventAsync(
        VerifiedStripeWebhookEvent verifiedEvent,
        CancellationToken cancellationToken)
    {
        var payment = await ResolvePaymentAsync(verifiedEvent, cancellationToken).ConfigureAwait(false);
        if (payment is null)
        {
            logger.LogDebug(
                "Stripe charge.refunded event {EventId} references no local payment; released",
                verifiedEvent.EventId);
            return false;
        }

        if (payment.Status is not (PaymentStatus.Succeeded
            or PaymentStatus.Disputed
            or PaymentStatus.PartiallyRefunded))
        {
            logger.LogDebug(
                "Stripe charge.refunded event {EventId} for payment {PaymentId} in status {Status} is ignored",
                verifiedEvent.EventId,
                payment.Id,
                payment.Status);
            return true;
        }

        var openRefund = await refundRepository
            .GetOpenRefundForPaymentAsync(payment.Id, cancellationToken)
            .ConfigureAwait(false);
        if (openRefund is null || openRefund.Status != RefundStatus.Processing)
        {
            logger.LogDebug(
                "Stripe charge.refunded event {EventId} has no single in-flight local refund for payment {PaymentId}; the per-refund events own the reconciliation",
                verifiedEvent.EventId,
                payment.Id);
            return true;
        }

        // Infrastructure failures (e.g. transient DB outages) propagate so the verified-event
        // inbox marks the delivery failed and redelivers; a swallowed exception would leave
        // the refund stuck in Processing forever. Business refusals arrive as failure results,
        // handled by the warning branch above.
        var completion = await refundProcessingService
            .CompleteFromGatewayAsync(
                openRefund,
                $"charge_refund_{verifiedEvent.ProviderObjectId}",
                StripeProvider,
                cancellationToken)
            .ConfigureAwait(false);
        if (completion.Success)
        {
            logger.LogInformation(
                "Refund {RefundId} completed from Stripe charge.refunded event {EventId}",
                openRefund.Id,
                verifiedEvent.EventId);
        }
        else
        {
            logger.LogWarning(
                "Stripe charge.refunded event {EventId} could not complete refund {RefundId}: {Reason}",
                verifiedEvent.EventId,
                openRefund.Id,
                completion.FailureReason);
        }

        return true;
    }

    /// <summary>
    ///     Resolves the local payment through the same verified provider-object identity the
    ///     dispute consumer uses (payment_intent / capture leg), with the legacy-column
    ///     fallbacks for payments captured before provider-object binding existed.
    /// </summary>
    private async Task<Payment?> ResolvePaymentAsync(
        VerifiedStripeWebhookEvent verifiedEvent,
        CancellationToken cancellationToken)
    {
        var payment = await paymentRepository.GetByProviderMappingAsync(
                StripeProvider,
                verifiedEvent.ProviderEnvironment,
                verifiedEvent.ProviderAccountId,
                verifiedEvent.ProviderObjectId,
                "payment_intent",
                "capture",
                cancellationToken)
            .ConfigureAwait(false);

        if (payment is not null)
        {
            return payment;
        }

        return await paymentRepository
            .GetByExternalPaymentIdAsync(verifiedEvent.ProviderObjectId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Parses the minimized Stripe payload of a payment lifecycle event: the provider
    ///     object's id, status and (for payment intents) the charge reference.
    /// </summary>
    internal static StripeLifecyclePayload? ParseLifecyclePayload(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!root.TryGetProperty("data", out var data)
                || !data.TryGetProperty("object", out var providerObject)
                || providerObject.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!providerObject.TryGetProperty("id", out var idElement)
                || idElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return new StripeLifecyclePayload(
                idElement.GetString()!,
                ReadString(providerObject, "status"),
                ReadString(providerObject, "charge"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    internal sealed record StripeLifecyclePayload(
        string ObjectId,
        string? Status,
        string? ChargeId);
}
