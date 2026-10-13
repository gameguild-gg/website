using GameGuild.CQRS;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for updating payment status
/// </summary>
public sealed class UpdatePaymentStatusCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentSubscriptionSyncService paymentSubscriptionSyncService,
    IOptions<PaymentRetryOptions> retryOptions,
    ILogger<UpdatePaymentStatusCommandHandler> logger) : ICommandHandler<UpdatePaymentStatusCommand, bool>
{
    public async Task<bool> Handle(UpdatePaymentStatusCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Updating payment {PaymentId} status to {Status}",
            request.PaymentId, request.Status);

        // 1. Get the payment
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken)
            .ConfigureAwait(false);

        if (payment == null)
        {
            logger.LogWarning("Payment {PaymentId} not found for status update", request.PaymentId);
            return false;
        }

        // 2. Validate status transition
        if (!payment.CanTransitionTo(request.Status))
        {
            logger.LogWarning("Payment {PaymentId} cannot transition from {CurrentStatus} to {NewStatus}",
                request.PaymentId, payment.Status, request.Status);
            return false;
        }

        // 3. Apply status change based on the target status
        switch (request.Status)
        {
            case PaymentStatus.Processing:
                payment.MarkAsProcessing(request.TransactionId);
                break;

            case PaymentStatus.Succeeded:
                var externalPaymentId = request.TransactionId ?? Guid.NewGuid().ToString();
                payment.MarkAsSucceeded(externalPaymentId, request.TransactionId);
                break;

            case PaymentStatus.Failed:
                payment.MarkAsFailed(
                    "Status updated to failed via UpdatePaymentStatusCommand",
                    maxRetries: retryOptions.Value.MaxRetries,
                    backoffBaseMinutes: retryOptions.Value.BackoffBaseMinutes,
                    backoffMultiplier: retryOptions.Value.BackoffMultiplier);
                break;

            case PaymentStatus.RequiresAction:
                payment.MarkAsRequiresAction(request.TransactionId);
                break;

            case PaymentStatus.Cancelled:
                payment.Cancel("Status updated to cancelled via UpdatePaymentStatusCommand");
                break;

            case PaymentStatus.Disputed:
                payment.MarkAsDisputed();
                break;

            default:
                logger.LogWarning("Unsupported status transition to {Status} for payment {PaymentId}",
                    request.Status, request.PaymentId);
                return false;
        }

        // 4. Save changes
        await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        if (request.Status == PaymentStatus.Succeeded && payment.ProcessedAt.HasValue)
        {
            var billingCycleNumber = SubscriptionPaymentIdentity.TryGetBillingCycleNumber(payment.IdempotencyKey);
            if (billingCycleNumber.HasValue)
            {
                await paymentSubscriptionSyncService.SyncSuccessfulPaymentAsync(
                    payment.Id,
                    payment.SubscriptionId,
                    payment.Amount,
                    payment.Currency,
                    billingCycleNumber,
                    payment.ProcessedAt.Value,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                logger.LogWarning(
                    "Payment {PaymentId} reached succeeded without an authoritative billing-cycle identity; subscription synchronization was blocked",
                    payment.Id);
            }
        }

        logger.LogInformation("Payment {PaymentId} status updated to {Status}",
            request.PaymentId, request.Status);

        return true;
    }
}
