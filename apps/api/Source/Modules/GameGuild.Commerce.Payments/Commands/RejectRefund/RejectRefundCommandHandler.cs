using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for <see cref="RejectRefundCommand"/> (issue #419). Guarded (permission +
///     authenticated reviewer + tenant match), versioned through the refund row's concurrency
///     token, and audited (a refund audit-trail entry records the rejection).
/// </summary>
public sealed class RejectRefundCommandHandler(
    IRefundRepository refundRepository,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor,
    ILogger<RejectRefundCommandHandler> logger) : ICommandHandler<RejectRefundCommand, RefundDecisionResult>
{
    public async Task<RefundDecisionResult> Handle(RejectRefundCommand request, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var reviewerId = actor.SubjectIdAsGuid
            ?? throw new UnauthorizedAccessException("An authenticated administrator is required to reject a refund.");

        var refund = await refundRepository.GetByIdAsync(request.RefundId, cancellationToken).ConfigureAwait(false);
        if (refund is null)
        {
            return new RefundDecisionResult
            {
                Success = false,
                Refund = null,
                FailureReason = $"Refund {request.RefundId} not found"
            };
        }

        if (!actor.IsSystemAdmin && refund.TenantId != actor.TenantId)
        {
            throw new UnauthorizedAccessException("The refund does not belong to the reviewer's tenant.");
        }

        if (refund.Status != RefundStatus.Pending)
        {
            return new RefundDecisionResult
            {
                Success = false,
                Refund = RefundRecord.From(refund),
                FailureReason = $"Refund {request.RefundId} is in status {refund.Status} and cannot be rejected"
            };
        }

        var statusBefore = refund.Status;
        refund.MarkRejected(reviewerId, request.Notes);
        await refundRepository.UpdateAsync(refund, cancellationToken).ConfigureAwait(false);

        await revenueAuditService.RecordAuditTrailAsync(
            entityType: nameof(Refund),
            entityId: refund.Id,
            action: "RefundRejected",
            changedBy: reviewerId,
            oldValue: JsonSerializer.Serialize(new { status = statusBefore.ToString() }),
            newValue: JsonSerializer.Serialize(new
            {
                status = refund.Status.ToString(),
                rejectedBy = reviewerId,
                notes = refund.DecisionNotes
            }),
            reason: request.Notes,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Refund {RefundId} rejected by {ReviewerId}", refund.Id, reviewerId);

        return new RefundDecisionResult
        {
            Success = true,
            Refund = RefundRecord.From(refund)
        };
    }
}
