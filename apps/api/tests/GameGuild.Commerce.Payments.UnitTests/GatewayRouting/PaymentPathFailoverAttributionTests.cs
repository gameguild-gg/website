using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using static GameGuild.Commerce.Payments.UnitTests.GatewayRouting.PaymentPathFailoverFixtures;

namespace GameGuild.Commerce.Payments.UnitTests.GatewayRouting;

/// <summary>
///     Payment-path integration for gateway failover attribution (issue #413): when the
///     failover composite answers from a gateway other than the primary, the payment is
///     re-attributed to the gateway that actually processed it before the provider mapping
///     is bound, while a bound mapping keeps its provider authoritative.
/// </summary>
public class PaymentPathFailoverAttributionTests
{
    [Fact]
    public async Task Handle_ReAttributesPaymentToTheGatewayThatProcessedTheCharge()
    {
        var fixture = CreateFixture();
        fixture.PaymentGateway
            .Setup(gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                Success: true,
                TransactionId: "CAP-77",
                ExternalPaymentId: "CAP-77",
                ErrorCode: null,
                ErrorMessage: null,
                Status: PaymentStatus.Succeeded,
                ProcessedAt: SystemClock.UtcNow,
                ProviderMapping: new GatewayProviderMapping("sandbox", "acct_paypal", "CAP-77", "capture", "capture"),
                ProviderId: "paypal"));

        var result = await fixture.Handler.Handle(
            new ProcessPaymentCommand(fixture.TenantId, fixture.SubscriptionId, 25m, "pm_test"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        fixture.AddedPayment.Should().NotBeNull();
        fixture.AddedPayment!.Provider.Should().Be("paypal", "failover re-attributed the payment to PayPal");
        fixture.AddedPayment.ProviderObjectId.Should().Be("CAP-77");
        fixture.AddedPayment.ProviderObjectType.Should().Be("capture");
    }

    [Fact]
    public async Task Handle_KeepsPrimaryProvider_WhenThePrimaryGatewayAnswers()
    {
        var fixture = CreateFixture();
        fixture.PaymentGateway
            .Setup(gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                Success: true,
                TransactionId: "pi_1",
                ExternalPaymentId: "ch_1",
                ErrorCode: null,
                ErrorMessage: null,
                Status: PaymentStatus.Succeeded,
                ProcessedAt: SystemClock.UtcNow,
                ProviderMapping: new GatewayProviderMapping("test", "acct_platform", "pi_1", "payment_intent", "capture"),
                ProviderId: "stripe"));

        var result = await fixture.Handler.Handle(
            new ProcessPaymentCommand(fixture.TenantId, fixture.SubscriptionId, 25m, "pm_test"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        fixture.AddedPayment!.Provider.Should().Be("stripe");
    }

    [Fact]
    public void RecordProcessingProvider_IsANoOp_WhenMappingIsAlreadyBound()
    {
        var payment = Payment.Create(
            tenantId: Guid.NewGuid(),
            amount: 25m,
            currency: "USD",
            idempotencyKey: "idem-bound",
            provider: "stripe");
        payment.BindProviderMapping("stripe", "test", "acct_1", "pi_bound", "payment_intent", "capture");

        payment.RecordProcessingProvider("paypal");

        payment.Provider.Should().Be("stripe", "a bound mapping keeps its provider authoritative");
        payment.ProviderObjectId.Should().Be("pi_bound");
    }

    [Fact]
    public void RecordProcessingProvider_Reattributes_OnlyWhileUnbound()
    {
        var payment = Payment.Create(
            tenantId: Guid.NewGuid(),
            amount: 25m,
            currency: "USD",
            idempotencyKey: "idem-free",
            provider: "stripe");

        payment.RecordProcessingProvider("PayPal");

        payment.Provider.Should().Be("paypal", "provider ids normalize to lowercase");
    }
}

internal static class PaymentPathFailoverFixtures
{
    public static PaymentHandlerFixture CreateFixture() => new();
}

internal sealed class PaymentHandlerFixture
{
    public PaymentHandlerFixture()
    {
        PaymentRepository = new Mock<IPaymentRepository>();
        PaymentGateway = new Mock<IPaymentGateway>();
        SyncService = new Mock<IPaymentSubscriptionSyncService>();
        ContextService = new Mock<ISubscriptionPaymentContextService>();
        FraudGate = new Mock<IFraudDetectionService>();

        PaymentGateway.SetupGet(gateway => gateway.ProviderId).Returns("stripe");

        // The merged fraud gate (issue #409) must allow the charge so the failover
        // attribution path reaches the gateway.
        FraudGate
            .Setup(fraud => fraud.AssessPaymentAsync(It.IsAny<FraudSignalContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FraudSignalContext context, CancellationToken _) => new FraudAssessment(
                context.TenantId,
                context.PaymentId,
                Score: 0,
                RiskLevel: FraudRiskLevel.Low,
                Decision: FraudDecision.Allow,
                Reason: "unit test",
                Signals: [],
                AssessedAt: SystemClock.UtcNow));

        PaymentRepository
            .Setup(repository => repository.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        PaymentRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((payment, _) => AddedPayment = payment)
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        PaymentRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);

        ContextService
            .Setup(service => service.GetPaymentContextAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid subscriptionId, CancellationToken _) => new SubscriptionPaymentContext(
                subscriptionId,
                TenantId,
                25m,
                "USD",
                "cus_123"));

        SyncService
            .Setup(service => service.SyncSuccessfulPaymentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Handler = new ProcessPaymentCommandHandler(
            PaymentRepository.Object,
            PaymentGateway.Object,
            SyncService.Object,
            ContextService.Object,
            Options.Create(new PaymentRetryOptions()),
            FraudGate.Object,
            Mock.Of<ILogger<ProcessPaymentCommandHandler>>());
    }

    public Guid TenantId { get; } = Guid.NewGuid();

    public Guid SubscriptionId { get; } = Guid.NewGuid();

    public Mock<IPaymentRepository> PaymentRepository { get; }

    public Mock<IPaymentGateway> PaymentGateway { get; }

    public Mock<IPaymentSubscriptionSyncService> SyncService { get; }

    public Mock<ISubscriptionPaymentContextService> ContextService { get; }

    public Mock<IFraudDetectionService> FraudGate { get; }

    public ProcessPaymentCommandHandler Handler { get; }

    public Payment? AddedPayment { get; private set; }
}
