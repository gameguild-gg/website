using GameGuild.CQRS;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Handler for retrying webhook events (issue #408). The retry EXECUTES the stored
///     delivery: after claiming the attempt, the row is replayed through its provider's
///     replay handler (the same routing used for live deliveries). Events that exhaust
///     the retry ceiling are dead-lettered with their failure evidence retained for
///     inspection; events without a replay handler surface an explicit non-retryable
///     failure instead of a silent "queued" success.
/// </summary>
public sealed class RetryWebhookEventHandler(
    IBillingWebhookRepository webhookRepository,
    BillingWebhookReplayDispatcher replayDispatcher,
    IBillingWebhookDeadLetterService deadLetterService,
    IOptions<BillingConfiguration> billingConfiguration,
    ILogger<RetryWebhookEventHandler> logger) : ICommandHandler<RetryWebhookEventCommand, WebhookRetryResult>
{
    /// <inheritdoc />
    public async Task<WebhookRetryResult> Handle(RetryWebhookEventCommand command, CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(command.EventId, out var eventId))
            {
                logger.LogWarning("Invalid event ID format for retry: {EventId}", command.EventId);
                return new WebhookRetryResult
                {
                    Success = false,
                    ErrorMessage = "Invalid event ID format",
                    AttemptNumber = 0
                };
            }

            var webhookEvent = await webhookRepository.GetByIdAsync(eventId, cancellationToken)
                .ConfigureAwait(false);

            if (webhookEvent == null)
            {
                logger.LogWarning("Webhook event not found for retry: {EventId}", command.EventId);
                return new WebhookRetryResult
                {
                    Success = false,
                    ErrorMessage = "Webhook event not found",
                    AttemptNumber = 0
                };
            }

            // Check if already processed successfully
            if (webhookEvent.IsProcessed && !webhookEvent.IsFailed)
            {
                logger.LogInformation("Webhook event {EventId} already processed successfully", command.EventId);
                return new WebhookRetryResult
                {
                    Success = true,
                    AttemptNumber = webhookEvent.ProcessingAttempts,
                    Message = "Event was already processed successfully"
                };
            }

            var maxRetryAttempts = billingConfiguration.Value.Webhook.RetryWorker.MaxRetryAttempts;

            // Check max retry attempts
            if (webhookEvent.ProcessingAttempts >= maxRetryAttempts)
            {
                logger.LogWarning("Webhook event {EventId} has exceeded max retry attempts ({MaxAttempts})",
                    command.EventId, maxRetryAttempts);
                await deadLetterService.DeadLetterAsync(
                        webhookEvent,
                        $"Maximum retry attempts ({maxRetryAttempts}) exceeded",
                        cancellationToken)
                    .ConfigureAwait(false);
                return new WebhookRetryResult
                {
                    Success = false,
                    ErrorMessage = $"Maximum retry attempts ({maxRetryAttempts}) exceeded; event dead-lettered",
                    AttemptNumber = webhookEvent.ProcessingAttempts
                };
            }

            var replayHandler = replayDispatcher.Resolve(webhookEvent.Provider);
            if (replayHandler is null)
            {
                logger.LogWarning(
                    "Webhook event {EventId} for provider {Provider} has no replay handler",
                    command.EventId,
                    webhookEvent.Provider);
                return new WebhookRetryResult
                {
                    Success = false,
                    ErrorMessage = $"No replay handler registered for provider '{webhookEvent.Provider}'",
                    AttemptNumber = webhookEvent.ProcessingAttempts
                };
            }

            // Claim this attempt, then execute the replay of the stored delivery.
            webhookEvent.IncrementAttempts();
            webhookEvent.IsFailed = false;
            webhookEvent.ErrorMessage = null;
            await webhookRepository.UpdateAsync(webhookEvent, cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Replaying webhook event {EventId} through the {Provider} handler. Attempt {Attempt} of {MaxAttempts}",
                command.EventId,
                webhookEvent.Provider,
                webhookEvent.ProcessingAttempts,
                maxRetryAttempts);

            var replayResult = await replayHandler.ReplayAsync(webhookEvent, cancellationToken).ConfigureAwait(false);

            if (replayResult.Processed)
            {
                return new WebhookRetryResult
                {
                    Success = true,
                    AttemptNumber = webhookEvent.ProcessingAttempts,
                    Message = replayResult.WasAlreadyProcessed
                        ? "Event was already processed successfully"
                        : $"Event replayed successfully (attempt {webhookEvent.ProcessingAttempts})"
                };
            }

            if (!replayResult.RequiresRetry || webhookEvent.ProcessingAttempts >= maxRetryAttempts)
            {
                // Terminal replay failure (non-retryable cause, or the retry budget is spent):
                // dead-letter so the failure evidence is preserved and surfaced for operators.
                await deadLetterService.DeadLetterAsync(
                        webhookEvent,
                        replayResult.ErrorMessage ?? "Replay failed",
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return new WebhookRetryResult
            {
                Success = false,
                ErrorMessage = replayResult.ErrorMessage,
                AttemptNumber = webhookEvent.ProcessingAttempts,
                Message = webhookEvent.DeadLetteredAt.HasValue
                    ? $"Replay failed on attempt {webhookEvent.ProcessingAttempts}; event dead-lettered"
                    : $"Replay failed on attempt {webhookEvent.ProcessingAttempts}; the retry worker will requeue it after its backoff window"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrying webhook event {EventId}", command.EventId);
            return new WebhookRetryResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                AttemptNumber = 0
            };
        }
    }
}
