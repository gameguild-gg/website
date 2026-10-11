using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Stripe payment processing, refunds, and webhook validation.
///     Handles core transactional operations against the Stripe API.
/// </summary>
public class StripePaymentService(
    IOptions<StripeGatewayOptions> options,
    ILogger<StripePaymentService> logger) : IStripePaymentService
{
    private readonly StripeGatewayOptions _options = InitializeOptions(options);
    private readonly PaymentIntentService _paymentIntentService = new();
    private readonly RefundService _refundService = new();
    private readonly Func<PaymentIntentCreateOptions, RequestOptions, CancellationToken, Task<PaymentIntent>>
        _createPaymentIntentAsync = static (createOptions, requestOptions, cancellationToken) =>
            new PaymentIntentService().CreateAsync(createOptions, requestOptions, cancellationToken);
    private readonly Func<string, DisputeCloseOptions, RequestOptions, CancellationToken, Task<Dispute>>
        _closeDisputeAsync = static (disputeId, closeOptions, requestOptions, cancellationToken) =>
            new Stripe.DisputeService().CloseAsync(disputeId, closeOptions, requestOptions, cancellationToken);
    private readonly Func<string, DisputeUpdateOptions, RequestOptions, CancellationToken, Task<Dispute>>
        _updateDisputeAsync = static (disputeId, updateOptions, requestOptions, cancellationToken) =>
            new Stripe.DisputeService().UpdateAsync(disputeId, updateOptions, requestOptions, cancellationToken);

    internal StripePaymentService(
        IOptions<StripeGatewayOptions> options,
        ILogger<StripePaymentService> logger,
        Func<PaymentIntentCreateOptions, RequestOptions, CancellationToken, Task<PaymentIntent>>? createPaymentIntentAsync = null,
        Func<string, DisputeCloseOptions, RequestOptions, CancellationToken, Task<Dispute>>? closeDisputeAsync = null,
        Func<string, DisputeUpdateOptions, RequestOptions, CancellationToken, Task<Dispute>>? updateDisputeAsync = null)
        : this(options, logger)
    {
        // The payment-intent factory is mandatory (fail-fast wiring contract asserted by
        // StripePaymentIntentCreationTests): a null factory means the service would silently
        // fall through to a misconfigured delegate.
        ArgumentNullException.ThrowIfNull(createPaymentIntentAsync);
        _createPaymentIntentAsync = createPaymentIntentAsync;

        if (closeDisputeAsync is not null)
        {
            _closeDisputeAsync = closeDisputeAsync;
        }

        if (updateDisputeAsync is not null)
        {
            _updateDisputeAsync = updateDisputeAsync;
        }
    }

    private static StripeGatewayOptions InitializeOptions(IOptions<StripeGatewayOptions> options)
    {
        var stripeOptions = options.Value;
        StripePaymentGateway.EnsureApiKey(stripeOptions);
        return stripeOptions;
    }

    public async Task<GatewayPaymentIntentSetupResult> CreatePaymentIntentAsync(
        GatewayPaymentIntentSetupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.Amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Currency);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Description);
        ArgumentNullException.ThrowIfNull(request.Metadata);
        if (_options.UseSimulation)
            throw new InvalidOperationException(
                "Unconfirmed Stripe payment intents are unavailable in simulation mode.");

        try
        {
            var paymentIntent = await _createPaymentIntentAsync(
                new PaymentIntentCreateOptions
                {
                    Amount = StripeAmountConverter.ToStripeAmount(request.Amount, request.Currency),
                    Currency = request.Currency.ToLowerInvariant(),
                    Confirm = false,
                    Description = request.Description,
                    Metadata = new Dictionary<string, string>(request.Metadata, StringComparer.Ordinal),
                    AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                    {
                        Enabled = true
                    }
                },
                new RequestOptions
                {
                    IdempotencyKey = request.IdempotencyKey,
                    StripeAccount = ResolveConnectedAccountId()
                },
                cancellationToken).ConfigureAwait(false);
            var status = paymentIntent.Status is "requires_payment_method" or "requires_confirmation" or "requires_action"
                ? PaymentStatus.RequiresAction
                : StripeStatusMapper.MapPaymentStatus(paymentIntent.Status);
            return new GatewayPaymentIntentSetupResult(
                paymentIntent.Id,
                status,
                paymentIntent.ClientSecret,
                CreateProviderMapping(paymentIntent.Id));
        }
        catch (StripeException exception)
        {
            var unknown = IsOutcomeUnknown(exception);
            return new GatewayPaymentIntentSetupResult(
                null,
                unknown ? PaymentStatus.Processing : PaymentStatus.Failed,
                null,
                null,
                unknown,
                exception.StripeError?.Code ?? "stripe_error",
                exception.StripeError?.Message ?? exception.Message);
        }
    }

    /// <inheritdoc />
    public async Task<GatewayPaymentResult> ProcessPaymentAsync(
        GatewayPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Processing Stripe payment: {Amount} {Currency} with idempotency key {IdempotencyKey} (Simulation: {IsSimulation})",
            request.Amount, request.Currency, request.IdempotencyKey, _options.UseSimulation);

        if (_options.UseSimulation)
        {
            return await ProcessPaymentSimulatedAsync(cancellationToken).ConfigureAwait(false);
        }

        return await ProcessPaymentRealAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GatewayPaymentResult> GetPaymentAsync(
        string externalTransactionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalTransactionId);

        if (_options.UseSimulation)
        {
            return new GatewayPaymentResult(
                false,
                externalTransactionId,
                null,
                null,
                "Additional authentication is required.",
                PaymentStatus.RequiresAction,
                SystemClock.UtcNow,
                $"{externalTransactionId}_secret_simulated",
                CreateProviderMapping(externalTransactionId));
        }

        try
        {
            var paymentIntent = await _paymentIntentService
                .GetAsync(
                    externalTransactionId,
                    options: null,
                    requestOptions: CreateRequestOptions(),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return MapPaymentIntent(paymentIntent);
        }
        catch (StripeException ex)
        {
            return MapStripeException(ex, $"retrieving payment intent {externalTransactionId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Payment state is unknown for Stripe payment intent {PaymentIntentId}", externalTransactionId);
            return UnknownPaymentResult(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<GatewayPaymentCancellationResult> CancelPaymentAsync(
        string externalTransactionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalTransactionId);

        if (_options.UseSimulation)
            return new GatewayPaymentCancellationResult(true, false, null, null);

        try
        {
            var current = await _paymentIntentService
                .GetAsync(
                    externalTransactionId,
                    options: null,
                    requestOptions: CreateRequestOptions(),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (string.Equals(current.Status, "canceled", StringComparison.Ordinal))
                return new GatewayPaymentCancellationResult(true, false, null, null);
            if (string.Equals(current.Status, "succeeded", StringComparison.Ordinal))
            {
                return new GatewayPaymentCancellationResult(
                    false,
                    false,
                    "payment_already_succeeded",
                    "The provider payment already succeeded and cannot be replaced.");
            }

            var paymentIntent = await _paymentIntentService
                .CancelAsync(
                    externalTransactionId,
                    requestOptions: CreateRequestOptions(),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return new GatewayPaymentCancellationResult(
                string.Equals(paymentIntent.Status, "canceled", StringComparison.Ordinal),
                false,
                null,
                string.Equals(paymentIntent.Status, "canceled", StringComparison.Ordinal)
                    ? null
                    : $"Stripe returned payment intent status {paymentIntent.Status} after cancellation.");
        }
        catch (StripeException ex)
        {
            var outcomeUnknown = IsOutcomeUnknown(ex);
            logger.LogError(
                ex,
                "Stripe payment intent cancellation failed for {PaymentIntentId}; outcome unknown: {OutcomeUnknown}",
                externalTransactionId,
                outcomeUnknown);
            return new GatewayPaymentCancellationResult(
                false,
                outcomeUnknown,
                ex.StripeError?.Code ?? "stripe_cancellation_failed",
                ex.StripeError?.Message ?? ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stripe payment intent cancellation outcome is unknown for {PaymentIntentId}", externalTransactionId);
            return new GatewayPaymentCancellationResult(
                false,
                true,
                "stripe_outcome_unknown",
                ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<GatewayRefundResult> ProcessRefundAsync(
        GatewayRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Processing Stripe refund for transaction {TransactionId} with idempotency key {IdempotencyKey} (Simulation: {IsSimulation})",
            request.OriginalTransactionId, request.IdempotencyKey, _options.UseSimulation);

        if (_options.UseSimulation)
        {
            return await ProcessRefundSimulatedAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return await ProcessRefundRealAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GatewayDisputeActionResult> AcceptDisputeAsync(
        GatewayDisputeActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExternalDisputeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey);

        logger.LogInformation(
            "Accepting Stripe dispute {DisputeId} with idempotency key {IdempotencyKey} (Simulation: {IsSimulation})",
            request.ExternalDisputeId, request.IdempotencyKey, _options.UseSimulation);

        if (_options.UseSimulation)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return new GatewayDisputeActionResult(true, null, null);
        }

        try
        {
            await _closeDisputeAsync(
                    request.ExternalDisputeId,
                    new DisputeCloseOptions(),
                    new RequestOptions
                    {
                        IdempotencyKey = request.IdempotencyKey,
                        StripeAccount = ResolveConnectedAccountId()
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return new GatewayDisputeActionResult(true, null, null);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex,
                "Stripe dispute accept failed for dispute {DisputeId}: {ErrorCode} - {ErrorMessage}",
                request.ExternalDisputeId, ex.StripeError?.Code, ex.StripeError?.Message);
            return new GatewayDisputeActionResult(
                false,
                ex.StripeError?.Code ?? "stripe_error",
                ex.StripeError?.Message ?? ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error accepting Stripe dispute {DisputeId}", request.ExternalDisputeId);
            return new GatewayDisputeActionResult(false, "unexpected_error", ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<GatewayDisputeActionResult> ChallengeDisputeAsync(
        GatewayDisputeChallengeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExternalDisputeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey);

        logger.LogInformation(
            "Challenging Stripe dispute {DisputeId} with idempotency key {IdempotencyKey} (Simulation: {IsSimulation})",
            request.ExternalDisputeId, request.IdempotencyKey, _options.UseSimulation);

        if (_options.UseSimulation)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return new GatewayDisputeActionResult(true, null, null);
        }

        try
        {
            var updateOptions = new DisputeUpdateOptions
            {
                Submit = true,
                Evidence = new DisputeEvidenceOptions
                {
                    UncategorizedText = string.IsNullOrWhiteSpace(request.EvidenceSummary)
                        ? "The merchant contests this dispute; the service was delivered and the charge was authorized."
                        : request.EvidenceSummary
                }
            };

            await _updateDisputeAsync(
                    request.ExternalDisputeId,
                    updateOptions,
                    new RequestOptions
                    {
                        IdempotencyKey = request.IdempotencyKey,
                        StripeAccount = ResolveConnectedAccountId()
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return new GatewayDisputeActionResult(true, null, null);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex,
                "Stripe dispute challenge failed for dispute {DisputeId}: {ErrorCode} - {ErrorMessage}",
                request.ExternalDisputeId, ex.StripeError?.Code, ex.StripeError?.Message);
            return new GatewayDisputeActionResult(
                false,
                ex.StripeError?.Code ?? "stripe_error",
                ex.StripeError?.Message ?? ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error challenging Stripe dispute {DisputeId}", request.ExternalDisputeId);
            return new GatewayDisputeActionResult(false, "unexpected_error", ex.Message);
        }
    }

    /// <inheritdoc />
    public Task<bool> ValidateWebhookSignatureAsync(
        string payload,
        string signature,
        string secret)
    {
        if (_options.UseSimulation)
        {
            return ValidateWebhookSignatureSimulatedAsync(signature, secret);
        }

        return ValidateWebhookSignatureRealAsync(payload, signature, secret);
    }

    #region Payment

    private async Task<GatewayPaymentResult> ProcessPaymentRealAsync(
        GatewayPaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var createOptions = new PaymentIntentCreateOptions
            {
                Amount = StripeAmountConverter.ToStripeAmount(request.Amount, request.Currency),
                Currency = request.Currency.ToLowerInvariant(),
                Customer = request.CustomerId,
                PaymentMethod = request.PaymentMethodId,
                Confirm = true,
                Description = request.Description,
                Metadata = request.Metadata,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never"
                }
            };

            var requestOptions = new RequestOptions
            {
                IdempotencyKey = request.IdempotencyKey,
                StripeAccount = ResolveConnectedAccountId()
            };

            var paymentIntent = await _paymentIntentService.CreateAsync(
                createOptions,
                requestOptions,
                cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Stripe payment processed: {PaymentIntentId} with status {Status}",
                paymentIntent.Id, paymentIntent.Status);

            return MapPaymentIntent(paymentIntent);
        }
        catch (StripeException ex)
        {
            return MapStripeException(ex, $"processing idempotency key {request.IdempotencyKey}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stripe payment outcome is unknown for idempotency key {IdempotencyKey}",
                request.IdempotencyKey);
            return UnknownPaymentResult(ex.Message);
        }
    }

    private GatewayPaymentResult MapPaymentIntent(PaymentIntent paymentIntent)
    {
        var status = StripeStatusMapper.MapPaymentStatus(paymentIntent.Status);
        return new GatewayPaymentResult(
            Success: status == PaymentStatus.Succeeded,
            TransactionId: paymentIntent.Id,
            ExternalPaymentId: paymentIntent.LatestChargeId,
            ErrorCode: null,
            ErrorMessage: status == PaymentStatus.RequiresAction ? "Additional authentication is required." : null,
            Status: status,
            ProcessedAt: SystemClock.UtcNow,
            ClientActionToken: status == PaymentStatus.RequiresAction ? paymentIntent.ClientSecret : null,
            ProviderMapping: CreateProviderMapping(paymentIntent.Id));
    }

    private GatewayProviderMapping CreateProviderMapping(string paymentIntentId) =>
        new(
            _options.UseSimulation || !_options.LiveMode ? "test" : "live",
            _options.UseSimulation ? "acct_simulated" : ResolveProviderObjectAccountId(_options),
            paymentIntentId,
            "payment_intent",
            "capture");

    internal static string ResolveProviderObjectAccountId(StripeGatewayOptions options) =>
        string.IsNullOrWhiteSpace(options.ConnectedAccountId)
            ? options.AccountId
            : options.ConnectedAccountId;

    private GatewayPaymentResult MapStripeException(StripeException exception, string operation)
    {
        var outcomeIsUnknown = IsOutcomeUnknown(exception);
        logger.LogError(
            exception,
            "Stripe error while {Operation}: {ErrorCode} - {ErrorMessage}. Outcome known: {OutcomeKnown}",
            operation,
            exception.StripeError?.Code,
            exception.StripeError?.Message,
            !outcomeIsUnknown);

        return outcomeIsUnknown
            ? UnknownPaymentResult(exception.StripeError?.Message ?? exception.Message)
            : new GatewayPaymentResult(
                false,
                null,
                null,
                exception.StripeError?.Code ?? "stripe_error",
                exception.StripeError?.Message ?? exception.Message,
                PaymentStatus.Failed,
                SystemClock.UtcNow,
                FailureKind: GatewayFailureKind.TerminalDecline);
    }

    internal static bool IsOutcomeUnknown(StripeException exception) =>
        exception.StripeError is null ||
        exception.HttpStatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
        (int)exception.HttpStatusCode >= 500;

    private static GatewayPaymentResult UnknownPaymentResult(string message) =>
        new(
            false,
            null,
            null,
            "stripe_outcome_unknown",
            $"Payment outcome is pending provider reconciliation: {message}",
            PaymentStatus.Processing,
            SystemClock.UtcNow,
            FailureKind: GatewayFailureKind.OutcomeUnknown);

    private async Task<GatewayPaymentResult> ProcessPaymentSimulatedAsync(
        CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Using simulated payment response for development/testing");
        return SimulatedPaymentResultFactory.PaymentSuccess(logger);
    }

    #endregion

    #region Refund

    private async Task<GatewayRefundResult> ProcessRefundRealAsync(
        GatewayRefundRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var createOptions = new RefundCreateOptions
            {
                PaymentIntent = request.OriginalTransactionId,
                Reason = StripeStatusMapper.MapRefundReason(request.Reason)
            };

            if (request.Amount.HasValue)
            {
                createOptions.Amount = StripeAmountConverter.ToStripeAmount(request.Amount.Value, "USD");
            }

            var requestOptions = new RequestOptions
            {
                IdempotencyKey = request.IdempotencyKey,
                StripeAccount = ResolveConnectedAccountId()
            };

            var refund = await _refundService.CreateAsync(
                createOptions,
                requestOptions,
                cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Stripe refund processed: {RefundId} with status {Status}",
                refund.Id, refund.Status);

            return new GatewayRefundResult(
                Success: refund.Status == "succeeded",
                RefundId: refund.Id,
                AmountRefunded: StripeAmountConverter.FromStripeAmount(refund.Amount, refund.Currency),
                ErrorCode: null,
                ErrorMessage: null,
                ProcessedAt: SystemClock.UtcNow);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex,
                "Stripe refund failed for transaction {TransactionId}: {ErrorCode} - {ErrorMessage}",
                request.OriginalTransactionId, ex.StripeError?.Code, ex.StripeError?.Message);

            return new GatewayRefundResult(
                Success: false,
                RefundId: null,
                AmountRefunded: 0,
                ErrorCode: ex.StripeError?.Code ?? "stripe_error",
                ErrorMessage: ex.StripeError?.Message ?? ex.Message,
                ProcessedAt: SystemClock.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during Stripe refund for transaction {TransactionId}",
                request.OriginalTransactionId);

            return SimulatedPaymentResultFactory.RefundFailure(ex.Message, "unexpected_error");
        }
    }

    private async Task<GatewayRefundResult> ProcessRefundSimulatedAsync(
        GatewayRefundRequest request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Using simulated refund response for development/testing");
        return SimulatedPaymentResultFactory.RefundSuccess(request.Amount ?? 0, logger);
    }

    private RequestOptions CreateRequestOptions() => new()
    {
        StripeAccount = ResolveConnectedAccountId()
    };

    private string? ResolveConnectedAccountId() =>
        string.IsNullOrWhiteSpace(_options.ConnectedAccountId)
            ? null
            : _options.ConnectedAccountId;

    #endregion

    #region Webhook

    private Task<bool> ValidateWebhookSignatureRealAsync(
        string payload,
        string signature,
        string secret)
    {
        try
        {
            if (string.IsNullOrEmpty(payload) || string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
            {
                logger.LogWarning("Webhook validation failed: missing required parameters");
                return Task.FromResult(false);
            }

            var stripeEvent = EventUtility.ConstructEvent(
                payload,
                signature,
                secret,
                tolerance: _options.WebhookToleranceSeconds);

            logger.LogDebug(
                "Webhook signature validated successfully for event {EventId} of type {EventType}",
                stripeEvent.Id, stripeEvent.Type);

            return Task.FromResult(true);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex,
                "Stripe webhook signature validation failed: {ErrorMessage}",
                ex.Message);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unexpected error during webhook signature validation");
            return Task.FromResult(false);
        }
    }

    private Task<bool> ValidateWebhookSignatureSimulatedAsync(
        string signature,
        string secret)
    {
        logger.LogDebug("Using simulated webhook validation for development/testing");

        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
        {
            logger.LogWarning("Simulated webhook validation failed: missing signature or secret");
            return Task.FromResult(false);
        }

        var isValidFormat = signature.StartsWith("t=", StringComparison.Ordinal) && signature.Contains(",v1=");
        return Task.FromResult(isValidFormat || !string.IsNullOrEmpty(signature));
    }

    #endregion
}
