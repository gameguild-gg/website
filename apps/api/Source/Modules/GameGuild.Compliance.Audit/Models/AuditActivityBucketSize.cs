namespace GameGuild.Compliance.Audit;

/// <summary>Resolution used to group audit activity over a date range.</summary>
public enum AuditActivityBucketSize
{
    Hourly = 0,
    Daily = 1
}

/// <summary>A count of audit records in a UTC time bucket.</summary>
public sealed record AuditActivityBucket(DateTime StartUtc, int EventCount);
