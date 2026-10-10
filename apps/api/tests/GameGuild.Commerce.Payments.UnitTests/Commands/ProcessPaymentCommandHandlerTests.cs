using FluentAssertions;
using GameGuild.Commerce;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Commands;

public class ProcessPaymentCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldFailClosed_WhenSubscriptionDoesNotExist()
    {
        var fixture = CreateFixture();
        fixture.PaymentContextService
            .Setup(service => service.GetPaymentContextAsync(fixture.SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionPaymentContext?)null);

        var act = () => fixture.Handler.Handle(
            new ProcessPaymentCommand(fixture.TenantId, fixture.SubscriptionId, 25m, "pm_test"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*subscription*not found*");
        fixture.PaymentGateway.Verify(
            gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRejectTenantMismatch_BeforeCallingGateway()
    {
        var fixture = CreateFixture();
        fixture.PaymentContextService
            .Setup(service => service.GetPaymentContextAsync(fixture.SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPaymentContext(
                fixture.SubscriptionId,
                Guid.NewGuid(),
                25m,
                "USD",
                "cus_123"));

        var act = () => fixture.Handler.Handle(
            new ProcessPaymentCommand(fixture.TenantId, fixture.SubscriptionId, 25m, "pm_test"),
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*tenant*");
        fixture.PaymentGateway.Verify(
            gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRejectAmountMismatch_BeforeCallingGateway()
    {
        var fixture = CreateFixture();
        fixture.PaymentContextService
            .Setup(service => service.GetPaymentContextAsync(fixture.SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPaymentContext(
                fixture.SubscriptionId,
                fixture.TenantId,
                50m,
                "USD",
                "cus_123"));

        var act = () => fixture.Handler.Handle(
            new ProcessPaymentCommand(fixture.TenantId, fixture.SubscriptionId, 25m, "pm_test"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*amount*");
        fixture.PaymentGateway.Verify(
            gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldUseAuthoritativeSubscriptionCurrency()
    {
        var fixture = CreateFixture();
        fixture.PaymentContextService
            .Setup(service => service.GetPaymentContextAsync(fixture.SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPaymentContext(
                fixture.SubscriptionId,
                fixture.TenantId,
                25m,
                "EUR",
                "cus_123"));
        fixture.PaymentGateway
            .Setup(gateway => gateway.ProcessPaymentAsync(
                It.IsAny<GatewayPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                Success: false,
                TransactionId: null,
                ExternalPaymentId: null,
                ErrorCode: "declined",
                ErrorMessage: "Declined",
                Status: PaymentStatus.Failed,
                ProcessedAt: SystemClock.UtcNow));

        await fixture.Handler.Handle(
            new ProcessPaymentCommand(fixture.TenantId, fixture.SubscriptionId, 25m, "pm_test"),
            CancellationToken.None);

        fixture.PaymentGateway.Verify(
            gateway => gateway.ProcessPaymentAsync(
                It.Is<GatewayPaymentRequest>(request => request.Amount == 25m && request.Currency == "EUR"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldUseDeterministicSubscriptionCycleIdempotencyKey()
    {
        var fixture = CreateFixture();
        fixture.PaymentContextService
            .Setup(service => service.GetPaymentContextAsync(fixture.SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPaymentContext(
                fixture.SubscriptionId,
                fixture.TenantId,
                25m,
                "USD",
                "cus_123"));
        fixture.PaymentGateway
            .Setup(gateway => gateway.ProcessPaymentAsync(
                It.IsAny<GatewayPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                Success: false,
                TransactionId: null,
                ExternalPaymentId: null,
                ErrorCode: "declined",
                ErrorMessage: "Declined",
                Status: PaymentStatus.Failed,
                ProcessedAt: SystemClock.UtcNow));

        await fixture.Handler.Handle(
            new ProcessPaymentCommand(fixture.TenantId, fixture.SubscriptionId, 25m, "pm_test"),
            CancellationToken.None);

        var expectedKey = $"subscription:{fixture.TenantId:N}:{fixture.SubscriptionId:N}:cycle:1:charge";
        fixture.PaymentRepository.Verify(
            repository => repository.GetByIdempotencyKeyAsync(expectedKey, It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.PaymentGateway.Verify(
            gateway => gateway.ProcessPaymentAsync(
                It.Is<GatewayPaymentRequest>(request =>
                    request.IdempotencyKey == expectedKey &&
                    request.Metadata != null &&
                    request.Metadata["billing_cycle"] == "1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPropagateSubscriptionExternalCustomerIdIntoGatewayRequestAndPaymentRecord()
    {
        var tenantId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();

        var paymentRepository = new Mock<IPaymentRepository>();
        var paymentGateway = new Mock<IPaymentGateway>();
        var paymentSubscriptionSyncService = new Mock<IPaymentSubscriptionSyncService>();
        var paymentContextService = new Mock<ISubscriptionPaymentContextService>();
        var logger = Mock.Of<ILogger<ProcessPaymentCommandHandler>>();
        paymentGateway.SetupGet(gateway => gateway.ProviderId).Returns("stripe");

        Payment? addedPayment = null;

        paymentRepository.Setup(repository => repository.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        paymentRepository.Setup(repository => repository.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .Callback<Payment, CancellationToken>((payment, _) => addedPayment = payment)
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        paymentRepository.Setup(repository => repository.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);

        paymentContextService.Setup(service => service.GetPaymentContextAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPaymentContext(subscriptionId, tenantId, 25m, "USD", "cus_123"));

        paymentGateway.Setup(gateway => gateway.ProcessPaymentAsync(
                It.Is<GatewayPaymentRequest>(request =>
                    request.CustomerId == "cus_123" &&
                    request.PaymentMethodId == "pm_from_setup"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                Success: true,
                TransactionId: "pi_123",
                ExternalPaymentId: "ch_123",
                ErrorCode: null,
                ErrorMessage: null,
                Status: PaymentStatus.Succeeded,
                ProcessedAt: SystemClock.UtcNow,
                ProviderMapping: new GatewayProviderMapping(
                    "test",
                    "acct_platform",
                    "pi_123",
                    "payment_intent",
                    "capture")));

        paymentSubscriptionSyncService
            .Setup(service => service.SyncSuccessfulPaymentAsync(
                It.IsAny<Guid>(),
                subscriptionId,
                25m,
                "USD",
                1,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ProcessPaymentCommandHandler(
            paymentRepository.Object,
            paymentGateway.Object,
            paymentSubscriptionSyncService.Object,
            paymentContextService.Object,
            Options.Create(new PaymentRetryOptions()),
            logger);

        var result = await handler.Handle(
            new ProcessPaymentCommand(tenantId, subscriptionId, 25m, "pm_from_setup"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        addedPayment.Should().NotBeNull();
        addedPayment!.ExternalCustomerId.Should().Be("cus_123");
        addedPayment.ProviderEnvironment.Should().Be("test");
        addedPayment.ProviderAccountId.Should().Be("acct_platform");
        addedPayment.ProviderObjectId.Should().Be("pi_123");
        addedPayment.ProviderObjectType.Should().Be("payment_intent");
        addedPayment.ProviderMonetaryLeg.Should().Be("capture");
    }

    [Fact]
    public async Task Handle_ShouldFailClosed_WhenSuccessfulGatewayResultHasNoProviderMapping()
    {
        var fixture = CreateFixture();
        fixture.PaymentContextService
            .Setup(service => service.GetPaymentContextAsync(fixture.SubscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPaymentContext(
                fixture.SubscriptionId,
                fixture.TenantId,
                25m,
                "USD",
                "cus_123"));
        fixture.PaymentGateway
            .SetupGet(gateway => gateway.ProviderId)
            .Returns("stripe");
        fixture.PaymentGateway
            .Setup(gateway => gateway.ProcessPaymentAsync(
                It.IsAny<GatewayPaymentRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                true,
                "pi_unbound",
                "ch_unbound",
                null,
                null,
                PaymentStatus.Succeeded,
                SystemClock.UtcNow));

        var act = () => fixture.Handler.Handle(
            new ProcessPaymentCommand(fixture.TenantId, fixture.SubscriptionId, 25m, "pm_test"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*provider mapping*");
    }

    private static PaymentHandlerFixture CreateFixture()
    {
        var paymentRepository = new Mock<IPaymentRepository>();
        var paymentGateway = new Mock<IPaymentGateway>();
        var syncService = new Mock<IPaymentSubscriptionSyncService>();
        var contextService = new Mock<ISubscriptionPaymentContextService>();
        paymentGateway.SetupGet(gateway => gateway.ProviderId).Returns("stripe");

        paymentRepository
            .Setup(repository => repository.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        paymentRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        paymentRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);

        var handler = new ProcessPaymentCommandHandler(
            paymentRepository.Object,
            paymentGateway.Object,
            syncService.Object,
            contextService.Object,
            Options.Create(new PaymentRetryOptions()),
            Mock.Of<ILogger<ProcessPaymentCommandHandler>>());

        return new PaymentHandlerFixture(
            Guid.NewGuid(),
            Guid.NewGuid(),
            paymentRepository,
            paymentGateway,
            syncService,
            contextService,
            handler);
    }

    private sealed record PaymentHandlerFixture(
        Guid TenantId,
        Guid SubscriptionId,
        Mock<IPaymentRepository> PaymentRepository,
        Mock<IPaymentGateway> PaymentGateway,
        Mock<IPaymentSubscriptionSyncService> SyncService,
        Mock<ISubscriptionPaymentContextService> PaymentContextService,
        ProcessPaymentCommandHandler Handler);
}
