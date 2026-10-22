using GameGuild.CQRS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Core retry loop for failed billing webhook inbox events (issues #396, #408).
///     A cycle claims nothing by itself: it selects failed inbox rows below the retry
///     ceiling, applies the configured exponential backoff schedule (1, 5, 15, 60, 360
///     minutes by default) against the row's last update time, and requeues the due ones
///     through the <see cref="RetryWebhookEventCommand" /> retry path, which now EXECUTES
///     the replay of the stored delivery. Rows that exhausted their budget are
///     dead-lettered (failure evidence retained, security event raised for alerting).
/// </summary>
public interface IBillingWebhookRetryWorker
{
    /// <summary>
    ///     Processes one polling cycle and returns the number of requeued events.
    /// </summary>
    Task<int> ProcessDueAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class BillingWebhookRetryWorker(
    IBillingWebhookRepository webhookRepository,
    ISender sender,
    IBillingWebhookDeadLetterService deadLetterService,
    IOptions<BillingConfiguration> billingConfiguration,
    ILogger<BillingWebhookRetryWorker> logger) : IBillingWebhookRetryWorker
{
    /// <inheritdoc />
    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var settings = billingConfiguration.Value.Webhook.RetryWorker;
        if (!settings.Enabled)
        {
            return 0;
        }

        await DeadLetterExhaustedAsync(settings.MaxRetryAttempts, settings.BatchSize, cancellationToken)
            .ConfigureAwait(false);

        var startedAt = SystemClock.UtcNow;
        var candidates = await webhookRepository
            .GetRetryCandidatesAsync(settings.MaxRetryAttempts, settings.BatchSize, cancellationToken)
            .ConfigureAwait(false);

        var workerSettings = billingConfiguration.Value.Webhook.RetryWorker;
        var requeued = 0;
        var dueSeen = 0;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Backoff gate: a failed attempt is due only after the exponential schedule
            // delay for its attempt number has elapsed since the last state update.
            var nextAttemptAt = candidate.UpdatedAt.Add(WebhookRetrySchedule.DelayFor(
                candidate.ProcessingAttempts,
                workerSettings.RetryScheduleMinutes,
                workerSettings.TerminalRetryScheduleMinutes));
            if (nextAttemptAt > startedAt)
            {
                continue;
            }

            dueSeen++;
            try
            {
                var result = await sender
                    .Send(new RetryWebhookEventCommand(candidate.Id.ToString()), cancellationToken)
                    .ConfigureAwait(false);

                if (result.Success)
                {
                    requeued++;
                    BillingWebhookRetryMetrics.RecordRetryScheduled(candidate.Provider);
                }
                else if (candidate.DeadLetteredAt.HasValue)
                {
                    // The retry path dead-lettered the event (terminal outcome), surfaced
                    // through the exhausted metric for alerting.
                    BillingWebhookRetryMetrics.RecordRetryExhausted(candidate.Provider);
                    logger.LogWarning(
                        "Webhook event {EventId} for provider {Provider} exhausted its retry budget ({Attempts}/{MaxAttempts}) and was dead-lettered.",
                        candidate.Id,
                        candidate.Provider,
                        candidate.ProcessingAttempts,
                        settings.MaxRetryAttempts);
                }
                else if (candidate.ProcessingAttempts + 1 >= settings.MaxRetryAttempts)
                {
                    // The retry path refused the event at the ceiling: this is the terminal
                    // outcome, surfaced through the exhausted metric for alerting.
                    BillingWebhookRetryMetrics.RecordRetryExhausted(candidate.Provider);
                    logger.LogWarning(
                        "Webhook event {EventId} for provider {Provider} exhausted its retry budget ({Attempts}/{MaxAttempts}).",
                        candidate.Id,
                        candidate.Provider,
                        candidate.ProcessingAttempts,
                        settings.MaxRetryAttempts);
                }
                else
                {
                    logger.LogDebug(
                        "Retry scheduling for webhook event {EventId} was refused: {Reason}",
                        candidate.Id,
                        result.ErrorMessage);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One event must never abort the cycle; the row stays failed and is
                // reconsidered on the next poll with a fresh backoff window.
                logger.LogError(exception,
                    "Retry cycle failed for webhook event {EventId}.",
                    candidate.Id);
            }
        }

        BillingWebhookRetryMetrics.RecordCycle(SystemClock.UtcNow - startedAt);

        if (requeued > 0 || dueSeen > 0)
        {
            logger.LogInformation(
                "Billing webhook retry cycle requeued {Requeued} of {Due} due events.",
                requeued,
                dueSeen);
        }

        return requeued;
    }

    /// <summary>
    ///     Dead-letters rows that exhausted their retry budget but were left behind by the
    ///     requeue path (their attempt count no longer matches the candidates filter).
    ///     Each dead-letter raises a critical security event for operator alerting.
    /// </summary>
    private async Task DeadLetterExhaustedAsync(int maxAttempts, int batchSize, CancellationToken cancellationToken)
    {
        var exhausted = await webhookRepository
            .GetExhaustedEventsAsync(maxAttempts, batchSize, cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in exhausted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await deadLetterService
                    .DeadLetterAsync(
                        row,
                        $"Retry budget exhausted after {row.ProcessingAttempts} attempt(s); last error: {row.ErrorMessage ?? "unknown"}",
                        cancellationToken)
                    .ConfigureAwait(false);
                BillingWebhookRetryMetrics.RecordRetryExhausted(row.Provider);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception,
                    "Dead-letter transition failed for webhook event {EventId}.",
                    row.Id);
            }
        }
    }
}

/// <summary>
///     Hosted service that polls failed billing webhook inbox events and requeues the ones
///     past their exponential-backoff next-attempt time through the existing retry path
///     (issues #396, #408). Honors the retry worker ceiling (default 5 attempts, matching the
///     manual retry path), dead-letters exhausted events, and stops polling when disabled
///     by configuration.
/// </summary>
public sealed class BillingWebhookRetryBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<BillingConfiguration> billingConfiguration,
    ILogger<BillingWebhookRetryBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = billingConfiguration.Value.Webhook.RetryWorker;
        if (!settings.Enabled)
        {
            logger.LogInformation("Billing webhook retry worker is disabled by configuration.");
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, settings.PollIntervalSeconds));
        logger.LogInformation("Billing webhook retry worker started with a {Interval} poll interval.", pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IBillingWebhookRetryWorker>()
                    .ProcessDueAsync(stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Billing webhook retry cycle failed");
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
