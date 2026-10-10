using GameGuild.Commerce;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
/// Processes and verifies payments bound to immutable order snapshots.
/// </summary>
public sealed class OrderPaymentService(
    IPaymentRepository paymentRepository,
    IPaymentGateway paymentGateway,
    IOptions<PaymentRetryOptions> retryOptions,
    ILogger<OrderPaymentService> logger) : IOrderPaymentProcessor, IOrderPaymentAuthority
{
    internal static readonly TimeSpan SafeProviderReplayWindow = TimeSpan.FromHours(23);

    public string? GetPaymentMethodValidationError(string paymentMethodId) =>
        StripePaymentMethodIdentifier.IsValid(paymentMethodId)
            ? null
            : StripePaymentMethodIdentifier.ValidationMessage;

    public async Task<OrderChargeResult> ProcessAsync(
        AuthoritativeOrderCharge charge,
        CancellationToken cancellationToken = default)
    {
        Validate(charge);
        var idempotencyKey = CreateIdempotencyKey(charge.TenantId, charge.OrderId);
        var existingPayment = await paymentRepository
            .GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken)
            .ConfigureAwait(false);

        if (existingPayment is not null)
        {
            return await ProcessExistingAsync(existingPayment, charge, cancellationToken).ConfigureAwait(false);
        }

        var proposedPayment = Payment.Create(
            charge.TenantId,
            charge.Amount,
            charge.Currency,
            idempotencyKey,
            paymentGateway.ProviderId,
            orderId: charge.OrderId,
            paymentMethodId: charge.PaymentMethodId,
            description: $"Payment for order {charge.OrderId}",
            maxRetries: retryOptions.Value.MaxRetries);
        var payment = await paymentRepository.AddAsync(proposedPayment, cancellationToken).ConfigureAwait(false);
        if (payment.Id != proposedPayment.Id)
            return await ProcessExistingAsync(payment, charge, cancellationToken).ConfigureAwait(false);

        return await ProcessAttemptAsync(payment, charge, idempotencyKey, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OrderChargeResult> ProcessExistingAsync(
        Payment payment,
        AuthoritativeOrderCharge charge,
        CancellationToken cancellationToken)
    {
        EnsureMatches(payment, charge);

        if (payment.Status == PaymentStatus.Succeeded)
            return OrderChargeResult.Succeeded(payment.Id, payment.ExternalPaymentId);

        if (payment.Status == PaymentStatus.Pending)
        {
            var originalCharge = charge with
            {
                PaymentMethodId = payment.PaymentMethodId ?? charge.PaymentMethodId
            };
            return await ProcessAttemptAsync(
                    payment,
                    originalCharge,
                    CreateActiveAttemptIdempotencyKey(payment),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (payment.Status == PaymentStatus.RequiresAction)
        {
            if (string.IsNullOrWhiteSpace(payment.ExternalTransactionId))
            {
                return OrderChargeResult.RequiresAction(
                    payment.Id,
                    "Additional payment action is required.",
                    clientActionToken: null);
            }

            var providerResult = await paymentGateway
                .GetPaymentAsync(payment.ExternalTransactionId, cancellationToken)
                .ConfigureAwait(false);
            return await ApplyGatewayResultAsync(payment, providerResult, cancellationToken).ConfigureAwait(false);
        }

        if (payment.Status == PaymentStatus.Processing)
        {
            if (!string.IsNullOrWhiteSpace(payment.ExternalTransactionId))
            {
                var providerResult = await paymentGateway
                    .GetPaymentAsync(payment.ExternalTransactionId, cancellationToken)
                    .ConfigureAwait(false);
                return await ApplyGatewayResultAsync(payment, providerResult, cancellationToken).ConfigureAwait(false);
            }

            if (payment.UpdatedAt < SystemClock.UtcNow.Subtract(SafeProviderReplayWindow))
            {
                return OrderChargeResult.RequiresReconciliation(
                    payment.Id,
                    "The provider outcome is unknown and the safe idempotency replay window has expired.");
            }

            var originalCharge = charge with
            {
                PaymentMethodId = payment.PaymentMethodId ?? charge.PaymentMethodId
            };
            return await ProcessAttemptAsync(
                    payment,
                    originalCharge,
                    CreateActiveAttemptIdempotencyKey(payment),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (payment.Status == PaymentStatus.Failed && payment.CanRetry)
        {
            var reconciliation = await CloseFailedProviderAttemptAsync(payment, cancellationToken).ConfigureAwait(false);
            if (reconciliation is not null)
                return reconciliation;

            payment.PrepareForRetry(charge.PaymentMethodId);
            await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);
            return await ProcessAttemptAsync(
                    payment,
                    charge,
                    CreateActiveAttemptIdempotencyKey(payment),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (payment.Status == PaymentStatus.Failed)
        {
            return await ProcessReplacementGenerationAsync(payment, charge, cancellationToken).ConfigureAwait(false);
        }

        return OrderChargeResult.Failed(
            payment.Id,
            payment.FailureReason ?? $"Payment is {payment.Status}; retry after its state changes.");
    }

    private async Task<OrderChargeResult> ProcessAttemptAsync(
        Payment payment,
        AuthoritativeOrderCharge charge,
        string gatewayIdempotencyKey,
        CancellationToken cancellationToken)
    {
        if (payment.Status == PaymentStatus.Pending)
        {
            payment.MarkAsProcessing();
            await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);
        }
        else if (payment.Status != PaymentStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot process payment in {payment.Status} status.");
        }

        var gatewayResult = await paymentGateway.ProcessPaymentAsync(
            new GatewayPaymentRequest(
                gatewayIdempotencyKey,
                charge.Amount,
                charge.Currency,
                CustomerId: null,
                charge.PaymentMethodId,
                $"Payment for order {charge.OrderId}",
                new Dictionary<string, string>
                {
                    ["tenant_id"] = charge.TenantId.ToString(),
                    ["order_id"] = charge.OrderId.ToString()
                }),
            cancellationToken).ConfigureAwait(false);

        return await ApplyGatewayResultAsync(payment, gatewayResult, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OrderChargeResult> ApplyGatewayResultAsync(
        Payment payment,
        GatewayPaymentResult gatewayResult,
        CancellationToken cancellationToken)
    {
        GatewayPaymentResultBinder.BindVerifiedProviderMapping(payment, paymentGateway.ProviderId, gatewayResult);
        var stateChanged = gatewayResult.ProviderMapping is not null;

        if (gatewayResult.Success || gatewayResult.Status == PaymentStatus.Succeeded)
        {
            payment.MarkAsSucceeded(
                gatewayResult.ExternalPaymentId ?? gatewayResult.TransactionId ?? Guid.NewGuid().ToString(),
                gatewayResult.TransactionId);
            stateChanged = true;
        }
        else if (gatewayResult.Status == PaymentStatus.RequiresAction)
        {
            if (payment.Status != PaymentStatus.RequiresAction)
            {
                payment.MarkAsRequiresAction(gatewayResult.TransactionId);
                stateChanged = true;
            }
            else if (payment.BindExternalTransactionId(gatewayResult.TransactionId))
            {
                stateChanged = true;
            }
        }
        else if (gatewayResult.Status is PaymentStatus.Pending or PaymentStatus.Processing)
        {
            if (payment.Status != PaymentStatus.Processing)
            {
                payment.MarkAsProcessing(gatewayResult.TransactionId);
                stateChanged = true;
            }
            else if (payment.BindExternalTransactionId(gatewayResult.TransactionId))
            {
                stateChanged = true;
            }
        }
        else
        {
            payment.MarkAsFailed(
                gatewayResult.ErrorMessage ?? "Order payment failed.",
                gatewayResult.ErrorCode,
                maxRetries: retryOptions.Value.MaxRetries,
                backoffBaseMinutes: retryOptions.Value.BackoffBaseMinutes,
                backoffMultiplier: retryOptions.Value.BackoffMultiplier);
            stateChanged = true;
        }

        if (stateChanged)
            await paymentRepository.UpdateAsync(payment, cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Order payment {PaymentId} for order {OrderId} finished with status {Status}",
            payment.Id,
            payment.OrderId,
            payment.Status);

        return payment.Status switch
        {
            PaymentStatus.Succeeded => OrderChargeResult.Succeeded(payment.Id, payment.ExternalPaymentId),
            PaymentStatus.RequiresAction => OrderChargeResult.RequiresAction(
                payment.Id,
                gatewayResult.ErrorMessage ?? "Additional payment action is required.",
                gatewayResult.ClientActionToken),
            PaymentStatus.Pending or PaymentStatus.Processing => OrderChargeResult.Processing(
                payment.Id,
                gatewayResult.ErrorMessage ?? "Payment is pending provider reconciliation."),
            _ => OrderChargeResult.Failed(
                payment.Id,
                payment.FailureReason ?? gatewayResult.ErrorMessage ?? "Order payment failed.")
        };
    }

    private async Task<OrderChargeResult> ProcessReplacementGenerationAsync(
        Payment exhaustedPayment,
        AuthoritativeOrderCharge charge,
        CancellationToken cancellationToken)
    {
        var replacementKey = CreateReplacementIdempotencyKey(charge.TenantId, charge.OrderId, exhaustedPayment.Id);
        var existingReplacement = await paymentRepository
            .GetByIdempotencyKeyAsync(replacementKey, cancellationToken)
            .ConfigureAwait(false);
        if (existingReplacement is not null)
            return await ProcessExistingAsync(existingReplacement, charge, cancellationToken).ConfigureAwait(false);

        var reconciliation = await CloseFailedProviderAttemptAsync(exhaustedPayment, cancellationToken).ConfigureAwait(false);
        if (reconciliation is not null)
            return reconciliation;

        var proposedPayment = Payment.Create(
            charge.TenantId,
            charge.Amount,
            charge.Currency,
            replacementKey,
            paymentGateway.ProviderId,
            orderId: charge.OrderId,
            paymentMethodId: charge.PaymentMethodId,
            description: $"Replacement payment for order {charge.OrderId}",
            maxRetries: retryOptions.Value.MaxRetries);
        var payment = await paymentRepository.AddAsync(proposedPayment, cancellationToken).ConfigureAwait(false);
        if (payment.Id != proposedPayment.Id)
            return await ProcessExistingAsync(payment, charge, cancellationToken).ConfigureAwait(false);

        return await ProcessAttemptAsync(payment, charge, replacementKey, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OrderChargeResult?> CloseFailedProviderAttemptAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payment.ExternalTransactionId))
            return null;

        var cancellation = await paymentGateway
            .CancelPaymentAsync(payment.ExternalTransactionId, cancellationToken)
            .ConfigureAwait(false);
        if (cancellation.Success)
            return null;

        return OrderChargeResult.RequiresReconciliation(
            payment.Id,
            cancellation.OutcomeUnknown
                ? "The previous provider attempt cancellation outcome is unknown."
                : "The previous provider attempt could not be closed safely.");
    }

    public async Task<bool> IsSettledAsync(
        OrderPaymentBinding binding,
        CancellationToken cancellationToken = default)
    {
        var payment = await paymentRepository.GetByIdAsync(binding.PaymentId, cancellationToken).ConfigureAwait(false);
        return payment is not null &&
               payment.Status == PaymentStatus.Succeeded &&
               payment.OrderId == binding.OrderId &&
               payment.TenantId == binding.TenantId &&
               payment.Amount == binding.Amount &&
               string.Equals(payment.Currency, binding.Currency, StringComparison.Ordinal);
    }

    internal static string CreateIdempotencyKey(Guid tenantId, Guid orderId) =>
        $"order:{tenantId:N}:{orderId:N}:charge";

    internal static string CreateReplacementIdempotencyKey(Guid tenantId, Guid orderId, Guid exhaustedPaymentId) =>
        $"{CreateIdempotencyKey(tenantId, orderId)}:after:{exhaustedPaymentId:N}";

    private static string CreateActiveAttemptIdempotencyKey(Payment payment) =>
        payment.RetryCount == 0
            ? payment.IdempotencyKey
            : $"{payment.IdempotencyKey}:retry:{payment.RetryCount}";

    private static void Validate(AuthoritativeOrderCharge charge)
    {
        if (charge.OrderId == Guid.Empty || charge.TenantId == Guid.Empty)
            throw new ArgumentException("Order and tenant identifiers are required.", nameof(charge));
        if (charge.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(charge), "Order amount must be positive.");
        if (charge.Currency.Length != 3 || !charge.Currency.All(char.IsAsciiLetterUpper))
            throw new ArgumentException("Currency must be a three-letter uppercase code.", nameof(charge));
        var paymentMethodError = StripePaymentMethodIdentifier.IsValid(charge.PaymentMethodId)
            ? null
            : StripePaymentMethodIdentifier.ValidationMessage;
        if (paymentMethodError is not null)
            throw new ArgumentException(paymentMethodError, nameof(charge));
    }

    private static void EnsureMatches(Payment payment, AuthoritativeOrderCharge charge)
    {
        if (payment.OrderId != charge.OrderId ||
            payment.TenantId != charge.TenantId ||
            payment.Amount != charge.Amount ||
            !string.Equals(payment.Currency, charge.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Existing order payment does not match the authoritative order snapshot.");
        }
    }
}
