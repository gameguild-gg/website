using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Lifecycle status of a <see cref="PermissionApprovalWorkflow"/>.
/// </summary>
public enum PermissionApprovalStatus
{
    /// <summary>Awaiting decisions; at least one stage is still pending.</summary>
    Pending = 0,

    /// <summary>Every stage approved; the guarded permission mutation has been applied.</summary>
    Approved = 1,

    /// <summary>A stage was rejected; no permission mutation was applied.</summary>
    Rejected = 2,

    /// <summary>Withdrawn by the requester before completion; no mutation was applied.</summary>
    Cancelled = 3
}

/// <summary>
///     Lifecycle status of one <see cref="PermissionApprovalStage"/>.
/// </summary>
public enum PermissionApprovalStageStatus
{
    /// <summary>Awaiting its turn (earlier stages have not all approved yet).</summary>
    Pending = 0,

    /// <summary>Approved by an approver.</summary>
    Approved = 1,

    /// <summary>Rejected by an approver; the workflow terminates as rejected.</summary>
    Rejected = 2,

    /// <summary>Never decided because the workflow was cancelled or an earlier stage rejected.</summary>
    Skipped = 3
}

/// <summary>
///     A multi-stage permission approval workflow (issue #188: permission approval
///     workflow tracking including multi-stage approval processes).
/// </summary>
/// <remarks>
///     <para>
///         A workflow requests a set of tenant permissions for a subject user and lists
///         the ordered approval stages it must pass. Each stage decision is audited
///         individually (<see cref="PermissionOperationType.ApprovalStage"/> entries in
///         <see cref="PermissionAuditLog"/>); only after the final stage approves does the
///         workflow execute the actual grant through the existing guarded-mutation flow
///         (<see cref="IPermissionGrantService"/> — versioned, cache-invalidated and
///         audited), which records the terminal <see cref="PermissionOperationType.Grant"/>.
///     </para>
///     <para>
///         <b>Fail closed:</b> stages are decided strictly in order, a pending workflow
///         with zero stages can never complete, and the acting approver identity always
///         comes from <c>IActorContextAccessor</c>, never from the request body.
///     </para>
/// </remarks>
[Table("PermissionApprovalWorkflows")]
[Index(nameof(TenantId), nameof(Status), Name = "IX_PermissionApprovalWorkflows_Tenant_Status")]
[Index(nameof(SubjectUserId), Name = "IX_PermissionApprovalWorkflows_Subject")]
public class PermissionApprovalWorkflow : EntityBase
{
    /// <summary>
    ///     The user the permissions are requested for.
    /// </summary>
    public Guid SubjectUserId { get; set; }

    /// <summary>
    ///     The permission strings requested.
    /// </summary>
    [Column(TypeName = "text[]")]
    public string[] Permissions { get; set; } = Array.Empty<string>();

    /// <summary>
    ///     Justification supplied when the workflow was created.
    /// </summary>
    [MaxLength(2000)]
    public string? Reason { get; set; }

    /// <summary>
    ///     Who requested the workflow.
    /// </summary>
    public Guid RequestedBy { get; set; }

    /// <summary>
    ///     Current lifecycle status.
    /// </summary>
    public PermissionApprovalStatus Status { get; set; } = PermissionApprovalStatus.Pending;

    /// <summary>
    ///     Zero-based index of the stage currently awaiting a decision
    ///     (<c>Stages.Count</c> when the workflow is complete).
    /// </summary>
    public int CurrentStageIndex { get; set; }

    /// <summary>
    ///     Total number of stages (denormalized for reporting; equals <c>Stages.Count</c>).
    /// </summary>
    public int TotalStages { get; set; }

    /// <summary>
    ///     When the workflow reached a terminal status (approved/rejected/cancelled), if it has.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    ///     The ordered approval stages.
    /// </summary>
    public ICollection<PermissionApprovalStage> Stages { get; set; } = new List<PermissionApprovalStage>();
}

/// <summary>
///     One stage of a <see cref="PermissionApprovalWorkflow"/>.
/// </summary>
[Table("PermissionApprovalStages")]
[Index(nameof(WorkflowId), nameof(StageIndex), IsUnique = true)]
public class PermissionApprovalStage
{
    /// <summary>
    ///     Row identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    ///     The workflow this stage belongs to.
    /// </summary>
    public Guid WorkflowId { get; set; }

    /// <summary>
    ///     Zero-based execution order within the workflow.
    /// </summary>
    public int StageIndex { get; set; }

    /// <summary>
    ///     Human-readable stage name (e.g. "Line manager", "Security review").
    /// </summary>
    [MaxLength(200)]
    public string StageName { get; set; } = string.Empty;

    /// <summary>
    ///     When set, only this user may decide this stage (fail closed for everyone else).
    /// </summary>
    public Guid? RequiredApproverUserId { get; set; }

    /// <summary>
    ///     Stage lifecycle status.
    /// </summary>
    public PermissionApprovalStageStatus Status { get; set; } = PermissionApprovalStageStatus.Pending;

    /// <summary>
    ///     Who decided this stage, when decided.
    /// </summary>
    public Guid? DecidedBy { get; set; }

    /// <summary>
    ///     When the stage was decided.
    /// </summary>
    public DateTime? DecidedAt { get; set; }

    /// <summary>
    ///     Approver notes recorded with the decision.
    /// </summary>
    [MaxLength(2000)]
    public string? DecisionNotes { get; set; }
}
