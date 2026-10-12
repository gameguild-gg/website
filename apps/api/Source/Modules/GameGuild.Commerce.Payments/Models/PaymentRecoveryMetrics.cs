namespace GameGuild.Commerce.Payments;

/// <summary>
///     Payment success and failed-payment recovery metrics for an inclusive window (issue #403).
///     Powers <c>GET api/v{v}/payments/recovery-metrics</c>.
/// </summary>
/// <param name="FromUtc">Inclusive window start.</param>
/// <param name="ToUtc">Inclusive window end.</param>
/// <param name="TenantId">Tenant the metrics were scoped to, or null for all tenants.</param>
/// <param name="Attempts">First-attempt outcome counters and the overall success rate.</param>
/// <param name="Retries">Retry-recovery counters and recovered amounts per currency.</param>
/// <param name="Dunning">Dunning outcome counters for failed payments in the window.</param>
public sealed record PaymentRecoveryMetrics(
    DateTime FromUtc,
    DateTime ToUtc,
    Guid? TenantId,
    PaymentAttemptMetrics Attempts,
    PaymentRetryRecoveryMetrics Retries,
    PaymentDunningOutcomeMetrics Dunning);

/// <summary>First-attempt outcome counters for payments created in the window.</summary>
/// <param name="TotalPayments">Payments created in the window.</param>
/// <param name="SucceededPayments">Payments that reached <see cref="PaymentStatus.Succeeded" /> (including later refunded/disputed).</param>
/// <param name="FailedPayments">Payments currently in <see cref="PaymentStatus.Failed" />.</param>
/// <param name="SuccessRate">Succeeded / total as a percentage (0–100); 0 when there are no payments.</param>
public sealed record PaymentAttemptMetrics(
    int TotalPayments,
    int SucceededPayments,
    int FailedPayments,
    decimal SuccessRate);

/// <summary>Retry-recovery counters for payments that failed at least once.</summary>
/// <param name="EverFailedPayments">Payments that failed at least once (currently failed or with retry history).</param>
/// <param name="RetriedPayments">Payments with at least one retry attempt.</param>
/// <param name="RecoveredPayments">Payments that failed at least once and eventually succeeded.</param>
/// <param name="RetryRecoveryRate">Recovered / ever-failed as a percentage (0–100); 0 when none ever failed.</param>
/// <param name="RecoveredAmounts">Recovered charge amounts grouped by currency (mixed currencies are never summed into one figure).</param>
public sealed record PaymentRetryRecoveryMetrics(
    int EverFailedPayments,
    int RetriedPayments,
    int RecoveredPayments,
    decimal RetryRecoveryRate,
    IReadOnlyList<PaymentCurrencyAmount> RecoveredAmounts);

/// <summary>Dunning outcomes for failed payments in the window, as of the query execution time.</summary>
/// <param name="PendingRetryPayments">Failed payments with retries remaining and a future next-retry time (recovery in flight).</param>
/// <param name="DueForRetryPayments">Failed payments with retries remaining whose next-retry time has passed (retry-queue candidates).</param>
/// <param name="ExhaustedPayments">Failed payments with no retries left (dunning escalation terminal).</param>
public sealed record PaymentDunningOutcomeMetrics(
    int PendingRetryPayments,
    int DueForRetryPayments,
    int ExhaustedPayments);

/// <summary>An amount total for one currency; currency totals are kept separate (issue #404 mixed-currency rule).</summary>
public sealed record PaymentCurrencyAmount(string Currency, decimal Amount, int PaymentCount);
