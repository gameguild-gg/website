using System.Text.Json;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Default <see cref="IPermissionApprovalService"/> implementation: ordered
///     multi-stage approval workflows whose every stage decision is individually audited
///     and whose final approval executes the guarded permission grant (issue #188).
/// </summary>
public sealed class PermissionApprovalService(
    IPermissionApprovalRepository repository,
    IPermissionGrantService grantService,
    IPermissionAuditService auditService,
    IActorContextAccessor actorContextAccessor,
    ILogger<PermissionApprovalService> logger) : IPermissionApprovalService
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <inheritdoc />
    public async Task<PermissionApprovalWorkflow> CreateAsync(
        Guid? tenantId,
        Guid subjectUserId,
        IReadOnlyList<string> permissions,
        IReadOnlyList<PermissionApprovalStageDefinition> stages,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (subjectUserId == Guid.Empty)
        {
            throw new ArgumentException("A subject user id is required.", nameof(subjectUserId));
        }

        if (permissions.Count == 0)
        {
            throw new ArgumentException("At least one permission is required.", nameof(permissions));
        }

        if (stages.Count == 0)
        {
            throw new ArgumentException(
                "At least one approval stage is required (fail closed: an unguarded grant needs no workflow).",
                nameof(stages));
        }

        foreach (var stage in stages)
        {
            if (string.IsNullOrWhiteSpace(stage.StageName))
            {
                throw new ArgumentException("Every approval stage needs a name.", nameof(stages));
            }
        }

        var actorId = Actor.SubjectIdAsGuid ?? throw new UnauthorizedAccessException(
            "Creating a permission approval workflow requires an authenticated actor.");

        var workflow = new PermissionApprovalWorkflow
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubjectUserId = subjectUserId,
            Permissions = permissions.ToArray(),
            Reason = reason,
            RequestedBy = actorId,
            Status = PermissionApprovalStatus.Pending,
            CurrentStageIndex = 0,
            TotalStages = stages.Count
        };

        foreach (var (index, stage) in stages.Index())
        {
            workflow.Stages.Add(new PermissionApprovalStage
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                StageIndex = index,
                StageName = stage.StageName,
                RequiredApproverUserId = stage.RequiredApproverUserId,
                Status = PermissionApprovalStageStatus.Pending
            });
        }

        repository.Add(workflow);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.Review,
            userId: subjectUserId,
            performedBy: actorId,
            tenantId: tenantId,
            permissionType: string.Join(",", workflow.Permissions),
            resourceId: workflow.Id,
            resourceType: "PermissionApproval",
            oldValue: null,
            newValue: JsonSerializer.Serialize(new
            {
                workflow.Id,
                workflow.SubjectUserId,
                workflow.Permissions,
                workflow.TotalStages,
                Stages = workflow.Stages
                    .OrderBy(stage => stage.StageIndex)
                    .Select(stage => new { stage.StageIndex, stage.StageName, stage.RequiredApproverUserId })
            }),
            reason: reason ?? "Multi-stage permission approval workflow created.",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Created permission approval workflow {WorkflowId} with {StageCount} stage(s) for subject {SubjectUserId}",
            workflow.Id,
            workflow.TotalStages,
            subjectUserId);

        return workflow;
    }

    /// <inheritdoc />
    public async Task<PermissionApprovalDecisionResult> DecideAsync(
        Guid workflowId,
        bool approve,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var actorId = Actor.SubjectIdAsGuid ?? throw new UnauthorizedAccessException(
            "Deciding an approval stage requires an authenticated actor.");

        var workflow = await repository.GetWithStagesAsync(workflowId, cancellationToken).ConfigureAwait(false);
        if (workflow is null)
        {
            throw new InvalidOperationException($"Permission approval workflow {workflowId} not found.");
        }

        if (workflow.Status != PermissionApprovalStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Workflow {workflowId} is {workflow.Status}; only a pending workflow can be decided (fail closed).");
        }

        var stages = workflow.Stages.OrderBy(stage => stage.StageIndex).ToArray();
        if (workflow.CurrentStageIndex >= stages.Length)
        {
            throw new InvalidOperationException(
                $"Workflow {workflowId} has no stage left to decide (fail closed).");
        }

        var stage = stages[workflow.CurrentStageIndex];

        // Fail closed: a stage pinned to a specific approver accepts that approver only.
        if (stage.RequiredApproverUserId is { } requiredApprover && requiredApprover != actorId)
        {
            throw new UnauthorizedAccessException(
                $"Stage '{stage.StageName}' of workflow {workflowId} can only be decided by its required approver.");
        }

        var stageBefore = $"{stage.StageName}:{PermissionApprovalStageStatus.Pending}";
        stage.Status = approve ? PermissionApprovalStageStatus.Approved : PermissionApprovalStageStatus.Rejected;
        stage.DecidedBy = actorId;
        stage.DecidedAt = SystemClock.UtcNow;
        stage.DecisionNotes = notes;

        var grantApplied = false;

        if (!approve)
        {
            workflow.Status = PermissionApprovalStatus.Rejected;
            workflow.CompletedAt = SystemClock.UtcNow;
            foreach (var remaining in stages.Where(candidate => candidate.StageIndex > stage.StageIndex))
            {
                remaining.Status = PermissionApprovalStageStatus.Skipped;
            }
        }
        else if (stage.StageIndex == stages.Length - 1)
        {
            // Final stage approved: execute the guarded-mutation flow (versioned,
            // cache-invalidating, audited). Acting identity is the approving actor.
            await grantService.GrantTenantPermissionAsync(
                workflow.SubjectUserId,
                workflow.TenantId,
                workflow.Permissions.ToArray(),
                grantedBy: actorId,
                expiresAt: null,
                reason: $"Granted by multi-stage approval workflow {workflow.Id} (final stage approved).",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            workflow.Status = PermissionApprovalStatus.Approved;
            workflow.CurrentStageIndex = stages.Length;
            workflow.CompletedAt = SystemClock.UtcNow;
            grantApplied = true;
        }
        else
        {
            workflow.CurrentStageIndex = stage.StageIndex + 1;
        }

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditService.LogPermissionChangeAsync(
            PermissionOperationType.ApprovalStage,
            userId: workflow.SubjectUserId,
            performedBy: actorId,
            tenantId: workflow.TenantId,
            permissionType: string.Join(",", workflow.Permissions),
            resourceId: workflow.Id,
            resourceType: "PermissionApproval",
            oldValue: stageBefore,
            newValue: JsonSerializer.Serialize(new
            {
                Stage = stage.StageName,
                stage.StageIndex,
                Decision = approve ? "Approved" : "Rejected",
                stage.DecisionNotes,
                workflow.Status,
                workflow.CurrentStageIndex,
                GrantApplied = grantApplied
            }),
            reason: notes ?? (approve
                ? $"Approval stage '{stage.StageName}' approved."
                : $"Approval stage '{stage.StageName}' rejected."),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Actor {ActorId} {Decision} stage {StageIndex} ('{StageName}') of workflow {WorkflowId}; status={Status}",
            actorId,
            approve ? "approved" : "rejected",
            stage.StageIndex,
            stage.StageName,
            workflow.Id,
            workflow.Status);

        return new PermissionApprovalDecisionResult(
            workflow.Id,
            workflow.Status,
            stage.StageIndex,
            stage.StageName,
            approve,
            notes,
            grantApplied);
    }

    /// <inheritdoc />
    public async Task<PermissionApprovalWorkflow?> GetAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
        => await repository.GetWithStagesAsync(workflowId, cancellationToken).ConfigureAwait(false);
}
