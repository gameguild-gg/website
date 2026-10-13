using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Failure-code retry policy resolver (issue #415).
///
///     Classification follows the internal taxonomy from issue #415:
///     <list type="bullet">
///         <item>Hard declines — expired card, incorrect CVC/number, unsupported card, fraud,
///         stolen card — are non-retriable immediately: charging the same instrument again
///         cannot succeed.</item>
///         <item>Insufficient funds follows the 3d/7d/14d recovery ladder (payday realignment).</item>
///         <item>Issuer refusals (<c>do_not_honor</c>) follow a 1d/7d ladder.</item>
///         <item>Processing/transient errors retry on the fast 0/1/2/3d ladder.</item>
///         <item>Everything else — including generic <c>card_declined</c>, which is a decline and
///         not a transient error — uses the configurable exponential backoff from
///         <c>Payments:Retry</c>, with jitter applied to avoid thundering-herd bursts.</item>
///     </list>
/// </summary>
public sealed class PaymentRetryPolicyResolver(
    IOptions<PaymentRetryOptions> retryOptions) : IPaymentRetryPolicyResolver
{
    /// <summary>Hard declines: retrying the same instrument cannot succeed.</summary>
    private static readonly HashSet<string> NonRetriableCodes = new(StringComparer.Ordinal)
    {
        "expired_card",
        "card_expired",
        "incorrect_cvc",
        "invalid_cvc",
        "cvc_check_failed",
        "incorrect_number",
        "invalid_number",
        "invalid_card_number",
        "card_number_invalid",
        "card_not_supported",
        "fraudulent",
        "fraud_detected",
        "card_declined_fraud",
        "stolen_card",
        "test_mode_decline_card"
    };

    private static readonly TimeSpan[] InsufficientFundsLadder =
    [
        TimeSpan.FromDays(3),
        TimeSpan.FromDays(7),
        TimeSpan.FromDays(14)
    ];

    private static readonly TimeSpan[] DoNotHonorLadder =
    [
        TimeSpan.FromDays(1),
        TimeSpan.FromDays(7)
    ];

    private static readonly TimeSpan[] ProcessingErrorLadder =
    [
        TimeSpan.Zero,
        TimeSpan.FromDays(1),
        TimeSpan.FromDays(2),
        TimeSpan.FromDays(3)
    ];

    /// <inheritdoc />
    public PaymentRetryPolicy Resolve(string? failureCode)
    {
        var normalized = Normalize(failureCode);

        if (normalized.Length == 0)
        {
            return new PaymentRetryPolicy(
                true,
                PaymentRetryScheduleKind.ConfiguredExponentialBackoff,
                [],
                "unclassified");
        }

        if (NonRetriableCodes.Contains(normalized))
        {
            return PaymentRetryPolicy.NonRetriable("hard_decline");
        }

        if (normalized == "insufficient_funds")
        {
            return new PaymentRetryPolicy(
                true,
                PaymentRetryScheduleKind.FailureCodeLadder,
                InsufficientFundsLadder,
                "insufficient_funds");
        }

        if (normalized == "do_not_honor")
        {
            return new PaymentRetryPolicy(
                true,
                PaymentRetryScheduleKind.FailureCodeLadder,
                DoNotHonorLadder,
                "do_not_honor");
        }

        if (normalized is "processing_error" or "temporarily_unavailable" or "try_again_later")
        {
            return new PaymentRetryPolicy(
                true,
                PaymentRetryScheduleKind.FailureCodeLadder,
                ProcessingErrorLadder,
                "processing_error");
        }

        // Generic card_declined and unknown provider codes: configured exponential backoff.
        // A decline is not a transient error, so it deliberately does not get the fast
        // processing-error ladder — but it remains retriable because declines are frequently
        // reversible (funds arriving, issuer throttling, ...).
        return new PaymentRetryPolicy(
            true,
            PaymentRetryScheduleKind.ConfiguredExponentialBackoff,
            [],
            normalized == "card_declined" ? "card_declined" : "unclassified");
    }

    /// <inheritdoc />
    public PaymentRetrySchedule ComputeSchedule(string? failureCode, int currentRetryCount, DateTime? asOfUtc = null)
    {
        if (currentRetryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentRetryCount), "Retry count cannot be negative.");
        }

        var options = retryOptions.Value;
        var policy = Resolve(failureCode);
        var now = asOfUtc ?? SystemClock.UtcNow;

        if (!policy.IsRetriable)
        {
            return new PaymentRetrySchedule(false, null, options.MaxRetries, policy.Classification);
        }

        if (policy.ScheduleKind == PaymentRetryScheduleKind.FailureCodeLadder)
        {
            var effectiveMaxRetries = Math.Min(options.MaxRetries, policy.RetryDelays.Count);
            if (currentRetryCount >= effectiveMaxRetries || currentRetryCount >= policy.RetryDelays.Count)
            {
                return new PaymentRetrySchedule(true, null, effectiveMaxRetries, policy.Classification);
            }

            var nextRetryAt = AddDelay(now, policy.RetryDelays[currentRetryCount], options.JitterRatio);
            return new PaymentRetrySchedule(true, nextRetryAt, effectiveMaxRetries, policy.Classification);
        }

        if (currentRetryCount >= options.MaxRetries)
        {
            return new PaymentRetrySchedule(true, null, options.MaxRetries, policy.Classification);
        }

        var backoffDelay = TimeSpan.FromMinutes(options.ComputeBackoffDelayMinutes(currentRetryCount));
        var jitteredRetryAt = AddDelay(now, backoffDelay, options.JitterRatio);
        return new PaymentRetrySchedule(true, jitteredRetryAt, options.MaxRetries, policy.Classification);
    }

    private static DateTime AddDelay(DateTime fromUtc, TimeSpan delay, double jitterRatio)
    {
        var jitter = delay * (jitterRatio * Random.Shared.NextDouble());
        return fromUtc + delay + jitter;
    }

    private static string Normalize(string? failureCode)
    {
        if (string.IsNullOrWhiteSpace(failureCode))
        {
            return string.Empty;
        }

        return failureCode.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}
