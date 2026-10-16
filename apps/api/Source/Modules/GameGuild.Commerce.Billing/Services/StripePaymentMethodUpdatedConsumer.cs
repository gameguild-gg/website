using System.Text.Json;
using GameGuild.Commerce.Payments;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Reconciles Stripe <c>payment_method.updated</c> (and
/// <c>payment_method.automatically_updated</c>, the Account Updater variant) webhooks into
/// the local payment-method registry (issue #414 follow-up): the card's brand, last4,
/// fingerprint and expiry metadata are refreshed from the verified provider payload.
/// Unknown gateway tokens are consumed as a no-op — Stripe notifies about payment methods
/// this platform never registered, and those must not fail the inbox. Runs through the
/// <see cref="IStripeVerifiedEventConsumer"/> extension point ahead of the generic routing.
/// </summary>
public sealed class StripePaymentMethodUpdatedConsumer(
    IPaymentMethodRepository paymentMethodRepository,
    ILogger<StripePaymentMethodUpdatedConsumer> logger) : IStripeVerifiedEventConsumer
{
    private const string StripeProvider = "stripe";

    public async ValueTask<bool> TryConsumeAsync(
        VerifiedStripeWebhookEvent verifiedEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedEvent);

        if (verifiedEvent.EventType is not ("payment_method.updated" or "payment_method.automatically_updated"))
        {
            return false;
        }

        var payload = ParsePaymentMethodPayload(verifiedEvent.VerifiedPayload);
        if (payload is null)
        {
            logger.LogWarning(
                "Stripe payment-method event {EventId} could not be parsed; skipping metadata refresh",
                verifiedEvent.EventId);
            return true;
        }

        var methods = await paymentMethodRepository
            .ListByGatewayIdAsync(StripeProvider, payload.PaymentMethodId, cancellationToken)
            .ConfigureAwait(false);

        if (methods.Count == 0)
        {
            // No local registry entry references this gateway token (never registered here, or
            // removed): consume the event as a no-op so the inbox can mark it processed.
            logger.LogInformation(
                "Stripe payment-method event {EventId} references unregistered gateway token {PaymentMethodId}; no-op",
                verifiedEvent.EventId,
                payload.PaymentMethodId);
            return true;
        }

        foreach (var method in methods)
        {
            method.UpdateGatewayMetadata(
                ResolveType(payload.Type),
                payload.CardBrand,
                payload.CardLast4,
                payload.CardExpMonth,
                payload.CardExpYear,
                payload.CardFingerprint);
            await paymentMethodRepository.UpdateAsync(method, cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Payment method {PaymentMethodId} metadata refreshed from Stripe event {EventId} (brand {Brand}, expires {ExpMonth}/{ExpYear})",
                method.Id,
                verifiedEvent.EventId,
                string.IsNullOrWhiteSpace(payload.CardBrand) ? "unchanged" : payload.CardBrand,
                payload.CardExpMonth,
                payload.CardExpYear);
        }

        return true;
    }

    private static PaymentMethodType ResolveType(string? stripeType) => stripeType switch
    {
        "card" => PaymentMethodType.Card,
        "us_bank_account" or "bank_account" => PaymentMethodType.BankAccount,
        "paypal" or "apple_pay" or "google_pay" => PaymentMethodType.Wallet,
        _ => PaymentMethodType.Card
    };

    internal static StripePaymentMethodPayload? ParsePaymentMethodPayload(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!root.TryGetProperty("data", out var data)
                || !data.TryGetProperty("object", out var paymentMethodObject)
                || paymentMethodObject.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!paymentMethodObject.TryGetProperty("id", out var idElement)
                || idElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string? type = null;
            string? brand = null;
            string? last4 = null;
            string? fingerprint = null;
            int? expMonth = null;
            int? expYear = null;

            if (paymentMethodObject.TryGetProperty("type", out var typeElement)
                && typeElement.ValueKind == JsonValueKind.String)
            {
                type = typeElement.GetString();
            }

            if (paymentMethodObject.TryGetProperty("card", out var cardElement)
                && cardElement.ValueKind == JsonValueKind.Object)
            {
                brand = ReadString(cardElement, "brand");
                last4 = ReadString(cardElement, "last4");
                fingerprint = ReadString(cardElement, "fingerprint");
                expMonth = ReadInt(cardElement, "exp_month");
                expYear = ReadInt(cardElement, "exp_year");
            }

            return new StripePaymentMethodPayload(
                idElement.GetString()!,
                type,
                brand,
                last4,
                expMonth,
                expYear,
                fingerprint);
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

    private static int? ReadInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32()
            : null;

    internal sealed record StripePaymentMethodPayload(
        string PaymentMethodId,
        string? Type,
        string? CardBrand,
        string? CardLast4,
        int? CardExpMonth,
        int? CardExpYear,
        string? CardFingerprint);
}
