using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Stripe;

namespace GameGuild.Commerce.Billing;

public sealed class StripeWebhookVerifier(
    IOptions<BillingConfiguration> options,
    IWebhookSigningSecretProvider? signingSecretProvider = null) : IStripeWebhookVerifier
{
    private readonly StripeSettings _settings = options.Value.Stripe;

    public VerifiedStripeWebhookEvent Verify(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(_settings.WebhookSecret) ||
            string.IsNullOrWhiteSpace(_settings.WebhookEndpointId) ||
            string.IsNullOrWhiteSpace(_settings.AccountId))
        {
            throw new InvalidOperationException("Stripe webhook verification is not configured.");
        }

        // Every signing secret currently valid for the endpoint verifies: the configuration
        // secret plus, during a rotation window, the rotation's active and previous secrets
        // (issue #408 dual-secret verification). Without a provider the configuration secret
        // is the single valid secret.
        var validSecrets = signingSecretProvider?.GetValidSecrets(PaymentProviders.Stripe) is { Count: > 0 } secrets
            ? secrets
            : [_settings.WebhookSecret];

        Event stripeEvent;
        try
        {
            stripeEvent = ConstructEventWithAnySecret(payload, signature, validSecrets);
        }
        catch (InvalidWebhookSignatureException)
        {
            throw;
        }
        catch (StripeException exception)
        {
            throw new InvalidWebhookSignatureException("Stripe signature or timestamp is invalid.", exception);
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            throw new InvalidWebhookPayloadException("Stripe webhook payload is malformed.", exception);
        }
        catch (Newtonsoft.Json.JsonException exception)
        {
            throw new InvalidWebhookPayloadException("Stripe webhook payload is malformed.", exception);
        }

        if (string.IsNullOrWhiteSpace(stripeEvent.Id) || string.IsNullOrWhiteSpace(stripeEvent.Type))
        {
            throw new InvalidWebhookPayloadException("Stripe webhook must contain an event ID and type.");
        }

        if (stripeEvent.Livemode != _settings.LiveMode)
        {
            throw new InvalidWebhookPayloadException("Stripe webhook livemode does not match this endpoint.");
        }

        var schemaVersion = stripeEvent.ApiVersion ?? string.Empty;
        if (!string.Equals(schemaVersion, _settings.ApiVersion, StringComparison.Ordinal))
        {
            throw new InvalidWebhookPayloadException("Stripe webhook API version is not supported.");
        }

        var expectedAccount = string.IsNullOrWhiteSpace(_settings.ConnectedAccountId)
            ? null
            : _settings.ConnectedAccountId;
        if (!string.Equals(stripeEvent.Account, expectedAccount, StringComparison.Ordinal))
        {
            throw new InvalidWebhookPayloadException("Stripe webhook account does not match this endpoint.");
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!root.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("object", out var providerObject) ||
                providerObject.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidWebhookPayloadException("Stripe webhook data object is required.");
            }

            var objectId = GetRequiredString(providerObject, "id");
            var objectType = GetRequiredString(providerObject, "object");
            var monetaryLeg = ResolveMonetaryLeg(stripeEvent.Type);
            var providerIdentity = ResolveProviderObjectIdentity(
                providerObject,
                objectId,
                objectType,
                monetaryLeg);
            var tenantId = ReadTenantId(providerObject);
            var amount = ReadMoney(providerObject, "amount_paid")
                         ?? ReadMoney(providerObject, "amount_due")
                         ?? ReadMoney(providerObject, "amount");
            var retainedPayload = MinimizePayload(root, providerObject);

            return new VerifiedStripeWebhookEvent
            {
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                OccurredAt = stripeEvent.Created,
                IsLiveMode = stripeEvent.Livemode,
                ProviderEnvironment = stripeEvent.Livemode ? "live" : "test",
                ProviderAccountId = ResolveProviderObjectAccountId(),
                WebhookEndpointId = _settings.WebhookEndpointId,
                EventSchemaVersion = schemaVersion,
                ProviderObjectId = providerIdentity.ObjectId,
                ProviderObjectType = providerIdentity.ObjectType,
                ProviderMonetaryLeg = monetaryLeg,
                TenantId = tenantId,
                ExternalSubscriptionId = ReadString(providerObject, "subscription") ??
                                         (objectType == "subscription" ? objectId : null),
                Amount = amount,
                Currency = ReadString(providerObject, "currency")?.ToUpperInvariant(),
                CumulativeRefundedAmount = ReadMoney(providerObject, "amount_refunded"),
                CumulativeDisputedAmount = ReadMoney(providerObject, "amount_disputed"),
                VerifiedPayload = payload,
                RetainedPayload = retainedPayload,
                PayloadSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()
            };
        }
        catch (InvalidWebhookPayloadException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new InvalidWebhookPayloadException("Stripe webhook payload is malformed.", exception);
        }
    }

    private string ResolveProviderObjectAccountId() =>
        string.IsNullOrWhiteSpace(_settings.ConnectedAccountId)
            ? _settings.AccountId
            : _settings.ConnectedAccountId;

    /// <summary>
    ///     Verifies the signature against each valid signing secret (issue #408): the first
    ///     secret that verifies wins. Malformed payloads fail identically for every secret
    ///     and surface as payload errors; a signature that matches no secret surfaces as the
    ///     signature failure of the last attempted secret.
    /// </summary>
    private Event ConstructEventWithAnySecret(string payload, string signature, IReadOnlyList<string> validSecrets)
    {
        Exception? lastSignatureFailure = null;
        foreach (var secret in validSecrets)
        {
            if (string.IsNullOrWhiteSpace(secret))
            {
                continue;
            }

            try
            {
                return EventUtility.ConstructEvent(
                    payload,
                    signature,
                    secret,
                    _settings.WebhookToleranceSeconds,
                    throwOnApiVersionMismatch: false);
            }
            catch (StripeException exception)
            {
                lastSignatureFailure = exception;
            }
        }

        throw new InvalidWebhookSignatureException(
            "Stripe signature or timestamp is invalid.",
            lastSignatureFailure ?? new InvalidOperationException("Signature did not match any valid signing secret."));
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        var value = ReadString(element, propertyName);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidWebhookPayloadException($"Stripe webhook data object requires {propertyName}.")
            : value;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static decimal? ReadMoney(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return property.GetDecimal() / 100m;
    }

    private static Guid? ReadTenantId(JsonElement providerObject)
    {
        if (!providerObject.TryGetProperty("metadata", out var metadata) ||
            metadata.ValueKind != JsonValueKind.Object ||
            !Guid.TryParse(ReadString(metadata, "tenant_id"), out var tenantId))
        {
            return null;
        }

        return tenantId;
    }

    private static string ResolveMonetaryLeg(string eventType) => eventType switch
    {
        "invoice.payment_succeeded" or "payment_intent.succeeded" or "charge.succeeded" => "capture",
        "charge.refunded" or "refund.created" or "refund.updated" => "refund",
        "charge.dispute.created" or "charge.dispute.updated" or "charge.dispute.closed" => "dispute",
        "invoice.payment_failed" or "payment_intent.payment_failed" => "failure",
        _ when eventType.StartsWith("customer.subscription.", StringComparison.Ordinal) => "subscription",
        _ => "nonmonetary"
    };

    private static StripeProviderObjectIdentity ResolveProviderObjectIdentity(
        JsonElement providerObject,
        string objectId,
        string objectType,
        string monetaryLeg)
    {
        if (monetaryLeg is "nonmonetary" or "subscription")
            return new StripeProviderObjectIdentity(objectId, objectType);

        var paymentIntentId = string.Equals(objectType, "payment_intent", StringComparison.Ordinal)
            ? objectId
            : ReadString(providerObject, "payment_intent");
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            throw new InvalidWebhookPayloadException(
                "Stripe monetary event must reference its canonical payment_intent.");
        }

        return new StripeProviderObjectIdentity(paymentIntentId, "payment_intent");
    }

    private static string MinimizePayload(JsonElement root, JsonElement providerObject)
    {
        var retainedObject = new Dictionary<string, object?>();
        foreach (var propertyName in new[]
                 {
                     "id", "object", "customer", "status", "subscription", "payment_intent", "charge",
                     "amount", "amount_paid", "amount_due", "amount_refunded", "amount_disputed", "currency",
                     "invoice", "current_period_start", "current_period_end", "billing_cycle_anchor"
                 })
        {
            if (providerObject.TryGetProperty(propertyName, out var value))
            {
                retainedObject[propertyName] = JsonSerializer.Deserialize<object?>(value.GetRawText());
            }
        }

        if (providerObject.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
        {
            var retainedMetadata = new Dictionary<string, string?>
            {
                ["tenant_id"] = ReadString(metadata, "tenant_id"),
                ["plan_id"] = ReadString(metadata, "plan_id")
            };
            retainedObject["metadata"] = retainedMetadata;
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = ReadString(root, "id"),
            ["type"] = ReadString(root, "type"),
            ["livemode"] = root.GetProperty("livemode").GetBoolean(),
            ["account"] = ReadString(root, "account"),
            ["api_version"] = ReadString(root, "api_version"),
            ["data"] = new Dictionary<string, object?> { ["object"] = retainedObject }
        });
    }
}

internal sealed record StripeProviderObjectIdentity(string ObjectId, string ObjectType);
