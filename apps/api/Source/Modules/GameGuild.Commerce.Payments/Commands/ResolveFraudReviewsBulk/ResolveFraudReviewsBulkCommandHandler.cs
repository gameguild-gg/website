using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for the bulk resolution of pending manual fraud reviews (issue #409 bulk
///     actions). Guarded (the <c>payments:admin</c> permission plus an authenticated
///     reviewer and tenant match) and audited (a payment audit-trail entry records each
///     transition). One invalid item never fails the batch: it is skipped with a reason.
/// </summary>
public sealed class ResolveFraudReviewsBulkCommandHandler(
    IPaymentRepository paymentRepository,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor,
    ILogger<ResolveFraudReviewsBulkCommandHandler> logger) : ICommandHandler<ResolveFraudReviewsBulkCommand, FraudReviewsBulkResolutionResult>
{
    private const int MaxBatchSize = 100;

    public async Task<FraudReviewsBulkResolutionResult> Handle(ResolveFraudReviewsBulkCommand request, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var reviewedByUserId = actor.SubjectIdAsGuid
            ?? throw new UnauthorizedAccessException("An authenticated administrator is required to resolve fraud reviews.");

        var paymentIds = request.PaymentIds.Distinct().Take(MaxBatchSize + 1).ToList();
        if (paymentIds.Count > MaxBatchSize)
        {
            throw new InvalidOperationException($"Bulk fraud-review resolution accepts at most {MaxBatchSize} payments per batch.");
        }

        logger.LogInformation(
            "Resolving {Count} fraud review(s) in bulk (approve: {Approve}) by reviewer {ReviewerId}",
            paymentIds.Count,
            request.Approve,
            reviewedByUserId);

        var items = new List<FraudReviewsBulkItemResult>();
        var resolved = 0;

        foreach (var paymentId in paymentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var payment = await paymentRepository.GetByIdAsync(paymentId, cancellationToken)
                .ConfigureAwait(false);

            if (payment is null)
            {
                items.Add(new FraudReviewsBulkItemResult
                {
                    PaymentId = paymentId,
                    Success = false,
                    FailureReason = $"Payment {paymentId} not found"
                });
                continue;
            }

            if (!actor.IsSystemAdmin && payment.TenantId != actor.TenantId)
            {
                items.Add(new FraudReviewsBulkItemResult
                {
                    PaymentId = paymentId,
                    Success = false,
                    FailureReason = "The fraud review does not belong to the reviewer's tenant."
                });
                continue;
            }

            if (payment.FraudDecision != FraudDecision.Review || payment.FraudReviewStatus != FraudReviewStatus.PendingReview)
            {
                items.Add(new FraudReviewsBulkItemResult
                {
                    PaymentId = paymentId,
                    Success = false,
                    FailureReason = $"Payment {paymentId} has no pending fraud review (decision {payment.FraudDecision}, review status {payment.FraudReviewStatus})."
                });
                continue;
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
                    payment.FraudReviewNotes,
                    bulkResolution = true
                }),
                reason: request.Notes,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            resolved++;
            items.Add(new FraudReviewsBulkItemResult { PaymentId = paymentId, Success = true });
        }

        return new FraudReviewsBulkResolutionResult
        {
            Requested = paymentIds.Count,
            Resolved = resolved,
            Skipped = paymentIds.Count - resolved,
            ReviewedByUserId = reviewedByUserId,
            Items = items
        };
    }
}
