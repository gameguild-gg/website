using GameGuild.Commerce.Subscriptions;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Apple Pay-specific implementation of the billing webhook service.
///     Handles App Store Server Notifications V2 webhook events.
/// </summary>
public class ApplePayBillingWebhookService : BillingWebhookService
{
    private readonly IBillingWebhookRepository _webhookRepository;
    private readonly IApplePayReceiptValidationService _receiptValidationService;
    private readonly IWebhookSecurityEventPublisher? _securityEvents;
    private readonly ILogger<ApplePayBillingWebhookService> _logger;

    public ApplePayBillingWebhookService(
        IBillingWebhookRepository webhookRepository,
        IApplePayReceiptValidationService receiptValidationService,
        ILogger<ApplePayBillingWebhookService> logger,
        ISubscriptionLifecycleService lifecycleService,
        ISubscriptionQueryService queryService,
        ISubscriptionBillingService billingService,
        ISubscriptionExternalIdService externalIdService,
        IWebhookSecurityEventPublisher? securityEvents = null)
        : base(logger, lifecycleService, queryService, billingService, externalIdService)
    {
        _webhookRepository = webhookRepository;
        _receiptValidationService = receiptValidationService;
        _securityEvents = securityEvents;
        _logger = logger;
    }

    /// <summary>
    ///     Process an App Store Server Notification V2 webhook.
    /// </summary>
    /// <param name="signedPayload">The signed JWS payload from Apple</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result</returns>
    public async Task<WebhookProcessingResult> ProcessAppStoreNotificationAsync(
        string signedPayload,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing App Store Server Notification V2");

        // Validate the signed notification using Apple's certificate chain
        var validationResult = await _receiptValidationService.VerifyNotificationAsync(signedPayload, cancellationToken)
            .ConfigureAwait(false);

        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Apple notification validation failed: {Error}", validationResult.ErrorMessage);
            await PublishSecurityEventAsync(
                    WebhookSecurityEventKind.SignatureFailed,
                    validationResult.ErrorMessage ?? "Validation failed",
                    eventId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            return WebhookProcessingResult.Failed("unknown", validationResult.ErrorMessage ?? "Validation failed");
        }

        // Use transaction ID as event ID for idempotency
        var eventId = validationResult.TransactionId ?? Guid.NewGuid().ToString();
        var eventType = validationResult.NotificationType ?? "unknown";

        // Check for duplicate event (idempotency)
        var existingEvent = await _webhookRepository.GetByExternalEventIdAsync(eventId, PaymentProviders.AppleAppStore, cancellationToken)
            .ConfigureAwait(false);
        if (existingEvent != null)
        {
            _logger.LogInformation("Duplicate Apple notification detected: {EventId}. Returning success.", eventId);
            await PublishSecurityEventAsync(
                    WebhookSecurityEventKind.ReplayDetected,
                    "Duplicate Apple notification delivery acknowledged by the idempotent inbox.",
                    eventId,
                    cancellationToken)
                .ConfigureAwait(false);
            return WebhookProcessingResult.AlreadyProcessed(eventId, existingEvent.ProcessedAt);
        }

        // Create webhook event record
        var webhookEvent = new BillingWebhookEvent
        {
            ExternalEventId = eventId,
            Provider = PaymentProviders.AppleAppStore,
            EventType = eventType,
            Payload = signedPayload,
            ProcessingAttempts = 1
        };

        try
        {
            // Store the event first (before processing) to handle concurrent retries
            webhookEvent = await _webhookRepository.CreateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);

            // Route to appropriate handler based on notification type
            await RouteAppStoreNotificationAsync(validationResult, cancellationToken).ConfigureAwait(false);

            // Mark as processed
            webhookEvent.MarkAsProcessed();
            await _webhookRepository.UpdateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Successfully processed Apple notification: {EventId} ({NotificationType}/{Subtype})",
                eventId, eventType, validationResult.Subtype);
            return WebhookProcessingResult.Success(eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Apple notification: {EventId}", eventId);

            webhookEvent.MarkAsFailed(ex.Message);
            await _webhookRepository.UpdateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);

            return WebhookProcessingResult.Failed(eventId, ex.Message);
        }
    }

    private async Task PublishSecurityEventAsync(
        WebhookSecurityEventKind kind,
        string detail,
        string? eventId,
        CancellationToken cancellationToken)
    {
        if (_securityEvents is null)
        {
            return;
        }

        await _securityEvents.PublishAsync(
                kind,
                PaymentProviders.AppleAppStore,
                sourceIpAddress: null,
                detail,
                eventId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Routes an App Store notification to the appropriate handler.
    /// </summary>
    private async Task RouteAppStoreNotificationAsync(
        AppleNotificationVerificationResult notification,
        CancellationToken cancellationToken)
    {
        // Create a webhook payload for the handler
        var subscriptionPayload = new ApplePaySubscriptionWebhookPayload
        {
            TenantId = Guid.Empty, // Will be resolved from transaction lookup
            ExternalSubscriptionId = notification.OriginalTransactionId ?? string.Empty,
            Status = notification.NotificationType ?? string.Empty,
            ProductId = notification.ProductId,
            OriginalTransactionId = notification.OriginalTransactionId,
            Environment = notification.Environment
        };

        // Route based on notification type
        // See: https://developer.apple.com/documentation/appstoreservernotifications/notificationtype
        switch (notification.NotificationType?.ToUpperInvariant())
        {
            case "SUBSCRIBED":
                await HandleSubscriptionCreatedAsync(subscriptionPayload).ConfigureAwait(false);
                break;

            case "DID_RENEW":
                // Subscription successfully renewed
                var paymentPayload = new ApplePayPaymentWebhookPayload
                {
                    TenantId = Guid.Empty,
                    PaymentId = notification.TransactionId ?? string.Empty,
                    ExternalSubscriptionId = notification.OriginalTransactionId ?? string.Empty,
                    TransactionId = notification.TransactionId,
                    OriginalTransactionId = notification.OriginalTransactionId,
                    ProductId = notification.ProductId,
                    PaidAt = SystemClock.UtcNow
                };
                await HandlePaymentSucceededAsync(paymentPayload).ConfigureAwait(false);
                break;

            case "DID_FAIL_TO_RENEW":
                var failedPayload = new ApplePayPaymentWebhookPayload
                {
                    TenantId = Guid.Empty,
                    PaymentId = notification.TransactionId ?? string.Empty,
                    ExternalSubscriptionId = notification.OriginalTransactionId ?? string.Empty,
                    FailureReason = notification.Subtype ?? "Billing retry"
                };
                await HandlePaymentFailedAsync(failedPayload).ConfigureAwait(false);
                break;

            case "EXPIRED":
                subscriptionPayload.Status = "expired";
                await HandleSubscriptionCanceledAsync(subscriptionPayload).ConfigureAwait(false);
                break;

            case "DID_CHANGE_RENEWAL_STATUS":
                // User changed auto-renewal status
                if (notification.Subtype == "AUTO_RENEW_DISABLED")
                {
                    subscriptionPayload.Status = "auto_renew_disabled";
                    await HandleSubscriptionUpdatedAsync(subscriptionPayload).ConfigureAwait(false);
                }
                break;

            case "GRACE_PERIOD_EXPIRED":
                subscriptionPayload.Status = "grace_period_expired";
                await HandleSubscriptionCanceledAsync(subscriptionPayload).ConfigureAwait(false);
                break;

            case "REFUND":
            case "REFUND_REVERSED":
            case "REFUND_DECLINED":
                // Handle refund events
                _logger.LogInformation(
                    "Apple refund event received: Type={Type}, TransactionId={TransactionId}",
                    notification.NotificationType, notification.TransactionId);
                break;

            default:
                _logger.LogInformation(
                    "Unhandled Apple notification type: {Type}/{Subtype}",
                    notification.NotificationType, notification.Subtype);
                break;
        }
    }

    /// <summary>
    ///     Legacy method for backward compatibility.
    /// </summary>
    [Obsolete("Use ProcessAppStoreNotificationAsync for App Store Server Notifications V2")]
    public async Task<WebhookProcessingResult> ProcessApplePayWebhookAsync(
        string payload,
        string merchantId,
        string signature,
        CancellationToken cancellationToken = default)
    {
        // For legacy Apple Pay payment webhooks, use signed payload validation
        return await ProcessAppStoreNotificationAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Routes an Apple Pay event to the appropriate handler based on event type.
    /// </summary>
    private async Task RouteApplePayEventAsync(string eventType, string payload, CancellationToken cancellationToken)
    {
        var webhookPayload = ParseApplePayPayloadData(payload);

        switch (eventType.ToLowerInvariant())
        {
            // Payment events
            case "payment_authorized":
            case "payment_completed":
                await HandlePaymentSucceededAsync(webhookPayload.ToPaymentPayload()).ConfigureAwait(false);
                break;

            case "payment_declined":
            case "payment_cancelled":
                await HandlePaymentFailedAsync(webhookPayload.ToPaymentPayload()).ConfigureAwait(false);
                break;

            // Subscription events (if using Apple subscriptions)
            case "subscription_created":
                await HandleSubscriptionCreatedAsync(webhookPayload.ToSubscriptionPayload()).ConfigureAwait(false);
                break;

            case "subscription_renewed":
            case "subscription_updated":
                await HandleSubscriptionUpdatedAsync(webhookPayload.ToSubscriptionPayload()).ConfigureAwait(false);
                break;

            case "subscription_cancelled":
            case "subscription_expired":
                await HandleSubscriptionCanceledAsync(webhookPayload.ToSubscriptionPayload()).ConfigureAwait(false);
                break;

            default:
                _logger.LogDebug("Unhandled Apple Pay event type: {EventType}", eventType);
                break;
        }
    }

    /// <summary>
    ///     Parses Apple Pay payload to extract event info.
    /// </summary>
    private static (string eventId, string eventType, string? transactionId) ParseApplePayPayload(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var eventId = root.TryGetProperty("eventId", out var idProp) ? idProp.GetString() : null;
            var eventType = root.TryGetProperty("eventType", out var typeProp) ? typeProp.GetString() : null;
            var transactionId = root.TryGetProperty("transactionId", out var txProp) ? txProp.GetString() : null;

            // Also check nested payment data
            if (root.TryGetProperty("payment", out var payment))
            {
                transactionId ??= payment.TryGetProperty("transactionIdentifier", out var txIdProp) ? txIdProp.GetString() : null;
            }

            return (eventId ?? string.Empty, eventType ?? "unknown", transactionId);
        }
        catch
        {
            return (string.Empty, "unknown", null);
        }
    }

    /// <summary>
    ///     Parses Apple Pay payload into structured data.
    /// </summary>
    private static ApplePayWebhookPayload ParseApplePayPayloadData(string payload)
    {
        var result = new ApplePayWebhookPayload();

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            result.EventType = root.TryGetProperty("eventType", out var typeProp) ? typeProp.GetString() ?? string.Empty : string.Empty;
            result.TransactionId = root.TryGetProperty("transactionId", out var txProp) ? txProp.GetString() : null;

            // Parse payment data
            if (root.TryGetProperty("payment", out var payment))
            {
                result.TransactionId ??= payment.TryGetProperty("transactionIdentifier", out var txIdProp) ? txIdProp.GetString() : null;
                
                if (payment.TryGetProperty("token", out var token))
                {
                    if (token.TryGetProperty("paymentData", out var paymentData))
                    {
                        // Parse payment data if present
                    }
                }
            }

            // Parse amount
            if (root.TryGetProperty("amount", out var amount))
            {
                if (decimal.TryParse(amount.GetString(), out var amountValue))
                {
                    result.Amount = amountValue;
                }
            }

            result.Currency = root.TryGetProperty("currency", out var currProp) ? currProp.GetString() : "USD";
        }
        catch
        {
            // Return empty payload on parse failure
        }

        return result;
    }
}

/// <summary>
///     Internal class for parsing Apple Pay webhook payloads
/// </summary>
internal class ApplePayWebhookPayload
{
    public string EventType { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Status { get; set; }
    public Guid TenantId { get; set; }
    public Guid PlanId { get; set; }

    public ApplePaySubscriptionWebhookPayload ToSubscriptionPayload() => new()
    {
        TenantId = TenantId,
        PlanId = PlanId,
        ExternalSubscriptionId = SubscriptionId ?? TransactionId ?? string.Empty,
        Status = Status ?? string.Empty,
        Amount = Amount,
        StartDate = SystemClock.UtcNow,
        EndDate = null,
        OriginalTransactionId = TransactionId
    };

    public ApplePayPaymentWebhookPayload ToPaymentPayload() => new()
    {
        TenantId = TenantId,
        PaymentId = TransactionId ?? string.Empty,
        ExternalSubscriptionId = SubscriptionId ?? string.Empty,
        Amount = Amount,
        Currency = Currency ?? "USD",
        PaidAt = SystemClock.UtcNow,
        FailureReason = null,
        TransactionId = TransactionId,
        OriginalTransactionId = TransactionId
    };
}
