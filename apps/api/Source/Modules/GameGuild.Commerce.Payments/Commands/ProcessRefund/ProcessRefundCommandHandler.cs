using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for the immediate (synchronous) refund path. Delegates to the approval-aware
///     <see cref="IRefundProcessingService"/> so every refund — immediate or workflow-driven —
///     records one <see cref="Refund"/> row, updates the payment aggregate and emits the
///     refund accounting exactly once (issue #419). Refunds at or above the configured
///     manual-approval threshold are queued as pending instead of executing.
/// </summary>
public sealed class ProcessRefundCommandHandler(
    IRefundProcessingService refundProcessingService,
    ILogger<ProcessRefundCommandHandler> logger) : ICommandHandler<ProcessRefundCommand, ProcessRefundResult>
{
    public async Task<ProcessRefundResult> Handle(ProcessRefundCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing refund for payment {PaymentId}, amount {Amount}, reason: {Reason}",
            request.PaymentId, request.Amount, request.Reason);

        var outcome = await refundProcessingService.RequestRefundAsync(
            new RefundRequestInput(
                PaymentId: request.PaymentId,
                Amount: request.Amount,
                Reason: request.Reason),
            cancellationToken).ConfigureAwait(false);

        if (!outcome.Success || outcome.Refund is null)
        {
            logger.LogWarning("Refund for payment {PaymentId} did not complete: {Reason}",
                request.PaymentId, outcome.FailureReason);
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
                ErrorMessage = outcome.FailureReason ?? "The refund could not be processed"
            };
        }

        var refund = outcome.Refund;
        var completed = refund.Status == RefundStatus.Completed;
        if (!completed)
        {
            return new ProcessRefundResult
            {
                RefundId = refund.Id,
                PaymentId = request.PaymentId,
                RefundedAmount = 0,
                Currency = refund.Currency,
                Status = TransactionStatus.Failed,
                Reason = request.Reason,
                ProcessedAt = SystemClock.UtcNow,
                IsSuccess = false,
                ErrorMessage = refund.FailureReason
                    ?? (outcome.RequiresApproval
                        ? $"Refund {refund.Id} requires manual approval before it can be processed"
                        : outcome.FailureReason ?? "The refund did not complete")
            };
        }

        return new ProcessRefundResult
        {
            RefundId = refund.Id,
            PaymentId = request.PaymentId,
            RefundedAmount = refund.Amount,
            Currency = refund.Currency,
            Status = TransactionStatus.Completed,
            Reason = request.Reason,
            ProcessedAt = refund.ProcessedAt ?? SystemClock.UtcNow,
            ReferenceNumber = refund.GatewayRefundId,
            EstimatedCompletionDate = SystemClock.UtcNow.AddDays(5), // Standard refund processing time
            ProcessingFee = 0,
            IsSuccess = true
        };
    }
}
