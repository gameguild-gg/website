using GameGuild.CQRS;
using GameGuild.Commerce;
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
    ILogger<ProcessPaymentCommandHandler> logger) : ICommandHandler<ProcessPaymentCommand, PaymentResult>
{
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
                FailureReason = existingPayment.FailureReason
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

        // 4. Mark as processing
        payment.MarkAsProcessing();
        await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        // 5. Process through payment gateway
        var gatewayRequest = new GatewayPaymentRequest(
            IdempotencyKey: idempotencyKey,
            Amount: subscription.Amount,
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
            PaymentMethodId = request.PaymentMethodId
        };
    }
}
