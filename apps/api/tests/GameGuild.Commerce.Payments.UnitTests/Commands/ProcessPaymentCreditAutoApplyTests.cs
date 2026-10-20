using FluentAssertions;
using GameGuild.Commerce;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Commands;

/// <summary>
///     Account-credit auto-apply on the local invoice-payment path (issue #410 follow-up):
///     when <c>Credits:AutoApply:Enabled</c> is on, the paying user's spendable credits
///     (FIFO by expiry) are planned against the invoice amount before the payment method
///     is charged — the gateway charge is reduced by the planned credit, a fully covered
///     invoice settles without any gateway call, a failed charge never consumes credits,
///     and the feature is a no-op while disabled.
/// </summary>
public class ProcessPaymentCreditAutoApplyTests
{
    private readonly Mock<IPaymentRepository> _paymentRepository = new();
    private readonly Mock<IPaymentGateway> _paymentGateway = new();
    private readonly Mock<IPaymentSubscriptionSyncService> _syncService = new();
    private readonly Mock<ISubscriptionPaymentContextService> _contextService = new();
    private readonly Mock<IAccountCreditService> _creditService = new();
    private readonly Mock<INotificationService> _notificationService = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _subscriptionId = Guid.NewGuid();
    private readonly Guid _ownerUserId = Guid.NewGuid();

    public ProcessPaymentCreditAutoApplyTests()
    {
        _paymentGateway.SetupGet(gateway => gateway.ProviderId).Returns("stripe");
        _paymentRepository
            .Setup(repository => repository.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        _paymentRepository
            .Setup(repository => repository.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        _paymentRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment payment, CancellationToken _) => payment);
        _notificationService
            .Setup(service => service.SendAsync(
                It.IsAny<Guid?>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<NotificationChannel>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Notification.Create(
                Guid.NewGuid(),
                NotificationType.CreditApplied,
                NotificationChannel.Email,
                "Credit applied",
                "Credit was applied to your payment.",
                _tenantId)));
    }

    [Fact]
    public async Task AutoApply_ReducesTheGatewayCharge_AndRecordsTheCreditTender()
    {
        var handler = CreateHandler(enabled: true);
        SetupSubscription(amount: 25m, ownerUserId: _ownerUserId);
        SetupBalance(10m);
        SetupSuccessfulGatewayCharge();
        SetupConsumption(applied: 10m);

        var result = await handler.Handle(Command(25m), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CreditApplied.Should().Be(10m);

        // The gateway charge was reduced by the planned credit tender.
        _paymentGateway.Verify(
            gateway => gateway.ProcessPaymentAsync(
                It.Is<GatewayPaymentRequest>(request => request.Amount == 15m),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Credits were consumed FIFO through the credit service after the charge outcome.
        _creditService.Verify(
            service => service.ConsumeAsync(
                _tenantId,
                _ownerUserId,
                "USD",
                10m,
                It.IsAny<Guid>(),
                "subscription",
                It.Is<string>(key => key.StartsWith("payment:", StringComparison.Ordinal) && key.EndsWith(":credit-auto-apply")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // The payment row keeps the full invoice amount with the credit tender recorded.
        _paymentRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<Payment>(payment => payment.Amount == 25m && payment.CreditAppliedAmount == 10m),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        NotificationShouldHaveBeenSent(NotificationType.CreditApplied);
    }

    [Fact]
    public async Task AutoApply_FullCoverage_SettlesWithoutChargingThePaymentMethod()
    {
        var handler = CreateHandler(enabled: true);
        SetupSubscription(amount: 25m, ownerUserId: _ownerUserId);
        SetupBalance(40m);
        SetupConsumption(applied: 25m);

        var result = await handler.Handle(Command(25m), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CreditApplied.Should().Be(25m);
        result.Status.Should().Be(PaymentStatus.Succeeded);
        result.TransactionId.Should().StartWith("account-credit:");

        _paymentGateway.Verify(
            gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _paymentRepository.Verify(
            repository => repository.UpdateAsync(
                It.Is<Payment>(payment =>
                    payment.Status == PaymentStatus.Succeeded
                    && payment.Amount == 25m
                    && payment.CreditAppliedAmount == 25m),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        NotificationShouldHaveBeenSent(NotificationType.CreditApplied);
    }

    [Fact]
    public async Task DisabledConfig_IsANoOp_FullAmountIsCharged()
    {
        var handler = CreateHandler(enabled: false);
        SetupSubscription(amount: 25m, ownerUserId: _ownerUserId);
        SetupSuccessfulGatewayCharge();

        var result = await handler.Handle(Command(25m), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.CreditApplied.Should().BeNull();

        _paymentGateway.Verify(
            gateway => gateway.ProcessPaymentAsync(
                It.Is<GatewayPaymentRequest>(request => request.Amount == 25m),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _creditService.Verify(
            service => service.GetBalancesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _creditService.Verify(
            service => service.ConsumeAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(),
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _notificationService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FailedCharge_NeverConsumesCredits()
    {
        var handler = CreateHandler(enabled: true);
        SetupSubscription(amount: 25m, ownerUserId: _ownerUserId);
        SetupBalance(10m);
        _paymentGateway
            .Setup(gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                Success: false,
                TransactionId: null,
                ExternalPaymentId: null,
                ErrorCode: "card_declined",
                ErrorMessage: "Card declined",
                Status: PaymentStatus.Failed,
                ProcessedAt: SystemClock.UtcNow));

        var result = await handler.Handle(Command(25m), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Status.Should().Be(PaymentStatus.Failed);
        result.CreditApplied.Should().BeNull();

        // The reduced charge was attempted, but no credits moved for a failed charge.
        _paymentGateway.Verify(
            gateway => gateway.ProcessPaymentAsync(
                It.Is<GatewayPaymentRequest>(request => request.Amount == 15m),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _creditService.Verify(
            service => service.ConsumeAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(),
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _notificationService.VerifyNoOtherCalls();
    }

    private ProcessPaymentCommandHandler CreateHandler(bool enabled) => new(
        _paymentRepository.Object,
        _paymentGateway.Object,
        _syncService.Object,
        _contextService.Object,
        Options.Create(new PaymentRetryOptions()),
        new PaymentRetryPolicyResolver(Options.Create(new PaymentRetryOptions())),
        FraudTestHelpers.AllowAllDetectionService(),
        Mock.Of<ILogger<ProcessPaymentCommandHandler>>(),
        _creditService.Object,
        Options.Create(new CreditAutoApplyOptions { Enabled = enabled }),
        _notificationService.Object);

    private ProcessPaymentCommand Command(decimal amount) => new(_tenantId, _subscriptionId, amount, "pm_test");

    private void SetupSubscription(decimal amount, Guid ownerUserId) =>
        _contextService
            .Setup(service => service.GetPaymentContextAsync(_subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionPaymentContext(
                _subscriptionId,
                _tenantId,
                amount,
                "USD",
                "cus_123",
                BillingCycleNumber: 1,
                BillingPeriodStart: null,
                BillingPeriodEnd: null,
                OwnerUserId: ownerUserId));

    private void SetupBalance(decimal available) =>
        _creditService
            .Setup(service => service.GetBalancesAsync(_tenantId, _ownerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new AccountCreditBalance("USD", available, 1, null) });

    private void SetupConsumption(decimal applied) =>
        _creditService
            .Setup(service => service.ConsumeAsync(
                _tenantId,
                _ownerUserId,
                "USD",
                It.IsAny<decimal>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AccountCreditApplicationResult(
                Guid.NewGuid(),
                applied,
                "USD",
                0m,
                [])));

    private void SetupSuccessfulGatewayCharge() =>
        _paymentGateway
            .Setup(gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                Success: true,
                TransactionId: "txn_123",
                ExternalPaymentId: "pi_123",
                ErrorCode: null,
                ErrorMessage: null,
                Status: PaymentStatus.Succeeded,
                ProcessedAt: SystemClock.UtcNow,
                ProviderId: "stripe",
                ProviderMapping: new GatewayProviderMapping("test", "acct_platform", "pi_123", "payment_intent", "capture")));

    private void NotificationShouldHaveBeenSent(NotificationType type) =>
        _notificationService.Verify(
            service => service.SendAsync(
                _ownerUserId,
                type,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<NotificationChannel>(),
                _tenantId,
                It.IsAny<string?>(),
                It.IsAny<NotificationPriority>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
}
