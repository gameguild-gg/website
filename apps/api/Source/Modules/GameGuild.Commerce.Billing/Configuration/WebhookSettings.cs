namespace GameGuild.Commerce.Billing;

/// <summary>
///     Webhook processing configuration
/// </summary>
public class WebhookSettings
{
    /// <summary>
    ///     Maximum number of retry attempts for failed webhooks
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    ///     Timeout for webhook processing in seconds
    /// </summary>
    public int ProcessingTimeoutSeconds { get; set; } = 30;

    /// <summary>
    ///     Whether to verify webhook signatures
    /// </summary>
    public bool VerifySignatures { get; set; } = true;

    /// <summary>
    ///     Whether to store webhook payloads in database
    /// </summary>
    public bool StorePayloads { get; set; } = true;

    /// <summary>
    ///     Retry policy configuration for failed webhook processing.
    /// </summary>
    public WebhookRetryPolicy RetryPolicy { get; set; } = new WebhookRetryPolicy();

    /// <summary>
    ///     Security hardening for webhook callback endpoints (source IP allowlist,
    ///     suspicious-activity auto-blocking). See <see cref="WebhookSecuritySettings"/>.
    /// </summary>
    public WebhookSecuritySettings Security { get; set; } = new WebhookSecuritySettings();

    /// <summary>
    ///     Asynchronous retry worker for failed webhook inbox events (issue #396).
    /// </summary>
    public WebhookRetryWorkerSettings RetryWorker { get; set; } = new WebhookRetryWorkerSettings();
}

/// <summary>
///     Configuration for the hosted billing webhook retry worker that requeues failed
///     inbox events with exponential backoff.
/// </summary>
public class WebhookRetryWorkerSettings
{
    /// <summary>
    ///     Whether the retry worker runs. When disabled, retries stay manual
    ///     (POST /api/v{v}/billing/webhooks/webhook-events/{id}:retry).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Delay between worker polling cycles in seconds.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 30;

    /// <summary>
    ///     Maximum number of due events processed per polling cycle.
    /// </summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>
    ///     Hard ceiling on retry attempts honored by the worker, matching the
    ///     manual retry path (RetryWebhookEventHandler). Events at or beyond this
    ///     count are left for dead-letter inspection instead of being requeued.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 5;
}

/// <summary>
///     Retry policy configuration for webhook processing.
/// </summary>
public class WebhookRetryPolicy
{
    /// <summary>
    ///     Whether automatic retries are enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Initial delay between retries in seconds.
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 5;

    /// <summary>
    ///     Maximum delay between retries in seconds.
    /// </summary>
    public int MaxDelaySeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    ///     Multiplier for exponential backoff (e.g., 2.0 doubles the delay each retry).
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    ///     Whether to add jitter to retry delays to prevent thundering herd.
    /// </summary>
    public bool AddJitter { get; set; } = true;

    /// <summary>
    ///     Maximum jitter in seconds to add to retry delays.
    /// </summary>
    public int MaxJitterSeconds { get; set; } = 5;

    /// <summary>
    ///     Calculates the delay for a given retry attempt with exponential backoff.
    /// </summary>
    /// <param name="attemptNumber">The current attempt number (1-based)</param>
    /// <returns>Delay in seconds</returns>
    public int CalculateDelaySeconds(int attemptNumber)
    {
        if (attemptNumber <= 0)
        {
            return 0;
        }

        var baseDelay = InitialDelaySeconds * Math.Pow(BackoffMultiplier, attemptNumber - 1);
        var delay = Math.Min(baseDelay, MaxDelaySeconds);

        if (AddJitter)
        {
            var jitter = Random.Shared.Next(0, MaxJitterSeconds + 1);
            delay += jitter;
        }

        return (int)delay;
    }

    /// <summary>
    ///     Calculates the delay as TimeSpan for a given retry attempt.
    /// </summary>
    public TimeSpan CalculateDelay(int attemptNumber)
    {
        return TimeSpan.FromSeconds(CalculateDelaySeconds(attemptNumber));
    }
}
