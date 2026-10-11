using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for <see cref="RequestRefundCommand"/> (issue #419): delegates to the
///     approval-aware <see cref="IRefundProcessingService"/>. Tenant access is enforced at the
///     API boundary; the requester identity always comes from the actor context.
/// </summary>
public sealed class RequestRefundCommandHandler(
    IRefundProcessingService refundProcessingService,
    ILogger<RequestRefundCommandHandler> logger) : ICommandHandler<RequestRefundCommand, RequestRefundResult>
{
    public async Task<RequestRefundResult> Handle(RequestRefundCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Requesting refund for payment {PaymentId}, amount {Amount}",
            request.PaymentId, request.Amount);

        var outcome = await refundProcessingService.RequestRefundAsync(
            new RefundRequestInput(
                PaymentId: request.PaymentId,
                Amount: request.Amount,
                Reason: request.Reason,
                ReasonDetails: request.ReasonDetails,
                ClientIdempotencyKey: request.IdempotencyKey),
            cancellationToken).ConfigureAwait(false);

        if (!outcome.Success || outcome.Refund is null)
        {
            return new RequestRefundResult
            {
                Success = false,
                RequiresApproval = false,
                Refund = null,
                FailureReason = outcome.FailureReason
            };
        }

        return new RequestRefundResult
        {
            Success = true,
            RequiresApproval = outcome.RequiresApproval,
            Refund = RefundRecord.From(outcome.Refund),
            FailureReason = outcome.Refund.Status == RefundStatus.Failed ? outcome.Refund.FailureReason : null
        };
    }
}
