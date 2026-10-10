using FluentAssertions;
using GameGuild.Commerce;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Services;

public sealed class OrderPaymentServiceTests
{
    [Fact]
    public async Task ProcessAsync_ShouldCreatePaymentFromAuthoritativeOrderFacts()
    {
        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        repository.Setup(repo => repo.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        repository.Setup(repo => repo.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);

        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.Setup(item => item.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                true,
                "txn_order",
                "pi_order",
                null,
                null,
                PaymentStatus.Succeeded,
                SystemClock.UtcNow).WithVerifiedMapping());

        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());
        var charge = new AuthoritativeOrderCharge(
            Guid.NewGuid(),
            Guid.NewGuid(),
            75m,
            "EUR",
            "pm_order");

        var result = await service.ProcessAsync(charge);

        result.Success.Should().BeTrue();
        result.PaymentId.Should().NotBeNull();
        repository.Verify(repo => repo.AddAsync(
            It.Is<Payment>(payment =>
                payment.OrderId == charge.OrderId &&
                payment.TenantId == charge.TenantId &&
                payment.Amount == 75m &&
                payment.Currency == "EUR" &&
                payment.SubscriptionId == null),
            It.IsAny<CancellationToken>()), Times.Once);
        gateway.Verify(item => item.ProcessPaymentAsync(
            It.Is<GatewayPaymentRequest>(request =>
                request.Amount == 75m &&
                request.Currency == "EUR" &&
                request.Metadata!["order_id"] == charge.OrderId.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IsSettledAsync_ShouldRejectAnyPaymentBindingMismatch()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var payment = Payment.Create(
            tenantId,
            75m,
            "USD",
            "order-payment",
            orderId: orderId);
        payment.MarkAsProcessing();
        payment.MarkAsSucceeded("pi_order", "txn_order");

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var service = new OrderPaymentService(
            repository.Object,
            Mock.Of<IPaymentGateway>(),
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var exact = await service.IsSettledAsync(new OrderPaymentBinding(orderId, payment.Id, tenantId, 75m, "USD"));
        var wrongAmount = await service.IsSettledAsync(new OrderPaymentBinding(orderId, payment.Id, tenantId, 1m, "USD"));
        var wrongTenant = await service.IsSettledAsync(new OrderPaymentBinding(orderId, payment.Id, Guid.NewGuid(), 75m, "USD"));

        exact.Should().BeTrue();
        wrongAmount.Should().BeFalse();
        wrongTenant.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAsync_ShouldReturnExistingSuccessfulPaymentWithoutChargingAgain()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var charge = new AuthoritativeOrderCharge(orderId, tenantId, 75m, "USD", "pm_order");
        var payment = Payment.Create(
            tenantId,
            charge.Amount,
            charge.Currency,
            OrderPaymentService.CreateIdempotencyKey(tenantId, orderId),
            orderId: orderId,
            paymentMethodId: charge.PaymentMethodId);
        payment.MarkAsProcessing();
        payment.MarkAsSucceeded("pi_existing", "txn_existing");

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(payment.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(charge);

        result.Success.Should().BeTrue();
        result.PaymentId.Should().Be(payment.Id);
        result.ExternalPaymentId.Should().Be("pi_existing");
        gateway.VerifyNoOtherCalls();
        repository.Verify(repo => repo.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ShouldUseConcurrentInsertWinnerWithoutChargingAgain()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var charge = new AuthoritativeOrderCharge(orderId, tenantId, 75m, "USD", "pm_order");
        var winner = Payment.Create(
            tenantId,
            charge.Amount,
            charge.Currency,
            OrderPaymentService.CreateIdempotencyKey(tenantId, orderId),
            orderId: orderId,
            paymentMethodId: charge.PaymentMethodId);
        winner.MarkAsProcessing();
        winner.MarkAsSucceeded("pi_winner", "txn_winner");

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(winner.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        repository.Setup(repo => repo.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(winner);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(charge);

        result.Success.Should().BeTrue();
        result.PaymentId.Should().Be(winner.Id);
        result.ExternalPaymentId.Should().Be("pi_winner");
        gateway.Verify(
            item => item.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ShouldRetryFailedOrderPaymentWithLatestMethod()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var charge = new AuthoritativeOrderCharge(orderId, tenantId, 75m, "USD", "pm_replacement");
        var payment = Payment.Create(
            tenantId,
            charge.Amount,
            charge.Currency,
            OrderPaymentService.CreateIdempotencyKey(tenantId, orderId),
            orderId: orderId,
            paymentMethodId: "pm_declined");
        payment.MarkAsProcessing();
        payment.MarkAsFailed("Card declined", "card_declined");

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(payment.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        repository.Setup(repo => repo.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.Setup(item => item.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                true,
                "txn_retry",
                "pi_retry",
                null,
                null,
                PaymentStatus.Succeeded,
                SystemClock.UtcNow).WithVerifiedMapping());
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(charge);

        result.Success.Should().BeTrue();
        payment.RetryCount.Should().Be(1);
        payment.PaymentMethodId.Should().Be("pm_replacement");
        gateway.Verify(item => item.ProcessPaymentAsync(
            It.Is<GatewayPaymentRequest>(request =>
                request.IdempotencyKey.EndsWith(":retry:1", StringComparison.Ordinal) &&
                request.PaymentMethodId == "pm_replacement"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ShouldCancelKnownFailedProviderIntentBeforeRetry()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var charge = new AuthoritativeOrderCharge(orderId, tenantId, 75m, "USD", "pm_replacement");
        var payment = Payment.Create(
            tenantId,
            charge.Amount,
            charge.Currency,
            OrderPaymentService.CreateIdempotencyKey(tenantId, orderId),
            orderId: orderId,
            paymentMethodId: "pm_declined");
        payment.MarkAsProcessing("pi_declined");
        payment.MarkAsFailed("Card declined", "card_declined");

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(payment.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        repository.Setup(repo => repo.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.Setup(item => item.CancelPaymentAsync("pi_declined", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentCancellationResult(true, false, null, null));
        gateway.Setup(item => item.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                true,
                "pi_retry",
                "ch_retry",
                null,
                null,
                PaymentStatus.Succeeded,
                SystemClock.UtcNow).WithVerifiedMapping());
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(charge);

        result.Success.Should().BeTrue();
        gateway.Verify(item => item.CancelPaymentAsync("pi_declined", It.IsAny<CancellationToken>()), Times.Once);
        gateway.Verify(item => item.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ShouldRequireReconciliationWhenFailedIntentCancellationIsNotConfirmed()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var payment = Payment.Create(
            tenantId,
            75m,
            "USD",
            OrderPaymentService.CreateIdempotencyKey(tenantId, orderId),
            orderId: orderId,
            paymentMethodId: "pm_declined");
        payment.MarkAsProcessing("pi_declined");
        payment.MarkAsFailed("Card declined", "card_declined");

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(payment.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.Setup(item => item.CancelPaymentAsync("pi_declined", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentCancellationResult(
                false,
                true,
                "stripe_outcome_unknown",
                "Cancellation outcome is unknown."));
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(
            new AuthoritativeOrderCharge(orderId, tenantId, 75m, "USD", "pm_replacement"));

        result.State.Should().Be(OrderChargeState.RequiresReconciliation);
        payment.ExternalTransactionId.Should().Be("pi_declined");
        gateway.Verify(
            item => item.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ShouldReconcileRequiresActionToSucceededWithoutNewCharge()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var charge = new AuthoritativeOrderCharge(orderId, tenantId, 75m, "USD", "pm_original");
        var payment = Payment.Create(
            tenantId,
            charge.Amount,
            charge.Currency,
            OrderPaymentService.CreateIdempotencyKey(tenantId, orderId),
            orderId: orderId,
            paymentMethodId: "pm_original");
        payment.MarkAsProcessing();
        payment.MarkAsRequiresAction("pi_requires_action");

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(payment.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.Setup(item => item.GetPaymentAsync("pi_requires_action", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                true,
                "txn_retry",
                "pi_retry",
                null,
                null,
                PaymentStatus.Succeeded,
                SystemClock.UtcNow).WithVerifiedMapping());
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(charge);

        result.Success.Should().BeTrue();
        payment.RetryCount.Should().Be(0);
        payment.Status.Should().Be(PaymentStatus.Succeeded);
        payment.PaymentMethodId.Should().Be("pm_original");
        gateway.Verify(
            item => item.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ShouldKeepAmbiguousPaymentProcessingWhenProviderIsStillUnknown()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var charge = new AuthoritativeOrderCharge(orderId, tenantId, 75m, "USD", "pm_order");
        var payment = Payment.Create(
            tenantId,
            charge.Amount,
            charge.Currency,
            OrderPaymentService.CreateIdempotencyKey(tenantId, orderId),
            orderId: orderId,
            paymentMethodId: charge.PaymentMethodId);
        payment.MarkAsProcessing();

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(payment.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.Setup(item => item.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                false,
                null,
                null,
                "stripe_outcome_unknown",
                "Payment outcome is pending provider reconciliation.",
                PaymentStatus.Processing,
                SystemClock.UtcNow).WithVerifiedMapping());
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(charge);

        result.Success.Should().BeFalse();
        result.PaymentId.Should().Be(payment.Id);
        result.State.Should().Be(OrderChargeState.Processing);
        gateway.Verify(item => item.ProcessPaymentAsync(
            It.Is<GatewayPaymentRequest>(request => request.IdempotencyKey == payment.IdempotencyKey),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ShouldReconcileAmbiguousProcessingPaymentWithOriginalGatewayRequest()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var idempotencyKey = OrderPaymentService.CreateIdempotencyKey(tenantId, orderId);
        var payment = Payment.Create(
            tenantId,
            75m,
            "USD",
            idempotencyKey,
            orderId: orderId,
            paymentMethodId: "pm_original");
        payment.MarkAsProcessing();

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        repository.Setup(repo => repo.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.Setup(item => item.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                true,
                "pi_original",
                "ch_original",
                null,
                null,
                PaymentStatus.Succeeded,
                SystemClock.UtcNow).WithVerifiedMapping());
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(
            new AuthoritativeOrderCharge(orderId, tenantId, 75m, "USD", "pm_replacement"));

        result.Success.Should().BeTrue();
        gateway.Verify(item => item.ProcessPaymentAsync(
            It.Is<GatewayPaymentRequest>(request =>
                request.IdempotencyKey == idempotencyKey &&
                request.PaymentMethodId == "pm_original"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ShouldReplayTheActiveRetryKeyAfterAmbiguousRetryOutcome()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var idempotencyKey = OrderPaymentService.CreateIdempotencyKey(tenantId, orderId);
        var payment = Payment.Create(
            tenantId,
            75m,
            "USD",
            idempotencyKey,
            orderId: orderId,
            paymentMethodId: "pm_original");
        payment.MarkAsProcessing("pi_declined");
        payment.MarkAsFailed("Card declined", "card_declined");
        payment.PrepareForRetry("pm_retry");
        payment.MarkAsProcessing();

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.Setup(item => item.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                false,
                null,
                null,
                "stripe_outcome_unknown",
                "Payment outcome remains unknown.",
                PaymentStatus.Processing,
                SystemClock.UtcNow).WithVerifiedMapping());
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(
            new AuthoritativeOrderCharge(orderId, tenantId, 75m, "USD", "pm_latest"));

        result.State.Should().Be(OrderChargeState.Processing);
        gateway.Verify(item => item.ProcessPaymentAsync(
            It.Is<GatewayPaymentRequest>(request =>
                request.IdempotencyKey == $"{idempotencyKey}:retry:1" &&
                request.PaymentMethodId == "pm_retry"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ShouldRecoverPersistedPendingPayment()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var idempotencyKey = OrderPaymentService.CreateIdempotencyKey(tenantId, orderId);
        var payment = Payment.Create(
            tenantId,
            75m,
            "USD",
            idempotencyKey,
            orderId: orderId,
            paymentMethodId: "pm_original");

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        repository.Setup(repo => repo.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.Setup(item => item.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                true,
                "pi_recovered",
                "ch_recovered",
                null,
                null,
                PaymentStatus.Succeeded,
                SystemClock.UtcNow).WithVerifiedMapping());
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(
            new AuthoritativeOrderCharge(orderId, tenantId, 75m, "USD", "pm_latest"));

        result.Success.Should().BeTrue();
        gateway.Verify(item => item.ProcessPaymentAsync(
            It.Is<GatewayPaymentRequest>(request =>
                request.IdempotencyKey == idempotencyKey &&
                request.PaymentMethodId == "pm_original"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ShouldReturnRequiresActionReferenceFromProviderReconciliation()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var idempotencyKey = OrderPaymentService.CreateIdempotencyKey(tenantId, orderId);
        var payment = Payment.Create(
            tenantId,
            75m,
            "USD",
            idempotencyKey,
            orderId: orderId,
            paymentMethodId: "pm_original");
        payment.MarkAsProcessing();
        payment.MarkAsRequiresAction("pi_requires_action");

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.Setup(item => item.GetPaymentAsync("pi_requires_action", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                false,
                "pi_requires_action",
                null,
                null,
                "Additional authentication is required.",
                PaymentStatus.RequiresAction,
                SystemClock.UtcNow,
                "pi_requires_action_secret_test").WithVerifiedMapping());
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(
            new AuthoritativeOrderCharge(orderId, tenantId, 75m, "USD", "pm_original"));

        result.Success.Should().BeFalse();
        result.State.Should().Be(OrderChargeState.RequiresAction);
        result.ClientActionToken.Should().Be("pi_requires_action_secret_test");
        gateway.Verify(
            item => item.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ShouldRequireManualReconciliationAfterSafeReplayWindowExpires()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var idempotencyKey = OrderPaymentService.CreateIdempotencyKey(tenantId, orderId);
        var payment = Payment.Create(
            tenantId,
            75m,
            "USD",
            idempotencyKey,
            orderId: orderId,
            paymentMethodId: "pm_original");
        payment.MarkAsProcessing();
        var staleAttemptAt = SystemClock.UtcNow.Subtract(OrderPaymentService.SafeProviderReplayWindow).AddMinutes(-1);
        payment.CreatedAt = staleAttemptAt;
        payment.UpdatedAt = staleAttemptAt;

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(
            new AuthoritativeOrderCharge(orderId, tenantId, 75m, "USD", "pm_original"));

        result.State.Should().Be(OrderChargeState.RequiresReconciliation);
        result.PaymentId.Should().Be(payment.Id);
        gateway.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessAsync_ShouldPersistProviderReferenceFromProcessingResult()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var idempotencyKey = OrderPaymentService.CreateIdempotencyKey(tenantId, orderId);
        var payment = Payment.Create(
            tenantId,
            75m,
            "USD",
            idempotencyKey,
            orderId: orderId,
            paymentMethodId: "pm_original");
        payment.MarkAsProcessing();

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(idempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        repository.Setup(repo => repo.UpdateAsync(payment, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.Setup(item => item.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                false,
                "pi_processing",
                null,
                null,
                "Provider is still processing.",
                PaymentStatus.Processing,
                SystemClock.UtcNow).WithVerifiedMapping());
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(
            new AuthoritativeOrderCharge(orderId, tenantId, 75m, "USD", "pm_original"));

        result.State.Should().Be(OrderChargeState.Processing);
        payment.ExternalTransactionId.Should().Be("pi_processing");
        repository.Verify(repo => repo.UpdateAsync(payment, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ShouldCreateNewPaymentGenerationAfterDefinitiveRetriesAreExhausted()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var idempotencyKey = OrderPaymentService.CreateIdempotencyKey(tenantId, orderId);
        var exhausted = Payment.Create(
            tenantId,
            75m,
            "USD",
            idempotencyKey,
            orderId: orderId,
            paymentMethodId: "pm_declined");
        for (var retry = 0; retry < 3; retry++)
        {
            exhausted.MarkAsProcessing();
            exhausted.MarkAsFailed("Card declined", "card_declined");
            exhausted.PrepareForRetry();
        }
        exhausted.MarkAsProcessing();
        exhausted.MarkAsFailed("Card declined", "card_declined");

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => key == idempotencyKey ? exhausted : null);
        repository.Setup(repo => repo.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        repository.Setup(repo => repo.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.Setup(item => item.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                true,
                "pi_replacement",
                "ch_replacement",
                null,
                null,
                PaymentStatus.Succeeded,
                SystemClock.UtcNow).WithVerifiedMapping());
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(
            new AuthoritativeOrderCharge(orderId, tenantId, 75m, "USD", "pm_replacement"));

        result.Success.Should().BeTrue();
        result.PaymentId.Should().NotBe(exhausted.Id);
        repository.Verify(repo => repo.AddAsync(
            It.Is<Payment>(payment =>
                payment.Id != exhausted.Id &&
                payment.IdempotencyKey.Contains(exhausted.Id.ToString("N"), StringComparison.Ordinal)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ShouldResumeExistingReplacementWithoutRecancellingExhaustedAttempt()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var idempotencyKey = OrderPaymentService.CreateIdempotencyKey(tenantId, orderId);
        var exhausted = Payment.Create(
            tenantId,
            75m,
            "USD",
            idempotencyKey,
            orderId: orderId,
            paymentMethodId: "pm_declined");
        for (var retry = 0; retry < 3; retry++)
        {
            exhausted.MarkAsProcessing();
            exhausted.MarkAsFailed("Card declined", "card_declined");
            exhausted.PrepareForRetry();
        }
        exhausted.MarkAsProcessing("pi_exhausted_cancelled");
        exhausted.MarkAsFailed("Card declined", "card_declined");

        var replacementKey = OrderPaymentService.CreateReplacementIdempotencyKey(
            tenantId,
            orderId,
            exhausted.Id);
        var replacement = Payment.Create(
            tenantId,
            75m,
            "USD",
            replacementKey,
            orderId: orderId,
            paymentMethodId: "pm_replacement");
        replacement.MarkAsProcessing("pi_replacement");
        replacement.MarkAsSucceeded("ch_replacement", "pi_replacement");

        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => key switch
            {
                var value when value == idempotencyKey => exhausted,
                var value when value == replacementKey => replacement,
                _ => null
            });
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(
            new AuthoritativeOrderCharge(orderId, tenantId, 75m, "USD", "pm_latest"));

        result.Success.Should().BeTrue();
        result.PaymentId.Should().Be(replacement.Id);
        gateway.Verify(
            item => item.CancelPaymentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void GetPaymentMethodValidationError_ShouldRejectMalformedIdentifierWithoutThrowing()
    {
        var service = new OrderPaymentService(
            Mock.Of<IPaymentRepository>(),
            Mock.Of<IPaymentGateway>(),
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        service.GetPaymentMethodValidationError("attacker-controlled")
            .Should().Be(StripePaymentMethodIdentifier.ValidationMessage);
        service.GetPaymentMethodValidationError("pm_valid").Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_ShouldRejectIdempotencyKeyBoundToDifferentSnapshot()
    {
        var orderId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var existing = Payment.Create(
            tenantId,
            75m,
            "USD",
            OrderPaymentService.CreateIdempotencyKey(tenantId, orderId),
            orderId: orderId,
            paymentMethodId: "pm_order");
        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(existing.IdempotencyKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var action = () => service.ProcessAsync(
            new AuthoritativeOrderCharge(orderId, tenantId, 1m, "USD", "pm_order"));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*authoritative order snapshot*");
        gateway.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessAsync_ShouldPersistGatewayFailureAndReturnFailedResult()
    {
        Payment? persisted = null;
        var repository = new Mock<IPaymentRepository>();
        repository.Setup(repo => repo.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        repository.Setup(repo => repo.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        repository.Setup(repo => repo.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((payment, _) => persisted = payment)
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        var gateway = new Mock<IPaymentGateway>();
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.SetupGet(item => item.ProviderId).Returns("stripe");
        gateway.Setup(item => item.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                false,
                null,
                null,
                "card_declined",
                "Card declined",
                PaymentStatus.Failed,
                SystemClock.UtcNow).WithVerifiedMapping());
        var service = new OrderPaymentService(
            repository.Object,
            gateway.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<OrderPaymentService>>());

        var result = await service.ProcessAsync(
            new AuthoritativeOrderCharge(Guid.NewGuid(), Guid.NewGuid(), 75m, "USD", "pm_order"));

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("Card declined");
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(PaymentStatus.Failed);
        persisted.ErrorCode.Should().Be("card_declined");
    }

}
internal static class OrderPaymentGatewayResultTestExtensions
{
    public static GatewayPaymentResult WithVerifiedMapping(this GatewayPaymentResult result)
    {
        if (string.IsNullOrWhiteSpace(result.TransactionId))
            return result;

        return result with
        {
            ProviderMapping = new GatewayProviderMapping(
                "test",
                "acct_platform",
                result.TransactionId,
                "payment_intent",
                "capture")
        };
    }
}
