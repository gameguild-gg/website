using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Durable row form of a <see cref="PermissionEvaluationRecord"/> (issues #359, #358).
///     Written by <see cref="PermissionEvaluationLogEntryRepository"/> through the standard
///     <see cref="IPermissionEvaluationLogSink"/> fan-out and consumed by the permission
///     compliance reporting service. Records are facts about authorization decisions;
///     they never alter the decision they describe.
/// </summary>
[Table("PermissionEvaluationLogs")]
[Index(nameof(TenantId), nameof(EvaluatedAtUtc), Name = "IX_PermissionEvaluationLogs_Tenant_Time")]
[Index(nameof(EvaluatedAtUtc), Name = "IX_PermissionEvaluationLogs_Time")]
[Index(nameof(Outcome), Name = "IX_PermissionEvaluationLogs_Outcome")]
public class PermissionEvaluationLogEntry
{
    /// <summary>Row identifier.</summary>
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tenant scope the evaluation ran in (null for contextless evaluations).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Actor whose permissions were evaluated.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Resource type identifier, for example <c>Project</c>.</summary>
    [MaxLength(200)]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>Optional resource identifier the permission was evaluated against.</summary>
    [MaxLength(200)]
    public string? ResourceId { get; set; }

    /// <summary>Permission set the evaluation required (PostgreSQL native array).</summary>
    [Column(TypeName = "text[]")]
    public string[] RequiredPermissions { get; set; } = Array.Empty<string>();

    /// <summary>Combined outcome of the evaluation.</summary>
    public PermissionEvaluationOutcome Outcome { get; set; }

    /// <summary>Stable source tag identifying the calling surface, for example <c>graphql</c>.</summary>
    [MaxLength(100)]
    public string Source { get; set; } = string.Empty;

    /// <summary>Optional operation or field name that triggered the evaluation.</summary>
    [MaxLength(200)]
    public string? Operation { get; set; }

    /// <summary>Optional machine-readable reason code.</summary>
    [MaxLength(200)]
    public string? Reason { get; set; }

    /// <summary>UTC timestamp of the evaluation.</summary>
    public DateTime EvaluatedAtUtc { get; set; } = SystemClock.UtcNow;

    /// <summary>Creates a row from a <see cref="PermissionEvaluationRecord"/>.</summary>
    public static PermissionEvaluationLogEntry FromRecord(PermissionEvaluationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new PermissionEvaluationLogEntry
        {
            TenantId = record.TenantId,
            UserId = record.UserId,
            ResourceType = record.ResourceType ?? string.Empty,
            ResourceId = record.ResourceId,
            RequiredPermissions = record.RequiredPermissions.ToArray(),
            Outcome = record.Outcome,
            Source = record.Source ?? string.Empty,
            Operation = record.Operation,
            Reason = record.Reason,
            EvaluatedAtUtc = record.EvaluatedAtUtc == default ? SystemClock.UtcNow : record.EvaluatedAtUtc
        };
    }

    /// <summary>Reconstructs the record view of this row.</summary>
    public PermissionEvaluationRecord ToRecord()
        => new(
            UserId,
            TenantId,
            ResourceType,
            ResourceId,
            RequiredPermissions,
            Outcome,
            Source,
            Operation,
            Reason,
            EvaluatedAtUtc);
}
