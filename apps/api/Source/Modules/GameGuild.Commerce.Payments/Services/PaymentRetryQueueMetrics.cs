using System.Diagnostics.Metrics;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     OpenTelemetry metrics for the automated payment retry queue (issue #415), following the
///     billing webhook retry worker metric conventions (GameGuild.Billing.WebhookRetry).
/// </summary>
public static class PaymentRetryQueueMetrics
{
    public const string MeterName = "GameGuild.Payments.RetryQueue";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> RetriesDispatched = Meter.CreateCounter<long>(
        "gameguild_payments_retry_queue_dispatched_total",
        "payments",
        "Failed payments dispatched for retry by the automated retry queue.");

    private static readonly Counter<long> Recoveries = Meter.CreateCounter<long>(
        "gameguild_payments_retry_queue_recovered_total",
        "payments",
        "Failed payments recovered by a successful retry (any dispatch path).");

    private static readonly Counter<long> Exhausted = Meter.CreateCounter<long>(
        "gameguild_payments_retry_queue_exhausted_total",
        "payments",
        "Payments whose retry budget was exhausted, triggering a dunning suspension.");

    private static readonly Histogram<double> CycleDuration = Meter.CreateHistogram<double>(
        "gameguild_payments_retry_queue_cycle_duration_ms",
        "ms",
        "Duration of a payment retry queue cycle.");

    public static void RecordDispatched(string provider) =>
        RetriesDispatched.Add(1, [new KeyValuePair<string, object?>("payments.provider", provider)]);

    public static void RecordRecovery(string provider) =>
        Recoveries.Add(1, [new KeyValuePair<string, object?>("payments.provider", provider)]);

    public static void RecordExhausted(string provider) =>
        Exhausted.Add(1, [new KeyValuePair<string, object?>("payments.provider", provider)]);

    public static void RecordCycle(TimeSpan duration) =>
        CycleDuration.Record(duration.TotalMilliseconds);
}
