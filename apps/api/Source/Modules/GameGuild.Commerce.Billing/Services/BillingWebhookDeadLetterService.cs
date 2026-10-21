using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Dead-letters billing webhook inbox events whose retry budget was exhausted
///     (issue #408). A dead-lettered row keeps its failure evidence for inspection
///     (status filter <c>deadlettered</c>), is never requeued by the retry worker again,
///     and raises a security event through the Compliance.Audit pipeline so the existing
///     alert rules surface repeated delivery failures to operators.
/// </summary>
public interface IBillingWebhookDeadLetterService
{
    /// <summary>
    ///     Marks the event as dead-lettered and publishes the security + integration
    ///     events. Idempotent: an already dead-lettered row is left untouched.
    /// </summary>
    /// <param name="webhookEvent">Exhausted inbox row.</param>
    /// <param name="reason">Bounded human-readable reason recorded on the row.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeadLetterAsync(
        BillingWebhookEvent webhookEvent,
        string reason,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class BillingWebhookDeadLetterService(
    IBillingWebhookRepository webhookRepository,
    IWebhookSecurityEventPublisher? securityEvents,
    IBillingIntegrationEventPublisher? billingEvents,
    ILogger<BillingWebhookDeadLetterService> logger) : IBillingWebhookDeadLetterService
{
    /// <inheritdoc />
    public async Task DeadLetterAsync(
        BillingWebhookEvent webhookEvent,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhookEvent);

        if (webhookEvent.DeadLetteredAt.HasValue)
        {
            return;
        }

        webhookEvent.MarkAsDeadLettered(reason);
        await webhookRepository.UpdateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);

        // Alert on exhausted retries: the dead-letter security event flows into the central
        // Compliance.Audit pipeline where the existing alert rules (critical-event) pick it up.
        // Publication is best-effort: an audit-pipeline outage must never lose the
        // dead-letter state that was already persisted above.
        try
        {
            if (securityEvents is not null)
            {
                await securityEvents
                    .PublishAsync(
                        WebhookSecurityEventKind.DeadLettered,
                        webhookEvent.Provider,
                        sourceIpAddress: null,
                        $"Webhook {webhookEvent.ExternalEventId} ({webhookEvent.EventType}) exhausted its retry budget after {webhookEvent.ProcessingAttempts} attempts: {reason}",
                        webhookEvent.ExternalEventId,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (billingEvents is not null)
            {
                await billingEvents
                    .PublishWebhookFailedAsync(webhookEvent, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception,
                "Dead-letter event publication failed for webhook event {EventId}; the row state is already persisted.",
                webhookEvent.ExternalEventId);
        }

        logger.LogWarning(
            "Webhook event {EventId} for provider {Provider} was dead-lettered after {Attempts} attempt(s): {Reason}",
            webhookEvent.ExternalEventId,
            webhookEvent.Provider,
            webhookEvent.ProcessingAttempts,
            reason);
    }
}
