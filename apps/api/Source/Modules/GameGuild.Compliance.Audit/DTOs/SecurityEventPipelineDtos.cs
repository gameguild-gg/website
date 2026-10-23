using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Compliance.Audit;

/// <summary>Response of <c>GET /api/audit/security-events/taxonomy</c>: the complete security event taxonomy.</summary>
public sealed record SecurityEventTaxonomyResponse
{
    [Required] public IReadOnlyList<SecurityEventTaxonomyEntry> Entries { get; init; } = [];

    public int TotalEntries { get; init; }

    public IReadOnlyList<string> Kinds { get; init; } = [];
}

/// <summary>Durable delivery status of the security event pipeline for the current instance.</summary>
public sealed record SecurityEventDeliveryStatusResponse
{
    public int SpooledEventCount { get; init; }

    public DateTime? OldestSpooledEventUtc { get; init; }

    public DateTime? LastDrainAttemptedAtUtc { get; init; }

    public DateTime? LastDrainSucceededAtUtc { get; init; }

    public string? LastDrainError { get; init; }

    public int DatabaseWriteAttemptsBeforeSpool { get; init; }

    public bool SpoolingEnabled { get; init; }
}

/// <summary>Query filters for security alerts.</summary>
public sealed record SecurityAlertListRequest
{
    [FromQuery(Name = "status")] public SecurityAlertStatus? Status { get; init; }

    [FromQuery(Name = "severity"), Range(0, 3)] public AuditRiskLevel? MinimumSeverity { get; init; }

    [FromQuery(Name = "kind")] public SecurityEventKind? Kind { get; init; }

    [FromQuery(Name = "ruleId"), MaxLength(100)] public string? RuleId { get; init; }

    [FromQuery(Name = "subjectUserId")] public Guid? SubjectUserId { get; init; }

    [FromQuery(Name = "skip"), Range(0, int.MaxValue)] public int Skip { get; init; }

    [FromQuery(Name = "take"), Range(1, 100)] public int Take { get; init; } = 25;
}

public sealed record SecurityAlertResponse(
    Guid Id,
    Guid? TenantId,
    string RuleId,
    SecurityEventKind Kind,
    AuditRiskLevel Severity,
    string Title,
    string Description,
    string SourceActionType,
    Guid? SourceAuditLogId,
    Guid? SubjectUserId,
    string? IpAddress,
    SecurityAlertStatus Status,
    int OccurrenceCount,
    DateTime FirstSeenAtUtc,
    DateTime LastSeenAtUtc,
    Guid? AcknowledgedByUserId,
    DateTime? AcknowledgedAtUtc,
    string? AcknowledgementNotes,
    Guid? ResolvedByUserId = null,
    DateTime? ResolvedAtUtc = null,
    string? ResolutionNotes = null);

/// <summary>Configures the retention policy applied to a tenant's security audit log.</summary>
public sealed record ConfigureSecurityLogRetentionRequest
{
    /// <summary>Revision the caller observed; rejected on mismatch to avoid lost updates.</summary>
    [Range(0, int.MaxValue)] public int ExpectedRevision { get; init; }

    [Range(30, 3650)] public int RetentionDays { get; init; } = 400;

    /// <summary>Per-category overrides. Keys are <c>AuditCategory</c> names; values are days (30..3650).</summary>
    [MaxLength(20)] public Dictionary<string, int>? CategoryOverrides { get; init; }

    /// <summary>
    ///     Storage-tier archival threshold (issue #193): audit rows older than this many days are moved
    ///     into the archive tier instead of staying hot. Null disables archival; must be at least 1 and
    ///     strictly shorter than <see cref="RetentionDays"/>.
    /// </summary>
    [Range(1, 3650)] public int? ArchiveAfterDays { get; init; }

    /// <summary>When set to a future instant, retention enforcement is suspended until then.</summary>
    public DateTime? LegalHoldUntilUtc { get; init; }
}

public sealed record SecurityLogRetentionPolicyResponse(
    Guid Id,
    Guid TenantId,
    int Revision,
    int RetentionDays,
    IReadOnlyDictionary<string, int> CategoryOverrides,
    DateTime? LegalHoldUntilUtc,
    Guid UpdatedByUserId,
    DateTime ConfiguredAtUtc,
    bool LegalHoldActive,
    int? ArchiveAfterDays = null);

public sealed record EnforceSecurityLogRetentionRequest
{
    /// <summary>When true, the pass only reports what would be deleted; no rows are removed.</summary>
    public bool DryRun { get; init; }
}

public sealed record SecurityLogRetentionExecutionResponse(
    Guid Id,
    Guid TenantId,
    DateTime ExecutedAtUtc,
    Guid? TriggeredByUserId,
    DateTime CutoffUtc,
    int PolicyRetentionDays,
    bool LegalHoldActive,
    bool DryRun,
    int DeletedCount,
    int EvaluatedCount,
    int ArchivedCount = 0);

/// <summary>One archived audit row served from the archive storage tier.</summary>
public sealed record ArchivedSecurityLogResponse(
    Guid Id,
    Guid SourceAuditLogId,
    string ActionType,
    string ResourceType,
    string? ResourceId,
    Guid? UserId,
    Guid? TenantId,
    string? Description,
    bool Success,
    string? ErrorMessage,
    AuditRiskLevel RiskLevel,
    AuditCategory Category,
    string? CorrelationId,
    DateTime OriginalCreatedAt,
    DateTime ArchivedAtUtc);

/// <summary>One page of archived audit rows plus the total archived count.</summary>
public sealed record ArchivedSecurityLogListResponse(
    IReadOnlyList<ArchivedSecurityLogResponse> Rows,
    int TotalCount);

/// <summary>
///     Storage-tier report for the tenant audit data (issue #193): row counts per real data-placement
///     tier plus the configured tier windows, so administrators can see cost placement while
///     accessibility is preserved by the archive endpoints.
/// </summary>
public sealed record SecurityLogStorageTierReportResponse(
    Guid TenantId,
    int HotRowCount,
    int ArchiveRowCount,
    int? ArchiveAfterDays,
    int RetentionDays,
    bool LegalHoldActive);

public sealed record SecurityLogRetentionExecutionListRequest
{
    [FromQuery(Name = "skip"), Range(0, int.MaxValue)] public int Skip { get; init; }

    [FromQuery(Name = "take"), Range(1, 100)] public int Take { get; init; } = 25;
}

public sealed record AcknowledgeSecurityAlertRequest
{
    [MaxLength(1000)] public string? Notes { get; init; }
}

/// <summary>Body of <c>POST /api/audit/security-events/alerts/{id}:resolve</c>.</summary>
public sealed record ResolveSecurityAlertRequest
{
    /// <summary>Resolution note recorded on the alert and in the resolution audit event.</summary>
    [MaxLength(1000)] public string? Notes { get; init; }
}
