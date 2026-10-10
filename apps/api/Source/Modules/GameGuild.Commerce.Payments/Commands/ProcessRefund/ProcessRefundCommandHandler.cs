using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for processing refund commands
/// </summary>
public sealed class ProcessRefundCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGateway paymentGateway,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor,
    ILogger<ProcessRefundCommandHandler> logger) : ICommandHandler<ProcessRefundCommand, ProcessRefundResult>
{
    public async Task<ProcessRefundResult> Handle(ProcessRefundCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing refund for payment {PaymentId}, amount {Amount}, reason: {Reason}",
            request.PaymentId, request.Amount, request.Reason);

        // 1. Get the payment
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken)
            .ConfigureAwait(false);

        if (payment == null)
        {
            logger.LogWarning("Payment {PaymentId} not found for refund", request.PaymentId);
            return new ProcessRefundResult
            {
                RefundId = Guid.Empty,
                PaymentId = request.PaymentId,
                RefundedAmount = 0,
                Currency = "USD",
                Status = TransactionStatus.Failed,
                Reason = request.Reason,
                ProcessedAt = SystemClock.UtcNow,
                IsSuccess = false,
                ErrorMessage = $"Payment {request.PaymentId} not found"
            };
        }

        // 2. Validate refund is possible
        if (payment.Status != PaymentStatus.Succeeded && payment.Status != PaymentStatus.Disputed)
        {
            logger.LogWarning("Payment {PaymentId} in status {Status} cannot be refunded",
                request.PaymentId, payment.Status);

            return new ProcessRefundResult
            {
                RefundId = Guid.Empty,
                PaymentId = request.PaymentId,
                RefundedAmount = 0,
                Currency = payment.Currency,
                Status = TransactionStatus.Failed,
                Reason = request.Reason,
                ProcessedAt = SystemClock.UtcNow,
                IsSuccess = false,
                ErrorMessage = $"Payment in status {payment.Status} cannot be refunded"
            };
        }

        // 3. Validate refund amount
        var maxRefundable = payment.Amount - payment.RefundedAmount;
        if (request.Amount > maxRefundable)
        {
            logger.LogWarning("Refund amount {Amount} exceeds maximum refundable {MaxRefundable} for payment {PaymentId}",
                request.Amount, maxRefundable, request.PaymentId);
            return new ProcessRefundResult
            {
                RefundId = Guid.Empty,
                PaymentId = request.PaymentId,
                RefundedAmount = 0,
                Currency = payment.Currency,
                Status = TransactionStatus.Failed,
                Reason = request.Reason,
                ProcessedAt = SystemClock.UtcNow,
                IsSuccess = false,
                ErrorMessage = $"Refund amount {request.Amount} exceeds maximum refundable {maxRefundable}"
            };
        }

        // 4. Process refund through payment gateway
        var refundIdempotencyKey = $"refund_{payment.Id}_{request.Amount}_{SystemClock.UtcNow:yyyyMMddHHmmss}";
        var gatewayRequest = new GatewayRefundRequest(
            IdempotencyKey: refundIdempotencyKey,
            OriginalTransactionId: payment.ExternalTransactionId ?? payment.ExternalPaymentId ?? payment.Id.ToString(),
            Amount: request.Amount,
            Reason: request.Reason);

        var gatewayResult = await paymentGateway.ProcessRefundAsync(gatewayRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!gatewayResult.Success)
        {
            logger.LogWarning("Refund failed for payment {PaymentId}: {ErrorMessage}",
                request.PaymentId, gatewayResult.ErrorMessage);
            return new ProcessRefundResult
            {
                RefundId = Guid.Empty,
                PaymentId = request.PaymentId,
                RefundedAmount = 0,
                Currency = payment.Currency,
                Status = TransactionStatus.Failed,
                Reason = request.Reason,
                ProcessedAt = SystemClock.UtcNow,
                IsSuccess = false,
                ErrorMessage = gatewayResult.ErrorMessage
            };
        }

        var statusBeforeRefund = payment.Status;

        // 5. Update payment with refund details
        var refundId = gatewayResult.RefundId ?? Guid.NewGuid().ToString();
        payment.ProcessRefund(request.Amount, refundId, request.Reason);
        await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Refund {RefundId} processed successfully for payment {PaymentId}",
            refundId, request.PaymentId);

        // 6. Record the RefundProcessed revenue event and post the balancing ledger
        //    entry so refunds reduce accounted revenue (issue #403).
        await RecordRefundRevenueAsync(payment, request.Amount, refundId, request.Reason, cancellationToken)
            .ConfigureAwait(false);

        // 7. Persist an audit-trail entry for the refund (issue #403)
        await RecordRefundAuditTrailAsync(payment, statusBeforeRefund, request.Amount, refundId, request.Reason, cancellationToken)
            .ConfigureAwait(false);

        // 8. Return result
        return new ProcessRefundResult
        {
            RefundId = Guid.TryParse(refundId, out var parsedId) ? parsedId : Guid.NewGuid(),
            PaymentId = request.PaymentId,
            RefundedAmount = request.Amount,
            Currency = payment.Currency,
            Status = TransactionStatus.Completed,
            Reason = request.Reason,
            ProcessedAt = SystemClock.UtcNow,
            ReferenceNumber = refundId,
            EstimatedCompletionDate = SystemClock.UtcNow.AddDays(5), // Standard refund processing time
            ProcessingFee = 0,
            IsSuccess = true
        };
    }

    private async Task RecordRefundRevenueAsync(
        Payment payment,
        decimal refundAmount,
        string refundId,
        string reason,
        CancellationToken cancellationToken)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            paymentId = payment.Id.ToString(),
            refundId,
            reason,
            paymentStatusAfterRefund = payment.Status.ToString(),
            cumulativeRefundedAmount = payment.RefundedAmount
        });

        var revenueEvent = await revenueAuditService.RecordRevenueEventAsync(
            eventType: RevenueEventType.RefundProcessed,
            amount: refundAmount,
            currency: payment.Currency,
            source: payment.SubscriptionId.HasValue ? RevenueSource.Subscription : RevenueSource.OneTimePayment,
            referenceId: payment.Id.ToString(),
            metadata: metadata,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await revenueAuditService.CreateLedgerEntryAsync(
            entryType: LedgerEntryType.Refund,
            debitAccount: LedgerAccount.RefundsAndChargebacks.ToAccountCode(),
            creditAccount: LedgerAccount.Cash.ToAccountCode(),
            amount: refundAmount,
            currency: payment.Currency,
            description: $"Refund {refundId} for payment {payment.Id}",
            revenueEventId: revenueEvent.Id,
            referenceNumber: refundId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordRefundAuditTrailAsync(
        Payment payment,
        PaymentStatus statusBeforeRefund,
        decimal refundAmount,
        string refundId,
        string reason,
        CancellationToken cancellationToken)
    {
        var oldValue = JsonSerializer.Serialize(new
        {
            status = statusBeforeRefund.ToString(),
            refundedAmount = payment.RefundedAmount - refundAmount
        });
        var newValue = JsonSerializer.Serialize(new
        {
            status = payment.Status.ToString(),
            refundedAmount = payment.RefundedAmount
        });

        await revenueAuditService.RecordAuditTrailAsync(
            entityType: "Payment",
            entityId: payment.Id,
            action: "StatusChanged",
            changedBy: RequireActorId(),
            oldValue: oldValue,
            newValue: newValue,
            reason: $"Refund {refundId} of {refundAmount} {payment.Currency} processed: {reason}",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private Guid RequireActorId() => actorContextAccessor.ActorContext.SubjectIdAsGuid
        ?? throw new UnauthorizedAccessException("An authenticated administrator is required.");
}
