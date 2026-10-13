using Microsoft.Extensions.Logging.Abstractions;

namespace GameGuild.Commerce.Payments.UnitTests.GatewayRouting;

/// <summary>
///     Programmable in-memory <see cref="IPaymentGateway" /> used to drive the selector,
///     failover and circuit-breaker tests (issue #413 fake gateway). Charge behaviour comes
///     from a scripted queue; when the queue is empty the gateway returns a simulated
///     success, so tests only script the failures they care about.
/// </summary>
public sealed class FakePaymentGateway(string providerId) : IPaymentGateway
{
    private readonly Queue<Func<GatewayPaymentRequest, GatewayPaymentResult>> _chargeScript = new();

    public List<string> ChargeCallLog { get; } = [];

    public List<GatewayRefundRequest> RefundCalls { get; } = [];

    public int ChargeCallCount => ChargeCallLog.Count;

    /// <summary>Gateway display name.</summary>
    public string DisplayName => $"Fake {ProviderId}";

    /// <summary>Provider id of the fake gateway.</summary>
    public string ProviderId { get; set; } = providerId;

    /// <summary>Whether the gateway reports itself enabled to the selector.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Enqueues one scripted charge outcome.</summary>
    public FakePaymentGateway EnqueueCharge(GatewayPaymentResult result) =>
        EnqueueCharge(_ => result);

    /// <summary>Enqueues one scripted charge computed from the request.</summary>
    public FakePaymentGateway EnqueueCharge(Func<GatewayPaymentRequest, GatewayPaymentResult> factory)
    {
        _chargeScript.Enqueue(factory);
        return this;
    }

    /// <summary>Enqueues a charge that throws a transport-level exception.</summary>
    public FakePaymentGateway EnqueueTransportError()
    {
        _chargeScript.Enqueue(_ => throw new HttpRequestException("simulated transport failure"));
        return this;
    }

    /// <summary>A successful gateway result with a provider mapping and the given provider id.</summary>
    public static GatewayPaymentResult SuccessResult(string providerId, string transactionId = "txn_ok") => new(
        Success: true,
        TransactionId: transactionId,
        ExternalPaymentId: transactionId,
        ErrorCode: null,
        ErrorMessage: null,
        Status: PaymentStatus.Succeeded,
        ProcessedAt: SystemClock.UtcNow,
        ProviderMapping: new GatewayProviderMapping("test", $"acct_{providerId}", transactionId, "capture", "capture"));

    /// <summary>A failed gateway result classified with the given failure kind.</summary>
    public static GatewayPaymentResult FailureResult(
        string providerId,
        GatewayFailureKind kind,
        string errorCode = "gateway_unavailable",
        string errorMessage = "transient gateway failure") => new(
        Success: false,
        TransactionId: null,
        ExternalPaymentId: null,
        ErrorCode: errorCode,
        ErrorMessage: errorMessage,
        Status: kind == GatewayFailureKind.OutcomeUnknown ? PaymentStatus.Processing : PaymentStatus.Failed,
        ProcessedAt: SystemClock.UtcNow,
        ProviderId: providerId,
        FailureKind: kind);

    /// <inheritdoc />
    public Task<GatewayPaymentResult> ProcessPaymentAsync(
        GatewayPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ChargeCallLog.Add(request.IdempotencyKey);

        var result = _chargeScript.Count > 0
            ? _chargeScript.Dequeue()(request)
            : SuccessResult(ProviderId);

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<GatewayPaymentResult> GetPaymentAsync(
        string externalTransactionId,
        CancellationToken cancellationToken = default,
        string? providerId = null) =>
        Task.FromResult(SuccessResult(ProviderId, externalTransactionId));

    /// <inheritdoc />
    public Task<GatewayPaymentCancellationResult> CancelPaymentAsync(
        string externalTransactionId,
        CancellationToken cancellationToken = default,
        string? providerId = null) =>
        Task.FromResult(new GatewayPaymentCancellationResult(true, OutcomeUnknown: false, null, null));

    /// <inheritdoc />
    public Task<GatewayRefundResult> ProcessRefundAsync(
        GatewayRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        RefundCalls.Add(request);

        return Task.FromResult(new GatewayRefundResult(
            Success: true,
            RefundId: $"re_{ProviderId}_{Guid.NewGuid():N}",
            AmountRefunded: request.Amount ?? 10m,
            ErrorCode: null,
            ErrorMessage: null,
            ProcessedAt: SystemClock.UtcNow));
    }

    /// <inheritdoc />
    public Task<GatewayDisputeActionResult> AcceptDisputeAsync(
        GatewayDisputeActionRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new GatewayDisputeActionResult(Success: true, ErrorCode: null, ErrorMessage: null));

    /// <inheritdoc />
    public Task<GatewayDisputeActionResult> ChallengeDisputeAsync(
        GatewayDisputeChallengeRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new GatewayDisputeActionResult(Success: true, ErrorCode: null, ErrorMessage: null));

    /// <inheritdoc />
    public Task<bool> ValidateWebhookSignatureAsync(string payload, string signature, string secret) =>
        Task.FromResult(true);

    /// <inheritdoc />
    public Task<GatewayCustomerResult> CreateCustomerAsync(
        GatewayCustomerRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new GatewayCustomerResult(true, $"cus_{ProviderId}", null, null));

    /// <inheritdoc />
    public Task<GatewayPaymentMethodResult> CreatePaymentMethodAsync(
        GatewayPaymentMethodRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new GatewayPaymentMethodResult(true, $"pm_{ProviderId}", "4242", "visa", 12, 2030, null, null));

    /// <inheritdoc />
    public Task<GatewayCancellationResult> CancelSubscriptionAsync(
        string externalSubscriptionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new GatewayCancellationResult(true, null, null, SystemClock.UtcNow));
}

/// <summary>
///     Registry over the fake gateways handed to it — mirrors <see cref="GatewayRegistry" />
///     without depending on the concrete Stripe/PayPal adapters.
/// </summary>
public sealed class FakeGatewayRegistry(params IPaymentGateway[] gateways) : IGatewayRegistry
{
    /// <inheritdoc />
    public IReadOnlyList<IPaymentGateway> Gateways { get; } = gateways;

    /// <inheritdoc />
    public IPaymentGateway? Find(string providerId) =>
        string.IsNullOrWhiteSpace(providerId)
            ? null
            : Gateways.FirstOrDefault(gateway =>
                string.Equals(gateway.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
///     Recording alerter capturing all-gateways-down raises for assertions.
/// </summary>
public sealed class RecordingGatewayAvailabilityAlerter : IGatewayAvailabilityAlerter
{
    public List<IReadOnlyList<GatewayCircuitSnapshot>> RaisedCircuitStates { get; } = [];

    /// <inheritdoc />
    public Task RaiseAllGatewaysUnavailableAsync(
        IReadOnlyList<GatewayCircuitSnapshot> circuitStates,
        CancellationToken cancellationToken = default)
    {
        RaisedCircuitStates.Add(circuitStates);
        return Task.CompletedTask;
    }
}

/// <summary>
///     Selectors need a logger; this factory keeps test setup terse.
/// </summary>
public static class TestLogger
{
    /// <summary>Returns a null logger for <typeparamref name="T" />.</summary>
    public static Microsoft.Extensions.Logging.ILogger<T> Of<T>() => NullLogger<T>.Instance;
}
