using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.GatewayRouting;

/// <summary>
///     Failover sequence tests for <see cref="FailoverPaymentGateway" /> (issue #413):
///     same-gateway retry on retriable failures then next-priority failover with
///     skip-attempted and the max-3-attempt budget; terminal declines and outcome-unknown
///     results stop the loop; the all-gateways-down scenario alerts and fails closed.
/// </summary>
public class GatewayFailoverTests
{
    private readonly RecordingGatewayAvailabilityAlerter _alerter = new();

    private readonly GatewayFailoverOptions _failoverOptions = new()
    {
        MaxAttemptsTotal = 3,
        SameGatewayRetryLimit = 1,
        CircuitFailureThreshold = 5,
        CircuitOpenSeconds = 60
    };

    private readonly PaymentRetryOptions _paymentRetryOptions = new() { MaxRetries = 3 };

    private FailoverPaymentGateway CreateSut(
        IReadOnlyList<IPaymentGateway> gateways,
        InMemoryGatewayCircuitBreaker? circuitBreaker = null,
        GatewayRoutingOptions? routing = null,
        PaymentRetryOptions? paymentRetry = null)
    {
        var registry = new FakeGatewayRegistry([.. gateways]);
        var selector = new ConfigPaymentGatewaySelector(
            registry,
            Options.Create(routing ?? new GatewayRoutingOptions()),
            NullLogger<ConfigPaymentGatewaySelector>.Instance);

        return new FailoverPaymentGateway(
            registry,
            selector,
            circuitBreaker ?? new InMemoryGatewayCircuitBreaker(_failoverOptions),
            _alerter,
            Options.Create(_failoverOptions),
            Options.Create(paymentRetry ?? _paymentRetryOptions),
            NullLogger<FailoverPaymentGateway>.Instance);
    }

    private static GatewayPaymentRequest ChargeRequest(decimal amount = 25m, string currency = "USD") =>
        new("idem-1", amount, currency, "cus_1", "pm_card_visa", "test charge");

    [Fact]
    public async Task Charge_ReturnsFirstGatewaySuccess_WithAttemptedProviderAttribution()
    {
        var stripe = new FakePaymentGateway("stripe");
        var sut = CreateSut([stripe, new FakePaymentGateway("paypal")]);

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeTrue();
        result.ProviderId.Should().Be("stripe");
        stripe.ChargeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Charge_RetriesSameGatewayOnce_ThenFailsOverToNextPriority()
    {
        var stripe = new FakePaymentGateway("stripe")
            .EnqueueCharge(FakePaymentGateway.FailureResult("stripe", GatewayFailureKind.RetriableGatewayFailure))
            .EnqueueCharge(FakePaymentGateway.FailureResult("stripe", GatewayFailureKind.RetriableGatewayFailure));
        var payPal = new FakePaymentGateway("paypal");
        var sut = CreateSut([stripe, payPal]);

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeTrue();
        result.ProviderId.Should().Be("paypal");
        stripe.ChargeCallCount.Should().Be(2, "one attempt plus one same-gateway retry");
        payPal.ChargeCallCount.Should().Be(1, "failover reaches the next-priority gateway once");
    }

    [Fact]
    public async Task Charge_StopsAtMaxThreeAttempts_TotalAcrossGateways()
    {
        var stripe = new FakePaymentGateway("stripe");
        for (var i = 0; i < 5; i++)
        {
            stripe.EnqueueCharge(FakePaymentGateway.FailureResult("stripe", GatewayFailureKind.RetriableGatewayFailure));
        }

        var payPal = new FakePaymentGateway("paypal")
            .EnqueueCharge(FakePaymentGateway.FailureResult("paypal", GatewayFailureKind.RetriableGatewayFailure));

        var sut = CreateSut([stripe, payPal]);

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeFalse();
        result.ProviderId.Should().Be("paypal");
        result.FailureKind.Should().Be(GatewayFailureKind.RetriableGatewayFailure);
        stripe.ChargeCallCount.Should().Be(2);
        payPal.ChargeCallCount.Should().Be(1);
        (stripe.ChargeCallCount + payPal.ChargeCallCount).Should().Be(3, "the attempt budget is three");
    }

    [Fact]
    public async Task Charge_DoesNotRetryOrFailOver_OnTerminalDecline()
    {
        var stripe = new FakePaymentGateway("stripe")
            .EnqueueCharge(FakePaymentGateway.FailureResult("stripe", GatewayFailureKind.TerminalDecline, "card_declined"));
        var payPal = new FakePaymentGateway("paypal");
        var sut = CreateSut([stripe, payPal]);

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("card_declined");
        result.ProviderId.Should().Be("stripe");
        stripe.ChargeCallCount.Should().Be(1);
        payPal.ChargeCallCount.Should().Be(0, "a decline is never replayed on another gateway");
    }

    [Fact]
    public async Task Charge_DoesNotRetryOrFailOver_OnOutcomeUnknown()
    {
        var stripe = new FakePaymentGateway("stripe")
            .EnqueueCharge(FakePaymentGateway.FailureResult("stripe", GatewayFailureKind.OutcomeUnknown, "stripe_outcome_unknown"));
        var payPal = new FakePaymentGateway("paypal");
        var sut = CreateSut([stripe, payPal]);

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeFalse();
        result.FailureKind.Should().Be(GatewayFailureKind.OutcomeUnknown);
        stripe.ChargeCallCount.Should().Be(1);
        payPal.ChargeCallCount.Should().Be(0, "an unknown outcome is left to reconciliation, never replayed");
    }

    [Fact]
    public async Task Charge_SkipsSameGatewayRetry_WhenPaymentRetryBudgetIsZero()
    {
        // Payments:Retry:MaxRetries = 0 disables the same-gateway retry; failover moves
        // straight to the next-priority gateway.
        var stripe = new FakePaymentGateway("stripe")
            .EnqueueCharge(FakePaymentGateway.FailureResult("stripe", GatewayFailureKind.RetriableGatewayFailure));
        var payPal = new FakePaymentGateway("paypal");
        var sut = CreateSut([stripe, payPal], paymentRetry: new PaymentRetryOptions { MaxRetries = 0 });

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeTrue();
        result.ProviderId.Should().Be("paypal");
        stripe.ChargeCallCount.Should().Be(1);
        payPal.ChargeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Charge_TreatsTransportExceptionsAsRetriableGatewayFailures()
    {
        var stripe = new FakePaymentGateway("stripe")
            .EnqueueTransportError()
            .EnqueueTransportError();
        var payPal = new FakePaymentGateway("paypal");
        var sut = CreateSut([stripe, payPal]);

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeTrue();
        result.ProviderId.Should().Be("paypal");
        stripe.ChargeCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Charge_FailsClosedWithAlert_WhenEveryCircuitIsOpen()
    {
        var stripe = new FakePaymentGateway("stripe");
        var payPal = new FakePaymentGateway("paypal");
        var circuitBreaker = new InMemoryGatewayCircuitBreaker(new GatewayFailoverOptions
        {
            CircuitFailureThreshold = 1,
            CircuitOpenSeconds = 60
        });

        // Open both circuits by recording one retriable failure each (threshold 1).
        circuitBreaker.RecordFailure("stripe");
        circuitBreaker.RecordFailure("paypal");

        var sut = CreateSut([stripe, payPal], circuitBreaker: circuitBreaker);

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailoverPaymentGateway.AllGatewaysUnavailableErrorCode);
        result.Status.Should().Be(PaymentStatus.Failed);
        stripe.ChargeCallCount.Should().Be(0);
        payPal.ChargeCallCount.Should().Be(0);
        _alerter.RaisedCircuitStates.Should().HaveCount(1);
        _alerter.RaisedCircuitStates[0].Select(state => state.ProviderId).Should().Contain("stripe", "paypal");
    }

    [Fact]
    public async Task Charge_SkipsGatewayWithOpenCircuit_AndUsesTheRemainingGateway()
    {
        var stripe = new FakePaymentGateway("stripe");
        var payPal = new FakePaymentGateway("paypal");
        var circuitBreaker = new InMemoryGatewayCircuitBreaker(new GatewayFailoverOptions
        {
            CircuitFailureThreshold = 1,
            CircuitOpenSeconds = 60
        });
        circuitBreaker.RecordFailure("stripe"); // only stripe's circuit opens

        var sut = CreateSut([stripe, payPal], circuitBreaker: circuitBreaker);

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeTrue();
        result.ProviderId.Should().Be("paypal");
        stripe.ChargeCallCount.Should().Be(0, "the open circuit skips the gateway entirely");
        payPal.ChargeCallCount.Should().Be(1);
        _alerter.RaisedCircuitStates.Should().BeEmpty("a healthy gateway remained available");
    }

    [Fact]
    public async Task Charge_FailsClosed_WhenSelectionYieldsNoEligibleGateway()
    {
        var stripe = new FakePaymentGateway("stripe") { IsEnabled = false };
        var payPal = new FakePaymentGateway("paypal") { IsEnabled = false };
        var sut = CreateSut([stripe, payPal]);

        var act = () => sut.ProcessPaymentAsync(ChargeRequest());

        await act.Should().ThrowAsync<GatewaySelectionFailedException>()
            .WithMessage("*No eligible payment gateway*");
    }

    [Fact]
    public async Task Charge_RespectsCurrencyFiltersDuringFailover()
    {
        // Stripe serves USD only; a EUR charge selects PayPal alone even though Stripe
        // is registered and enabled.
        var stripe = new FakePaymentGateway("stripe");
        var payPal = new FakePaymentGateway("paypal");
        var routing = new GatewayRoutingOptions
        {
            Gateways = [new GatewayRouteEntry { ProviderId = "stripe", SupportedCurrencies = ["USD"] }]
        };
        var sut = CreateSut([stripe, payPal], routing: routing);

        var result = await sut.ProcessPaymentAsync(ChargeRequest(currency: "EUR"));

        result.Success.Should().BeTrue();
        result.ProviderId.Should().Be("paypal");
        stripe.ChargeCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Refund_IsPinnedToTheOwningGateway_AndRetriesOnlyThere()
    {
        var stripe = new FakePaymentGateway("stripe");
        var payPal = new FakePaymentGateway("paypal");
        var sut = CreateSut([stripe, payPal]);

        var result = await sut.ProcessRefundAsync(new GatewayRefundRequest(
            "refund-1",
            "capture_9",
            Amount: 10m,
            Reason: "customer request",
            ProviderId: "paypal"));

        result.Success.Should().BeTrue();
        payPal.RefundCalls.Should().ContainSingle();
        stripe.RefundCalls.Should().BeEmpty("refunds never reroute to another gateway");
    }

    [Fact]
    public async Task Refund_FailsClosed_WhenOwnerProviderIsNotRegistered()
    {
        var sut = CreateSut([new FakePaymentGateway("stripe")]);

        var act = () => sut.ProcessRefundAsync(new GatewayRefundRequest(
            "refund-1",
            "capture_9",
            Amount: 10m,
            Reason: "customer request",
            ProviderId: "adyen"));

        await act.Should().ThrowAsync<GatewaySelectionFailedException>()
            .WithMessage("*'adyen'*not a registered gateway*");
    }

    [Fact]
    public async Task Inquiry_IsPinnedToTheOwningGateway()
    {
        var stripe = new FakePaymentGateway("stripe");
        var payPal = new FakePaymentGateway("paypal");
        var sut = CreateSut([stripe, payPal]);

        var result = await sut.GetPaymentAsync("capture_9", CancellationToken.None, providerId: "paypal");

        result.Success.Should().BeTrue();
        result.TransactionId.Should().Be("capture_9");
    }

    [Fact]
    public async Task ProviderId_ExposesThePrimaryGateway()
    {
        var sut = CreateSut([new FakePaymentGateway("paypal"), new FakePaymentGateway("stripe")]);

        sut.ProviderId.Should().Be("stripe");
        sut.DisplayName.Should().Be("Fake stripe");
        sut.IsEnabled.Should().BeTrue();
    }
}
