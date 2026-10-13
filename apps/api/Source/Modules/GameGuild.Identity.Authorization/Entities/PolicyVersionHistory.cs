using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Which policy family a <see cref="PolicyVersionHistory"/> row snapshots.
/// </summary>
public enum PolicyVersionKind
{
    /// <summary>Unset — never persisted (fail closed on read).</summary>
    None = 0,

    /// <summary>A <see cref="ConditionalPolicy"/> snapshot.</summary>
    ConditionalPolicy = 1,

    /// <summary>A <see cref="PolicyDefinitionEntity"/> snapshot.</summary>
    PolicyDefinition = 2
}

/// <summary>
///     The mutation that produced a policy version snapshot.
/// </summary>
public enum PolicyVersionChangeType
{
    /// <summary>Unset — never persisted.</summary>
    None = 0,

    /// <summary>The policy was created; the snapshot captures its initial state.</summary>
    Created = 1,

    /// <summary>The policy was modified; the snapshot captures the post-change state.</summary>
    Updated = 2,

    /// <summary>The policy was deleted; the snapshot captures the last known state.</summary>
    Deleted = 3
}

/// <summary>
///     Append-only policy snapshot persisted on every permission-policy mutation
///     (issue #188: permission policy change tracking with complete version history).
/// </summary>
/// <remarks>
///     <para>
///         Rows are written inside the same unit of work as the policy mutation itself
///         (see <c>PolicyVersionSnapshotService</c>), so a snapshot exists for every
///         committed state. There is deliberately no update or delete path: the table is
///         the version history, and mutation of history would defeat its purpose.
///     </para>
///     <para>
///         <see cref="PolicyVersionHistoryRepository.ListAsync"/> returns versions ordered
///         ascending, and <see cref="PolicyVersionHistoryRepository.GetLatestVersionAsync"/>
///         resolves the next version number under the writing transaction's isolation.
///     </para>
/// </remarks>
[Table("PolicyVersionHistory")]
[Index(nameof(PolicyId), nameof(Version), IsUnique = true)]
[Index(nameof(PolicyId), Name = "IX_PolicyVersionHistory_PolicyId")]
[Index(nameof(TenantId), nameof(RecordedAt), Name = "IX_PolicyVersionHistory_Tenant_Time")]
public class PolicyVersionHistory
{
    /// <summary>
    ///     Row identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    ///     The identifier of the snapshotted policy (a <see cref="ConditionalPolicy"/> id
    ///     or a <see cref="PolicyDefinitionEntity"/> id).
    /// </summary>
    public Guid PolicyId { get; set; }

    /// <summary>
    ///     Which policy family the <see cref="PolicyId"/> belongs to.
    /// </summary>
    public PolicyVersionKind Kind { get; set; }

    /// <summary>
    ///     Tenant scope of the policy (null for global policies).
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    ///     Monotonically increasing per-policy version number, starting at 1.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    ///     The mutation that produced this snapshot.
    /// </summary>
    public PolicyVersionChangeType ChangeType { get; set; }

    /// <summary>
    ///     Full JSON snapshot of the policy state after the change (for deletes: the
    ///     last known state before removal).
    /// </summary>
    [Required]
    public string SnapshotJson { get; set; } = string.Empty;

    /// <summary>
    ///     The policy's display name captured with the snapshot (survives policy deletion).
    /// </summary>
    [MaxLength(200)]
    public string? PolicyName { get; set; }

    /// <summary>
    ///     Who performed the mutation, when known (system/sync mutations may be null).
    /// </summary>
    public Guid? ChangedBy { get; set; }

    /// <summary>
    ///     Free-form reason for the change, when supplied.
    /// </summary>
    [MaxLength(2000)]
    public string? Reason { get; set; }

    /// <summary>
    ///     When the mutation was recorded.
    /// </summary>
    public DateTime RecordedAt { get; set; } = SystemClock.UtcNow;
}
