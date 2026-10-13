using FluentAssertions;
using GameGuild.Commerce;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Services;

public sealed class OrderPaymentIntentServiceTests
{
    [Fact]
    public async Task PrepareAsync_CreatesBoundPaymentIntentFromAuthoritativeOrder()
    {
        var intent = Intent();
        var (repository, stripe, service) = Service();
        Payment? updated = null;
        repository.Setup(item => item.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        repository.Setup(item => item.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback((Payment payment, CancellationToken _) => updated = payment)
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        stripe.Setup(item => item.CreatePaymentIntentAsync(It.IsAny<GatewayPaymentIntentSetupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentIntentSetupResult(
                "pi_order", PaymentStatus.RequiresAction, "pi_secret", Mapping()));

        var result = await service.PrepareAsync(intent);

        result.Should().Be(new OrderPaymentIntentPreparation(true, updated!.Id, "pi_secret", null, OrderChargeState.RequiresAction));
        updated.Status.Should().Be(PaymentStatus.RequiresAction);
        updated.OrderId.Should().Be(intent.OrderId);
        updated.ProviderObjectId.Should().Be("pi_order");
        stripe.Verify(item => item.CreatePaymentIntentAsync(
            It.Is<GatewayPaymentIntentSetupRequest>(request =>
                request.Amount == intent.Amount && request.Currency == intent.Currency &&
                request.Metadata["order_id"] == intent.OrderId.ToString("N") &&
                request.Metadata["tenant_id"] == intent.TenantId.ToString("N")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false, PaymentStatus.Failed, OrderChargeState.Failed)]
    [InlineData(true, PaymentStatus.Processing, OrderChargeState.RequiresReconciliation)]
    public async Task PrepareAsync_DistinguishesKnownFailureFromAmbiguousOutcome(
        bool outcomeUnknown,
        PaymentStatus expectedStatus,
        OrderChargeState expectedState)
    {
        var intent = Intent();
        var (repository, stripe, service) = Service();
        Payment? updated = null;
        repository.Setup(item => item.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        repository.Setup(item => item.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback((Payment payment, CancellationToken _) => updated = payment)
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        stripe.Setup(item => item.CreatePaymentIntentAsync(It.IsAny<GatewayPaymentIntentSetupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentIntentSetupResult(
                null, PaymentStatus.Failed, null, null, outcomeUnknown, "provider_error", "Provider failed."));

        var result = await service.PrepareAsync(intent);

        result.Success.Should().BeFalse();
        result.State.Should().Be(expectedState);
        updated!.Status.Should().Be(expectedStatus);
        updated.ErrorCode.Should().Be(outcomeUnknown ? null : "provider_error");
    }

    [Fact]
    public async Task PrepareAsync_ReplaysConcurrentInsertWinner()
    {
        var intent = Intent();
        var winner = PaymentFor(intent);
        winner.MarkAsProcessing("pi_winner");
        var (repository, stripe, service) = Service();
        repository.Setup(item => item.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>())).ReturnsAsync(winner);
        stripe.Setup(item => item.GetPaymentAsync("pi_winner", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderResult(PaymentStatus.RequiresAction, "pi_secret"));

        var result = await service.PrepareAsync(intent);

        result.Success.Should().BeTrue();
        result.PaymentId.Should().Be(winner.Id);
        stripe.Verify(item => item.CreatePaymentIntentAsync(It.IsAny<GatewayPaymentIntentSetupRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PrepareAsync_RejectsMismatchedReplayAndUnboundIntent()
    {
        var intent = Intent();
        var mismatch = Payment.Create(intent.TenantId, intent.Amount + 1, intent.Currency,
            OrderPaymentService.CreateIdempotencyKey(intent.TenantId, intent.OrderId), orderId: intent.OrderId);
        var (repository, _, service) = Service();
        repository.SetupSequence(item => item.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mismatch)
            .ReturnsAsync(PaymentFor(intent));

        var mismatched = await service.PrepareAsync(intent);
        var unbound = await service.PrepareAsync(intent);

        mismatched.State.Should().Be(OrderChargeState.Failed);
        mismatched.FailureReason.Should().Contain("does not match");
        unbound.State.Should().Be(OrderChargeState.RequiresReconciliation);
    }

    [Theory]
    [InlineData(PaymentStatus.Succeeded, OrderChargeState.Succeeded, false)]
    [InlineData(PaymentStatus.RequiresAction, OrderChargeState.RequiresAction, true)]
    [InlineData(PaymentStatus.RequiresAction, OrderChargeState.RequiresAction, false, "")]
    [InlineData(PaymentStatus.Processing, OrderChargeState.Processing, false)]
    [InlineData(PaymentStatus.Pending, OrderChargeState.Processing, false)]
    [InlineData(PaymentStatus.Failed, OrderChargeState.Failed, false)]
    public async Task PrepareAsync_MapsProviderReplayStatus(
        PaymentStatus providerStatus,
        OrderChargeState expectedState,
        bool expectedSuccess,
        string? clientToken = "pi_secret")
    {
        var intent = Intent();
        var payment = PaymentFor(intent);
        payment.MarkAsProcessing("pi_existing");
        var (repository, stripe, service) = Service();
        repository.Setup(item => item.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        stripe.Setup(item => item.GetPaymentAsync("pi_existing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderResult(providerStatus, clientToken));

        var result = await service.PrepareAsync(intent);

        result.State.Should().Be(expectedState);
        result.Success.Should().Be(expectedSuccess);
        result.ClientSecret.Should().Be(clientToken);
    }

    private static (Mock<IPaymentRepository> Repository, Mock<IStripePaymentService> Stripe, OrderPaymentIntentService Service) Service()
    {
        var repository = new Mock<IPaymentRepository>();
        repository.Setup(item => item.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        var stripe = new Mock<IStripePaymentService>();
        return (repository, stripe, new OrderPaymentIntentService(
            repository.Object,
            stripe.Object,
            Options.Create(new PaymentRetryOptions())));
    }

    private static AuthoritativeOrderPaymentIntent Intent() => new(Guid.NewGuid(), Guid.NewGuid(), 45m, "USD");

    private static Payment PaymentFor(AuthoritativeOrderPaymentIntent intent) => Payment.Create(
        intent.TenantId,
        intent.Amount,
        intent.Currency,
        OrderPaymentService.CreateIdempotencyKey(intent.TenantId, intent.OrderId),
        orderId: intent.OrderId);

    private static GatewayProviderMapping Mapping() => new("test", "acct_platform", "pi_order", "payment_intent", "capture");

    private static GatewayPaymentResult ProviderResult(PaymentStatus status, string? token) => new(
        status == PaymentStatus.Succeeded,
        "pi_existing",
        status == PaymentStatus.Succeeded ? "ch_existing" : null,
        status == PaymentStatus.Failed ? "failed" : null,
        status == PaymentStatus.Failed ? "Provider failed." : null,
        status,
        SystemClock.UtcNow,
        token,
        Mapping());
}
