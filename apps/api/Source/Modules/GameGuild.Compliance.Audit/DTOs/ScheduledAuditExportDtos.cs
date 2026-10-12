using System.ComponentModel.DataAnnotations;

namespace GameGuild.Compliance.Audit;

public sealed class CreateScheduledAuditExportRequest : IValidatableObject
{
    public Guid TenantId { get; set; }

    [Required, StringLength(100, MinimumLength = 1)]
    public string JobName { get; set; } = string.Empty;

    /// <summary>Five-field cron expression: minute, hour, day of month, month, and day of week.</summary>
    [Required, StringLength(100)]
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>Timezone identifier recognized by the API host, such as UTC or America/New_York.</summary>
    [Required, StringLength(80)]
    public string Timezone { get; set; } = "UTC";

    public ExportFormat ExportFormat { get; set; }

    [StringLength(100)]
    public string? ActionType { get; set; }

    /// <summary>Ordered CSV columns. Omit to include every supported audit field.</summary>
    public string[]? Columns { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public Guid? UserId { get; set; }

    public AuditRiskLevel? RiskLevel { get; set; }

    /// <summary>Number of days a generated file is retained. Execution history is retained.</summary>
    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 30;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TenantId == Guid.Empty)
        {
            yield return new ValidationResult("TenantId is required.", [nameof(TenantId)]);
        }

        if (ExportFormat is not (ExportFormat.Csv or ExportFormat.Json))
        {
            yield return new ValidationResult("Scheduled exports support CSV and JSON formats.", [nameof(ExportFormat)]);
        }

        if (ExportFormat == ExportFormat.Csv
            && !AuditCsvExporter.TryResolveColumns(Columns, out _, out var columnsError))
        {
            yield return new ValidationResult(columnsError, [nameof(Columns)]);
        }

        if (ExportFormat == ExportFormat.Json && Columns is { Length: > 0 })
        {
            yield return new ValidationResult("Column selection is only supported for CSV exports.", [nameof(Columns)]);
        }

        if (StartDate.HasValue && EndDate.HasValue && StartDate.Value > EndDate.Value)
        {
            yield return new ValidationResult("StartDate must not be later than EndDate.", [nameof(StartDate), nameof(EndDate)]);
        }
    }
}

public sealed record ScheduledAuditExportResponse(
    Guid Id,
    Guid TenantId,
    string JobName,
    string CronExpression,
    string Timezone,
    string Destination,
    ExportFormat ExportFormat,
    bool IsEnabled,
    DateTime? NextRunAt,
    DateTime? LastRunAt,
    int SuccessCount,
    int FailureCount,
    int RetentionDays,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record AuditExportHistoryResponse(
    Guid Id,
    Guid ScheduledExportId,
    DateTime ExecutedAt,
    ExportStatus Status,
    int RecordCount,
    long FileSizeBytes,
    string? FileName,
    bool FileAvailable,
    string? ErrorMessage,
    TimeSpan ExecutionDuration);

public sealed record StoredAuditExportFileReference(
    string BucketName,
    string ObjectKey,
    string FileName,
    string ContentType);

public interface IAuditScheduledExportStorage
{
    Task<string> StoreAsync(
        Guid tenantId,
        Stream content,
        string contentHash,
        string contentType,
        string fileName,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(Guid tenantId, string storageReference, CancellationToken cancellationToken);

    Task DeleteAsync(Guid tenantId, string storageReference, CancellationToken cancellationToken);
}
