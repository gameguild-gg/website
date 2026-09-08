using System.Diagnostics.Metrics;

namespace GameGuild.API.Eventing;

public static class EventTransportMetrics
{
    public const string MeterName = "GameGuild.EventTransport";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> MessagesClaimed = Meter.CreateCounter<long>(
        "gameguild_outbox_messages_claimed_total",
        "messages",
        "Outbox messages claimed by dispatch cycles.");
    private static readonly Counter<long> ConsumerCompletions = Meter.CreateCounter<long>(
        "gameguild_outbox_consumer_completions_total",
        "deliveries",
        "Completed outbox consumer deliveries.");
    private static readonly Counter<long> ConsumerFailures = Meter.CreateCounter<long>(
        "gameguild_outbox_consumer_failures_total",
        "deliveries",
        "Failed outbox consumer deliveries.");
    private static readonly Counter<long> DeadLetters = Meter.CreateCounter<long>(
        "gameguild_outbox_dead_letters_total",
        "deliveries",
        "Outbox consumer deliveries moved to dead letter.");
    private static readonly Histogram<double> DispatchDuration = Meter.CreateHistogram<double>(
        "gameguild_outbox_dispatch_duration_ms",
        "ms",
        "Duration of an outbox dispatch cycle.");

    public static void RecordDispatchCycle(int claimedMessages, TimeSpan duration)
    {
        if (claimedMessages > 0)
            MessagesClaimed.Add(claimedMessages);

        DispatchDuration.Record(duration.TotalMilliseconds);
    }

    public static void RecordConsumerCompletion(string eventName, string consumerName) =>
        ConsumerCompletions.Add(1, DeliveryTags(eventName, consumerName));

    public static void RecordConsumerFailure(string eventName, string consumerName, bool deadLettered)
    {
        var tags = DeliveryTags(eventName, consumerName);
        ConsumerFailures.Add(1, tags);
        if (deadLettered)
            DeadLetters.Add(1, tags);
    }

    private static KeyValuePair<string, object?>[] DeliveryTags(string eventName, string consumerName) =>
    [
        new("event.name", eventName),
        new("consumer.name", consumerName)
    ];
}
