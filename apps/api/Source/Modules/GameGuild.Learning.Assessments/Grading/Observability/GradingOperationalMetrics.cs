using System.Diagnostics.Metrics;
using GameGuild.Learning.Assessments.Grading.Contracts;

namespace GameGuild.Learning.Assessments.Grading;

/// <summary>Operational signals for durable grading work and legitimate review waits.</summary>
public static class GradingOperationalMetrics
{
    public const string MeterName = "GameGuild.Learning.Assessments.Grading";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> ReviewWaits = Meter.CreateCounter<long>(
        "gameguild_grading_review_waits_total",
        "stages",
        "Review stages entering a legitimate evidence or instructor-resolution wait.");

    private static readonly Counter<long> OutboxConfirmed = Meter.CreateCounter<long>(
        "gameguild_grading_outbox_deliveries_confirmed_total",
        "deliveries",
        "Academic outbox deliveries confirmed by consumer.");

    private static readonly Counter<long> OutboxFailed = Meter.CreateCounter<long>(
        "gameguild_grading_outbox_deliveries_failed_total",
        "deliveries",
        "Academic outbox deliveries scheduled for retry after a technical failure.");

    private static readonly Counter<long> DeadlineResumes = Meter.CreateCounter<long>(
        "gameguild_grading_deadline_resumes_total",
        "executions",
        "Review executions resumed after an evidence deadline.");

    private static readonly Counter<long> ScheduledReleases = Meter.CreateCounter<long>(
        "gameguild_grading_scheduled_releases_total",
        "schedules",
        "Due grade-result release schedules processed by outcome.");

    public static void RecordReviewWait(ReviewMethod method, string state) =>
        ReviewWaits.Add(1, Tags(
            ("grading.review_method", method.ToString()),
            ("grading.wait_state", state)));

    public static void RecordOutboxConfirmed(string eventType, string consumerKey) =>
        OutboxConfirmed.Add(1, Tags(
            ("grading.event_type", eventType),
            ("grading.consumer_key", consumerKey)));

    public static void RecordOutboxFailure(string eventType, string consumerKey) =>
        OutboxFailed.Add(1, Tags(
            ("grading.event_type", eventType),
            ("grading.consumer_key", consumerKey)));

    public static void RecordDeadlineResume(bool succeeded) =>
        DeadlineResumes.Add(1, Tags(("grading.outcome", succeeded ? "succeeded" : "failed")));

    public static void RecordScheduledRelease(bool succeeded) =>
        ScheduledReleases.Add(1, Tags(("grading.outcome", succeeded ? "released" : "failed")));

    private static KeyValuePair<string, object?>[] Tags(params (string Key, string Value)[] values) =>
        values.Select(value => new KeyValuePair<string, object?>(value.Key, value.Value)).ToArray();
}
