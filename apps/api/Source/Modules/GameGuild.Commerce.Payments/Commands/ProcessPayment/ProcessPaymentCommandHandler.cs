using GameGuild.CQRS;
using GameGuild.Commerce;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for processing payment commands
/// </summary>
public sealed class ProcessPaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IPaymentGateway paymentGateway,
    IPaymentSubscriptionSyncService paymentSubscriptionSyncService,
    ISubscriptionPaymentContextService subscriptionPaymentContextService,
    IOptions<PaymentRetryOptions> retryOptions,
    IPaymentRetryPolicyResolver retryPolicyResolver,
    IFraudDetectionService fraudDetectionService,
    ILogger<ProcessPaymentCommandHandler> logger,
    IAccountCreditService? creditService = null,
    IOptions<CreditAutoApplyOptions>? creditAutoApplyOptions = null,
    INotificationService? notificationService = null) : ICommandHandler<ProcessPaymentCommand, PaymentResult>
{
    private const string SubscriptionInvoiceKind = "subscription";

    public async Task<PaymentResult> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionPaymentContextService.GetPaymentContextAsync(request.SubscriptionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Payment subscription {request.SubscriptionId} was not found.");

        if (subscription.TenantId != request.TenantId)
        {
            throw new UnauthorizedAccessException("The payment subscription does not belong to the requested tenant.");
        }

        if (subscription.Amount != request.Amount)
        {
            throw new InvalidOperationException("The requested payment amount does not match the authoritative subscription amount.");
        }

        if (subscription.Amount <= 0m || string.IsNullOrWhiteSpace(subscription.Currency))
        {
            throw new InvalidOperationException("The subscription does not contain valid authoritative pricing.");
        }

        var idempotencyKey = SubscriptionPaymentIdentity.CreateIdempotencyKey(
            subscription.TenantId,
            subscription.SubscriptionId,
            subscription.BillingCycleNumber);

        logger.LogInformation(
            "Processing payment for tenant {TenantId}, subscription {SubscriptionId}, amount {Amount}",
            request.TenantId, request.SubscriptionId, request.Amount);

        // 1. Check for existing payment with same idempotency key (idempotency check)
        var existingPayment = await paymentRepository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken)
            .ConfigureAwait(false);

        if (existingPayment != null)
        {
            logger.LogInformation("Payment already exists with idempotency key {IdempotencyKey}, returning existing result",
                idempotencyKey);

            return new PaymentResult
            {
                TenantId = existingPayment.TenantId,
                Success = existingPayment.Status == PaymentStatus.Succeeded,
                TransactionId = existingPayment.ExternalTransactionId,
                PaymentId = existingPayment.Id.ToString(),
                Amount = new Money(existingPayment.Amount, existingPayment.Currency),
                ProcessedAt = existingPayment.ProcessedAt,
                Status = existingPayment.Status,
                FailureReason = existingPayment.FailureReason,
                CreditApplied = existingPayment.CreditAppliedAmount > 0 ? existingPayment.CreditAppliedAmount : null
            };
        }

        var externalCustomerId = subscription.ExternalCustomerId;

        // 2. Create payment record (retry budget comes from Payments:Retry options, issue #403)
        var payment = Payment.Create(
            tenantId: subscription.TenantId,
            amount: subscription.Amount,
            currency: subscription.Currency,
            idempotencyKey: idempotencyKey,
            provider: paymentGateway.ProviderId,
            subscriptionId: request.SubscriptionId,
            externalCustomerId: externalCustomerId,
            paymentMethodId: request.PaymentMethodId,
            maxRetries: retryOptions.Value.MaxRetries);

        await paymentRepository.AddAsync(payment, cancellationToken).ConfigureAwait(false);

        // 3. Pre-gateway fraud assessment (issue #409): Review/Block hold the payment as
        //    failed without gateway execution and without an automatic retry.
        var fraudContext = new FraudSignalContext(
            payment.TenantId,
            payment.Id,
            request.SubscriptionId,
            externalCustomerId,
            request.PaymentMethodId,
            subscription.Amount,
            subscription.Currency);

        var assessment = await fraudDetectionService.AssessPaymentAsync(fraudContext, cancellationToken)
            .ConfigureAwait(false);

        payment.RecordFraudAssessment(
            assessment.Decision,
            assessment.Score,
            assessment.Reason,
            assessment.ToEvidenceJson());

        await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        if (assessment.Decision != FraudDecision.Allow)
        {
            logger.LogWarning(
                "Payment {PaymentId} held by fraud gate: decision {Decision}, score {Score}, reason {Reason}",
                payment.Id, assessment.Decision, assessment.Score, assessment.Reason);

            return new PaymentResult
            {
                TenantId = payment.TenantId,
                Success = false,
                PaymentId = payment.Id.ToString(),
                Amount = new Money(payment.Amount, payment.Currency),
                ProcessedAt = payment.ProcessedAt,
                Status = payment.Status,
                FailureReason = payment.FailureReason,
                PaymentMethodId = request.PaymentMethodId
            };
        }

        // Account-credit auto-apply (issue #410): plan the credit tender BEFORE charging the
        // payment method so the gateway charge is reduced by the spendable credits, capped at
        // the invoice amount (FIFO by expiry at consumption time). The consumption itself is
        // only committed after the charge outcome is known, so a failed charge never burns
        // credits. Config-gated via Credits:AutoApply:Enabled (default false); without an
        // owner or a credit service the path is a no-op.
        var plannedCreditAmount = 0m;
        var chargeAmount = subscription.Amount;
        Guid? autoApplyOwnerId = null;
        if (creditAutoApplyOptions?.Value.Enabled == true
            && creditService is not null
            && subscription.OwnerUserId is { } creditOwnerId)
        {
            autoApplyOwnerId = creditOwnerId;
            var balances = await creditService
                .GetBalancesAsync(subscription.TenantId, creditOwnerId, cancellationToken)
                .ConfigureAwait(false);
            var available = balances
                .FirstOrDefault(balance => string.Equals(balance.Currency, subscription.Currency, StringComparison.OrdinalIgnoreCase))
                ?.Available ?? 0m;
            plannedCreditAmount = Math.Min(available, subscription.Amount);
            chargeAmount = subscription.Amount - plannedCreditAmount;

            if (plannedCreditAmount > 0)
            {
                logger.LogInformation(
                    "Auto-applying {CreditAmount} {Currency} of account credit for subscription {SubscriptionId}; gateway charge reduced from {InvoiceAmount} to {ChargeAmount}",
                    plannedCreditAmount,
                    subscription.Currency,
                    request.SubscriptionId,
                    subscription.Amount,
                    chargeAmount);
            }
        }

        // 4. Mark as processing
        payment.MarkAsProcessing();
        await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        // 5. Process through payment gateway — or settle entirely with credits when they
        //    cover the full invoice amount (no payment-method charge happens at all).
        if (chargeAmount <= 0m && plannedCreditAmount > 0m)
        {
            return await SettleWithCreditsOnlyAsync(
                    payment,
                    autoApplyOwnerId!.Value,
                    plannedCreditAmount,
                    subscription.BillingCycleNumber,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var gatewayRequest = new GatewayPaymentRequest(
            IdempotencyKey: idempotencyKey,
            Amount: chargeAmount,
            Currency: subscription.Currency,
            CustomerId: externalCustomerId,
            PaymentMethodId: request.PaymentMethodId,
            Description: $"Payment for subscription {request.SubscriptionId}",
            Metadata: new Dictionary<string, string>
            {
                ["tenant_id"] = subscription.TenantId.ToString(),
                ["subscription_id"] = request.SubscriptionId.ToString(),
                ["billing_cycle"] = subscription.BillingCycleNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });

        var gatewayResult = await paymentGateway.ProcessPaymentAsync(gatewayRequest, cancellationToken)
            .ConfigureAwait(false);

        // Failover may route the charge away from the primary gateway (issue #413):
        // attribute the payment to the gateway that actually answered before binding
        // the verified provider mapping. When a provider mapping is already bound the
        // mapped provider stays authoritative (RecordProcessingProvider is a no-op).
        payment.RecordProcessingProvider(gatewayResult.ProviderId ?? paymentGateway.ProviderId);
        GatewayPaymentResultBinder.BindVerifiedProviderMapping(payment, payment.Provider, gatewayResult);

        // 6. Update payment based on gateway result
        if (gatewayResult.Success)
        {
            payment.MarkAsSucceeded(
                gatewayResult.ExternalPaymentId ?? gatewayResult.TransactionId ?? Guid.NewGuid().ToString(),
                gatewayResult.TransactionId);

            logger.LogInformation("Payment {PaymentId} succeeded with transaction {TransactionId}",
                payment.Id, gatewayResult.TransactionId);
        }
        else if (gatewayResult.Status == PaymentStatus.RequiresAction)
        {
            payment.MarkAsRequiresAction(gatewayResult.TransactionId);
            logger.LogInformation("Payment {PaymentId} requires additional action", payment.Id);
        }
        else
        {
            // First-failure classification (issue #415): hard declines are marked non-retriable
            // immediately and never enter the retry queue; everything else is scheduled by the
            // failure-code policy on top of the Payments:Retry options.
            var schedule = retryPolicyResolver.ComputeSchedule(gatewayResult.ErrorCode, payment.RetryCount);

            payment.MarkAsFailed(
                gatewayResult.ErrorMessage ?? "Payment processing failed",
                gatewayResult.ErrorCode,
                maxRetries: schedule.EffectiveMaxRetries,
                backoffBaseMinutes: retryOptions.Value.BackoffBaseMinutes,
                backoffMultiplier: retryOptions.Value.BackoffMultiplier,
                nextRetryAt: schedule.NextRetryAt,
                isRetriable: schedule.IsRetriable);

            logger.LogWarning("Payment {PaymentId} failed: {ErrorMessage} ({ErrorCode}, class {Classification}, retriable {IsRetriable})",
                payment.Id, gatewayResult.ErrorMessage, gatewayResult.ErrorCode, schedule.Classification, schedule.IsRetriable);
        }

        // Credits are consumed only after the charge outcome is known: a failed or
        // action-pending charge leaves the grants untouched.
        var appliedCredit = 0m;
        if (gatewayResult.Success && plannedCreditAmount > 0m && autoApplyOwnerId.HasValue)
        {
            appliedCredit = await ConsumePlannedCreditsAsync(
                    payment,
                    autoApplyOwnerId.Value,
                    plannedCreditAmount,
                    cancellationToken)
                .ConfigureAwait(false);

            if (appliedCredit > 0m)
            {
                payment.RecordCreditApplied(appliedCredit);
            }
        }

        await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        if (gatewayResult.Success && payment.ProcessedAt.HasValue)
        {
            await paymentSubscriptionSyncService.SyncSuccessfulPaymentAsync(
                payment.Id,
                payment.SubscriptionId,
                payment.Amount,
                payment.Currency,
                subscription.BillingCycleNumber,
                payment.ProcessedAt.Value,
                cancellationToken).ConfigureAwait(false);
        }

        if (appliedCredit > 0m && autoApplyOwnerId.HasValue)
        {
            await NotifyCreditAppliedAsync(payment, autoApplyOwnerId.Value, appliedCredit, cancellationToken)
                .ConfigureAwait(false);
        }

        // 7. Return result
        return new PaymentResult
        {
            TenantId = payment.TenantId,
            Success = gatewayResult.Success,
            TransactionId = gatewayResult.TransactionId,
            PaymentId = payment.Id.ToString(),
            Amount = new Money(payment.Amount, payment.Currency),
            ProcessedAt = payment.ProcessedAt,
            Status = payment.Status,
            FailureReason = gatewayResult.ErrorMessage,
            PaymentMethodId = request.PaymentMethodId,
            CreditApplied = appliedCredit > 0m ? appliedCredit : null
        };
    }

    /// <summary>
    ///     Full-credit-coverage settlement (issue #410 auto-apply): the credits cover the
    ///     whole invoice amount, so no gateway charge happens. Fails closed when the planned
    ///     consumption cannot be committed (for example the balance was drained concurrently):
    ///     the payment is recorded as failed and retriable, and no credits move.
    /// </summary>
    private async Task<PaymentResult> SettleWithCreditsOnlyAsync(
        Payment payment,
        Guid ownerUserId,
        decimal plannedCreditAmount,
        int billingCycleNumber,
        CancellationToken cancellationToken)
    {
        var appliedCredit = await ConsumePlannedCreditsAsync(payment, ownerUserId, plannedCreditAmount, cancellationToken)
            .ConfigureAwait(false);

        if (appliedCredit <= 0m)
        {
            var schedule = retryPolicyResolver.ComputeSchedule(failureCode: null, payment.RetryCount);
            payment.MarkAsFailed(
                "Account credit auto-apply could not be reconciled; no charge was made.",
                "credit_auto_apply_reconcile_failed",
                maxRetries: schedule.EffectiveMaxRetries,
                backoffBaseMinutes: retryOptions.Value.BackoffBaseMinutes,
                backoffMultiplier: retryOptions.Value.BackoffMultiplier,
                nextRetryAt: schedule.NextRetryAt,
                isRetriable: schedule.IsRetriable);
            await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

            return new PaymentResult
            {
                TenantId = payment.TenantId,
                Success = false,
                PaymentId = payment.Id.ToString(),
                Amount = new Money(payment.Amount, payment.Currency),
                ProcessedAt = payment.ProcessedAt,
                Status = payment.Status,
                FailureReason = payment.FailureReason
            };
        }

        payment.RecordCreditApplied(appliedCredit);
        var settlementReference = $"account-credit:{payment.Id}";
        payment.MarkAsSucceeded(settlementReference, settlementReference);

        await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        if (payment.ProcessedAt.HasValue)
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

        await NotifyCreditAppliedAsync(payment, ownerUserId, appliedCredit, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Payment {PaymentId} settled entirely with {CreditAmount} {Currency} of account credit; no gateway charge",
            payment.Id,
            appliedCredit,
            payment.Currency);

        return new PaymentResult
        {
            TenantId = payment.TenantId,
            Success = true,
            TransactionId = settlementReference,
            PaymentId = payment.Id.ToString(),
            Amount = new Money(payment.Amount, payment.Currency),
            ProcessedAt = payment.ProcessedAt,
            Status = payment.Status,
            CreditApplied = appliedCredit
        };
    }

    private async Task<decimal> ConsumePlannedCreditsAsync(
        Payment payment,
        Guid ownerUserId,
        decimal plannedAmount,
        CancellationToken cancellationToken)
    {
        if (creditService is null || plannedAmount <= 0m)
        {
            return 0m;
        }

        var consumption = await creditService
            .ConsumeAsync(
                payment.TenantId,
                ownerUserId,
                payment.Currency,
                plannedAmount,
                payment.Id,
                SubscriptionInvoiceKind,
                $"payment:{payment.Id}:credit-auto-apply",
                cancellationToken)
            .ConfigureAwait(false);

        if (consumption.IsFailure)
        {
            // The planned credit tender could not be committed (for example the balance was
            // drained concurrently). The already-made provider charge stands; flag the
            // shortfall for manual settlement instead of silently re-charging the user.
            logger.LogWarning(
                "Planned credit auto-apply of {PlannedAmount} {Currency} for payment {PaymentId} could not be consumed ({Error}); the provider charge stands and the credit shortfall must be settled manually",
                plannedAmount,
                payment.Currency,
                payment.Id,
                consumption.Error.Description);
            return 0m;
        }

        return consumption.Value.AmountApplied;
    }

    private async Task NotifyCreditAppliedAsync(
        Payment payment,
        Guid ownerUserId,
        decimal appliedAmount,
        CancellationToken cancellationToken)
    {
        if (notificationService is null)
        {
            return;
        }

        try
        {
            var chargedAmount = payment.Amount - appliedAmount;
            var result = await notificationService
                .SendAsync(
                    recipientId: ownerUserId,
                    type: NotificationType.CreditApplied,
                    title: "Account credits applied to your payment",
                    message:
                        $"{appliedAmount:0.##} {payment.Currency} of account credit was applied to your invoice payment. " +
                        (chargedAmount > 0m
                            ? $"Your payment method was charged {chargedAmount:0.##} {payment.Currency}."
                            : "Your payment method was not charged."),
                    channel: NotificationChannel.Email,
                    tenantId: payment.TenantId,
                    priority: NotificationPriority.Normal,
                    referenceEntityId: payment.Id,
                    referenceEntityType: nameof(Payment),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                logger.LogInformation(
                    "Credit-applied notice for payment {PaymentId} was not delivered: {Reason}",
                    payment.Id,
                    result.Error?.Description ?? "preference decision");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Notifications are best-effort: delivery problems must never fail a settled payment.
            logger.LogWarning(
                exception,
                "Failed to send the credit-applied notice for payment {PaymentId}",
                payment.Id);
        }
    }
}
