using System.ComponentModel.DataAnnotations;

namespace GameGuild.Compliance.Audit;

public sealed class AuditExportRequest : IValidatableObject
{
    public Guid? UserId { get; set; }

    public Guid? TenantId { get; set; }

    public string? ActionType { get; set; }

    public string? ResourceType { get; set; }

    public AuditCategory? Category { get; set; }

    public AuditRiskLevel? RiskLevel { get; set; }

    public bool? Success { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? IpAddress { get; set; }

    /// <summary>Optional 1-based page number. Omit both pagination fields to export all matching rows.</summary>
    [Range(1, int.MaxValue)]
    public int? PageNumber { get; set; }

    /// <summary>Optional page size, capped at 1,000 rows.</summary>
    [Range(1, 1000)]
    public int? PageSize { get; set; }

    /// <summary>Ordered CSV columns. Omit to include every supported audit field.</summary>
    public string[]? Columns { get; set; }

    /// <summary>
    /// Optional HTTPS endpoint to notify when this export completes, fails, or is cancelled.
    /// Its exact host must be listed in <c>Audit:ExportWebhooks:AllowedHosts</c>; notifications are signed with
    /// <c>Audit:ExportWebhooks:SigningSecret</c> and include an idempotency key.
    /// </summary>
    [MaxLength(2048)]
    public string? WebhookUrl { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PageNumber.HasValue != PageSize.HasValue)
        {
            yield return new ValidationResult(
                "PageNumber and PageSize must be provided together.",
                [nameof(PageNumber), nameof(PageSize)]);
        }

        if (PageNumber.HasValue && PageSize.HasValue && PageNumber.Value > int.MaxValue / PageSize.Value)
        {
            yield return new ValidationResult(
                "The requested page offset is too large.",
                [nameof(PageNumber), nameof(PageSize)]);
        }

        if (StartDate.HasValue && EndDate.HasValue && StartDate.Value > EndDate.Value)
        {
            yield return new ValidationResult(
                "StartDate must be earlier than or equal to EndDate.",
                [nameof(StartDate), nameof(EndDate)]);
        }
    }
}
