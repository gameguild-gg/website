using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.GatewayRouting;

/// <summary>
///     PayPal adapter tests (issue #413): the fail-closed sandbox-default configuration, the
///     simulated charge/refund round-trip, real-mode failure classification (transport →
///     retriable, HTTP 4xx → terminal, HTTP 5xx → retriable), outcome-unknown capture
///     handling, and the options startup validation.
/// </summary>
public class PayPalPaymentGatewayTests
{
    private readonly Mock<IPayPalApiClient> _apiClient = new();

    private PayPalPaymentGateway CreateSut(PayPalGatewayOptions? options = null) =>
        new(
            Options.Create(options ?? new PayPalGatewayOptions()),
            _apiClient.Object,
            NullLogger<PayPalPaymentGateway>.Instance);

    private static PayPalGatewayOptions Enabled(bool simulation = true) => new()
    {
        IsEnabled = true,
        UseSimulation = simulation,
        Environment = "sandbox",
        ClientId = "test-client-id",
        ClientSecret = "test-client-secret"
    };

    private static GatewayPaymentRequest ChargeRequest(string? paymentMethodId = "ba-token-1") =>
        new("idem-pp-1", 42.50m, "USD", "cus-1", paymentMethodId, "paypal charge");

    private static string CompletedCaptureBody(string captureId = "CAP-123") =>
        $$"""
        {
          "id": "ORDER-123",
          "status": "COMPLETED",
          "purchase_units": [
            {
              "payments": {
                "captures": [ { "id": "{{captureId}}", "status": "COMPLETED" } ]
              }
            }
          ]
        }
        """;

    [Fact]
    public void Defaults_AreFailClosedAndSandboxFirst()
    {
        var options = new PayPalGatewayOptions();

        options.IsEnabled.Should().BeFalse("the adapter is disabled until opted in");
        options.Environment.Should().Be("sandbox", "sandbox is the safe default environment");
        options.ResolveBaseUrl().Should().Be("https://api-m.sandbox.paypal.com");
        options.EnvironmentLabel.Should().Be("sandbox");
    }

    [Fact]
    public async Task Charge_FailsClosed_WhenAdapterIsDisabled()
    {
        var sut = CreateSut(new PayPalGatewayOptions());

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("paypal_adapter_disabled");
        result.FailureKind.Should().Be(GatewayFailureKind.TerminalDecline);
        _apiClient.Verify(client => client.CreateAndCaptureOrderAsync(
            It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Charge_Simulated_ReturnsSuccessWithProviderMapping()
    {
        var sut = CreateSut(Enabled(simulation: true));

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeTrue();
        result.Status.Should().Be(PaymentStatus.Succeeded);
        result.ProviderMapping.Should().NotBeNull();
        result.ProviderMapping!.ProviderEnvironment.Should().Be("test");
        result.ProviderMapping.ProviderObjectType.Should().Be("capture");
        result.TransactionId.Should().StartWith("PAYID-SIMULATED-");
    }

    [Fact]
    public async Task Refund_Simulated_ReturnsSuccessRoundTrip()
    {
        var sut = CreateSut(Enabled(simulation: true));

        var charge = await sut.ProcessPaymentAsync(ChargeRequest());
        var refund = await sut.ProcessRefundAsync(new GatewayRefundRequest(
            "refund-pp-1",
            charge.TransactionId!,
            Amount: 42.50m,
            Reason: "customer request",
            ProviderId: "paypal",
            Currency: "USD"));

        refund.Success.Should().BeTrue();
        refund.AmountRefunded.Should().Be(42.50m);
        refund.RefundId.Should().StartWith("PAYPAL-REFUND-SIMULATED-");
    }

    [Fact]
    public async Task Charge_RealMode_RequiresAPaymentMethodToken()
    {
        var sut = CreateSut(Enabled(simulation: false));

        var result = await sut.ProcessPaymentAsync(ChargeRequest(paymentMethodId: null));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("paypal_payment_method_required");
        result.FailureKind.Should().Be(GatewayFailureKind.TerminalDecline);
    }

    [Fact]
    public async Task Charge_RealMode_MapsCompletedCaptureToSuccess()
    {
        var sut = CreateSut(Enabled(simulation: false));
        _apiClient
            .Setup(client => client.CreateAndCaptureOrderAsync(
                "idem-pp-1", 42.50m, "USD", "ba-token-1", "paypal charge", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayPalApiResult(true, 201, CompletedCaptureBody(), null, null, TransportFailed: false));

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeTrue();
        result.TransactionId.Should().Be("CAP-123");
        result.ProviderMapping.Should().NotBeNull();
        result.ProviderMapping!.ProviderEnvironment.Should().Be("sandbox");
        result.ProviderMapping.ProviderObjectId.Should().Be("CAP-123");
        result.ProviderMapping.ProviderObjectType.Should().Be("capture");
        result.ProviderMapping.ProviderMonetaryLeg.Should().Be("capture");
    }

    [Fact]
    public async Task Charge_TransportFailure_IsRetriable()
    {
        var sut = CreateSut(Enabled(simulation: false));
        _apiClient
            .Setup(client => client.CreateAndCaptureOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayPalApiResult(false, null, null, "paypal_transport_failure", "connection reset", TransportFailed: true));

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);
        result.FailureKind.Should().Be(GatewayFailureKind.RetriableGatewayFailure);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task Charge_ServerOrThrottleHttpFailures_AreRetriable(int statusCode)
    {
        var sut = CreateSut(Enabled(simulation: false));
        _apiClient
            .Setup(client => client.CreateAndCaptureOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayPalApiResult(
                false,
                statusCode,
                """{ "name": "INTERNAL_SERVICE_ERROR", "message": "upstream failure" }""",
                "INTERNAL_SERVICE_ERROR",
                "upstream failure",
                TransportFailed: false));

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeFalse();
        result.FailureKind.Should().Be(GatewayFailureKind.RetriableGatewayFailure);
    }

    [Fact]
    public async Task Charge_Http4xxAnswer_IsTerminalDecline()
    {
        var sut = CreateSut(Enabled(simulation: false));
        _apiClient
            .Setup(client => client.CreateAndCaptureOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayPalApiResult(
                false,
                422,
                """{ "name": "UNPROCESSABLE_ENTITY", "details": [ { "issue": "INSTRUMENT_DECLINED" } ] }""",
                "INSTRUMENT_DECLINED",
                null,
                TransportFailed: false));

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeFalse();
        result.FailureKind.Should().Be(GatewayFailureKind.TerminalDecline);
        result.ErrorCode.Should().Be("INSTRUMENT_DECLINED");
    }

    [Fact]
    public async Task Charge_UnconfirmedCapture_IsOutcomeUnknown_WithoutTransactionReference()
    {
        var sut = CreateSut(Enabled(simulation: false));
        _apiClient
            .Setup(client => client.CreateAndCaptureOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayPalApiResult(
                true,
                201,
                """{ "id": "ORDER-9", "status": "PENDING" }""",
                null,
                null,
                TransportFailed: false));

        var result = await sut.ProcessPaymentAsync(ChargeRequest());

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Processing);
        result.FailureKind.Should().Be(GatewayFailureKind.OutcomeUnknown);
        result.TransactionId.Should().BeNull("an unverified provider object may not be referenced without its mapping");
        result.ErrorMessage.Should().Contain("ORDER-9");
    }

    [Fact]
    public async Task Refund_RealMode_MapsFailureKinds()
    {
        var sut = CreateSut(Enabled(simulation: false));
        _apiClient
            .Setup(client => client.RefundCaptureAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayPalApiResult(false, 503, "{}", "INTERNAL_SERVICE_ERROR", "try later", TransportFailed: false));

        var result = await sut.ProcessRefundAsync(new GatewayRefundRequest(
            "refund-pp-2", "CAP-123", 10m, "dup", ProviderId: "paypal", Currency: "USD"));

        result.Success.Should().BeFalse();
        result.FailureKind.Should().Be(GatewayFailureKind.RetriableGatewayFailure);
    }

    [Fact]
    public async Task SubscriptionAndCustomerOperations_FailClosedWithExplicitNotSupported()
    {
        var sut = CreateSut(Enabled(simulation: true));

        var customer = await sut.CreateCustomerAsync(new GatewayCustomerRequest("a@b.c", null, null));
        var paymentMethod = await sut.CreatePaymentMethodAsync(new GatewayPaymentMethodRequest("cus-1", "tok"));
        var subscription = await sut.CancelSubscriptionAsync("I-123");

        customer.Success.Should().BeFalse();
        customer.ErrorCode.Should().Be("paypal_customer_management_not_supported");
        paymentMethod.Success.Should().BeFalse();
        paymentMethod.ErrorCode.Should().Be("paypal_payment_method_management_not_supported");
        subscription.Success.Should().BeFalse();
        subscription.ErrorCode.Should().Be("paypal_subscription_management_not_supported");
    }

    [Theory]
    [InlineData(10, "USD", "10.00")]
    [InlineData(0.5, "USD", "0.50")]
    [InlineData(1000, "JPY", "1000")]
    public void AmountFormatter_UsesTheCurrencyNaturalDecimals(decimal amount, string currency, string expected) =>
        PayPalAmountFormatter.Format(amount, currency).Should().Be(expected);

    [Fact]
    public void OptionsValidator_FailsFast_WhenEnabledWithoutCredentialsOrSimulation()
    {
        var validator = new PayPalGatewayOptionsValidator();
        var options = new PayPalGatewayOptions { IsEnabled = true, UseSimulation = false };

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure => failure.Contains("ClientId"));
        result.Failures.Should().Contain(failure => failure.Contains("ClientSecret"));
    }

    [Fact]
    public void OptionsValidator_AcceptsTheFailClosedDefaults()
    {
        var validator = new PayPalGatewayOptionsValidator();

        var result = validator.Validate(null, new PayPalGatewayOptions());

        result.Failed.Should().BeFalse();
    }

    [Fact]
    public void OptionsValidator_RejectsUnknownEnvironments()
    {
        var validator = new PayPalGatewayOptionsValidator();
        var options = new PayPalGatewayOptions { Environment = "production" };

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(failure => failure.Contains("Environment"));
    }
}
