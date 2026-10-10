using GameGuild.Commerce.Subscriptions;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Service for handling billing webhooks from external providers.
///     Integrates with the Subscriptions module to process subscription and payment events.
/// </summary>
public abstract class BillingWebhookService : IBillingWebhookService
{
    private readonly ILogger<BillingWebhookService> _logger;
    private readonly ISubscriptionLifecycleService _lifecycleService;
    private readonly ISubscriptionQueryService _queryService;
    private readonly ISubscriptionBillingService _billingService;
    private readonly ISubscriptionExternalIdService _externalIdService;
    private readonly IBillingIntegrationEventPublisher? _billingEvents;

    /// <summary>
    ///     Initializes a new instance of the BillingWebhookService.
    /// </summary>
    /// <param name="logger">Logger for webhook events</param>
    /// <param name="lifecycleService">Subscription lifecycle service</param>
    /// <param name="queryService">Subscription query service</param>
    /// <param name="billingService">Subscription billing service</param>
    /// <param name="externalIdService">Subscription external ID service</param>
    /// <param name="billingEvents">Optional named-event publisher for billing integration events</param>
    protected BillingWebhookService(
        ILogger<BillingWebhookService> logger,
        ISubscriptionLifecycleService lifecycleService,
        ISubscriptionQueryService queryService,
        ISubscriptionBillingService billingService,
        ISubscriptionExternalIdService externalIdService,
        IBillingIntegrationEventPublisher? billingEvents = null)
    {
        _logger = logger;
        _lifecycleService = lifecycleService;
        _queryService = queryService;
        _billingService = billingService;
        _externalIdService = externalIdService;
        _billingEvents = billingEvents;
    }

    /// <summary>
    ///     The payment provider identifier used on published billing integration events.
    /// </summary>
    protected abstract string ProviderName { get; }

    /// <inheritdoc />
    public async Task HandleSubscriptionCreatedAsync(SubscriptionWebhookPayload payload)
    {
        try
        {
            _logger.LogInformation("Handling subscription created webhook for tenant {TenantId}, subscription {SubscriptionId}", 
                payload.TenantId, payload.ExternalSubscriptionId);

            // Create subscription via the lifecycle service
            var subscription = await _lifecycleService.CreateAsync(
                tenantId: payload.TenantId,
                planId: payload.PlanId,
                createdByUserId: Guid.Empty, // System-created via webhook
                billingCycle: BillingCycle.Monthly, // Default, should be in payload
                amount: new Money(payload.Amount, "USD"),
                startDate: payload.StartDate,
                trialDays: null
            );

            // Set external IDs for future webhook correlation
            await _externalIdService.SetExternalIdsAsync(
                subscription.Id,
                payload.ExternalSubscriptionId,
                externalCustomerId: null
            ).ConfigureAwait(false);

            _logger.LogInformation("Successfully created subscription {SubscriptionId} from webhook for tenant {TenantId}",
                subscription.Id, payload.TenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling subscription created webhook for tenant {TenantId}, subscription {SubscriptionId}",
                payload.TenantId, payload.ExternalSubscriptionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task HandleSubscriptionUpdatedAsync(SubscriptionWebhookPayload payload)
    {
        try
        {
            _logger.LogInformation("Handling subscription updated webhook for tenant {TenantId}, subscription {SubscriptionId}", 
                payload.TenantId, payload.ExternalSubscriptionId);

            // Find subscription by external ID
            var subscription = await _queryService.GetByExternalIdAsync(payload.ExternalSubscriptionId)
                .ConfigureAwait(false);

            if (subscription == null)
            {
                _logger.LogWarning("Subscription not found for external ID {ExternalSubscriptionId}", 
                    payload.ExternalSubscriptionId);
                return;
            }

            // Handle status changes
            var newStatus = ParseSubscriptionStatus(payload.Status);
            if (subscription.Status != newStatus)
            {
                await HandleStatusTransitionAsync(subscription, newStatus).ConfigureAwait(false);
            }

            _logger.LogInformation("Successfully processed subscription updated webhook for subscription {SubscriptionId}",
                subscription.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling subscription updated webhook for tenant {TenantId}, subscription {SubscriptionId}",
                payload.TenantId, payload.ExternalSubscriptionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task HandleSubscriptionCanceledAsync(SubscriptionWebhookPayload payload)
    {
        try
        {
            _logger.LogInformation("Handling subscription canceled webhook for tenant {TenantId}, subscription {SubscriptionId}", 
                payload.TenantId, payload.ExternalSubscriptionId);

            // Find subscription by external ID
            var subscription = await _queryService.GetByExternalIdAsync(payload.ExternalSubscriptionId)
                .ConfigureAwait(false);

            if (subscription == null)
            {
                _logger.LogWarning("Subscription not found for external ID {ExternalSubscriptionId}", 
                    payload.ExternalSubscriptionId);
                return;
            }

            // Cancel the subscription
            await _lifecycleService.CancelAsync(
                subscription.Id,
                CancellationReason.Custom,
                "Canceled via webhook from payment provider",
                payload.EndDate
            ).ConfigureAwait(false);

            await PublishSubscriptionCancelledAsync(
                    subscription.Id,
                    ((ISubscription)subscription).TenantId,
                    payload.ExternalSubscriptionId)
                .ConfigureAwait(false);

            _logger.LogInformation("Successfully canceled subscription {SubscriptionId} from webhook",
                subscription.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling subscription canceled webhook for tenant {TenantId}, subscription {SubscriptionId}",
                payload.TenantId, payload.ExternalSubscriptionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task HandlePaymentSucceededAsync(PaymentWebhookPayload payload)
    {
        try
        {
            _logger.LogInformation("Handling payment succeeded webhook for tenant {TenantId}, payment {PaymentId}", 
                payload.TenantId, payload.PaymentId);

            // Find subscription by external ID
            var subscription = await _queryService.GetByExternalIdAsync(payload.ExternalSubscriptionId)
                .ConfigureAwait(false);

            if (subscription == null)
            {
                _logger.LogWarning("Subscription not found for external ID {ExternalSubscriptionId}", 
                    payload.ExternalSubscriptionId);
                return;
            }

            // A payment against an already-active subscription is a billing renewal;
            // capture the pre-payment status before the payment is recorded.
            var wasActiveBeforePayment = subscription.Status == SubscriptionStatus.Active;

            // Record the payment
            await _billingService.RecordPaymentAsync(
                subscription.Id,
                payload.Amount,
                payload.Currency,
                payload.PaidAt ?? SystemClock.UtcNow
            ).ConfigureAwait(false);

            var tenantId = ((ISubscription)subscription).TenantId;
            await PublishInvoicePaidAsync(
                    subscription.Id,
                    tenantId,
                    payload.ExternalSubscriptionId,
                    payload.Amount,
                    payload.Currency)
                .ConfigureAwait(false);

            if (wasActiveBeforePayment)
            {
                await PublishSubscriptionRenewedAsync(
                        subscription.Id,
                        tenantId,
                        payload.ExternalSubscriptionId)
                    .ConfigureAwait(false);
            }

            _logger.LogInformation("Successfully recorded payment {PaymentId} for subscription {SubscriptionId}",
                payload.PaymentId, subscription.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling payment succeeded webhook for tenant {TenantId}, payment {PaymentId}",
                payload.TenantId, payload.PaymentId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task HandlePaymentFailedAsync(PaymentWebhookPayload payload)
    {
        try
        {
            _logger.LogInformation("Handling payment failed webhook for tenant {TenantId}, payment {PaymentId}", 
                payload.TenantId, payload.PaymentId);

            // Find subscription by external ID
            var subscription = await _queryService.GetByExternalIdAsync(payload.ExternalSubscriptionId)
                .ConfigureAwait(false);

            if (subscription == null)
            {
                _logger.LogWarning("Subscription not found for external ID {ExternalSubscriptionId}", 
                    payload.ExternalSubscriptionId);
                return;
            }

            // Record the payment failure
            await _billingService.RecordPaymentFailureAsync(
                subscription.Id,
                payload.FailureReason ?? "Payment failed via webhook",
                payload.PaidAt ?? SystemClock.UtcNow
            ).ConfigureAwait(false);

            _logger.LogInformation("Successfully recorded payment failure {PaymentId} for subscription {SubscriptionId}",
                payload.PaymentId, subscription.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling payment failed webhook for tenant {TenantId}, payment {PaymentId}",
                payload.TenantId, payload.PaymentId);
            throw;
        }
    }

    /// <summary>
    ///     Publishes the invoice-paid (and optionally renewal) named events for a recorded payment.
    /// </summary>
    private async Task PublishInvoicePaidAsync(
        Guid subscriptionId,
        Guid tenantId,
        string externalSubscriptionId,
        decimal amount,
        string currency)
    {
        if (_billingEvents is null)
        {
            return;
        }

        await _billingEvents.PublishInvoicePaidAsync(
                subscriptionId,
                tenantId,
                ProviderName,
                externalSubscriptionId,
                amount,
                string.IsNullOrWhiteSpace(currency) ? CurrencyCodes.Default : currency)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Publishes the subscription-renewed named event for a recurring payment.
    /// </summary>
    private async Task PublishSubscriptionRenewedAsync(Guid subscriptionId, Guid tenantId, string externalSubscriptionId)
    {
        if (_billingEvents is null)
        {
            return;
        }

        await _billingEvents.PublishSubscriptionRenewedAsync(
                subscriptionId,
                tenantId,
                ProviderName,
                externalSubscriptionId)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Publishes the subscription-cancelled named event for a provider-driven cancellation.
    /// </summary>
    private async Task PublishSubscriptionCancelledAsync(Guid subscriptionId, Guid tenantId, string externalSubscriptionId)
    {
        if (_billingEvents is null)
        {
            return;
        }

        await _billingEvents.PublishSubscriptionCancelledAsync(
                subscriptionId,
                tenantId,
                ProviderName,
                externalSubscriptionId)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Handles status transitions for a subscription
    /// </summary>
    private async Task HandleStatusTransitionAsync(Subscription subscription, SubscriptionStatus newStatus)
    {
        switch (newStatus)
        {
            case SubscriptionStatus.Active:
                if (subscription.Status is SubscriptionStatus.PendingActivation or SubscriptionStatus.Trialing or SubscriptionStatus.Suspended)
                {
                    await _lifecycleService.ActivateAsync(subscription.Id).ConfigureAwait(false);
                }
                break;

            case SubscriptionStatus.Suspended:
                if (subscription.Status == SubscriptionStatus.Active)
                {
                    await _lifecycleService.SuspendAsync(subscription.Id, "Suspended via webhook").ConfigureAwait(false);
                }
                break;

            case SubscriptionStatus.Cancelled:
                await _lifecycleService.CancelAsync(subscription.Id, CancellationReason.Custom).ConfigureAwait(false);
                break;

            case SubscriptionStatus.Trialing:
                if (subscription.Status == SubscriptionStatus.PendingActivation)
                {
                    await _lifecycleService.StartTrialAsync(subscription.Id, 14).ConfigureAwait(false);
                }
                break;

            default:
                _logger.LogDebug("No handler for status transition from {OldStatus} to {NewStatus}",
                    subscription.Status, newStatus);
                break;
        }
    }

    /// <summary>
    ///     Parses a status string to SubscriptionStatus enum
    /// </summary>
    private static SubscriptionStatus ParseSubscriptionStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" or "trial" => SubscriptionStatus.Trialing,
            "past_due" or "pastdue" => SubscriptionStatus.PastDue,
            "canceled" or "cancelled" => SubscriptionStatus.Cancelled,
            "unpaid" => SubscriptionStatus.Suspended,
            "incomplete" or "incomplete_expired" => SubscriptionStatus.PendingActivation,
            "paused" => SubscriptionStatus.Suspended,
            _ => SubscriptionStatus.PendingActivation
        };
    }
}
