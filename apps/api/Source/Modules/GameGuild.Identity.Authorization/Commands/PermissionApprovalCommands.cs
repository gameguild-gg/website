using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Command creating a multi-stage permission approval workflow (issue #188). The
///     acting requester identity is resolved from the actor context, never the body.
/// </summary>
public sealed record CreatePermissionApprovalCommand : ICommand<Guid>
{
    /// <summary>
    ///     Gets the user the permissions are requested for.
    /// </summary>
    public required Guid SubjectUserId { get; init; }

    /// <summary>
    ///     Gets the tenant scope (resolved server-side against the actor's tenant when omitted).
    /// </summary>
    public TenantId? TenantId { get; init; }

    /// <summary>
    ///     Gets the permission strings requested.
    /// </summary>
    public required string[] Permissions { get; init; }

    /// <summary>
    ///     Gets the ordered approval stage definitions.
    /// </summary>
    public required PermissionApprovalStageDefinition[] Stages { get; init; }

    /// <summary>
    ///     Gets the justification for the request.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
///     Handler for <see cref="CreatePermissionApprovalCommand"/>.
/// </summary>
public sealed class CreatePermissionApprovalCommandHandler(
    IPermissionApprovalService approvalService,
    IActorContextAccessor actorContextAccessor,
    ILogger<CreatePermissionApprovalCommandHandler> logger)
    : ICommandHandler<CreatePermissionApprovalCommand, Guid>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <inheritdoc />
    public async Task<Guid> Handle(CreatePermissionApprovalCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Creating permission approval workflow for subject {SubjectUserId} with {StageCount} stage(s)",
            request.SubjectUserId,
            request.Stages.Length);

        var workflow = await approvalService.CreateAsync(
            request.TenantId?.Value ?? Actor.TenantId,
            request.SubjectUserId,
            request.Permissions,
            request.Stages,
            request.Reason,
            cancellationToken).ConfigureAwait(false);

        return workflow.Id;
    }
}

/// <summary>
///     Command deciding the current stage of a permission approval workflow
/// (issue #188). The approver identity is resolved from the actor context.
/// </summary>
public sealed record DecidePermissionApprovalCommand : ICommand<PermissionApprovalDecisionResult>
{
    /// <summary>
    ///     Gets the workflow to decide.
    /// </summary>
    public required Guid WorkflowId { get; init; }

    /// <summary>
    ///     Gets the decision for the current stage.
    /// </summary>
    public required bool Approve { get; init; }

    /// <summary>
    ///     Gets optional notes recorded with the decision.
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
///     Handler for <see cref="DecidePermissionApprovalCommand"/>.
/// </summary>
public sealed class DecidePermissionApprovalCommandHandler(
    IPermissionApprovalService approvalService,
    ILogger<DecidePermissionApprovalCommandHandler> logger)
    : ICommandHandler<DecidePermissionApprovalCommand, PermissionApprovalDecisionResult>
{
    /// <inheritdoc />
    public async Task<PermissionApprovalDecisionResult> Handle(
        DecidePermissionApprovalCommand request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Deciding permission approval workflow {WorkflowId}: {Decision}",
            request.WorkflowId,
            request.Approve ? "approve" : "reject");

        return await approvalService.DecideAsync(
            request.WorkflowId,
            request.Approve,
            request.Notes,
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     Query loading one permission approval workflow with its stages.
/// </summary>
public sealed record GetPermissionApprovalQuery(Guid WorkflowId) : IQuery<PermissionApprovalWorkflow?>;

/// <summary>
///     Handler for <see cref="GetPermissionApprovalQuery"/>.
/// </summary>
public sealed class GetPermissionApprovalQueryHandler(
    IPermissionApprovalService approvalService)
    : IQueryHandler<GetPermissionApprovalQuery, PermissionApprovalWorkflow?>
{
    /// <inheritdoc />
    public Task<PermissionApprovalWorkflow?> Handle(
        GetPermissionApprovalQuery request,
        CancellationToken cancellationToken)
        => approvalService.GetAsync(request.WorkflowId, cancellationToken);
}
