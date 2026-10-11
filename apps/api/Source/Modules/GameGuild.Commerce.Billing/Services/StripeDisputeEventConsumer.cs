using System.Text.Json;
using GameGuild.Commerce.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Reconciles <c>charge.dispute.*</c> Stripe webhooks into local <see cref="PaymentDispute"/>
///     records (issue #419). Runs ahead of the subscription/invoice routing in
///     <c>StripeBillingWebhookService</c> through the <see cref="IStripeVerifiedEventConsumer"/>
///     extension point, so disputes reach the local dispute surface from the same verified,
///     idempotent inbox as every other Stripe financial event. The payment is resolved through
///     its verified provider object identity (payment_intent / capture leg).
/// </summary>
public sealed class StripeDisputeEventConsumer(
    IPaymentRepository paymentRepository,
    IPaymentMethodRepository paymentMethodRepository,
    IApplicationDbContext context,
    ILogger<StripeDisputeEventConsumer> logger) : IStripeVerifiedEventConsumer
{
    private const string StripeProvider = "stripe";

    public async ValueTask<bool> TryConsumeAsync(
        VerifiedStripeWebhookEvent verifiedEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedEvent);

        if (!verifiedEvent.EventType.StartsWith("charge.dispute.", StringComparison.Ordinal))
        {
            return false;
        }

        var disputePayload = ParseDisputePayload(verifiedEvent.VerifiedPayload);
        if (disputePayload is null)
        {
            logger.LogWarning(
                "Stripe dispute event {EventId} could not be parsed; skipping dispute reconciliation",
                verifiedEvent.EventId);
            return true;
        }

        var payment = await ResolvePaymentAsync(verifiedEvent, cancellationToken).ConfigureAwait(false);
        if (payment is null)
        {
            // The verified-event inbox will surface this failure; a dispute for an unknown
            // payment must never be recorded against a guessed target (fail closed).
            throw new InvalidWebhookPayloadException(
                $"Stripe dispute event {verifiedEvent.EventId} references an unknown payment.");
        }

        var existing = await context.Set<PaymentDispute>()
            .FirstOrDefaultAsync(
                dispute => dispute.GatewayDisputeId == disputePayload.DisputeId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await CreateDisputeAsync(payment, verifiedEvent, disputePayload, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            existing.ApplyGatewayStatus(MapStatus(disputePayload.Status), disputePayload.EvidenceDueBy);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Dispute {DisputeId} reconciled to gateway status {Status} from Stripe event {EventId}",
                existing.Id,
                existing.Status,
                verifiedEvent.EventId);
        }

        return true;
    }

    private async Task CreateDisputeAsync(
        Payment payment,
        VerifiedStripeWebhookEvent verifiedEvent,
        StripeDisputePayload payload,
        CancellationToken cancellationToken)
    {
        var userId = await ResolvePaymentMethodOwnerAsync(payment, cancellationToken).ConfigureAwait(false);
        var statusBefore = payment.Status;

        var dispute = new PaymentDispute
        {
            PaymentId = payment.Id,
            UserId = userId,
            Type = MapType(payload.Reason),
            Amount = verifiedEvent.Amount ?? payload.Amount,
            Reason = string.IsNullOrWhiteSpace(payload.Reason) ? "unspecified" : payload.Reason,
            Description = $"Stripe dispute {payload.DisputeId} ({verifiedEvent.EventType}).",
            Status = MapStatus(payload.Status),
            DueDate = payload.EvidenceDueBy
        };
        dispute.BindGatewayIdentity(StripeProvider, payload.DisputeId, payload.EvidenceDueBy);

        await context.Set<PaymentDispute>().AddAsync(dispute, cancellationToken).ConfigureAwait(false);

        if (payment.Status == PaymentStatus.Succeeded)
        {
            payment.MarkAsDisputed();
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Dispute {DisputeId} created from Stripe dispute {GatewayDisputeId} for payment {PaymentId} (payment status {Before} -> {After})",
            dispute.Id,
            payload.DisputeId,
            payment.Id,
            statusBefore,
            payment.Status);
    }

    private async Task<Payment?> ResolvePaymentAsync(
        VerifiedStripeWebhookEvent verifiedEvent,
        CancellationToken cancellationToken)
    {
        // The verified provider object for a dispute event is the canonical payment_intent of
        // the disputed charge (see StripeWebhookVerifier.ResolveProviderObjectIdentity); resolve
        // the payment through the same verified identity the binding validator uses.
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

        // Fallback for payments captured before provider-object binding existed: match the
        // payment intent reference directly, which is the same identity in a legacy column.
        // Tracked on purpose — the consumer may move the payment to Disputed on creation.
        return await paymentRepository.GetByExternalPaymentIdAsync(verifiedEvent.ProviderObjectId, cancellationToken)
                   .ConfigureAwait(false)
               ?? await context.Set<Payment>()
                   .Where(p => p.ExternalTransactionId == verifiedEvent.ProviderObjectId)
                   .OrderByDescending(p => p.CreatedAt)
                   .FirstOrDefaultAsync(cancellationToken)
                   .ConfigureAwait(false);
    }

    private async Task<Guid> ResolvePaymentMethodOwnerAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(payment.PaymentMethodId))
        {
            var method = await paymentMethodRepository
                .GetByGatewayIdAsync(payment.TenantId, StripeProvider, payment.PaymentMethodId, cancellationToken)
                .ConfigureAwait(false);
            if (method is not null)
            {
                return method.UserId;
            }
        }

        // Gateway-initiated disputes raised by the cardholder's bank are not attributable to a
        // local user account; the sentinel keeps the required column honest without inventing one.
        return Guid.Empty;
    }

    internal static DisputeStatus MapStatus(string? stripeStatus) => stripeStatus switch
    {
        "needs_response" or "warning_needs_response" => DisputeStatus.Submitted,
        "under_review" or "warning_under_review" => DisputeStatus.UnderReview,
        "won" => DisputeStatus.Won,
        "lost" => DisputeStatus.Lost,
        "charge_refunded" => DisputeStatus.Resolved,
        _ => DisputeStatus.UnderReview
    };

    internal static DisputeType MapType(string? stripeReason) => stripeReason switch
    {
        "fraudulent" => DisputeType.Fraudulent,
        "product_not_received" or "service_not_rendered" => DisputeType.ProductNotReceived,
        "product_unacceptable" or "not_as_described" => DisputeType.ProductNotAsDescribed,
        "duplicate" => DisputeType.Duplicate,
        "incorrect_amount" => DisputeType.IncorrectAmount,
        "credit_not_processed" or "subscription_canceled" => DisputeType.CreditNotProcessed,
        _ => DisputeType.Other
    };

    internal static StripeDisputePayload? ParseDisputePayload(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!root.TryGetProperty("data", out var data)
                || !data.TryGetProperty("object", out var disputeObject)
                || disputeObject.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!disputeObject.TryGetProperty("id", out var idElement)
                || idElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return new StripeDisputePayload(
                idElement.GetString()!,
                ReadString(disputeObject, "status"),
                ReadString(disputeObject, "reason"),
                ReadMoney(disputeObject, "amount"),
                ReadUnixTimestamp(disputeObject, "evidence_due_by"));
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

    private static decimal ReadMoney(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetDecimal() / 100m
            : 0m;

    private static DateTime? ReadUnixTimestamp(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(property.GetInt64()).UtcDateTime
            : null;

    internal sealed record StripeDisputePayload(
        string DisputeId,
        string? Status,
        string? Reason,
        decimal Amount,
        DateTime? EvidenceDueBy);
}
