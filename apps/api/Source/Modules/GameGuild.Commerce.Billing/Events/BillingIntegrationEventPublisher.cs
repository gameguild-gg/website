using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Publishes the named billing integration events through the durable event transport.
///     Publication is best-effort from the webhook processing path: a recording failure is
///     logged and never changes the webhook processing outcome, mirroring the
///     <see cref="IWebhookSecurityEventPublisher" /> reliability contract.
/// </summary>
public interface IBillingIntegrationEventPublisher
{
    /// <summary>Records the inbox transition of a webhook to processed.</summary>
    Task PublishWebhookProcessedAsync(
        BillingWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default);

    /// <summary>Records a failed webhook processing attempt.</summary>
    Task PublishWebhookFailedAsync(
        BillingWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default);

    /// <summary>Records a successful provider invoice payment against a subscription.</summary>
    Task PublishInvoicePaidAsync(
        Guid subscriptionId,
        Guid? tenantId,
        string provider,
        string externalEventId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default);

    /// <summary>Records a recurring payment against an already-active subscription.</summary>
    Task PublishSubscriptionRenewedAsync(
        Guid subscriptionId,
        Guid? tenantId,
        string provider,
        string externalEventId,
        CancellationToken cancellationToken = default);

    /// <summary>Records a subscription cancellation driven by a provider lifecycle event.</summary>
    Task PublishSubscriptionCancelledAsync(
        Guid subscriptionId,
        Guid? tenantId,
        string provider,
        string externalEventId,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class BillingIntegrationEventPublisher(
    IDurableEventProducer durableEvents,
    IUseCaseOperationContextAccessor operationContextAccessor,
    ILogger<BillingIntegrationEventPublisher> logger) : IBillingIntegrationEventPublisher
{
    public Task PublishWebhookProcessedAsync(
        BillingWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            new BillingWebhookProcessedV1(
                webhookEvent.Id,
                webhookEvent.Provider,
                webhookEvent.ExternalEventId,
                webhookEvent.EventType,
                webhookEvent.ProcessingAttempts)
            {
                TenantId = webhookEvent.TenantId ?? DurableIntegrationEventTenants.Platform,
                AggregateType = "BillingWebhookEvent",
                AggregateId = webhookEvent.Id.ToString()
            },
            cancellationToken);

    public Task PublishWebhookFailedAsync(
        BillingWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            new BillingWebhookFailedV1(
                webhookEvent.Id,
                webhookEvent.Provider,
                webhookEvent.ExternalEventId,
                webhookEvent.EventType,
                webhookEvent.ProcessingAttempts,
                webhookEvent.ErrorMessage ?? string.Empty)
            {
                TenantId = webhookEvent.TenantId ?? DurableIntegrationEventTenants.Platform,
                AggregateType = "BillingWebhookEvent",
                AggregateId = webhookEvent.Id.ToString()
            },
            cancellationToken);

    public Task PublishInvoicePaidAsync(
        Guid subscriptionId,
        Guid? tenantId,
        string provider,
        string externalEventId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            new BillingInvoicePaidV1(subscriptionId, provider, externalEventId, amount, currency)
            {
                TenantId = tenantId ?? DurableIntegrationEventTenants.Platform,
                AggregateType = "Subscription",
                AggregateId = subscriptionId.ToString()
            },
            cancellationToken);

    public Task PublishSubscriptionRenewedAsync(
        Guid subscriptionId,
        Guid? tenantId,
        string provider,
        string externalEventId,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            new BillingSubscriptionRenewedV1(subscriptionId, provider, externalEventId)
            {
                TenantId = tenantId ?? DurableIntegrationEventTenants.Platform,
                AggregateType = "Subscription",
                AggregateId = subscriptionId.ToString()
            },
            cancellationToken);

    public Task PublishSubscriptionCancelledAsync(
        Guid subscriptionId,
        Guid? tenantId,
        string provider,
        string externalEventId,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            new BillingSubscriptionCancelledV1(subscriptionId, provider, externalEventId)
            {
                TenantId = tenantId ?? DurableIntegrationEventTenants.Platform,
                AggregateType = "Subscription",
                AggregateId = subscriptionId.ToString()
            },
            cancellationToken);

    private async Task RecordAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : DurableIntegrationEventBase
    {
        try
        {
            // Correlate with the ambient use-case operation so the durable transport and the
            // event contract registry observe the named event as part of the same operation.
            // The generic constraint keeps the derived record payload through the copy expression.
            var operationContext = operationContextAccessor.Current;
            if (operationContext is not null)
            {
                integrationEvent = integrationEvent with
                {
                    CorrelationId = operationContext.CorrelationId,
                    CausationId = operationContext.CausationId
                };
            }

            await durableEvents.RecordAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Failed to record billing integration event {EventName} for aggregate {AggregateId}",
                integrationEvent.EventName,
                integrationEvent.AggregateId);
        }
    }
}
