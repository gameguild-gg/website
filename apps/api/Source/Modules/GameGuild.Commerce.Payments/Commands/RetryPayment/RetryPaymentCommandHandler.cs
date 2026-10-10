using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for retrying failed payments
/// </summary>
public sealed class RetryPaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGateway paymentGateway,
    IPaymentSubscriptionSyncService paymentSubscriptionSyncService,
    IOptions<PaymentRetryOptions> retryOptions,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor,
    ILogger<RetryPaymentCommandHandler> logger) : ICommandHandler<RetryPaymentCommand, PaymentRetryResult>
{
    public async Task<PaymentRetryResult> Handle(RetryPaymentCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Retrying payment {PaymentId}", request.PaymentId);

        // 1. Get the original payment
        var payment = await paymentRepository.GetByIdAsync(request.PaymentId, cancellationToken)
            .ConfigureAwait(false);

        if (payment == null)
        {
            logger.LogWarning("Payment {PaymentId} not found for retry", request.PaymentId);
            return new PaymentRetryResult
            {
                Success = false,
                RetryAttempt = 0,
                FailureReason = $"Payment {request.PaymentId} not found"
            };
        }

        // 2. Check if payment can be retried
        if (!payment.CanRetry)
        {
            var reason = payment.MaxRetriesReached
                ? $"Maximum retry attempts ({payment.MaxRetries}) reached"
                : $"Payment in status {payment.Status} cannot be retried";

            logger.LogWarning("Payment {PaymentId} cannot be retried: {Reason}", request.PaymentId, reason);

            return new PaymentRetryResult
            {
                Success = false,
                RetryAttempt = payment.RetryCount,
                MaxRetriesReached = payment.MaxRetriesReached,
                FailureReason = reason
            };
        }

        var statusBeforeRetry = payment.Status;
        var retryCountBeforeRetry = payment.RetryCount;

        // 3. Prepare for retry (increments retry count, resets status)
        payment.PrepareForRetry();
        await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Payment {PaymentId} retry attempt {RetryCount}", request.PaymentId, payment.RetryCount);

        // 4. Mark as processing
        payment.MarkAsProcessing();
        await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        // 5. Retry through payment gateway with new idempotency key
        var retryIdempotencyKey = $"{payment.IdempotencyKey}_retry_{payment.RetryCount}";
        var gatewayRequest = new GatewayPaymentRequest(
            IdempotencyKey: retryIdempotencyKey,
            Amount: payment.Amount,
            Currency: payment.Currency,
            CustomerId: payment.ExternalCustomerId,
            PaymentMethodId: payment.PaymentMethodId,
            Description: $"Retry {payment.RetryCount} for payment {payment.Id}",
            Metadata: new Dictionary<string, string>
            {
                ["original_payment_id"] = payment.Id.ToString(),
                ["retry_count"] = payment.RetryCount.ToString()
            });

        var gatewayResult = await paymentGateway.ProcessPaymentAsync(gatewayRequest, cancellationToken)
            .ConfigureAwait(false);
        GatewayPaymentResultBinder.BindVerifiedProviderMapping(payment, paymentGateway.ProviderId, gatewayResult);

        // 6. Update payment based on gateway result (retry schedule resolved from Payments:Retry, issue #403)
        PaymentResult? paymentResult = null;

        if (gatewayResult.Success)
        {
            payment.MarkAsSucceeded(
                gatewayResult.ExternalPaymentId ?? gatewayResult.TransactionId ?? Guid.NewGuid().ToString(),
                gatewayResult.TransactionId);

            logger.LogInformation("Payment {PaymentId} retry succeeded on attempt {RetryCount}",
                request.PaymentId, payment.RetryCount);

            paymentResult = new PaymentResult
            {
                TenantId = payment.TenantId,
                Success = true,
                TransactionId = gatewayResult.TransactionId,
                PaymentId = payment.Id.ToString(),
                Amount = new Money(payment.Amount, payment.Currency),
                ProcessedAt = payment.ProcessedAt,
                Status = PaymentStatus.Succeeded
            };
        }
        else
        {
            payment.MarkAsFailed(
                gatewayResult.ErrorMessage ?? "Payment retry failed",
                gatewayResult.ErrorCode,
                maxRetries: retryOptions.Value.MaxRetries,
                backoffBaseMinutes: retryOptions.Value.BackoffBaseMinutes,
                backoffMultiplier: retryOptions.Value.BackoffMultiplier);

            logger.LogWarning("Payment {PaymentId} retry failed on attempt {RetryCount}: {ErrorMessage}",
                request.PaymentId, payment.RetryCount, gatewayResult.ErrorMessage);

            paymentResult = PaymentResult.Failed(gatewayResult.ErrorMessage ?? "Payment retry failed");
        }

        await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        // 7. Persist an audit-trail entry for the retry attempt (issue #403)
        await RecordRetryAuditTrailAsync(
            payment,
            statusBeforeRetry,
            retryCountBeforeRetry,
            gatewayResult.ErrorMessage,
            cancellationToken).ConfigureAwait(false);

        if (gatewayResult.Success && payment.ProcessedAt.HasValue)
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
                    "Payment {PaymentId} succeeded on retry but has no authoritative billing-cycle identity; subscription synchronization was blocked",
                    payment.Id);
            }
        }

        // 8. Return retry result
        return new PaymentRetryResult
        {
            Success = gatewayResult.Success,
            RetryAttempt = payment.RetryCount,
            NextRetryAt = payment.NextRetryAt,
            PaymentResult = paymentResult,
            MaxRetriesReached = payment.MaxRetriesReached,
            FailureReason = gatewayResult.Success ? null : gatewayResult.ErrorMessage
        };
    }

    private async Task RecordRetryAuditTrailAsync(
        Payment payment,
        PaymentStatus statusBeforeRetry,
        int retryCountBeforeRetry,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        var oldValue = JsonSerializer.Serialize(new
        {
            status = statusBeforeRetry.ToString(),
            retryCount = retryCountBeforeRetry
        });
        var newValue = JsonSerializer.Serialize(new
        {
            status = payment.Status.ToString(),
            retryCount = payment.RetryCount,
            nextRetryAt = payment.NextRetryAt
        });

        await revenueAuditService.RecordAuditTrailAsync(
            entityType: "Payment",
            entityId: payment.Id,
            action: "StatusChanged",
            changedBy: RequireActorId(),
            oldValue: oldValue,
            newValue: newValue,
            reason: failureReason is null
                ? $"Payment retry attempt {payment.RetryCount} succeeded"
                : $"Payment retry attempt {payment.RetryCount} failed: {failureReason}",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private Guid RequireActorId() => actorContextAccessor.ActorContext.SubjectIdAsGuid
        ?? throw new UnauthorizedAccessException("An authenticated administrator is required.");
}
