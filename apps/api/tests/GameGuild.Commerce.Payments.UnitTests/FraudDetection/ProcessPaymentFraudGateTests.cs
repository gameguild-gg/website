using FluentAssertions;
using GameGuild.Commerce;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.FraudDetection;

/// <summary>
///     Issue #409: the pre-gateway fraud gate inside <see cref="ProcessPaymentCommandHandler"/>.
///     Block and Review must skip the gateway, persist a failed payment with evidence, and
///     never schedule an automatic retry; Allow must proceed to the gateway exactly once.
/// </summary>
public class ProcessPaymentFraudGateTests
{
    [Fact]
    public async Task Handle_ShouldSkipGatewayAndPersistFailedPayment_WhenFraudGateBlocks()
    {
        var fixture = CreateFixture(FraudTestHelpers.DetectionServiceReturning(FraudDecision.Block, 85, "blocked by test"));

        var result = await fixture.Handler.Handle(Command(fixture), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);
        result.FailureReason.Should().Contain("Fraud");

        fixture.PaymentGateway.Verify(
            gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var payment = fixture.GetCapturedPayment();
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Failed);
        payment.FraudDecision.Should().Be(FraudDecision.Block);
        payment.FraudScore.Should().Be(85);
        payment.FailureReason.Should().Contain("Fraud gate block");
        payment.ErrorCode.Should().Be("fraud_block");
        payment.FraudEvidence.Should().NotBeNullOrWhiteSpace();
        payment.NextRetryAt.Should().BeNull("a fraud-blocked payment must never be auto-retried");
        payment.CanRetry.Should().BeFalse("a fraud-blocked payment must never be retryable");
    }

    [Fact]
    public async Task Handle_ShouldQueueReview_WhenFraudGateReturnsReview()
    {
        var fixture = CreateFixture(FraudTestHelpers.DetectionServiceReturning(FraudDecision.Review, 55, "review by test"));

        var result = await fixture.Handler.Handle(Command(fixture), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);

        fixture.PaymentGateway.Verify(
            gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var payment = fixture.GetCapturedPayment();
        payment!.FraudDecision.Should().Be(FraudDecision.Review);
        payment.FraudReviewStatus.Should().Be(FraudReviewStatus.PendingReview);
        payment.NextRetryAt.Should().BeNull();
        payment.CanRetry.Should().BeFalse("a pending review must block retrying");
    }

    [Fact]
    public async Task Handle_ShouldProceedThroughGateway_WhenFraudGateAllows()
    {
        var fixture = CreateFixture(FraudTestHelpers.DetectionServiceReturning(FraudDecision.Allow, 0, "clean"));
        fixture.PaymentGateway
            .Setup(gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                Success: true,
                TransactionId: "pi_ok",
                ExternalPaymentId: "ch_ok",
                ErrorCode: null,
                ErrorMessage: null,
                Status: PaymentStatus.Succeeded,
                ProcessedAt: SystemClock.UtcNow,
                ProviderMapping: new GatewayProviderMapping("test", "acct_platform", "pi_ok", "payment_intent", "capture")));

        var result = await fixture.Handler.Handle(Command(fixture), CancellationToken.None);

        result.Success.Should().BeTrue();
        fixture.PaymentGateway.Verify(
            gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var payment = fixture.GetCapturedPayment()!;
        payment.FraudDecision.Should().Be(FraudDecision.Allow);
        payment.Status.Should().Be(PaymentStatus.Succeeded);
    }

    [Fact]
    public async Task Handle_ShouldAssessBeforeGatewayExecution()
    {
        var callOrder = new List<string>();
        var fixture = CreateFixture(
            new OrderingDetectionService(callOrder, FraudDecision.Allow),
            gatewayCallRecorder: () => callOrder.Add("gateway"));

        await fixture.Handler.Handle(Command(fixture), CancellationToken.None);

        callOrder.Should().Equal("assess", "gateway");
    }

    [Fact]
    public async Task BlockedPayment_ShouldNotBeFedToTheRetryWorker()
    {
        var fixture = CreateFixture(FraudTestHelpers.DetectionServiceReturning(FraudDecision.Block, 90, "blocked"));
        await fixture.Handler.Handle(Command(fixture), CancellationToken.None);

        var blockedPayment = fixture.GetCapturedPayment()!;

        // Entity-level guard: preparing a fraud-blocked payment for retry throws.
        var act = () => blockedPayment.PrepareForRetry();
        act.Should().Throw<InvalidOperationException>().WithMessage("*fraud*");

        // Handler-level guard: the retry command refuses the fraud-blocked payment.
        var retryRepository = new Mock<IPaymentRepository>();
        retryRepository
            .Setup(repository => repository.GetByIdAsync(blockedPayment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(blockedPayment);
        var retryHandler = new RetryPaymentCommandHandler(
            retryRepository.Object,
            fixture.PaymentGateway.Object,
            Mock.Of<IPaymentSubscriptionSyncService>(),
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<IRevenueAuditService>(),
            Mock.Of<IActorContextAccessor>(),
            Mock.Of<ILogger<RetryPaymentCommandHandler>>());

        var retryResult = await retryHandler.Handle(new RetryPaymentCommand(blockedPayment.Id), CancellationToken.None);

        retryResult.Success.Should().BeFalse();
        retryResult.FailureReason.Should().Contain("held by the fraud gate");
        fixture.PaymentGateway.Verify(
            gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ApprovedReview_ShouldReleaseTheRetryHold()
    {
        var fixture = CreateFixture(FraudTestHelpers.DetectionServiceReturning(FraudDecision.Review, 55, "review"));
        await fixture.Handler.Handle(Command(fixture), CancellationToken.None);

        var payment = fixture.GetCapturedPayment()!;
        payment.FraudReviewStatus.Should().Be(FraudReviewStatus.PendingReview);
        payment.CanRetry.Should().BeFalse();

        payment.ResolveFraudReview(approved: true, Guid.NewGuid(), "verified customer");

        payment.FraudReviewStatus.Should().Be(FraudReviewStatus.Approved);
        payment.CanRetry.Should().BeTrue("an approved review releases the fraud hold");
    }

    private static ProcessPaymentCommand Command(FraudGateFixture fixture) =>
        new(fixture.TenantId, fixture.SubscriptionId, 25m, "pm_test");

    private static FraudGateFixture CreateFixture(
        IFraudDetectionService fraudDetectionService,
        Action? gatewayCallRecorder = null)
    {
        var paymentRepository = new Mock<IPaymentRepository>();
        var paymentGateway = new Mock<IPaymentGateway>();
        var syncService = new Mock<IPaymentSubscriptionSyncService>();
        var contextService = new Mock<ISubscriptionPaymentContextService>();

        var tenantId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();

        Payment? capturedPayment = null;
        paymentRepository
            .Setup(repository => repository.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        paymentRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((payment, _) => capturedPayment = payment)
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        paymentRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((payment, _) => capturedPayment = payment)
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);

        paymentGateway.SetupGet(gateway => gateway.ProviderId).Returns("stripe");
        paymentGateway
            .Setup(gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => gatewayCallRecorder?.Invoke())
            .ReturnsAsync(new GatewayPaymentResult(
                Success: false,
                TransactionId: null,
                ExternalPaymentId: null,
                ErrorCode: "declined",
                ErrorMessage: "Declined",
                Status: PaymentStatus.Failed,
                ProcessedAt: SystemClock.UtcNow));

        contextService
            .Setup(service => service.GetPaymentContextAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPaymentContext(subscriptionId, tenantId, 25m, "USD", "cus_123"));

        var handler = new ProcessPaymentCommandHandler(
            paymentRepository.Object,
            paymentGateway.Object,
            syncService.Object,
            contextService.Object,
            Options.Create(new PaymentRetryOptions()),
            fraudDetectionService,
            Mock.Of<ILogger<ProcessPaymentCommandHandler>>());

        return new FraudGateFixture(tenantId, subscriptionId, paymentRepository, paymentGateway, () => capturedPayment, handler);
    }

    private sealed record FraudGateFixture(
        Guid TenantId,
        Guid SubscriptionId,
        Mock<IPaymentRepository> PaymentRepository,
        Mock<IPaymentGateway> PaymentGateway,
        Func<Payment?> CapturedPaymentAccessor,
        ProcessPaymentCommandHandler Handler)
    {
        public Payment? GetCapturedPayment() => CapturedPaymentAccessor();
    }

    private sealed class OrderingDetectionService(List<string> callOrder, FraudDecision decision) : IFraudDetectionService
    {
        public Task<FraudAssessment> AssessPaymentAsync(FraudSignalContext signalContext, CancellationToken cancellationToken = default)
        {
            callOrder.Add("assess");
            return Task.FromResult(new FraudAssessment(
                signalContext.TenantId,
                signalContext.PaymentId,
                0,
                FraudRiskLevel.Low,
                decision,
                "ordering probe",
                [],
                SystemClock.UtcNow));
        }
    }
}
