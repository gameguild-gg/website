using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for <see cref="ChallengeDisputeCommand"/> (issue #419). Guarded (permission +
///     authenticated actor + tenant match) and audited. Fails closed when the dispute has no
///     gateway identity to challenge.
/// </summary>
public sealed class ChallengeDisputeCommandHandler(
    IApplicationDbContext context,
    IPaymentRepository paymentRepository,
    IPaymentGateway paymentGateway,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor,
    ILogger<ChallengeDisputeCommandHandler> logger) : ICommandHandler<ChallengeDisputeCommand, DisputeActionResult>
{
    public async Task<DisputeActionResult> Handle(ChallengeDisputeCommand request, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var actorId = actor.SubjectIdAsGuid
            ?? throw new UnauthorizedAccessException("An authenticated administrator is required to challenge a dispute.");

        var dispute = await context.Set<PaymentDispute>()
            .FirstOrDefaultAsync(d => d.Id == request.DisputeId, cancellationToken)
            .ConfigureAwait(false);
        if (dispute is null)
        {
            return Failure(request.DisputeId, $"Dispute {request.DisputeId} not found");
        }

        if (dispute.GatewayDisputeId is null)
        {
            return Failure(request.DisputeId, "Only disputes reconciled from a gateway webhook can be challenged at the gateway.");
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

        var gatewayResult = await paymentGateway.ChallengeDisputeAsync(
                new GatewayDisputeChallengeRequest(
                    IdempotencyKey: $"dispute_challenge_{dispute.Id}",
                    ExternalDisputeId: dispute.GatewayDisputeId,
                    EvidenceSummary: request.EvidenceSummary,
                    ProviderId: payment.Provider),
                cancellationToken)
            .ConfigureAwait(false);

        if (!gatewayResult.Success)
        {
            logger.LogWarning(
                "Gateway refused to challenge dispute {DisputeId} ({GatewayDisputeId}): {ErrorMessage}",
                dispute.Id,
                dispute.GatewayDisputeId,
                gatewayResult.ErrorMessage);
            return Failure(request.DisputeId, $"The gateway refused the challenge: {gatewayResult.ErrorMessage}");
        }

        var statusBefore = dispute.Status;
        if (dispute.Status == DisputeStatus.Submitted)
        {
            dispute.MoveToReview();
        }
        else
        {
            dispute.Touch();
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await revenueAuditService.RecordAuditTrailAsync(
            entityType: nameof(PaymentDispute),
            entityId: dispute.Id,
            action: "DisputeChallenged",
            changedBy: actorId,
            oldValue: JsonSerializer.Serialize(new { status = statusBefore.ToString() }),
            newValue: JsonSerializer.Serialize(new
            {
                status = dispute.Status.ToString(),
                gatewayDisputeId = dispute.GatewayDisputeId
            }),
            reason: request.EvidenceSummary,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Dispute {DisputeId} challenged by {ActorId}", dispute.Id, actorId);

        return new DisputeActionResult { Success = true, DisputeId = dispute.Id };
    }

    private static DisputeActionResult Failure(Guid disputeId, string reason) => new()
    {
        Success = false,
        DisputeId = disputeId,
        FailureReason = reason
    };
}
