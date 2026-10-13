using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for canceling payments
/// </summary>
public sealed class CancelPaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGateway paymentGateway,
    ILogger<CancelPaymentCommandHandler> logger) : ICommandHandler<CancelPaymentCommand, PaymentCancellationResult>
{
    public async Task<PaymentCancellationResult> Handle(CancelPaymentCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Cancelling payment {PaymentId} with reason: {Reason}",
            request.PaymentId, request.CancellationReason);

        // 1. Get the payment
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken)
            .ConfigureAwait(false);

        if (payment == null)
        {
            logger.LogWarning("Payment {PaymentId} not found for cancellation", request.PaymentId);
            return new PaymentCancellationResult
            {
                PaymentId = request.PaymentId,
                CancellationReason = request.CancellationReason,
                CanceledAt = SystemClock.UtcNow,
                Success = false,
                ErrorMessage = $"Payment {request.PaymentId} not found"
            };
        }

        // 2. Check if payment can be cancelled
        if (!payment.CanTransitionTo(PaymentStatus.Cancelled))
        {
            logger.LogWarning("Payment {PaymentId} in status {Status} cannot be cancelled",
                request.PaymentId, payment.Status);

            return new PaymentCancellationResult
            {
                PaymentId = request.PaymentId,
                CancellationReason = request.CancellationReason,
                CanceledAt = SystemClock.UtcNow,
                CanceledBy = request.CanceledBy,
                Success = false,
                ErrorMessage = $"Payment in status {payment.Status} cannot be cancelled"
            };
        }

        // 3. If payment was already processed/succeeded, process refund instead
        bool refundProcessed = false;
        decimal? refundAmount = null;

        if (payment.Status == PaymentStatus.Succeeded && !string.IsNullOrEmpty(payment.ExternalPaymentId))
        {
            logger.LogInformation("Payment {PaymentId} was already succeeded, processing refund", request.PaymentId);

            var refundRequest = new GatewayRefundRequest(
                IdempotencyKey: $"cancel_refund_{payment.Id}_{SystemClock.UtcNow:yyyyMMddHHmmss}",
                OriginalTransactionId: payment.ExternalTransactionId ?? payment.ExternalPaymentId,
                Amount: payment.Amount,
                Reason: request.CancellationReason,
                ProviderId: payment.Provider,
                Currency: payment.Currency);

            var refundResult = await paymentGateway.ProcessRefundAsync(refundRequest, cancellationToken)
                .ConfigureAwait(false);

            if (refundResult.Success)
            {
                payment.ProcessRefund(
                    refundAmount: payment.Amount,
                    refundId: refundResult.RefundId ?? Guid.NewGuid().ToString(),
                    reason: request.CancellationReason);

                refundProcessed = true;
                refundAmount = payment.Amount;

                logger.LogInformation("Refund processed for payment {PaymentId}", request.PaymentId);
            }
            else
            {
                logger.LogWarning("Refund failed for payment {PaymentId}: {ErrorMessage}",
                    request.PaymentId, refundResult.ErrorMessage);

                return new PaymentCancellationResult
                {
                    PaymentId = request.PaymentId,
                    CancellationReason = request.CancellationReason,
                    CanceledAt = SystemClock.UtcNow,
                    CanceledBy = request.CanceledBy,
                    Success = false,
                    ErrorMessage = $"Refund failed: {refundResult.ErrorMessage}"
                };
            }
        }
        else
        {
            // 4. Cancel payment that hasn't been processed yet
            payment.Cancel(request.CancellationReason, request.CanceledBy);
        }

        await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Payment {PaymentId} successfully cancelled", request.PaymentId);

        return new PaymentCancellationResult
        {
            PaymentId = request.PaymentId,
            CancellationReason = request.CancellationReason,
            CanceledAt = payment.CancelledAt ?? SystemClock.UtcNow,
            CanceledBy = request.CanceledBy,
            Success = true,
            RefundProcessed = refundProcessed,
            RefundAmount = refundAmount
        };
    }
}
