using GameGuild.Commerce.Subscriptions;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     PayPal-specific implementation of the billing webhook service.
///     Handles webhook events from PayPal payment gateway (IPN and v2 webhooks).
/// </summary>
public class PayPalBillingWebhookService : BillingWebhookService
{
    private readonly IBillingWebhookRepository _webhookRepository;
    private readonly IPayPalSignatureVerificationService _signatureVerificationService;
    private readonly IWebhookSecurityEventPublisher? _securityEvents;
    private readonly IBillingIntegrationEventPublisher? _billingEvents;
    private readonly ILogger<PayPalBillingWebhookService> _logger;

    /// <inheritdoc />
    protected override string ProviderName => PaymentProviders.PayPal;

    public PayPalBillingWebhookService(
        IBillingWebhookRepository webhookRepository,
        IPayPalSignatureVerificationService signatureVerificationService,
        ILogger<PayPalBillingWebhookService> logger,
        ISubscriptionLifecycleService lifecycleService,
        ISubscriptionQueryService queryService,
        ISubscriptionBillingService billingService,
        ISubscriptionExternalIdService externalIdService,
        IWebhookSecurityEventPublisher? securityEvents = null,
        IBillingIntegrationEventPublisher? billingEvents = null)
        : base(logger, lifecycleService, queryService, billingService, externalIdService, billingEvents)
    {
        _webhookRepository = webhookRepository;
        _signatureVerificationService = signatureVerificationService;
        _securityEvents = securityEvents;
        _billingEvents = billingEvents;
        _logger = logger;
    }

    /// <summary>
    ///     Process a PayPal webhook event with idempotency checking.
    /// </summary>
    /// <param name="webhookId">PayPal webhook ID from transmission headers</param>
    /// <param name="payload">Raw JSON payload</param>
    /// <param name="transmissionId">PayPal transmission ID for verification</param>
    /// <param name="transmissionTime">PayPal transmission time</param>
    /// <param name="transmissionSig">PayPal signature for verification</param>
    /// <param name="certUrl">PayPal certificate URL for verification</param>
    /// <param name="authAlgo">PayPal auth algorithm for verification</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result</returns>
    public async Task<WebhookProcessingResult> ProcessPayPalWebhookAsync(
        string webhookId,
        string payload,
        string transmissionId,
        string transmissionTime,
        string transmissionSig,
        string? certUrl = null,
        string? authAlgo = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing PayPal webhook: TransmissionId={TransmissionId}", transmissionId);

        // Use transmission ID as idempotency key
        var eventId = transmissionId;

        // Check for duplicate event (idempotency)
        var existingEvent = await _webhookRepository.GetByExternalEventIdAsync(eventId, PaymentProviders.PayPal, cancellationToken).ConfigureAwait(false);
        if (existingEvent != null)
        {
            _logger.LogInformation("Duplicate PayPal webhook detected: {TransmissionId}. Returning success.", transmissionId);
            await PublishSecurityEventAsync(
                    WebhookSecurityEventKind.ReplayDetected,
                    "Duplicate PayPal webhook delivery acknowledged by the idempotent inbox.",
                    eventId,
                    cancellationToken)
                .ConfigureAwait(false);
            return WebhookProcessingResult.AlreadyProcessed(eventId, existingEvent.ProcessedAt);
        }

        // Parse payload to determine event type
        var (eventType, resourceId) = ParsePayPalPayload(payload);

        // Create webhook event record
        var webhookEvent = new BillingWebhookEvent
        {
            ExternalEventId = eventId,
            Provider = PaymentProviders.PayPal,
            EventType = eventType,
            Payload = payload,
            ProcessingAttempts = 1
        };

        try
        {
            // Store the event first (before processing) to handle concurrent retries
            webhookEvent = await _webhookRepository.CreateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);

            // Verify webhook signature with PayPal API
            var verificationResult = await _signatureVerificationService.VerifySignatureAsync(
                webhookId,
                transmissionId,
                transmissionTime,
                transmissionSig,
                certUrl,
                authAlgo,
                payload,
                cancellationToken);

            if (!verificationResult.IsValid)
            {
                _logger.LogWarning("PayPal webhook signature verification failed: {Error}", verificationResult.ErrorMessage);
                await PublishSecurityEventAsync(
                        WebhookSecurityEventKind.SignatureFailed,
                        verificationResult.ErrorMessage ?? "Invalid webhook signature",
                        eventId,
                        cancellationToken)
                    .ConfigureAwait(false);
                webhookEvent.MarkAsFailed($"Invalid webhook signature: {verificationResult.ErrorMessage}");
                await _webhookRepository.UpdateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);
                await PublishBillingEventAsync(webhookEvent, processed: false, cancellationToken).ConfigureAwait(false);
                return WebhookProcessingResult.Failed(eventId, verificationResult.ErrorMessage ?? "Invalid webhook signature");
            }

            // Route to appropriate handler based on event type
            await RoutePayPalEventAsync(eventType, payload, cancellationToken).ConfigureAwait(false);

            // Mark as processed
            webhookEvent.MarkAsProcessed();
            await _webhookRepository.UpdateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);
            await PublishBillingEventAsync(webhookEvent, processed: true, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully processed PayPal webhook: {TransmissionId} ({EventType})", transmissionId, eventType);
            return WebhookProcessingResult.Success(eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process PayPal webhook: {TransmissionId}", transmissionId);

            webhookEvent.MarkAsFailed(ex.Message);
            await _webhookRepository.UpdateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);
            await PublishBillingEventAsync(webhookEvent, processed: false, cancellationToken).ConfigureAwait(false);

            return WebhookProcessingResult.Failed(eventId, ex.Message);
        }
    }

    /// <summary>
    ///     Publishes the named inbox-transition event for the processed/failed outcome.
    ///     Best-effort: a publication failure never changes the processing result.
    /// </summary>
    private async Task PublishBillingEventAsync(
        BillingWebhookEvent webhookEvent,
        bool processed,
        CancellationToken cancellationToken)
    {
        if (_billingEvents is null)
        {
            return;
        }

        if (processed)
        {
            await _billingEvents.PublishWebhookProcessedAsync(webhookEvent, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _billingEvents.PublishWebhookFailedAsync(webhookEvent, cancellationToken).ConfigureAwait(false);
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
                PaymentProviders.PayPal,
                sourceIpAddress: null,
                detail,
                eventId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Routes a PayPal event to the appropriate handler based on event type.
    /// </summary>
    private async Task RoutePayPalEventAsync(string eventType, string payload, CancellationToken cancellationToken)
    {
        var webhookPayload = ParsePayPalPayloadData(payload);

        switch (eventType)
        {
            // Subscription events
            case "BILLING.SUBSCRIPTION.CREATED":
                await HandleSubscriptionCreatedAsync(webhookPayload.ToSubscriptionPayload()).ConfigureAwait(false);
                break;

            case "BILLING.SUBSCRIPTION.ACTIVATED":
            case "BILLING.SUBSCRIPTION.UPDATED":
                await HandleSubscriptionUpdatedAsync(webhookPayload.ToSubscriptionPayload()).ConfigureAwait(false);
                break;

            case "BILLING.SUBSCRIPTION.CANCELLED":
            case "BILLING.SUBSCRIPTION.SUSPENDED":
            case "BILLING.SUBSCRIPTION.EXPIRED":
                await HandleSubscriptionCanceledAsync(webhookPayload.ToSubscriptionPayload()).ConfigureAwait(false);
                break;

            // Payment events
            case "PAYMENT.SALE.COMPLETED":
            case "PAYMENT.CAPTURE.COMPLETED":
                await HandlePaymentSucceededAsync(webhookPayload.ToPaymentPayload()).ConfigureAwait(false);
                break;

            case "PAYMENT.SALE.DENIED":
            case "PAYMENT.SALE.REFUNDED":
            case "PAYMENT.CAPTURE.DENIED":
                await HandlePaymentFailedAsync(webhookPayload.ToPaymentPayload()).ConfigureAwait(false);
                break;

            default:
                _logger.LogDebug("Unhandled PayPal event type: {EventType}", eventType);
                break;
        }
    }

    /// <summary>
    ///     Parses PayPal payload to extract event type and resource ID.
    /// </summary>
    private static (string eventType, string resourceId) ParsePayPalPayload(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var eventType = root.TryGetProperty("event_type", out var typeProp) ? typeProp.GetString() : null;
            
            var resourceId = string.Empty;
            if (root.TryGetProperty("resource", out var resource))
            {
                resourceId = resource.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty;
            }

            return (eventType ?? "unknown", resourceId);
        }
        catch
        {
            return ("unknown", string.Empty);
        }
    }

    /// <summary>
    ///     Parses PayPal payload into structured data.
    /// </summary>
    private static PayPalWebhookPayload ParsePayPalPayloadData(string payload)
    {
        var result = new PayPalWebhookPayload();

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            result.EventType = root.TryGetProperty("event_type", out var typeProp) ? typeProp.GetString() ?? string.Empty : string.Empty;

            if (root.TryGetProperty("resource", out var resource))
            {
                result.ResourceId = resource.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                result.Status = resource.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;

                // Parse billing agreement / subscription data
                if (resource.TryGetProperty("billing_agreement_id", out var agreementProp))
                {
                    result.SubscriptionId = agreementProp.GetString();
                }
                
                // Parse amount
                if (resource.TryGetProperty("amount", out var amount))
                {
                    if (amount.TryGetProperty("total", out var totalProp) && 
                        decimal.TryParse(totalProp.GetString(), out var total))
                    {
                        result.Amount = total;
                    }
                    result.Currency = amount.TryGetProperty("currency", out var currProp) ? currProp.GetString() : "USD";
                }
            }
        }
        catch
        {
            // Return empty payload on parse failure
        }

        return result;
    }
}

/// <summary>
///     Internal class for parsing PayPal webhook payloads
/// </summary>
internal class PayPalWebhookPayload
{
    public string EventType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? CustomerId { get; set; }
    public string? PaymentId { get; set; }
    public string? InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? Status { get; set; }
    public Guid TenantId { get; set; }
    public Guid PlanId { get; set; }

    public PayPalSubscriptionWebhookPayload ToSubscriptionPayload() => new()
    {
        TenantId = TenantId,
        PlanId = PlanId,
        ExternalSubscriptionId = SubscriptionId ?? ResourceId ?? string.Empty,
        Status = Status ?? string.Empty,
        Amount = Amount,
        StartDate = SystemClock.UtcNow,
        EndDate = null,
        PayerId = CustomerId,
        BillingAgreementId = SubscriptionId
    };

    public PayPalPaymentWebhookPayload ToPaymentPayload() => new()
    {
        TenantId = TenantId,
        PaymentId = PaymentId ?? ResourceId ?? string.Empty,
        ExternalSubscriptionId = SubscriptionId ?? string.Empty,
        Amount = Amount,
        Currency = Currency ?? "USD",
        PaidAt = SystemClock.UtcNow,
        FailureReason = null,
        TransactionId = PaymentId,
        PayerId = CustomerId
    };
}
