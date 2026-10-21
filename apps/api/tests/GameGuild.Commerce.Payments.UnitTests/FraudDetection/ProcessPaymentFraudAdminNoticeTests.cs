using FluentAssertions;
using GameGuild.Commerce;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.FraudDetection;

/// <summary>
///     Issue #409: admin notification for fraud-gate holds. Blocked payments raise an
///     Urgent system-channel notice and review holds a High one, both addressed to the
///     admin channel (recipient <see cref="Guid.Empty" />) with the payment as reference;
///     Allow decisions never notify, and delivery problems never change the payment outcome.
/// </summary>
public class ProcessPaymentFraudAdminNoticeTests
{
    [Fact]
    public async Task Handle_ShouldNotifyAdminsOnBlockedPayment()
    {
        var fixture = CreateFixture(FraudTestHelpers.DetectionServiceReturning(FraudDecision.Block, 85, "card testing burst"));

        var result = await fixture.Handler.Handle(Command(fixture), CancellationToken.None);

        result.Success.Should().BeFalse();
        fixture.NotificationService.Verify(
            service => service.SendAsync(
                Guid.Empty,
                NotificationType.System,
                It.Is<string>(title => title.Contains("blocked", StringComparison.OrdinalIgnoreCase)),
                It.Is<string>(message => message.Contains(fixture.CapturedPayment()!.Id.ToString())),
                NotificationChannel.InApp,
                fixture.TenantId,
                It.IsAny<string?>(),
                NotificationPriority.Urgent,
                fixture.CapturedPayment()!.Id,
                nameof(Payment),
                It.Is<string>(metadata => metadata.Contains("Block")),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotifyAdminsOnReviewHold()
    {
        var fixture = CreateFixture(FraudTestHelpers.DetectionServiceReturning(FraudDecision.Review, 55, "velocity spike"));

        var result = await fixture.Handler.Handle(Command(fixture), CancellationToken.None);

        result.Success.Should().BeFalse();
        fixture.NotificationService.Verify(
            service => service.SendAsync(
                Guid.Empty,
                NotificationType.System,
                It.Is<string>(title => title.Contains("review", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<string>(),
                NotificationChannel.InApp,
                fixture.TenantId,
                It.IsAny<string?>(),
                NotificationPriority.High,
                fixture.CapturedPayment()!.Id,
                nameof(Payment),
                It.Is<string>(metadata => metadata.Contains("Review")),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNeverNotifyOnAllowedPayment()
    {
        var fixture = CreateFixture(FraudTestHelpers.AllowAllDetectionService());

        await fixture.Handler.Handle(Command(fixture), CancellationToken.None);

        fixture.NotificationService.Verify(
            service => service.SendAsync(
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
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldKeepTheFraudHold_WhenNoticeDeliveryFails()
    {
        var fixture = CreateFixture(
            FraudTestHelpers.DetectionServiceReturning(FraudDecision.Block, 85, "blocked by test"),
            deliveryResult: () => Result.Failure<Notification>(Error.Failure("Notification.Failed", "channel unavailable")));

        var result = await fixture.Handler.Handle(Command(fixture), CancellationToken.None);

        result.Success.Should().BeFalse("a failed admin notice must not flip the fraud decision");
        result.Status.Should().Be(PaymentStatus.Failed);
        fixture.CapturedPayment()!.FraudDecision.Should().Be(FraudDecision.Block);
    }

    [Fact]
    public async Task Handle_ShouldSwallowNotificationPipelineExceptions()
    {
        var fixture = CreateFixture(
            FraudTestHelpers.DetectionServiceReturning(FraudDecision.Block, 85, "blocked by test"),
            deliveryResult: () => throw new InvalidOperationException("notification pipeline down"));

        var result = await fixture.Handler.Handle(Command(fixture), CancellationToken.None);

        result.Success.Should().BeFalse("a broken notification pipeline must not change the fraud outcome");
        result.Status.Should().Be(PaymentStatus.Failed);
    }

    private static ProcessPaymentCommand Command(FraudAdminNoticeFixture fixture) =>
        new(fixture.TenantId, fixture.SubscriptionId, 25m, "pm_test");

    private static FraudAdminNoticeFixture CreateFixture(
        IFraudDetectionService fraudDetectionService,
        Func<Result<Notification>>? deliveryResult = null)
    {
        var paymentRepository = new Mock<IPaymentRepository>();
        var paymentGateway = new Mock<IPaymentGateway>();
        var syncService = new Mock<IPaymentSubscriptionSyncService>();
        var contextService = new Mock<ISubscriptionPaymentContextService>();
        var notificationService = new Mock<INotificationService>();

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

        notificationService
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
            .ReturnsAsync(deliveryResult ?? (Func<Result<Notification>>)(() => Result.Success(Notification.Create(
                Guid.Empty,
                NotificationType.System,
                NotificationChannel.InApp,
                "Fraud hold",
                "Payment held by the fraud gate.",
                tenantId))));

        var handler = new ProcessPaymentCommandHandler(
            paymentRepository.Object,
            paymentGateway.Object,
            syncService.Object,
            contextService.Object,
            Options.Create(new PaymentRetryOptions()),
            new PaymentRetryPolicyResolver(Options.Create(new PaymentRetryOptions())),
            fraudDetectionService,
            Mock.Of<ILogger<ProcessPaymentCommandHandler>>(),
            notificationService: notificationService.Object);

        return new FraudAdminNoticeFixture(
            tenantId,
            subscriptionId,
            notificationService,
            () => capturedPayment,
            handler);
    }

    private sealed record FraudAdminNoticeFixture(
        Guid TenantId,
        Guid SubscriptionId,
        Mock<INotificationService> NotificationService,
        Func<Payment?> CapturedPaymentAccessor,
        ProcessPaymentCommandHandler Handler)
    {
        public Payment? CapturedPayment() => CapturedPaymentAccessor();
    }
}
