namespace GameGuild.Compliance.Audit;

/// <summary>Tenant configuration with optimistic revision checking.</summary>
public sealed class AuditRetentionConfiguration : EntityBase
{
    public int Revision { get; set; }
    public Guid UpdatedByUserId { get; set; }
    public string ConfigurationJson { get; set; } = string.Empty;
}

/// <summary>Saved simulation evidence and assumptions. Runs are append-only through the public service.</summary>
public sealed class AuditRetentionSimulationRun : EntityBase
{
    public Guid CreatedByUserId { get; set; }
    public int ConfigurationRevision { get; set; }
    public string ConfigurationJson { get; set; } = string.Empty;
    public string RequestJson { get; set; } = string.Empty;
    public string ReportJson { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public int ForecastMonths { get; set; }
    public decimal BaselineTotalCost { get; set; }
    public decimal? RecommendedTotalCost { get; set; }
}

/// <summary>Aggregated actual reads by record date. No record content or personal identifiers are copied.</summary>
public sealed class AuditDataAccessObservation : EntityBase
{
    public DateOnly RecordDateUtc { get; set; }
    public long ReadCount { get; set; }
}
