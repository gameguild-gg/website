using System.Diagnostics.Metrics;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     OpenTelemetry metrics for the billing webhook retry worker, following the
///     platform event-transport metric conventions (GameGuild.EventTransport).
/// </summary>
public static class BillingWebhookRetryMetrics
{
    public const string MeterName = "GameGuild.Billing.WebhookRetry";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> RetriesScheduled = Meter.CreateCounter<long>(
        "gameguild_billing_webhook_retries_scheduled_total",
        "retries",
        "Failed webhook inbox events requeued for retry by the retry worker.");

    private static readonly Counter<long> RetriesExhausted = Meter.CreateCounter<long>(
        "gameguild_billing_webhook_retries_exhausted_total",
        "events",
        "Failed webhook inbox events that reached the retry attempt ceiling.");

    private static readonly Histogram<double> CycleDuration = Meter.CreateHistogram<double>(
        "gameguild_billing_webhook_retry_cycle_duration_ms",
        "ms",
        "Duration of a billing webhook retry worker cycle.");

    public static void RecordRetryScheduled(string provider) =>
        RetriesScheduled.Add(1, [new KeyValuePair<string, object?>("billing.provider", provider)]);

    public static void RecordRetryExhausted(string provider) =>
        RetriesExhausted.Add(1, [new KeyValuePair<string, object?>("billing.provider", provider)]);

    public static void RecordCycle(TimeSpan duration) =>
        CycleDuration.Record(duration.TotalMilliseconds);
}
