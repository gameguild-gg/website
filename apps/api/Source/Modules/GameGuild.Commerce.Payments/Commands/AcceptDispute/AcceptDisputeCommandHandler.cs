using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for <see cref="AcceptDisputeCommand"/> (issue #419). Guarded (permission +
///     authenticated actor + tenant match) and audited (dispute audit-trail entry). The refund
///     is issued through <see cref="IRefundProcessingService"/> so the manual-approval threshold,
///     refund row lifecycle, accounting and audit trail all apply.
/// </summary>
public sealed class AcceptDisputeCommandHandler(
    IApplicationDbContext context,
    IPaymentRepository paymentRepository,
    IPaymentGateway paymentGateway,
    IRefundProcessingService refundProcessingService,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor,
    ILogger<AcceptDisputeCommandHandler> logger) : ICommandHandler<AcceptDisputeCommand, DisputeActionResult>
{
    public async Task<DisputeActionResult> Handle(AcceptDisputeCommand request, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var actorId = actor.SubjectIdAsGuid
            ?? throw new UnauthorizedAccessException("An authenticated administrator is required to accept a dispute.");

        var dispute = await context.Set<PaymentDispute>()
            .FirstOrDefaultAsync(d => d.Id == request.DisputeId, cancellationToken)
            .ConfigureAwait(false);
        if (dispute is null)
        {
            return Failure(request.DisputeId, $"Dispute {request.DisputeId} not found");
        }

        var payment = await paymentRepository.GetByIdAsync(dispute.PaymentId, cancellationToken).ConfigureAwait(false);
        if (payment is null)
        {
            return Failure(request.DisputeId, $"Payment {dispute.PaymentId} for the dispute no longer exists");
        }

        if (!actor.IsSystemAdmin && payment.TenantId != actor.TenantId)
        {
            throw new UnauthorizedAccessException("The dispute does not belong to the actor's tenant.");
        }

        if (dispute.Status is DisputeStatus.Resolved or DisputeStatus.Won or DisputeStatus.Lost or DisputeStatus.Cancelled)
        {
            return Failure(request.DisputeId, $"Dispute {request.DisputeId} is already resolved ({dispute.Status})");
        }

        // Close the dispute at the gateway when it was reconciled from a gateway webhook.
        if (dispute.GatewayDisputeId is not null)
        {
            var gatewayResult = await paymentGateway.AcceptDisputeAsync(
                    new GatewayDisputeActionRequest(
                        IdempotencyKey: $"dispute_accept_{dispute.Id}",
                        ExternalDisputeId: dispute.GatewayDisputeId),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!gatewayResult.Success)
            {
                logger.LogWarning(
                    "Gateway refused to accept dispute {DisputeId} ({GatewayDisputeId}): {ErrorMessage}",
                    dispute.Id,
                    dispute.GatewayDisputeId,
                    gatewayResult.ErrorMessage);
                return Failure(request.DisputeId, $"The gateway refused to accept the dispute: {gatewayResult.ErrorMessage}");
            }
        }

        var statusBefore = dispute.Status;
        dispute.MarkAsLost(request.Notes ?? "Dispute accepted; customer keeps the funds.", actorId);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await revenueAuditService.RecordAuditTrailAsync(
            entityType: nameof(PaymentDispute),
            entityId: dispute.Id,
            action: "DisputeAccepted",
            changedBy: actorId,
            oldValue: JsonSerializer.Serialize(new { status = statusBefore.ToString() }),
            newValue: JsonSerializer.Serialize(new
            {
                status = dispute.Status.ToString(),
                gatewayDisputeId = dispute.GatewayDisputeId,
                issueRefund = request.IssueRefund
            }),
            reason: request.Notes,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Dispute {DisputeId} accepted by {ActorId}", dispute.Id, actorId);

        if (!request.IssueRefund)
        {
            return new DisputeActionResult { Success = true, DisputeId = dispute.Id };
        }

        // Accepting a gateway dispute already returned the funds at the gateway (chargeback), so
        // the refund is recorded for accounting parity without a second gateway call. Locally
        // created disputes (no gateway identity) execute a real gateway refund.
        var executeAtGateway = dispute.GatewayDisputeId is null;
        var refundOutcome = await refundProcessingService.RequestRefundAsync(
            new RefundRequestInput(
                PaymentId: dispute.PaymentId,
                Amount: dispute.Amount,
                Reason: "chargeback_accepted",
                ReasonDetails: request.Notes ?? "Dispute accepted; refund recorded for the chargeback.",
                RequestedByUserId: actorId,
                DisputeId: dispute.Id,
                ExecuteAtGateway: executeAtGateway),
            cancellationToken).ConfigureAwait(false);

        if (!refundOutcome.Success || refundOutcome.Refund is null)
        {
            return new DisputeActionResult
            {
                Success = false,
                DisputeId = dispute.Id,
                Refund = refundOutcome.Refund is null ? null : RefundRecord.From(refundOutcome.Refund),
                FailureReason = refundOutcome.FailureReason ?? "The dispute was accepted but the refund could not be recorded"
            };
        }

        return new DisputeActionResult
        {
            Success = true,
            DisputeId = dispute.Id,
            Refund = RefundRecord.From(refundOutcome.Refund)
        };
    }

    private static DisputeActionResult Failure(Guid disputeId, string reason) => new()
    {
        Success = false,
        DisputeId = disputeId,
        FailureReason = reason
    };
}
