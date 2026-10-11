using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for resolving a pending manual fraud review (issue #409). Guarded (the
///     <c>payments:admin</c> permission plus an authenticated reviewer and tenant match)
///     and audited (a payment audit-trail entry records the transition with reviewer notes).
/// </summary>
public sealed class ResolveFraudReviewCommandHandler(
    IPaymentRepository paymentRepository,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor,
    ILogger<ResolveFraudReviewCommandHandler> logger) : ICommandHandler<ResolveFraudReviewCommand, FraudReviewResolutionResult>
{
    public async Task<FraudReviewResolutionResult> Handle(ResolveFraudReviewCommand request, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var reviewedByUserId = actor.SubjectIdAsGuid
            ?? throw new UnauthorizedAccessException("An authenticated administrator is required to resolve a fraud review.");

        logger.LogInformation(
            "Resolving fraud review for payment {PaymentId} (approve: {Approve}) by reviewer {ReviewerId}",
            request.PaymentId, request.Approve, reviewedByUserId);

        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken)
            .ConfigureAwait(false);

        if (payment is null)
        {
            return Failure(request.PaymentId, $"Payment {request.PaymentId} not found");
        }

        if (!actor.IsSystemAdmin && payment.TenantId != actor.TenantId)
        {
            throw new UnauthorizedAccessException("The fraud review does not belong to the reviewer's tenant.");
        }

        if (payment.FraudDecision != FraudDecision.Review || payment.FraudReviewStatus != FraudReviewStatus.PendingReview)
        {
            return Failure(
                request.PaymentId,
                $"Payment {request.PaymentId} has no pending fraud review (decision {payment.FraudDecision}, review status {payment.FraudReviewStatus}).");
        }

        payment.ResolveFraudReview(request.Approve, reviewedByUserId, request.Notes);
        await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        await revenueAuditService.RecordAuditTrailAsync(
            "Payment",
            payment.Id,
            "FraudReviewResolved",
            reviewedByUserId,
            oldValue: JsonSerializer.Serialize(new { fraudReviewStatus = FraudReviewStatus.PendingReview.ToString() }),
            newValue: JsonSerializer.Serialize(new
            {
                fraudReviewStatus = payment.FraudReviewStatus!.Value.ToString(),
                reviewedBy = reviewedByUserId,
                payment.FraudReviewNotes
            }),
            reason: request.Notes,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Fraud review for payment {PaymentId} resolved as {Status} by reviewer {ReviewerId}",
            payment.Id, payment.FraudReviewStatus, reviewedByUserId);

        return new FraudReviewResolutionResult
        {
            Success = true,
            PaymentId = payment.Id,
            ReviewStatus = payment.FraudReviewStatus,
            ReviewedByUserId = reviewedByUserId,
            ResolvedAt = payment.FraudReviewedAt
        };
    }

    private static FraudReviewResolutionResult Failure(Guid paymentId, string reason) => new()
    {
        Success = false,
        PaymentId = paymentId,
        FailureReason = reason
    };
}
