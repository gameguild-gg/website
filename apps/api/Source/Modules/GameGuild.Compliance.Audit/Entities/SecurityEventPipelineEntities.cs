using System.Text.Json.Serialization;

namespace GameGuild.Compliance.Audit;

/// <summary>Status of a security alert raised by the security event pipeline.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SecurityAlertStatus>))]
public enum SecurityAlertStatus
{
    Open = 0,
    Acknowledged = 1,
    Resolved = 2
}

/// <summary>
///     A security alert raised by the security event pipeline. Alerts are the durable alerting
///     surface of security logging: every rule hit is persisted, deduplicated per rule and subject,
///     and stays queryable until acknowledged or resolved.
/// </summary>
public sealed class SecurityAlert : EntityBase
{
    public new Guid? TenantId { get; private set; }

    public string RuleId { get; private set; } = string.Empty;

    public SecurityEventKind Kind { get; private set; }

    public AuditRiskLevel Severity { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string SourceActionType { get; private set; } = string.Empty;

    public Guid? SourceAuditLogId { get; private set; }

    public Guid? SubjectUserId { get; private set; }

    public string? IpAddress { get; private set; }

    /// <summary>Stable key (rule + subject) used to deduplicate repeated rule hits into one open alert.</summary>
    public string DeduplicationKey { get; private set; } = string.Empty;

    public SecurityAlertStatus Status { get; private set; } = SecurityAlertStatus.Open;

    public int OccurrenceCount { get; private set; } = 1;

    public DateTime FirstSeenAtUtc { get; private set; }

    public DateTime LastSeenAtUtc { get; private set; }

    public Guid? AcknowledgedByUserId { get; private set; }

    public DateTime? AcknowledgedAtUtc { get; private set; }

    public string? AcknowledgementNotes { get; private set; }

    /// <summary>Administrator that resolved the alert; set when the alert reaches <see cref="SecurityAlertStatus.Resolved" />.</summary>
    public Guid? ResolvedByUserId { get; private set; }

    /// <summary>UTC instant at which the alert was resolved.</summary>
    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>Resolution note recorded by the resolving administrator.</summary>
    public string? ResolutionNotes { get; private set; }

    private SecurityAlert() { }

    public static SecurityAlert Raise(
        Guid? tenantId,
        string ruleId,
        SecurityEventKind kind,
        AuditRiskLevel severity,
        string title,
        string description,
        string sourceActionType,
        Guid? sourceAuditLogId,
        Guid? subjectUserId,
        string? ipAddress,
        DateTime detectedAtUtc)
    {
        return new SecurityAlert
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RuleId = ruleId,
            Kind = kind,
            Severity = severity,
            Title = title,
            Description = description,
            SourceActionType = sourceActionType,
            SourceAuditLogId = sourceAuditLogId,
            SubjectUserId = subjectUserId,
            IpAddress = ipAddress,
            DeduplicationKey = BuildDeduplicationKey(ruleId, tenantId, subjectUserId, ipAddress),
            Status = SecurityAlertStatus.Open,
            OccurrenceCount = 1,
            FirstSeenAtUtc = detectedAtUtc,
            LastSeenAtUtc = detectedAtUtc,
            CreatedAt = detectedAtUtc,
            UpdatedAt = detectedAtUtc
        };
    }

    /// <summary>Records another occurrence of the same rule hit on an already open alert.</summary>
    public void RecordOccurrence(DateTime seenAtUtc, Guid? sourceAuditLogId)
    {
        OccurrenceCount++;
        LastSeenAtUtc = seenAtUtc;
        SourceAuditLogId ??= sourceAuditLogId;
        UpdatedAt = seenAtUtc;
    }

    public void Acknowledge(Guid acknowledgedByUserId, string? notes, DateTime acknowledgedAtUtc)
    {
        if (Status == SecurityAlertStatus.Resolved)
        {
            throw new SecurityAlertTransitionException(Id, Status, SecurityAlertStatus.Acknowledged,
                "A resolved alert can no longer be acknowledged.");
        }

        Status = SecurityAlertStatus.Acknowledged;
        AcknowledgedByUserId = acknowledgedByUserId;
        AcknowledgedAtUtc = acknowledgedAtUtc;
        AcknowledgementNotes = notes;
        UpdatedAt = acknowledgedAtUtc;
    }

    /// <summary>
    ///     Resolves the alert (terminal lifecycle step). Valid from <see cref="SecurityAlertStatus.Open" />
    ///     and <see cref="SecurityAlertStatus.Acknowledged" />; resolving an already resolved alert is a
    ///     transition violation.
    /// </summary>
    public void Resolve(Guid resolvedByUserId, string? notes, DateTime resolvedAtUtc)
    {
        if (Status == SecurityAlertStatus.Resolved)
        {
            throw new SecurityAlertTransitionException(Id, Status, SecurityAlertStatus.Resolved,
                "The alert is already resolved.");
        }

        Status = SecurityAlertStatus.Resolved;
        ResolvedByUserId = resolvedByUserId;
        ResolvedAtUtc = resolvedAtUtc;
        ResolutionNotes = notes;
        UpdatedAt = resolvedAtUtc;
    }

    public static string BuildDeduplicationKey(string ruleId, Guid? tenantId, Guid? subjectUserId, string? ipAddress) =>
        $"{ruleId}|{tenantId?.ToString() ?? "-"}|{subjectUserId?.ToString() ?? "-"}|{ipAddress ?? "-"}";
}

/// <summary>
///     Raised when a security alert lifecycle transition is invalid (for example resolving an
///     already resolved alert). Mapped to HTTP 409 Conflict by the security event controller.
/// </summary>
public sealed class SecurityAlertTransitionException(
    Guid alertId,
    SecurityAlertStatus currentStatus,
    SecurityAlertStatus requestedStatus,
    string message) : InvalidOperationException(message)
{
    public Guid AlertId { get; } = alertId;

    public SecurityAlertStatus CurrentStatus { get; } = currentStatus;

    public SecurityAlertStatus RequestedStatus { get; } = requestedStatus;

    public IReadOnlyDictionary<string, string[]> Errors { get; } = new Dictionary<string, string[]>
    {
        ["Transition"] = [$"Cannot move alert {alertId} from {currentStatus} to {requestedStatus}: {message}"]
    };
}

/// <summary>
///     Retention policy applied to a tenant's security audit log (the <c>AuditLogs</c> table).
///     One row per tenant; enforcement deletes audit log rows older than the applicable retention
///     window unless a legal hold is active. Retention only ever shortens storage lifetime when a
///     policy explicitly exists — tenants without a policy row keep every record.
/// </summary>
public sealed class SecurityLogRetentionPolicy : EntityBase
{
    public new Guid TenantId { get; private set; }

    /// <summary>Optimistic revision, incremented on every configuration change.</summary>
    public int Revision { get; private set; }

    public int RetentionDays { get; private set; }

    /// <summary>Optional per-category retention overrides stored as <c>AuditCategory</c> name to days JSON.</summary>
    public string? CategoryOverridesJson { get; private set; }

    /// <summary>When set to a future instant, retention enforcement is suspended for this tenant.</summary>
    public DateTime? LegalHoldUntilUtc { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTime ConfiguredAtUtc { get; private set; }

    private SecurityLogRetentionPolicy() { }

    public static SecurityLogRetentionPolicy Create(
        Guid tenantId,
        int retentionDays,
        string? categoryOverridesJson,
        DateTime? legalHoldUntilUtc,
        Guid updatedByUserId,
        DateTime configuredAtUtc)
    {
        return new SecurityLogRetentionPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Revision = 1,
            RetentionDays = retentionDays,
            CategoryOverridesJson = categoryOverridesJson,
            LegalHoldUntilUtc = legalHoldUntilUtc,
            UpdatedByUserId = updatedByUserId,
            ConfiguredAtUtc = configuredAtUtc,
            CreatedAt = configuredAtUtc,
            UpdatedAt = configuredAtUtc
        };
    }

    public void Update(
        int retentionDays,
        string? categoryOverridesJson,
        DateTime? legalHoldUntilUtc,
        Guid updatedByUserId,
        DateTime configuredAtUtc)
    {
        Revision++;
        RetentionDays = retentionDays;
        CategoryOverridesJson = categoryOverridesJson;
        LegalHoldUntilUtc = legalHoldUntilUtc;
        UpdatedByUserId = updatedByUserId;
        ConfiguredAtUtc = configuredAtUtc;
        UpdatedAt = configuredAtUtc;
    }
}

/// <summary>Result of one security log retention enforcement pass for one tenant.</summary>
public sealed class SecurityLogRetentionExecution : EntityBase
{
    public new Guid TenantId { get; private set; }

    public DateTime ExecutedAtUtc { get; private set; }

    /// <summary>Null when the pass ran from the background enforcement service.</summary>
    public Guid? TriggeredByUserId { get; private set; }

    public DateTime CutoffUtc { get; private set; }

    public int PolicyRetentionDays { get; private set; }

    public bool LegalHoldActive { get; private set; }

    public bool DryRun { get; private set; }

    public int DeletedCount { get; private set; }

    public int EvaluatedCount { get; private set; }

    private SecurityLogRetentionExecution() { }

    public static SecurityLogRetentionExecution Record(
        Guid tenantId,
        DateTime executedAtUtc,
        Guid? triggeredByUserId,
        DateTime cutoffUtc,
        int policyRetentionDays,
        bool legalHoldActive,
        bool dryRun,
        int deletedCount,
        int evaluatedCount)
    {
        return new SecurityLogRetentionExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ExecutedAtUtc = executedAtUtc,
            TriggeredByUserId = triggeredByUserId,
            CutoffUtc = cutoffUtc,
            PolicyRetentionDays = policyRetentionDays,
            LegalHoldActive = legalHoldActive,
            DryRun = dryRun,
            DeletedCount = deletedCount,
            EvaluatedCount = evaluatedCount,
            CreatedAt = executedAtUtc,
            UpdatedAt = executedAtUtc
        };
    }
}
