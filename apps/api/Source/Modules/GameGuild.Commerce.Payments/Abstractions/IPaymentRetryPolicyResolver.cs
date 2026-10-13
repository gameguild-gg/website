namespace GameGuild.Commerce.Payments;

/// <summary>
///     How the next retry attempt is scheduled after a failure (issue #415).
/// </summary>
public enum PaymentRetryScheduleKind
{
    /// <summary>Exponential backoff resolved from <c>Payments:Retry</c> (default for unclassified codes).</summary>
    ConfiguredExponentialBackoff,

    /// <summary>Fixed ladder of delays chosen by failure-code class (e.g. insufficient funds → 3d/7d/14d).</summary>
    FailureCodeLadder,

    /// <summary>Hard decline: no further retries are scheduled.</summary>
    NonRetriable
}

/// <summary>Resolved retry policy for one gateway failure code (issue #415).</summary>
/// <param name="IsRetriable">Whether the failure class permits further retry attempts at all.</param>
/// <param name="ScheduleKind">Which schedule drives the delays between attempts.</param>
/// <param name="RetryDelays">
///     Delays before each subsequent retry attempt, indexed by the payment's current
///     <see cref="Payment.RetryCount" /> (delay[0] applies after the initial failure).
///     Empty for non-retriable classes.
/// </param>
/// <param name="Classification">Stable classifier name for logging and metrics.</param>
public sealed record PaymentRetryPolicy(
    bool IsRetriable,
    PaymentRetryScheduleKind ScheduleKind,
    IReadOnlyList<TimeSpan> RetryDelays,
    string Classification)
{
    /// <summary>Convenience for hard-decline policies.</summary>
    public static PaymentRetryPolicy NonRetriable(string classification) =>
        new(false, PaymentRetryScheduleKind.NonRetriable, [], classification);
}

/// <summary>
///     The schedule for the next retry attempt after a failure, computed from the failure-code
///     policy and the <c>Payments:Retry</c> options (issue #415).
/// </summary>
/// <param name="IsRetriable">Whether further retry attempts are permitted.</param>
/// <param name="NextRetryAt">
///     When the next retry becomes eligible (with jitter applied), or <c>null</c> when the
///     payment must leave the retry queue (non-retriable failure or budget exhausted).
/// </param>
/// <param name="EffectiveMaxRetries">
///     Retry budget that applies to this payment: the configured maximum, capped by the
///     failure-code ladder length when the ladder is shorter.
/// </param>
/// <param name="Classification">Stable classifier name for logging and metrics.</param>
public sealed record PaymentRetrySchedule(
    bool IsRetriable,
    DateTime? NextRetryAt,
    int EffectiveMaxRetries,
    string Classification);

/// <summary>
///     Maps gateway failure codes to retry behavior (issue #415): hard declines
///     (expired card, incorrect CVC, fraud, ...) are non-retriable, soft declines follow
///     failure-code-specific retry ladders, and unclassified codes fall back to the
///     configurable exponential backoff from <c>Payments:Retry</c>. Injected at the handler
///     layer; the payment entity stays free of policy logic.
/// </summary>
public interface IPaymentRetryPolicyResolver
{
    /// <summary>Classifies a gateway failure code into a retry policy.</summary>
    /// <param name="failureCode">Gateway failure code (case-insensitive; whitespace tolerated).</param>
    PaymentRetryPolicy Resolve(string? failureCode);

    /// <summary>
    ///     Computes the retry schedule that applies after a failure with the given code:
    ///     retriability, the jittered next-retry time (or <c>null</c> to leave the queue) and
    ///     the effective retry budget.
    /// </summary>
    /// <param name="failureCode">Gateway failure code.</param>
    /// <param name="currentRetryCount">The payment's retry count after the failing attempt.</param>
    /// <param name="asOfUtc">Timestamp the schedule is computed against.</param>
    PaymentRetrySchedule ComputeSchedule(string? failureCode, int currentRetryCount, DateTime? asOfUtc = null);
}
