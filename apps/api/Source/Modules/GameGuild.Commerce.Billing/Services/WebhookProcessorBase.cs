using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Result of tenant validation in webhook processing.
/// </summary>
public sealed record TenantValidationResult
{
    public bool IsValid { get; init; }
    public Guid TenantId { get; init; }
    public string? ErrorMessage { get; init; }

    public static TenantValidationResult Success(Guid tenantId) => new()
    {
        IsValid = true,
        TenantId = tenantId
    };

    public static TenantValidationResult Failure(string errorMessage) => new()
    {
        IsValid = false,
        TenantId = Guid.Empty,
        ErrorMessage = errorMessage
    };
}

/// <summary>
///     Base class for webhook processors implementing Template Method pattern.
///     Encapsulates the common flow: validate signature → check idempotency → store event → process → update status.
///     Includes configurable retry logic with exponential backoff.
/// </summary>
/// <remarks>
///     Provider-specific implementations should override the abstract/virtual methods to customize behavior.
/// </remarks>
public abstract class WebhookProcessorBase
{
    private readonly IBillingWebhookRepository _webhookRepository;
    private readonly ILogger _logger;
    private readonly WebhookSettings _webhookSettings;

    /// <summary>
    ///     The payment provider identifier (e.g., PaymentProviders.Stripe).
    /// </summary>
    protected abstract string ProviderName { get; }

    protected WebhookProcessorBase(
        IBillingWebhookRepository webhookRepository,
        IOptions<BillingConfiguration> billingConfiguration,
        ILogger logger)
    {
        _webhookRepository = webhookRepository;
        _webhookSettings = billingConfiguration.Value.Webhook;
        _logger = logger;
    }

    /// <summary>
    ///     Gets the configured webhook settings.
    /// </summary>
    protected WebhookSettings Settings => _webhookSettings;

    /// <summary>
    ///     Validates tenant context for webhook processing.
    ///     Economic invariant: All financial operations require valid tenant context.
    /// </summary>
    /// <remarks>
    ///     Fail-closed behavior: If tenant cannot be determined or validated, the webhook
    ///     processing should be rejected to prevent cross-tenant data corruption.
    /// </remarks>
    /// <param name="tenantIdFromPayload">Tenant ID extracted from webhook payload metadata</param>
    /// <param name="subscriptionExternalId">External subscription ID for cross-reference validation</param>
    /// <param name="validateSubscriptionOwnership">
    ///     Callback to validate that the subscription belongs to the claimed tenant.
    ///     Should return true if ownership is confirmed.
    /// </param>
    /// <returns>Validation result with tenant ID if valid</returns>
    protected async Task<TenantValidationResult> ValidateTenantContextAsync(
        Guid? tenantIdFromPayload,
        string? subscriptionExternalId,
        Func<Guid, string, Task<bool>>? validateSubscriptionOwnership = null)
    {
        // Rule 1: Tenant ID must be present
        if (!tenantIdFromPayload.HasValue || tenantIdFromPayload.Value == Guid.Empty)
        {
            _logger.LogWarning(
                "Webhook rejected: Missing or empty tenant ID in payload. " +
                "Provider: {Provider}, SubscriptionExternalId: {ExternalId}",
                ProviderName,
                subscriptionExternalId ?? "null");

            return TenantValidationResult.Failure(
                "Missing tenant context. Webhook metadata must include valid tenant ID.");
        }

        var tenantId = tenantIdFromPayload.Value;

        // Rule 2: If subscription ID provided, validate ownership (prevents cross-tenant attacks)
        if (!string.IsNullOrEmpty(subscriptionExternalId) && validateSubscriptionOwnership != null)
        {
            try
            {
                var ownershipValid = await validateSubscriptionOwnership(tenantId, subscriptionExternalId).ConfigureAwait(false);

                if (!ownershipValid)
                {
                    _logger.LogWarning(
                        "Webhook rejected: Subscription ownership validation failed. " +
                        "Provider: {Provider}, ClaimedTenant: {TenantId}, SubscriptionExternalId: {ExternalId}",
                        ProviderName,
                        tenantId,
                        subscriptionExternalId);

                    return TenantValidationResult.Failure(
                        $"Subscription {subscriptionExternalId} does not belong to tenant {tenantId}.");
                }
            }
            catch (Exception ex)
            {
                // Fail-closed: If we can't validate, reject the webhook
                _logger.LogError(
                    ex,
                    "Webhook rejected: Failed to validate subscription ownership. " +
                    "Provider: {Provider}, TenantId: {TenantId}, SubscriptionExternalId: {ExternalId}",
                    ProviderName,
                    tenantId,
                    subscriptionExternalId);

                return TenantValidationResult.Failure(
                    "Failed to validate subscription ownership. Processing rejected.");
            }
        }

        _logger.LogDebug(
            "Tenant context validated for webhook. Provider: {Provider}, TenantId: {TenantId}",
            ProviderName,
            tenantId);

        return TenantValidationResult.Success(tenantId);
    }

    /// <summary>
    ///     Extracts tenant ID from webhook metadata.
    ///     Override in derived classes for provider-specific metadata extraction.
    /// </summary>
    /// <param name="metadata">Provider-specific metadata dictionary</param>
    /// <returns>Tenant ID if found, null otherwise</returns>
    protected virtual Guid? ExtractTenantIdFromMetadata(IDictionary<string, string>? metadata)
    {
        if (metadata == null)
        {
            return null;
        }

        // Common metadata key patterns across providers
        var tenantIdKeys = new[] { "tenant_id", "tenantId", "TenantId", "tenant-id" };

        foreach (var key in tenantIdKeys)
        {
            if (metadata.TryGetValue(key, out var value) && Guid.TryParse(value, out var tenantId))
            {
                return tenantId;
            }
        }

        return null;
    }

    /// <summary>
    ///     Template Method: Process a webhook with standard idempotency, retry logic, and error handling.
    /// </summary>
    /// <param name="eventId">Unique event identifier for idempotency</param>
    /// <param name="eventType">The event type (e.g., customer.subscription.created)</param>
    /// <param name="payload">Raw payload string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result</returns>
    protected async Task<WebhookProcessingResult> ProcessWebhookAsync(
        string eventId,
        string eventType,
        string payload,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing {Provider} webhook: {EventType} with ID {EventId}",
            ProviderName, eventType, eventId);

        // Step 1: Check for duplicate event (idempotency)
        var existingEvent = await _webhookRepository
            .GetByExternalEventIdAsync(eventId, ProviderName, cancellationToken)
            .ConfigureAwait(false);

        if (existingEvent != null)
        {
            _logger.LogInformation(
                "Duplicate {Provider} webhook detected: {EventId}. Returning success.",
                ProviderName, eventId);
            return WebhookProcessingResult.AlreadyProcessed(eventId, existingEvent.ProcessedAt);
        }

        // Step 2: Create webhook event record
        var webhookEvent = new BillingWebhookEvent
        {
            ExternalEventId = eventId,
            Provider = ProviderName,
            EventType = eventType,
            Payload = _webhookSettings.StorePayloads ? payload : string.Empty,
            ProcessingAttempts = 0
        };

        try
        {
            // Step 3: Store the event first (before processing) to handle concurrent retries
            webhookEvent = await _webhookRepository
                .CreateAsync(webhookEvent, cancellationToken)
                .ConfigureAwait(false);

            // Step 4: Process with retry logic
            await ProcessWithRetryAsync(webhookEvent, eventType, payload, cancellationToken)
                .ConfigureAwait(false);

            return WebhookProcessingResult.Success(eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process {Provider} webhook: {EventId} after {Attempts} attempts",
                ProviderName, eventId, webhookEvent.ProcessingAttempts);

            return WebhookProcessingResult.Failed(eventId, ex.Message);
        }
    }

    /// <summary>
    ///     Processes the webhook with configurable retry logic.
    /// </summary>
    private async Task ProcessWithRetryAsync(
        BillingWebhookEvent webhookEvent,
        string eventType,
        string payload,
        CancellationToken cancellationToken)
    {
        var maxAttempts = _webhookSettings.RetryPolicy.Enabled
            ? _webhookSettings.MaxRetryAttempts
            : 1;

        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            webhookEvent.ProcessingAttempts = attempt;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(_webhookSettings.ProcessingTimeoutSeconds));

                await RouteEventAsync(eventType, payload, cts.Token).ConfigureAwait(false);

                // Success
                webhookEvent.MarkAsProcessed();
                await _webhookRepository.UpdateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "Successfully processed {Provider} webhook: {EventId} on attempt {Attempt}",
                    ProviderName, webhookEvent.ExternalEventId, attempt);

                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // External cancellation requested, don't retry
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;

                _logger.LogWarning(ex,
                    "Attempt {Attempt}/{MaxAttempts} failed for {Provider} webhook: {EventId}",
                    attempt, maxAttempts, ProviderName, webhookEvent.ExternalEventId);

                // Update webhook event with failure info
                webhookEvent.MarkAsFailed(ex.Message);
                await _webhookRepository.UpdateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);

                // Wait before retry (if not last attempt)
                if (attempt < maxAttempts && _webhookSettings.RetryPolicy.Enabled)
                {
                    var delay = _webhookSettings.RetryPolicy.CalculateDelay(attempt);

                    _logger.LogDebug(
                        "Waiting {Delay} before retry attempt {NextAttempt} for webhook {EventId}",
                        delay, attempt + 1, webhookEvent.ExternalEventId);

                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // All retries exhausted
        throw new WebhookProcessingException(
            $"Failed to process webhook {webhookEvent.ExternalEventId} after {maxAttempts} attempts",
            lastException);
    }

    /// <summary>
    ///     Routes the event to the appropriate handler based on event type.
    ///     Override in derived classes to implement provider-specific routing.
    /// </summary>
    /// <param name="eventType">The event type</param>
    /// <param name="payload">The raw payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected abstract Task RouteEventAsync(
        string eventType,
        string payload,
        CancellationToken cancellationToken);
}

/// <summary>
///     Exception thrown when webhook processing fails after all retry attempts.
/// </summary>
public class WebhookProcessingException : Exception
{
    public WebhookProcessingException(string message) : base(message) { }
    public WebhookProcessingException(string message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>
///     Event type constants for common webhook events across providers.
/// </summary>
public static class WebhookEventTypes
{
    /// <summary>Stripe event types</summary>
    public static class Stripe
    {
        public const string SubscriptionCreated = "customer.subscription.created";
        public const string SubscriptionUpdated = "customer.subscription.updated";
        public const string SubscriptionDeleted = "customer.subscription.deleted";
        public const string InvoicePaymentSucceeded = "invoice.payment_succeeded";
        public const string InvoicePaymentFailed = "invoice.payment_failed";
        public const string PaymentIntentSucceeded = "payment_intent.succeeded";
        public const string PaymentIntentFailed = "payment_intent.payment_failed";
    }

    /// <summary>PayPal event types</summary>
    public static class PayPal
    {
        public const string SubscriptionCreated = "BILLING.SUBSCRIPTION.CREATED";
        public const string SubscriptionActivated = "BILLING.SUBSCRIPTION.ACTIVATED";
        public const string SubscriptionUpdated = "BILLING.SUBSCRIPTION.UPDATED";
        public const string SubscriptionCancelled = "BILLING.SUBSCRIPTION.CANCELLED";
        public const string SubscriptionSuspended = "BILLING.SUBSCRIPTION.SUSPENDED";
        public const string SubscriptionExpired = "BILLING.SUBSCRIPTION.EXPIRED";
        public const string PaymentSaleCompleted = "PAYMENT.SALE.COMPLETED";
        public const string PaymentCaptureCompleted = "PAYMENT.CAPTURE.COMPLETED";
        public const string PaymentCaptureDenied = "PAYMENT.CAPTURE.DENIED";
    }

    /// <summary>Apple App Store notification types</summary>
    public static class Apple
    {
        public const string SubscribedInitial = "SUBSCRIBED";
        public const string Renewed = "DID_RENEW";
        public const string FailedToRenew = "DID_FAIL_TO_RENEW";
        public const string Expired = "EXPIRED";
        public const string Refunded = "REFUND";
        public const string GracePeriodExpired = "GRACE_PERIOD_EXPIRED";
        public const string Revoked = "REVOKE";
    }
}
