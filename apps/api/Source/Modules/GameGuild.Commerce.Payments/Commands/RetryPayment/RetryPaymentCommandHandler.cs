using System.Text.Json;
using GameGuild.Commerce;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Actor attribution for retry attempts executed without an interactive administrator:
///     the automated retry queue (issue #415) dispatches retries as a well-known system actor.
///     Follows the Finance.Economy ledger precedent of well-known system-actor GUIDs.
/// </summary>
public static class PaymentRetryAuditActors
{
    /// <summary>Audit <c>changedBy</c> used for retries dispatched by the automated retry queue.</summary>
    public static readonly Guid RetryQueueSystemActorId = new("b6a4d201-4c15-4f7e-9a63-2d8c05f41500");
}

/// <summary>
///     Handler for retrying failed payments. Both the manual admin retry endpoint and the
///     automated retry queue (issue #415) dispatch through this single path: each attempt is
///     claimed via <see cref="Payment.PrepareForRetry" />, executed at the gateway, recorded in
///     the <see cref="PaymentRetryAttempt" /> history, scheduled by the failure-code retry
///     policy, and — when the retry budget is exhausted — escalated to a dunning suspension of
///     the subscription. A successful retry of a dunning-suspended subscription reactivates it.
/// </summary>
public sealed class RetryPaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGateway paymentGateway,
    IPaymentSubscriptionSyncService paymentSubscriptionSyncService,
    IOptions<PaymentRetryOptions> retryOptions,
    IPaymentRetryPolicyResolver retryPolicyResolver,
    IPaymentDunningLifecycleService dunningLifecycleService,
    IRevenueAuditService revenueAuditService,
    IActorContextAccessor actorContextAccessor,
    ILogger<RetryPaymentCommandHandler> logger,
    INotificationService? notificationService = null,
    IPaymentMethodRepository? paymentMethodRepository = null) : ICommandHandler<RetryPaymentCommand, PaymentRetryResult>
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
                : payment.FraudBlocksRetry
                    ? "Payment is held by the fraud gate; resolve the fraud review before retrying"
                    : payment.IsRetriable
                        ? $"Payment in status {payment.Status} cannot be retried"
                        : "Payment failure is non-retriable (hard decline)";

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
        var scheduledForUtc = payment.NextRetryAt; // When the queue scheduled this attempt.

        // 3. Prepare for retry (increments retry count, resets status). The persisted
        //    Failed → Pending transition is the queue's claim: a second dispatcher loading
        //    the payment afterwards no longer sees a Failed payment to retry.
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

        // Failover may route the retry away from the primary gateway (issue #413):
        // attribute the payment to the gateway that actually answered before binding
        // the verified provider mapping. When a provider mapping is already bound the
        // mapped provider stays authoritative (RecordProcessingProvider is a no-op).
        payment.RecordProcessingProvider(gatewayResult.ProviderId ?? paymentGateway.ProviderId);
        GatewayPaymentResultBinder.BindVerifiedProviderMapping(payment, payment.Provider, gatewayResult);

        // 6. Update payment based on gateway result. The retry schedule is resolved from the
        //    failure-code retry policy (issue #415) on top of the Payments:Retry options.
        PaymentResult? paymentResult = null;
        PaymentRetrySchedule? failureSchedule = null;

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
            failureSchedule = retryPolicyResolver.ComputeSchedule(gatewayResult.ErrorCode, payment.RetryCount);

            payment.MarkAsFailed(
                gatewayResult.ErrorMessage ?? "Payment retry failed",
                gatewayResult.ErrorCode,
                maxRetries: failureSchedule.EffectiveMaxRetries,
                backoffBaseMinutes: retryOptions.Value.BackoffBaseMinutes,
                backoffMultiplier: retryOptions.Value.BackoffMultiplier,
                nextRetryAt: failureSchedule.NextRetryAt,
                isRetriable: failureSchedule.IsRetriable);

            logger.LogWarning(
                "Payment {PaymentId} retry failed on attempt {RetryCount}: {ErrorMessage} (class {Classification}, next retry {NextRetryAt})",
                request.PaymentId, payment.RetryCount, gatewayResult.ErrorMessage,
                failureSchedule.Classification, failureSchedule.NextRetryAt);

            paymentResult = PaymentResult.Failed(gatewayResult.ErrorMessage ?? "Payment retry failed");
        }

        await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        // 7. Persist the retry-attempt history entry (issue #415).
        var attempt = PaymentRetryAttempt.Create(
            paymentId: payment.Id,
            attemptNumber: payment.RetryCount,
            scheduledForUtc: scheduledForUtc,
            executedAtUtc: SystemClock.UtcNow,
            outcome: payment.Status,
            gateway: paymentGateway.ProviderId,
            failureCode: gatewayResult.Success ? null : gatewayResult.ErrorCode,
            failureMessage: gatewayResult.Success ? null : gatewayResult.ErrorMessage,
            gatewayTransactionId: gatewayResult.TransactionId);

        await RecordAttemptHistoryAsync(attempt, cancellationToken).ConfigureAwait(false);

        // 8. Persist an audit-trail entry for the retry attempt (issue #403)
        await RecordRetryAuditTrailAsync(
            payment,
            statusBeforeRetry,
            retryCountBeforeRetry,
            gatewayResult.ErrorMessage,
            cancellationToken).ConfigureAwait(false);

        // 9. Recovery lifecycle (issue #415): a successful retry lifts a dunning suspension;
        //    a terminal failure suspends the subscription and sends the final dunning notice.
        await ApplyRecoveryLifecycleAsync(payment, gatewayResult.Success, gatewayResult.ErrorMessage, cancellationToken)
            .ConfigureAwait(false);

        // Payment-event notifications (issue #415): a successful retry emails the payment
        // receipt; the second failure overall prompts the customer to update the payment method.
        await SendOutcomeNotificationsAsync(payment, gatewayResult.Success, cancellationToken)
            .ConfigureAwait(false);

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

        // 10. Return retry result
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

    /// <summary>
    ///     Outcome notifications for a retry attempt (issue #415): the payment receipt on
    ///     success, and the update-payment-method prompt on the second failure overall
    ///     (first retry failure, <c>RetryCount == 1</c>). Best-effort: notification delivery
    ///     problems never fail the recorded retry outcome.
    /// </summary>
    private async Task SendOutcomeNotificationsAsync(
        Payment payment,
        bool retrySucceeded,
        CancellationToken cancellationToken)
    {
        if (notificationService is null)
        {
            return;
        }

        Guid? ownerUserId = null;
        try
        {
            ownerUserId = await ResolvePaymentOwnerAsync(payment, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Owner resolution for payment {PaymentId} outcome notifications failed",
                payment.Id);
        }

        if (ownerUserId is null)
        {
            return;
        }

        try
        {
            if (retrySucceeded)
            {
                var result = await notificationService.SendAsync(
                    recipientId: ownerUserId.Value,
                    type: NotificationType.PaymentReceipt,
                    title: "Payment receipt",
                    message:
                        $"Your retried payment of {payment.Amount:0.##} {payment.Currency} was processed successfully " +
                        $"on attempt {payment.RetryCount}. Payment reference " +
                        $"{payment.ExternalTransactionId ?? payment.Id.ToString()}, processed {payment.ProcessedAt:u}.",
                    channel: NotificationChannel.Email,
                    tenantId: payment.TenantId,
                    priority: NotificationPriority.Normal,
                    referenceEntityId: payment.Id,
                    referenceEntityType: nameof(Payment),
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!result.IsSuccess)
                {
                    logger.LogInformation(
                        "Payment receipt for payment {PaymentId} was not delivered: {Reason}",
                        payment.Id,
                        result.Error?.Description ?? "preference decision");
                }

                return;
            }

            if (payment.RetryCount == 1)
            {
                var prompt = await notificationService.SendAsync(
                    recipientId: ownerUserId.Value,
                    type: NotificationType.PaymentMethodUpdateRequired,
                    title: "Update your payment method",
                    message:
                        $"Your payment of {payment.Amount:0.##} {payment.Currency} has failed twice" +
                        (string.IsNullOrWhiteSpace(payment.FailureReason)
                            ? string.Empty
                            : $" ({payment.FailureReason})") +
                        ". Please update your payment method so the next retry can succeed and your service stays active.",
                    channel: NotificationChannel.Email,
                    tenantId: payment.TenantId,
                    priority: NotificationPriority.High,
                    referenceEntityId: payment.Id,
                    referenceEntityType: nameof(Payment),
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!prompt.IsSuccess)
                {
                    logger.LogInformation(
                        "Payment-method update prompt for payment {PaymentId} was not delivered: {Reason}",
                        payment.Id,
                        prompt.Error?.Description ?? "preference decision");
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Outcome notification for payment {PaymentId} failed; the retry outcome stays recorded",
                payment.Id);
        }
    }

    /// <summary>Resolves the user that owns the payment through its payment-method registration.</summary>
    private async Task<Guid?> ResolvePaymentOwnerAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (paymentMethodRepository is null || string.IsNullOrWhiteSpace(payment.PaymentMethodId))
        {
            return null;
        }

        var method = await paymentMethodRepository
            .GetByGatewayIdAsync(payment.TenantId, payment.Provider, payment.PaymentMethodId, cancellationToken)
            .ConfigureAwait(false);

        return method?.UserId;
    }

    private async Task RecordAttemptHistoryAsync(
        PaymentRetryAttempt attempt,
        CancellationToken cancellationToken)
    {
        try
        {
            await paymentRepository.AddRetryAttemptAsync(attempt, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // The attempt history must never fail the payment retry itself: the payment state
            // transition already persisted, and the history row can be reconciled from the
            // audit trail if the write is lost.
            logger.LogError(exception,
                "Failed to persist the retry-attempt history for payment {PaymentId} attempt {AttemptNumber}.",
                attempt.PaymentId,
                attempt.AttemptNumber);
        }
    }

    private async Task ApplyRecoveryLifecycleAsync(
        Payment payment,
        bool retrySucceeded,
        string? failureMessage,
        CancellationToken cancellationToken)
    {
        if (!payment.SubscriptionId.HasValue)
        {
            return;
        }

        try
        {
            if (retrySucceeded)
            {
                var reactivated = await dunningLifecycleService
                    .TryReactivateAfterRecoveryAsync(payment.SubscriptionId.Value, cancellationToken)
                    .ConfigureAwait(false);

                if (reactivated)
                {
                    PaymentRetryQueueMetrics.RecordRecovery(payment.Provider);
                }

                return;
            }

            if (!payment.CanRetry)
            {
                // Terminal failure: the budget is exhausted or the failure class is a hard
                // decline. Suspend the subscription and trigger the final dunning notice.
                var suspended = await dunningLifecycleService
                    .SuspendForRetryExhaustionAsync(
                        payment.SubscriptionId.Value,
                        payment.TenantId,
                        failureMessage ?? payment.FailureReason ?? "payment retry exhausted",
                        cancellationToken)
                    .ConfigureAwait(false);

                if (suspended)
                {
                    PaymentRetryQueueMetrics.RecordExhausted(payment.Provider);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Dunning lifecycle failures must not roll back the recorded payment outcome.
            logger.LogError(exception,
                "Recovery lifecycle step failed for payment {PaymentId} (subscription {SubscriptionId}).",
                payment.Id,
                payment.SubscriptionId);
        }
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

    /// <summary>
    ///     Interactive administrators stay the audit actor; retries dispatched by the automated
    ///     retry queue run as a well-known system actor (issue #415). Anonymous callers still
    ///     fail closed.
    /// </summary>
    private Guid RequireActorId()
    {
        var actorContext = actorContextAccessor.ActorContext;

        if (actorContext.SubjectIdAsGuid is { } actorId)
        {
            return actorId;
        }

        if (actorContext.ActorKind == ActorKind.System)
        {
            return PaymentRetryAuditActors.RetryQueueSystemActorId;
        }

        throw new UnauthorizedAccessException("An authenticated administrator is required.");
    }
}
