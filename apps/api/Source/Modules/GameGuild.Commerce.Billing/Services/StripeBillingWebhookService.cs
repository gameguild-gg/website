using GameGuild.Commerce.Subscriptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Stripe-specific implementation of the billing webhook service.
///     Handles webhook events from Stripe payment gateway.
/// </summary>
public class StripeBillingWebhookService : BillingWebhookService
{
    private readonly IBillingWebhookRepository _webhookRepository;
    private readonly ILogger<StripeBillingWebhookService> _logger;
    private readonly IStripeWebhookVerifier _webhookVerifier;
    private readonly IStripeProviderObjectBindingValidator _providerObjectBindingValidator;
    private readonly IReadOnlyList<IStripeVerifiedEventConsumer> _verifiedEventConsumers;
    private readonly ISubscriptionQueryService _subscriptionQueryService;
    private readonly WebhookSettings _webhookSettings;
    private readonly IWebhookSecurityEventPublisher? _securityEvents;
    private readonly IBillingIntegrationEventPublisher? _billingEvents;
    private readonly IInvoiceGenerationService? _invoiceGeneration;

    /// <inheritdoc />
    protected override string ProviderName => PaymentProviders.Stripe;

    public StripeBillingWebhookService(
        IBillingWebhookRepository webhookRepository,
        IStripeWebhookVerifier webhookVerifier,
        IStripeProviderObjectBindingValidator providerObjectBindingValidator,
        ILogger<StripeBillingWebhookService> logger,
        ISubscriptionLifecycleService lifecycleService,
        ISubscriptionQueryService queryService,
        ISubscriptionBillingService billingService,
        ISubscriptionExternalIdService externalIdService,
        IOptions<BillingConfiguration>? configuration = null,
        IEnumerable<IStripeVerifiedEventConsumer>? verifiedEventConsumers = null,
        IWebhookSecurityEventPublisher? securityEvents = null,
        IBillingIntegrationEventPublisher? billingEvents = null,
        IInvoiceGenerationService? invoiceGeneration = null)
        : base(logger, lifecycleService, queryService, billingService, externalIdService, billingEvents)
    {
        _webhookRepository = webhookRepository;
        _webhookVerifier = webhookVerifier;
        _providerObjectBindingValidator = providerObjectBindingValidator;
        _verifiedEventConsumers = verifiedEventConsumers?.ToArray() ?? [];
        _subscriptionQueryService = queryService;
        _logger = logger;
        _webhookSettings = configuration?.Value.Webhook ?? new WebhookSettings();
        _securityEvents = securityEvents;
        _billingEvents = billingEvents;
        _invoiceGeneration = invoiceGeneration;
    }

    /// <summary>
    ///     Process a raw Stripe webhook event with idempotency checking.
    /// </summary>
    /// <param name="payload">Raw JSON payload</param>
    /// <param name="signature">Stripe signature header</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result</returns>
    public async Task<WebhookProcessingResult> ProcessStripeWebhookAsync(
        string payload,
        string signature,
        CancellationToken cancellationToken = default)
    {
        VerifiedStripeWebhookEvent verifiedEvent;
        try
        {
            verifiedEvent = _webhookVerifier.Verify(payload, signature);
        }
        catch (InvalidWebhookSignatureException exception)
        {
            _logger.LogWarning("Stripe webhook signature verification failed: {Message}", exception.Message);
            await PublishSecurityEventAsync(
                    WebhookSecurityEventKind.SignatureFailed,
                    exception.Message,
                    eventId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            throw;
        }

        _logger.LogInformation(
            "Processing verified Stripe webhook: {EventType} with ID {EventId}",
            verifiedEvent.EventType,
            verifiedEvent.EventId);

        var existingEvent = await _webhookRepository.GetByProviderScopeAsync(
                PaymentProviders.Stripe,
                verifiedEvent.ProviderEnvironment,
                verifiedEvent.ProviderAccountId,
                verifiedEvent.WebhookEndpointId,
                verifiedEvent.EventId,
                cancellationToken)
            .ConfigureAwait(false);
        if (existingEvent?.IsProcessed == true)
        {
            _logger.LogInformation("Duplicate Stripe webhook detected: {EventId}. Returning success.", verifiedEvent.EventId);
            await PublishSecurityEventAsync(
                    WebhookSecurityEventKind.ReplayDetected,
                    "Duplicate Stripe webhook delivery acknowledged by the idempotent inbox.",
                    verifiedEvent.EventId,
                    cancellationToken)
                .ConfigureAwait(false);
            return WebhookProcessingResult.AlreadyProcessed(verifiedEvent.EventId, existingEvent.ProcessedAt);
        }

        var binding = await ValidateSubscriptionBindingAsync(verifiedEvent, cancellationToken).ConfigureAwait(false);
        var paymentBinding = verifiedEvent.EventType.StartsWith("invoice.", StringComparison.Ordinal)
            ? null
            : await _providerObjectBindingValidator
                .ValidateAsync(verifiedEvent, cancellationToken)
                .ConfigureAwait(false);

        var webhookEvent = existingEvent ?? new BillingWebhookEvent
        {
            ExternalEventId = verifiedEvent.EventId,
            Provider = PaymentProviders.Stripe,
            ProviderEnvironment = verifiedEvent.ProviderEnvironment,
            ProviderAccountId = verifiedEvent.ProviderAccountId,
            WebhookEndpointId = verifiedEvent.WebhookEndpointId,
            ProviderObjectId = verifiedEvent.ProviderObjectId,
            ProviderObjectType = verifiedEvent.ProviderObjectType,
            ProviderMonetaryLeg = verifiedEvent.ProviderMonetaryLeg,
            IsLiveMode = verifiedEvent.IsLiveMode,
            EventSchemaVersion = verifiedEvent.EventSchemaVersion,
            EventType = verifiedEvent.EventType,
            Payload = verifiedEvent.RetainedPayload,
            Headers = System.Text.Json.JsonSerializer.Serialize(new
            {
                classification = "stripe-financial-event-minimized",
                payloadSha256 = verifiedEvent.PayloadSha256,
                signatureRetained = false
            }),
            TenantId = binding?.TenantId ?? paymentBinding?.TenantId ?? verifiedEvent.TenantId,
            SubscriptionId = binding?.SubscriptionId
        };

        if (existingEvent is null)
        {
            try
            {
                webhookEvent = await _webhookRepository.CreateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not durably accept Stripe webhook {EventId}", verifiedEvent.EventId);
                return WebhookProcessingResult.Failed(verifiedEvent.EventId, "Webhook inbox persistence failed.");
            }

            if (webhookEvent.IsProcessed)
            {
                return WebhookProcessingResult.AlreadyProcessed(verifiedEvent.EventId, webhookEvent.ProcessedAt);
            }
        }

        var staleBefore = SystemClock.UtcNow.AddSeconds(-_webhookSettings.ProcessingTimeoutSeconds);
        var claimed = await _webhookRepository
            .TryClaimProcessingAsync(webhookEvent, staleBefore, cancellationToken)
            .ConfigureAwait(false);
        if (!claimed)
        {
            _logger.LogInformation(
                "Stripe webhook {EventId} is already being processed by another worker.",
                verifiedEvent.EventId);
            return WebhookProcessingResult.Failed(
                verifiedEvent.EventId,
                "Webhook is already being processed.",
                requiresRetry: true);
        }

        try
        {
            await RouteStripeEventAsync(verifiedEvent, cancellationToken).ConfigureAwait(false);

            webhookEvent.MarkAsProcessed();
            await _webhookRepository.UpdateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);
            await PublishBillingEventAsync(webhookEvent, processed: true, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully processed Stripe webhook: {EventId}", verifiedEvent.EventId);
            return WebhookProcessingResult.Success(verifiedEvent.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process Stripe webhook: {EventId}", verifiedEvent.EventId);

            webhookEvent.MarkAsFailed(ex.Message);
            await _webhookRepository.UpdateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);
            await PublishBillingEventAsync(webhookEvent, processed: false, cancellationToken).ConfigureAwait(false);

            return WebhookProcessingResult.Failed(verifiedEvent.EventId, ex.Message);
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
                PaymentProviders.Stripe,
                sourceIpAddress: null,
                detail,
                eventId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<StripeWebhookSubscriptionBinding?> ValidateSubscriptionBindingAsync(
        VerifiedStripeWebhookEvent verifiedEvent,
        CancellationToken cancellationToken)
    {
        var requiresSubscriptionBinding = verifiedEvent.EventType.StartsWith("invoice.", StringComparison.Ordinal) ||
                                          verifiedEvent.EventType.StartsWith("customer.subscription.", StringComparison.Ordinal);
        if (!requiresSubscriptionBinding)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(verifiedEvent.ExternalSubscriptionId))
        {
            throw new InvalidWebhookPayloadException("Stripe event is missing its external subscription binding.");
        }

        var subscription = await _subscriptionQueryService
            .GetByExternalIdAsync(verifiedEvent.ExternalSubscriptionId, cancellationToken)
            .ConfigureAwait(false);
        if (subscription is null)
        {
            throw new InvalidWebhookPayloadException("Stripe event references an unknown subscription.");
        }

        var localTenantId = ((ISubscription)subscription).TenantId;
        if (verifiedEvent.TenantId.HasValue && verifiedEvent.TenantId.Value != localTenantId)
        {
            throw new InvalidWebhookPayloadException("Stripe event tenant does not match the subscription owner.");
        }

        if (verifiedEvent.EventType.StartsWith("invoice.payment_", StringComparison.Ordinal))
        {
            if (!verifiedEvent.Amount.HasValue || verifiedEvent.Amount.Value != subscription.Amount.Amount)
            {
                throw new InvalidWebhookPayloadException("Stripe invoice amount does not match the authoritative subscription price.");
            }

            if (!string.Equals(verifiedEvent.Currency, subscription.Amount.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidWebhookPayloadException("Stripe invoice currency does not match the authoritative subscription currency.");
            }
        }

        return new StripeWebhookSubscriptionBinding(subscription.Id, localTenantId);
    }

    /// <summary>
    ///     Materializes the local invoice for a provider-billed cycle confirmed by
    ///     <c>invoice.payment_succeeded</c>. Idempotent per subscription cycle; the provider
    ///     invoice id (<see cref="VerifiedStripeWebhookEvent.ProviderObjectId"/>) is stamped via
    ///     <see cref="Invoice.SetExternalId"/> and treated as authoritative.
    /// </summary>
    private async Task MaterializeProviderInvoiceAsync(
        VerifiedStripeWebhookEvent verifiedEvent,
        CancellationToken cancellationToken)
    {
        if (_invoiceGeneration is null)
        {
            _logger.LogWarning(
                "Invoice generation is not configured; Stripe event {EventId} cannot materialize an invoice",
                verifiedEvent.EventId);
            return;
        }

        if (string.IsNullOrWhiteSpace(verifiedEvent.ExternalSubscriptionId) ||
            !verifiedEvent.Amount.HasValue)
        {
            _logger.LogWarning(
                "Stripe event {EventId} lacks subscription or amount context; skipping invoice materialization",
                verifiedEvent.EventId);
            return;
        }

        var subscription = await _subscriptionQueryService
            .GetByExternalIdAsync(verifiedEvent.ExternalSubscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            // ValidateSubscriptionBindingAsync already rejects invoice.* events with unknown
            // subscriptions, so this is a defensive guard only.
            _logger.LogWarning(
                "Stripe event {EventId} references unknown subscription {ExternalSubscriptionId}; skipping invoice materialization",
                verifiedEvent.EventId,
                verifiedEvent.ExternalSubscriptionId);
            return;
        }

        // The confirmed cycle is the one this provider charge settles: the subscription's next
        // unprocessed cycle. Materialization precedes subscription payment-sync, so both agree on N.
        var confirmedCycle = subscription.LastProcessedBillingCycle + 1;

        await _invoiceGeneration
            .MaterializeForConfirmedCycleAsync(
                new ConfirmedCycleInvoiceRequest(
                    subscription.Id,
                    confirmedCycle,
                    verifiedEvent.Amount.Value,
                    verifiedEvent.Currency ?? subscription.Amount.Currency,
                    verifiedEvent.OccurredAt.UtcDateTime,
                    PaymentId: null,
                    ProviderInvoiceId: verifiedEvent.ProviderObjectId),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Routes a Stripe event to the appropriate handler based on event type.
    /// </summary>
    private async Task RouteStripeEventAsync(VerifiedStripeWebhookEvent verifiedEvent, CancellationToken cancellationToken)
    {
        foreach (var consumer in _verifiedEventConsumers)
        {
            if (await consumer.TryConsumeAsync(verifiedEvent, cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        var webhookPayload = ParseStripePayload(verifiedEvent.EventType, verifiedEvent.VerifiedPayload);

        switch (verifiedEvent.EventType)
        {
            case "customer.subscription.created":
                await HandleSubscriptionCreatedAsync(webhookPayload.ToSubscriptionPayload()).ConfigureAwait(false);
                break;

            case "customer.subscription.updated":
                await HandleSubscriptionUpdatedAsync(webhookPayload.ToSubscriptionPayload()).ConfigureAwait(false);
                break;

            case "customer.subscription.deleted":
                await HandleSubscriptionCanceledAsync(webhookPayload.ToSubscriptionPayload()).ConfigureAwait(false);
                break;

            case "invoice.payment_succeeded":
                // Materialize the local invoice first (idempotent per cycle, provider data authoritative):
                // the provider already confirmed this cycle's charge, so the invoice exists even if the
                // subscription payment-sync below needs the webhook inbox to retry.
                await MaterializeProviderInvoiceAsync(verifiedEvent, cancellationToken).ConfigureAwait(false);

                var paymentPayload = webhookPayload.ToPaymentPayload();
                // For invoice.* events the provider object IS the invoice: its id is the authoritative
                // provider invoice id (the payload's "invoice" property is absent on invoice objects).
                paymentPayload.InvoiceId ??= verifiedEvent.ProviderObjectId;
                await HandlePaymentSucceededAsync(paymentPayload).ConfigureAwait(false);
                break;

            case "invoice.payment_failed":
                await HandlePaymentFailedAsync(webhookPayload.ToPaymentPayload()).ConfigureAwait(false);
                break;

            default:
                _logger.LogDebug("Unhandled Stripe event type: {EventType}", verifiedEvent.EventType);
                break;
        }
    }

    /// <summary>
    ///     Parses Stripe payload into a common format using System.Text.Json.
    /// </summary>
    private static StripeWebhookPayload ParseStripePayload(string eventType, string payload)
    {
        var result = new StripeWebhookPayload
        {
            EventType = eventType,
            RawPayload = payload
        };

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(payload);
            var root = document.RootElement;

            // Parse common Stripe event structure
            if (root.TryGetProperty("data", out var dataElement) &&
                dataElement.TryGetProperty("object", out var objectElement))
            {
                // Extract subscription-related fields
                if (objectElement.TryGetProperty("id", out var idElement))
                {
                    result.ExternalSubscriptionId = idElement.GetString();
                }

                if (objectElement.TryGetProperty("customer", out var customerElement))
                {
                    result.CustomerId = customerElement.GetString();
                }

                if (objectElement.TryGetProperty("status", out var statusElement))
                {
                    result.Status = statusElement.GetString();
                }

                // Extract metadata for TenantId and PlanId
                if (objectElement.TryGetProperty("metadata", out var metadataElement))
                {
                    if (metadataElement.TryGetProperty("tenant_id", out var tenantIdElement) &&
                        Guid.TryParse(tenantIdElement.GetString(), out var tenantId))
                    {
                        result.TenantId = tenantId;
                    }

                    if (metadataElement.TryGetProperty("plan_id", out var planIdElement) &&
                        Guid.TryParse(planIdElement.GetString(), out var planId))
                    {
                        result.PlanId = planId;
                    }
                }

                // Extract subscription/invoice specific fields
                if (objectElement.TryGetProperty("subscription", out var subscriptionElement))
                {
                    result.ExternalSubscriptionId = subscriptionElement.GetString();
                }

                if (objectElement.TryGetProperty("amount_paid", out var amountPaidElement))
                {
                    result.Amount = amountPaidElement.GetDecimal() / 100m; // Stripe uses cents
                }

                if (objectElement.TryGetProperty("amount_due", out var amountDueElement) && !result.Amount.HasValue)
                {
                    result.Amount = amountDueElement.GetDecimal() / 100m;
                }

                if (objectElement.TryGetProperty("currency", out var currencyElement))
                {
                    result.Currency = currencyElement.GetString()?.ToUpperInvariant();
                }

                if (objectElement.TryGetProperty("invoice", out var invoiceElement))
                {
                    result.InvoiceId = invoiceElement.GetString();
                }

                // Extract plan/price info
                if (objectElement.TryGetProperty("items", out var itemsElement) &&
                    itemsElement.TryGetProperty("data", out var itemsDataElement) &&
                    itemsDataElement.GetArrayLength() > 0)
                {
                    var firstItem = itemsDataElement[0];
                    if (firstItem.TryGetProperty("price", out var priceElement))
                    {
                        if (priceElement.TryGetProperty("id", out var priceIdElement))
                        {
                            result.PriceId = priceIdElement.GetString();
                        }

                        if (priceElement.TryGetProperty("product", out var productElement))
                        {
                            result.ProductId = productElement.GetString();
                        }
                    }
                }

                // Extract dates
                if (objectElement.TryGetProperty("current_period_start", out var periodStartElement))
                {
                    result.StartDate = DateTimeOffset.FromUnixTimeSeconds(periodStartElement.GetInt64()).UtcDateTime;
                }

                if (objectElement.TryGetProperty("current_period_end", out var periodEndElement))
                {
                    result.EndDate = DateTimeOffset.FromUnixTimeSeconds(periodEndElement.GetInt64()).UtcDateTime;
                }

                if (objectElement.TryGetProperty("billing_cycle_anchor", out var anchorElement))
                {
                    result.NextBillingDate = DateTimeOffset.FromUnixTimeSeconds(anchorElement.GetInt64()).UtcDateTime;
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // If parsing fails, return with raw payload only
            // The caller should handle partial data gracefully
        }

        return result;
    }
}

internal sealed record StripeWebhookSubscriptionBinding(Guid SubscriptionId, Guid TenantId);

/// <summary>
///     Internal class for parsing Stripe webhook payloads
/// </summary>
internal class StripeWebhookPayload
{
    public string EventType { get; set; } = string.Empty;
    public string RawPayload { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public Guid? PlanId { get; set; }
    public string? ExternalSubscriptionId { get; set; }
    public string? CustomerId { get; set; }
    public string? ProductId { get; set; }
    public string? PriceId { get; set; }
    public string? PaymentId { get; set; }
    public string? InvoiceId { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? Status { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? NextBillingDate { get; set; }

    public StripeSubscriptionWebhookPayload ToSubscriptionPayload() => new()
    {
        TenantId = TenantId ?? Guid.Empty,
        PlanId = PlanId ?? Guid.Empty,
        ExternalSubscriptionId = ExternalSubscriptionId ?? string.Empty,
        CustomerId = CustomerId,
        ProductId = ProductId,
        PriceId = PriceId,
        Status = Status ?? string.Empty,
        Amount = Amount ?? 0,
        StartDate = StartDate,
        EndDate = EndDate,
        NextBillingDate = NextBillingDate
    };

    public StripePaymentWebhookPayload ToPaymentPayload() => new()
    {
        TenantId = TenantId ?? Guid.Empty,
        PaymentId = PaymentId ?? string.Empty,
        ExternalSubscriptionId = ExternalSubscriptionId ?? string.Empty,
        CustomerId = CustomerId,
        InvoiceId = InvoiceId,
        Amount = Amount ?? 0,
        Currency = Currency ?? "USD",
        Status = Status ?? string.Empty,
        PaidAt = PaidAt ?? SystemClock.UtcNow
    };
}

// WebhookProcessingResult is defined in Models/WebhookProcessingResult.cs
