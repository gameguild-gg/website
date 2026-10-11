using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Failover orchestration around gateway charges (issue #413). This composite is the
///     single <see cref="IPaymentGateway" /> resolved by the payment path, so every existing
///     handler gains multi-gateway behaviour without depending on a specific adapter:
///     <list type="bullet">
///         <item>charges are routed by <see cref="IPaymentGatewaySelector" /> (config-driven, fail-closed);</item>
///         <item>a retriable gateway failure first retries the same gateway (bounded by
///         <c>Payments:GatewayFailover:SameGatewayRetryLimit</c> and clamped by
///         <c>Payments:Retry:MaxRetries</c>), then fails over to the next-priority gateway,
///         skipping gateways already attempted this charge, with a hard cap of
///         <c>MaxAttemptsTotal</c> (default 3) attempts in total;</item>
///         <item>terminal declines and outcome-unknown results stop the loop immediately —
///         they are never retried or failed over;</item>
///         <item>gateways whose circuit breaker is open are skipped; when no gateway can be
///         attempted at all the all-gateways-down alert fires and the charge fails closed
///         with the <c>all_gateways_unavailable</c> error code;</item>
///         <item>refunds, inquiries and cancellations are pinned to the gateway that owns
///         the external object (<c>ProviderId</c>), never rerouted to another gateway.</item>
///     </list>
/// </summary>
public sealed class FailoverPaymentGateway(
    IGatewayRegistry registry,
    IPaymentGatewaySelector selector,
    IGatewayCircuitBreaker circuitBreaker,
    IGatewayAvailabilityAlerter alerter,
    IOptions<GatewayFailoverOptions> failoverOptions,
    IOptions<PaymentRetryOptions> paymentRetryOptions,
    ILogger<FailoverPaymentGateway> logger) : IPaymentGateway
{
    private readonly GatewayFailoverOptions _failoverOptions = failoverOptions.Value;
    private readonly PaymentRetryOptions _paymentRetryOptions = paymentRetryOptions.Value;

    /// <summary>
    ///     Provider id of the primary (highest-priority eligible) gateway. Fails closed with
    ///     <see cref="GatewaySelectionFailedException" /> when no gateway is eligible — used
    ///     to attribute new payment records before the first attempt.
    /// </summary>
    public string ProviderId => selector.SelectPrimary().Gateway.ProviderId;

    /// <inheritdoc />
    public string DisplayName => selector.SelectPrimary().Gateway.DisplayName;

    /// <inheritdoc />
    public bool IsEnabled => selector.Select(new GatewayRouteContext(string.Empty)).Count > 0;

    /// <inheritdoc />
    public async Task<GatewayPaymentResult> ProcessPaymentAsync(
        GatewayPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var routeContext = GatewayRouteContext.FromRequest(request);
        var candidates = selector.Select(routeContext);

        if (candidates.Count == 0)
        {
            // Fail closed: no eligible gateway is an explicit failure, never a silent
            // default. The selector already logged the per-gateway rejection reasons.
            throw GatewaySelectionFailedException.Create(
                routeContext,
                BuildSelectionRejectionDetail(routeContext));
        }

        var attemptedProviderIds = new HashSet<string>(StringComparer.Ordinal);
        var attemptsUsed = 0;
        GatewayPaymentResult? lastRetriableFailure = null;

        foreach (var candidate in candidates)
        {
            if (attemptsUsed >= _failoverOptions.MaxAttemptsTotal)
            {
                break;
            }

            if (!attemptedProviderIds.Add(candidate.Gateway.ProviderId))
            {
                continue; // skip already-attempted gateways
            }

            var sameGatewayAttempts = 1 + ResolveSameGatewayRetryLimit();
            for (var attemptOnGateway = 0;
                 attemptOnGateway < sameGatewayAttempts && attemptsUsed < _failoverOptions.MaxAttemptsTotal;
                 attemptOnGateway++)
            {
                if (!circuitBreaker.TryAcquireAttempt(candidate.Gateway.ProviderId))
                {
                    logger.LogWarning(
                        "Skipping gateway {ProviderId}: circuit breaker is {State}",
                        candidate.Gateway.ProviderId,
                        circuitBreaker.GetSnapshot(candidate.Gateway.ProviderId).State);
                    break;
                }

                attemptsUsed++;
                GatewayPaymentResult result;
                try
                {
                    result = await candidate.Gateway.ProcessPaymentAsync(request, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception) when (
                    exception is HttpRequestException or TaskCanceledException or TimeoutException)
                {
                    // The gateway adapter blew up at the transport layer before producing a
                    // result: count as a retriable gateway failure and keep the loop going.
                    circuitBreaker.RecordFailure(candidate.Gateway.ProviderId);
                    lastRetriableFailure = SynthesizeTransportFailure(candidate.Gateway.ProviderId, exception);
                    logger.LogWarning(
                        exception,
                        "Gateway {ProviderId} charge failed at the transport layer (attempt {Attempt} of {Max})",
                        candidate.Gateway.ProviderId,
                        attemptsUsed,
                        _failoverOptions.MaxAttemptsTotal);
                    continue;
                }

                if (result.FailureKind == GatewayFailureKind.RetriableGatewayFailure)
                {
                    circuitBreaker.RecordFailure(candidate.Gateway.ProviderId);
                    lastRetriableFailure = result with { ProviderId = candidate.Gateway.ProviderId };
                    logger.LogWarning(
                        "Gateway {ProviderId} charge failed retriable ({ErrorCode}); attempt {Attempt} of {Max}",
                        candidate.Gateway.ProviderId,
                        result.ErrorCode,
                        attemptsUsed,
                        _failoverOptions.MaxAttemptsTotal);
                    continue;
                }

                // A conclusive answer — success, requires-action, terminal decline or
                // outcome-unknown — means the gateway itself responded: healthy for the
                // breaker, and the loop stops (failover never replays a decline).
                circuitBreaker.RecordSuccess(candidate.Gateway.ProviderId);
                return result with { ProviderId = candidate.Gateway.ProviderId };
            }
        }

        if (lastRetriableFailure is not null)
        {
            logger.LogError(
                "All {Attempts} gateway attempts failed retriable for idempotency key {IdempotencyKey}; last error {ErrorCode}",
                attemptsUsed,
                request.IdempotencyKey,
                lastRetriableFailure.ErrorCode);

            return lastRetriableFailure;
        }

        // Candidates exist but none could be attempted: every circuit is open.
        await RaiseAllGatewaysDownAlertAsync(cancellationToken).ConfigureAwait(false);

        return new GatewayPaymentResult(
            Success: false,
            TransactionId: null,
            ExternalPaymentId: null,
            ErrorCode: AllGatewaysUnavailableErrorCode,
            ErrorMessage: "Every eligible payment gateway is unavailable (circuit breaker open); the charge failed closed.",
            Status: PaymentStatus.Failed,
            ProcessedAt: SystemClock.UtcNow,
            FailureKind: GatewayFailureKind.RetriableGatewayFailure);
    }

    /// <inheritdoc />
    public async Task<GatewayPaymentResult> GetPaymentAsync(
        string externalTransactionId,
        CancellationToken cancellationToken = default,
        string? providerId = null)
    {
        var gateway = ResolveOwnerGateway(providerId, "inquiry");

        return await gateway.GetPaymentAsync(externalTransactionId, cancellationToken, providerId: gateway.ProviderId)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GatewayPaymentCancellationResult> CancelPaymentAsync(
        string externalTransactionId,
        CancellationToken cancellationToken = default,
        string? providerId = null)
    {
        var gateway = ResolveOwnerGateway(providerId, "cancellation");

        return await gateway.CancelPaymentAsync(externalTransactionId, cancellationToken, providerId: gateway.ProviderId)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<GatewayRefundResult> ProcessRefundAsync(
        GatewayRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Refunds are pinned to the gateway that owns the captured payment; a refund can
        // never be rerouted to a different gateway. Retriable failures retry the same
        // gateway only, bounded by the failover attempt budget.
        var gateway = ResolveOwnerGateway(request.ProviderId, "refund");
        var maxAttempts = Math.Min(1 + ResolveSameGatewayRetryLimit(), _failoverOptions.MaxAttemptsTotal);

        for (var attempt = 1; ; attempt++)
        {
            GatewayRefundResult result;
            try
            {
                result = await gateway.ProcessRefundAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is HttpRequestException or TaskCanceledException or TimeoutException)
            {
                circuitBreaker.RecordFailure(gateway.ProviderId);
                if (attempt >= maxAttempts)
                {
                    return SynthesizeRefundTransportFailure(exception);
                }

                continue;
            }

            if (result.FailureKind == GatewayFailureKind.RetriableGatewayFailure && attempt < maxAttempts)
            {
                circuitBreaker.RecordFailure(gateway.ProviderId);
                logger.LogWarning(
                    "Gateway {ProviderId} refund failed retriable ({ErrorCode}); retrying on the same gateway (attempt {Attempt} of {Max})",
                    gateway.ProviderId,
                    result.ErrorCode,
                    attempt + 1,
                    maxAttempts);
                continue;
            }

            if (result.Success)
            {
                circuitBreaker.RecordSuccess(gateway.ProviderId);
            }

            return result;
        }
    }

    /// <inheritdoc />
    public async Task<GatewayDisputeActionResult> AcceptDisputeAsync(
        GatewayDisputeActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Dispute actions are pinned to the gateway that owns the disputed payment (issue #413
        // routing rules): they are never rerouted to another gateway.
        var gateway = ResolveOwnerGateway(request.ProviderId, "dispute");
        try
        {
            var result = await gateway.AcceptDisputeAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.Success)
            {
                circuitBreaker.RecordSuccess(gateway.ProviderId);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            circuitBreaker.RecordFailure(gateway.ProviderId);
            return SynthesizeDisputeTransportFailure(exception);
        }
    }

    /// <inheritdoc />
    public async Task<GatewayDisputeActionResult> ChallengeDisputeAsync(
        GatewayDisputeChallengeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var gateway = ResolveOwnerGateway(request.ProviderId, "dispute");
        try
        {
            var result = await gateway.ChallengeDisputeAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.Success)
            {
                circuitBreaker.RecordSuccess(gateway.ProviderId);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            circuitBreaker.RecordFailure(gateway.ProviderId);
            return SynthesizeDisputeTransportFailure(exception);
        }
    }

    /// <inheritdoc />
    public Task<bool> ValidateWebhookSignatureAsync(
        string payload,
        string signature,
        string secret) =>
        selector.SelectPrimary().Gateway.ValidateWebhookSignatureAsync(payload, signature, secret);

    /// <inheritdoc />
    public Task<GatewayCustomerResult> CreateCustomerAsync(
        GatewayCustomerRequest request,
        CancellationToken cancellationToken = default) =>
        selector.SelectPrimary().Gateway.CreateCustomerAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<GatewayPaymentMethodResult> CreatePaymentMethodAsync(
        GatewayPaymentMethodRequest request,
        CancellationToken cancellationToken = default) =>
        selector.SelectPrimary().Gateway.CreatePaymentMethodAsync(request, cancellationToken);

    /// <inheritdoc />
    public Task<GatewayCancellationResult> CancelSubscriptionAsync(
        string externalSubscriptionId,
        CancellationToken cancellationToken = default) =>
        selector.SelectPrimary().Gateway.CancelSubscriptionAsync(externalSubscriptionId, cancellationToken);

    /// <summary>Error code used when no gateway could be attempted at all.</summary>
    public const string AllGatewaysUnavailableErrorCode = "all_gateways_unavailable";

    private int ResolveSameGatewayRetryLimit() =>
        Math.Min(
            _failoverOptions.SameGatewayRetryLimit,
            Math.Max(0, _paymentRetryOptions.MaxRetries));

    private IPaymentGateway ResolveOwnerGateway(string? providerId, string operation)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return selector.SelectPrimary().Gateway;
        }

        // Fail closed: an owner provider that is not registered is an explicit failure,
        // never a silent rerouting of the operation to the primary gateway.
        return registry.Find(providerId)
            ?? throw new GatewaySelectionFailedException(
                $"The provider '{providerId}' that owns this payment {operation} is not a registered gateway; failing closed.");
    }

    private (string ProviderId, string Reason)[] BuildSelectionRejectionDetail(GatewayRouteContext context)
    {
        var rejections = new List<(string ProviderId, string Reason)>();
        foreach (var gateway in registry.Gateways)
        {
            if (!gateway.IsEnabled)
            {
                rejections.Add((gateway.ProviderId, "adapter disabled (PaymentGateways configuration)"));
            }
            else
            {
                rejections.Add((gateway.ProviderId, "filtered by Payments:GatewayRouting"));
            }
        }

        return [.. rejections];
    }

    private async Task RaiseAllGatewaysDownAlertAsync(CancellationToken cancellationToken)
    {
        try
        {
            await alerter
                .RaiseAllGatewaysUnavailableAsync(circuitBreaker.GetAllSnapshots(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Alerting must never break the fail-closed payment result.
            logger.LogError(exception, "Raising the all-gateways-down alert failed");
        }
    }

    private static GatewayPaymentResult SynthesizeTransportFailure(string providerId, Exception exception) =>
        new(
            Success: false,
            TransactionId: null,
            ExternalPaymentId: null,
            ErrorCode: "gateway_transport_failure",
            ErrorMessage: $"The gateway request never produced a response: {exception.Message}",
            Status: PaymentStatus.Failed,
            ProcessedAt: SystemClock.UtcNow,
            ProviderId: providerId,
            FailureKind: GatewayFailureKind.RetriableGatewayFailure);

    private static GatewayDisputeActionResult SynthesizeDisputeTransportFailure(Exception exception) =>
        new(
            Success: false,
            ErrorCode: "gateway_transport_failure",
            ErrorMessage: $"The gateway dispute request never produced a response: {exception.Message}");

    private static GatewayRefundResult SynthesizeRefundTransportFailure(Exception exception) =>
        new(
            Success: false,
            RefundId: null,
            AmountRefunded: 0,
            ErrorCode: "gateway_transport_failure",
            ErrorMessage: $"The gateway refund request never produced a response: {exception.Message}",
            ProcessedAt: SystemClock.UtcNow,
            FailureKind: GatewayFailureKind.RetriableGatewayFailure);
}
