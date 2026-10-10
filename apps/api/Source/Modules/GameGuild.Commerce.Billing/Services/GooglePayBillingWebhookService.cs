using System.Text.Json;
using GameGuild.Commerce.Subscriptions;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Google Pay-specific implementation of the billing webhook service.
///     Verifies the callback bearer JWT, stores the event in the idempotent webhook
///     inbox, and routes subscription/payment notifications to the shared handlers.
/// </summary>
public class GooglePayBillingWebhookService : BillingWebhookService
{
    private readonly IBillingWebhookRepository _webhookRepository;
    private readonly IGooglePayWebhookVerificationService _verificationService;
    private readonly IWebhookSecurityEventPublisher? _securityEvents;
    private readonly ILogger<GooglePayBillingWebhookService> _logger;

    public GooglePayBillingWebhookService(
        IBillingWebhookRepository webhookRepository,
        IGooglePayWebhookVerificationService verificationService,
        ILogger<GooglePayBillingWebhookService> logger,
        ISubscriptionLifecycleService lifecycleService,
        ISubscriptionQueryService queryService,
        ISubscriptionBillingService billingService,
        ISubscriptionExternalIdService externalIdService,
        IWebhookSecurityEventPublisher? securityEvents = null)
        : base(logger, lifecycleService, queryService, billingService, externalIdService)
    {
        _webhookRepository = webhookRepository;
        _verificationService = verificationService;
        _securityEvents = securityEvents;
        _logger = logger;
    }

    /// <summary>
    ///     Processes a verified Google Pay webhook callback with idempotency checking.
    /// </summary>
    public async Task<WebhookProcessingResult> ProcessGooglePayWebhookAsync(
        string payload,
        string authHeader,
        string projectId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing Google Pay webhook for project {ProjectId}", projectId);

        var verification = _verificationService.Verify(payload, authHeader, projectId);
        if (!verification.IsValid)
        {
            _logger.LogWarning("Google Pay webhook verification failed: {Error}", verification.ErrorMessage);

            if (_securityEvents is not null)
            {
                await _securityEvents.PublishAsync(
                        WebhookSecurityEventKind.SignatureFailed,
                        PaymentProviders.GooglePay,
                        sourceIpAddress: null,
                        verification.ErrorMessage ?? "Verification failed.")
                    .ConfigureAwait(false);
            }

            throw new InvalidWebhookSignatureException(
                verification.ErrorMessage ?? "Google Pay webhook verification failed.");
        }

        var eventId = verification.EventId!;

        // Idempotency: duplicate deliveries are acknowledged without reprocessing.
        var existingEvent = await _webhookRepository
            .GetByExternalEventIdAsync(eventId, PaymentProviders.GooglePay, cancellationToken)
            .ConfigureAwait(false);
        if (existingEvent is not null)
        {
            _logger.LogInformation("Duplicate Google Pay webhook detected: {EventId}. Returning success.", eventId);

            if (_securityEvents is not null)
            {
                await _securityEvents.PublishAsync(
                        WebhookSecurityEventKind.ReplayDetected,
                        PaymentProviders.GooglePay,
                        sourceIpAddress: null,
                        "Duplicate Google Pay webhook delivery acknowledged by the idempotent inbox.",
                        eventId)
                    .ConfigureAwait(false);
            }

            return WebhookProcessingResult.AlreadyProcessed(eventId, existingEvent.ProcessedAt);
        }

        var eventType = verification.EventType ?? "unknown";
        var webhookEvent = new BillingWebhookEvent
        {
            ExternalEventId = eventId,
            Provider = PaymentProviders.GooglePay,
            EventType = eventType,
            Payload = payload,
            ProcessingAttempts = 1
        };

        try
        {
            webhookEvent = await _webhookRepository.CreateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);

            await RouteGooglePayEventAsync(eventType, payload, cancellationToken).ConfigureAwait(false);

            webhookEvent.MarkAsProcessed();
            await _webhookRepository.UpdateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully processed Google Pay webhook: {EventId} ({EventType})", eventId, eventType);
            return WebhookProcessingResult.Success(eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Google Pay webhook: {EventId}", eventId);

            webhookEvent.MarkAsFailed(ex.Message);
            await _webhookRepository.UpdateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);

            return WebhookProcessingResult.Failed(eventId, ex.Message);
        }
    }

    private async Task RouteGooglePayEventAsync(string eventType, string payload, CancellationToken cancellationToken)
    {
        var webhookPayload = ParseGooglePayPayload(payload);

        switch (eventType.ToUpperInvariant())
        {
            case "SUBSCRIPTION_PURCHASED":
            case "SUBSCRIPTION_CREATED":
                await HandleSubscriptionCreatedAsync(webhookPayload.ToSubscriptionPayload()).ConfigureAwait(false);
                break;

            case "SUBSCRIPTION_RENEWED":
            case "SUBSCRIPTION_UPDATED":
            case "SUBSCRIPTION_IN_GRACE":
                await HandleSubscriptionUpdatedAsync(webhookPayload.ToSubscriptionPayload()).ConfigureAwait(false);
                break;

            case "SUBSCRIPTION_CANCELED":
            case "SUBSCRIPTION_EXPIRED":
                await HandleSubscriptionCanceledAsync(webhookPayload.ToSubscriptionPayload()).ConfigureAwait(false);
                break;

            case "ONE_TIME_PAYMENT_SUCCEEDED":
            case "PAYMENT_SUCCEEDED":
                await HandlePaymentSucceededAsync(webhookPayload.ToPaymentPayload()).ConfigureAwait(false);
                break;

            case "ONE_TIME_PAYMENT_FAILED":
            case "PAYMENT_FAILED":
                await HandlePaymentFailedAsync(webhookPayload.ToPaymentPayload()).ConfigureAwait(false);
                break;

            default:
                _logger.LogDebug("Unhandled Google Pay event type: {EventType}", eventType);
                break;
        }
    }

    private static GooglePayWebhookPayloadData ParseGooglePayPayload(string payload)
    {
        var result = new GooglePayWebhookPayloadData { EventType = "unknown" };

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            result.EventType = ReadString(root, "event_type") ?? ReadString(root, "eventType") ?? "unknown";
            result.EventId = ReadString(root, "event_id") ?? ReadString(root, "messageId");

            if (root.TryGetProperty("subscription", out var subscription))
            {
                result.ExternalSubscriptionId = ReadString(subscription, "subscription_id")
                                                 ?? ReadString(subscription, "external_id");
                result.Status = ReadString(subscription, "status");
            }

            if (root.TryGetProperty("payment", out var payment))
            {
                result.PaymentId = ReadString(payment, "payment_id") ?? ReadString(payment, "id");
                result.ExternalSubscriptionId ??= ReadString(payment, "subscription_id");
                result.FailureReason = ReadString(payment, "failure_reason");

                if (payment.TryGetProperty("amount", out var amount) && amount.ValueKind == JsonValueKind.Number)
                {
                    result.Amount = amount.GetDecimal();
                }

                result.Currency = ReadString(payment, "currency");
            }

            if (root.TryGetProperty("metadata", out var metadata))
            {
                if (Guid.TryParse(ReadString(metadata, "tenant_id"), out var tenantId))
                {
                    result.TenantId = tenantId;
                }

                if (Guid.TryParse(ReadString(metadata, "plan_id"), out var planId))
                {
                    result.PlanId = planId;
                }
            }
        }
        catch (JsonException)
        {
            // Verification already validated the JSON; partial field extraction is best effort.
        }

        return result;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}

/// <summary>Internal class for parsing Google Pay webhook payloads.</summary>
internal sealed class GooglePayWebhookPayloadData
{
    public string EventType { get; set; } = "unknown";

    public string? EventId { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? PlanId { get; set; }

    public string? ExternalSubscriptionId { get; set; }

    public string? PaymentId { get; set; }

    public decimal? Amount { get; set; }

    public string? Currency { get; set; }

    public string? Status { get; set; }

    public string? FailureReason { get; set; }

    public GooglePaySubscriptionWebhookPayload ToSubscriptionPayload() => new()
    {
        TenantId = TenantId ?? Guid.Empty,
        PlanId = PlanId ?? Guid.Empty,
        ExternalSubscriptionId = ExternalSubscriptionId ?? EventId ?? string.Empty,
        Status = Status ?? string.Empty,
        Amount = Amount ?? 0,
        StartDate = SystemClock.UtcNow,
        EndDate = null
    };

    public GooglePayPaymentWebhookPayload ToPaymentPayload() => new()
    {
        TenantId = TenantId ?? Guid.Empty,
        PaymentId = PaymentId ?? EventId ?? string.Empty,
        ExternalSubscriptionId = ExternalSubscriptionId ?? string.Empty,
        Amount = Amount ?? 0,
        Currency = Currency ?? CurrencyCodes.Default,
        Status = Status ?? string.Empty,
        PaidAt = SystemClock.UtcNow,
        FailureReason = FailureReason
    };
}
