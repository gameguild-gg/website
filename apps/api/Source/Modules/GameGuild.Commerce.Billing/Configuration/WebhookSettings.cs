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

    /// <summary>
    ///     Signing-secret rotation window in hours (issue #408): after a rotation is
    ///     recorded, the previous secret stays accepted for this long before it is
    ///     automatically deprecated. Default 24 hours.
    /// </summary>
    public int SecretRotationWindowHours { get; set; } = 24;
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
    ///     count are dead-lettered for inspection instead of being requeued.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>
    ///     Exponential backoff schedule honored by the retry worker, in minutes, indexed
    ///     by the failed row's attempt count (issue #408): a row that failed its Nth
    ///     attempt is due for requeue <c>RetryScheduleMinutes[N-1]</c> minutes after its
    ///     last state update. The default matches the issue's schedule of 1, 5, 15, 60
    ///     and 360 minutes.
    /// </summary>
    public int[] RetryScheduleMinutes { get; set; } = [1, 5, 15, 60, 360];

    /// <summary>
    ///     Terminal cadence, in minutes, applied to attempt counts beyond the end of
    ///     <see cref="RetryScheduleMinutes"/> (issue #408: 24 hours).
    /// </summary>
    public int TerminalRetryScheduleMinutes { get; set; } = 1440;
}

/// <summary>
///     Schedule math for the hosted billing webhook retry worker (issue #408).
/// </summary>
public static class WebhookRetrySchedule
{
    /// <summary>
    ///     Computes the backoff delay before a failed row's next requeue: a row that
    ///     failed its Nth attempt waits <c>scheduleMinutes[N-1]</c>; counts beyond the
    ///     schedule wait the terminal cadence.
    /// </summary>
    /// <param name="failedAttemptCount">The row's current processing-attempt count.</param>
    /// <param name="scheduleMinutes">Backoff schedule in minutes, one entry per attempt.</param>
    /// <param name="terminalMinutes">Cadence for attempt counts beyond the schedule.</param>
    /// <returns>Delay before the next requeue attempt.</returns>
    public static TimeSpan DelayFor(int failedAttemptCount, IReadOnlyList<int> scheduleMinutes, int terminalMinutes)
    {
        if (scheduleMinutes.Count == 0 || failedAttemptCount > scheduleMinutes.Count)
        {
            return TimeSpan.FromMinutes(Math.Max(0, terminalMinutes));
        }

        var index = Math.Max(0, failedAttemptCount - 1);
        return TimeSpan.FromMinutes(Math.Max(0, scheduleMinutes[index]));
    }
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
