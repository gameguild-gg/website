using GameGuild.CQRS.Models;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Persistence surface for multi-stage permission approval workflows (issue #188).
/// </summary>
public interface IPermissionApprovalRepository
{
    /// <summary>
    ///     Loads a workflow with its ordered stages (null when not found).
    /// </summary>
    Task<PermissionApprovalWorkflow?> GetWithStagesAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a workflow (with its stages) to the unit of work.
    /// </summary>
    void Add(PermissionApprovalWorkflow workflow);

    /// <summary>
    ///     Commits the unit of work.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     EF implementation of the permission approval workflow store.
/// </summary>
public class PermissionApprovalRepository(IApplicationDbContext context) : IPermissionApprovalRepository
{
    /// <inheritdoc />
    public async Task<PermissionApprovalWorkflow?> GetWithStagesAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
        => await context.Set<PermissionApprovalWorkflow>()
            .Include(workflow => workflow.Stages)
            .FirstOrDefaultAsync(workflow => workflow.Id == workflowId, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public void Add(PermissionApprovalWorkflow workflow) => context.Set<PermissionApprovalWorkflow>().Add(workflow);

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}

/// <summary>
///     One stage definition supplied when creating an approval workflow.
/// </summary>
public sealed record PermissionApprovalStageDefinition(
    string StageName,
    Guid? RequiredApproverUserId = null);

/// <summary>
///     Result of deciding the current stage of an approval workflow.
/// </summary>
public sealed record PermissionApprovalDecisionResult(
    Guid WorkflowId,
    PermissionApprovalStatus WorkflowStatus,
    int DecidedStageIndex,
    string DecidedStageName,
    bool Approved,
    string? Notes,
    bool GrantApplied);

/// <summary>
///     Multi-stage permission approval workflow service (issue #188: permission approval
///     workflow tracking including multi-stage approval processes).
/// </summary>
/// <remarks>
///     <para>
///         Every stage decision is audited as an individual <see cref="PermissionAuditLog"/>
///         entry (<see cref="PermissionOperationType.ApprovalStage"/>). The permission
///         grant itself is only executed after the final stage approves, through the
///         existing guarded-mutation flow (<see cref="IPermissionGrantService"/>), which
///         adds its own versioned, cache-invalidating, audited grant entry.
///     </para>
///     <para>
///         <b>Fail closed:</b> stages decide strictly in order; a stage with a required
///         approver can only be decided by that approver; a rejected stage terminates the
///         workflow with no grant; the acting approver identity always comes from
///         <c>IActorContextAccessor</c>, never from the request body.
///     </para>
/// </remarks>
public interface IPermissionApprovalService
{
    /// <summary>
    ///     Creates a pending workflow with the supplied ordered stages (at least one).
    ///     The creation itself is audited as a <see cref="PermissionOperationType.Review"/>
    ///     entry; no permission mutation happens yet.
    /// </summary>
    Task<PermissionApprovalWorkflow> CreateAsync(
        Guid? tenantId,
        Guid subjectUserId,
        IReadOnlyList<string> permissions,
        IReadOnlyList<PermissionApprovalStageDefinition> stages,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Decides the workflow's current stage. Audits the stage decision; on the final
    ///     approval executes the guarded grant and audits it.
    /// </summary>
    Task<PermissionApprovalDecisionResult> DecideAsync(
        Guid workflowId,
        bool approve,
        string? notes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads a workflow with its stages (null when not found).
    /// </summary>
    Task<PermissionApprovalWorkflow?> GetAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);
}
