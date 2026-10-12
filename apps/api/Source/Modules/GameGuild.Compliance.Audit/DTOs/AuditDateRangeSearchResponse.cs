namespace GameGuild.Compliance.Audit;

public sealed class AuditDateRangeSearchResponse
{
    public DateTimeOffset StartDateUtc { get; init; }

    public DateTimeOffset EndDateUtc { get; init; }

    public string TimeZoneId { get; init; } = "UTC";

    public AuditActivityBucketSize BucketSize { get; init; }

    public AuditLogResponse Results { get; init; } = new();

    public IReadOnlyList<AuditActivityBucketResponse> Activity { get; init; } = [];
}

public sealed class AuditActivityBucketResponse
{
    public DateTimeOffset StartUtc { get; init; }

    public DateTimeOffset StartLocal { get; init; }

    public int EventCount { get; init; }
}
