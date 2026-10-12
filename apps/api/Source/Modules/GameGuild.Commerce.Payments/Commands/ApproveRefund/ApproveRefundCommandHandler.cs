using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for <see cref="ApproveRefundCommand"/> (issue #419). Guarded (permission +
///     authenticated approver + tenant match), versioned through the refund row's concurrency
///     token, and audited (a refund audit-trail entry records the decision and the executed
///     outcome).
/// </summary>
public sealed class ApproveRefundCommandHandler(
    IRefundRepository refundRepository,
    IRefundProcessingService refundProcessingService,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor,
    ILogger<ApproveRefundCommandHandler> logger) : ICommandHandler<ApproveRefundCommand, RefundDecisionResult>
{
    public async Task<RefundDecisionResult> Handle(ApproveRefundCommand request, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var approverId = actor.SubjectIdAsGuid
            ?? throw new UnauthorizedAccessException("An authenticated administrator is required to approve a refund.");

        var refund = await refundRepository.GetByIdAsync(request.RefundId, cancellationToken).ConfigureAwait(false);
        if (refund is null)
        {
            return Failure($"Refund {request.RefundId} not found");
        }

        if (!actor.IsSystemAdmin && refund.TenantId != actor.TenantId)
        {
            throw new UnauthorizedAccessException("The refund does not belong to the approver's tenant.");
        }

        if (refund.Status != RefundStatus.Pending)
        {
            return Failure($"Refund {request.RefundId} is in status {refund.Status} and cannot be approved");
        }

        refund.MarkApproved(approverId, request.Notes);
        await refundRepository.UpdateAsync(refund, cancellationToken).ConfigureAwait(false);

        await revenueAuditService.RecordAuditTrailAsync(
            entityType: nameof(Refund),
            entityId: refund.Id,
            action: "RefundApproved",
            changedBy: approverId,
            oldValue: JsonSerializer.Serialize(new { status = RefundStatus.Pending.ToString() }),
            newValue: JsonSerializer.Serialize(new
            {
                status = refund.Status.ToString(),
                approvedBy = approverId,
                notes = refund.DecisionNotes
            }),
            reason: request.Notes,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Refund {RefundId} approved by {ApproverId}; executing", refund.Id, approverId);

        var execution = await refundProcessingService.ExecuteAsync(refund, cancellationToken).ConfigureAwait(false);

        return new RefundDecisionResult
        {
            Success = execution.Success,
            Refund = RefundRecord.From(execution.Refund),
            FailureReason = execution.FailureReason
        };
    }

    private static RefundDecisionResult Failure(string reason) => new()
    {
        Success = false,
        Refund = null,
        FailureReason = reason
    };
}
