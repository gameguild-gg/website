using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Commands;

public class RefundAndRetryAuditTrailTests
{
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly Mock<IRevenueAuditService> _revenueAuditService = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();

    public RefundAndRetryAuditTrailTests()
    {
        _gateway.SetupGet(gateway => gateway.ProviderId).Returns("stripe");
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(AuthenticatedActor());
        _revenueAuditService
            .Setup(service => service.RecordAuditTrailAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _revenueAuditService
            .Setup(service => service.RecordRevenueEventAsync(
                It.IsAny<RevenueEventType>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<RevenueSource>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewRevenueEvent());
        _revenueAuditService
            .Setup(service => service.CreateLedgerEntryAsync(
                It.IsAny<LedgerEntryType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinancialLedgerEntry());
    }

    [Fact]
    public async Task ProcessRefund_OnSuccess_RecordsRefundProcessedRevenueEvent()
    {
        var payment = SucceededPayment();
        var handler = CreateRefundHandler(payment);
        SetupGatewayRefundSuccess();

        await handler.Handle(new ProcessRefundCommand(payment.Id, 10m, "duplicate charge"), CancellationToken.None);

        _revenueAuditService.Verify(
            service => service.RecordRevenueEventAsync(
                RevenueEventType.RefundProcessed,
                10m,
                "USD",
                It.IsAny<RevenueSource>(),
                payment.Id.ToString(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessRefund_OnSuccess_PostsRefundLedgerEntryLinkedToRevenueEvent()
    {
        var payment = SucceededPayment();
        var revenueEvent = NewRevenueEvent();
        _revenueAuditService
            .Setup(service => service.RecordRevenueEventAsync(
                It.IsAny<RevenueEventType>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<RevenueSource>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(revenueEvent);
        var handler = CreateRefundHandler(payment);
        SetupGatewayRefundSuccess();

        await handler.Handle(new ProcessRefundCommand(payment.Id, payment.Amount, "full refund"), CancellationToken.None);

        _revenueAuditService.Verify(
            service => service.CreateLedgerEntryAsync(
                LedgerEntryType.Refund,
                It.Is<string>(account => account == LedgerAccount.RefundsAndChargebacks.ToAccountCode()),
                It.Is<string>(account => account == LedgerAccount.Cash.ToAccountCode()),
                payment.Amount,
                "USD",
                It.Is<string>(description => description.Contains(payment.Id.ToString(), StringComparison.Ordinal)),
                revenueEvent.Id,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessRefund_OnSuccess_WritesPaymentAuditTrailWithActingUser()
    {
        var payment = SucceededPayment();
        var actor = AuthenticatedActor();
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(actor);
        var handler = CreateRefundHandler(payment);
        SetupGatewayRefundSuccess();

        await handler.Handle(new ProcessRefundCommand(payment.Id, 5m, "goodwill"), CancellationToken.None);

        _revenueAuditService.Verify(
            service => service.RecordAuditTrailAsync(
                "Payment",
                payment.Id,
                It.IsAny<string>(),
                actor.SubjectIdAsGuid!.Value,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.Is<string>(reason => reason.Contains("refund", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessRefund_WhenGatewayFails_DoesNotRecordRevenueEventOrAuditTrail()
    {
        var payment = SucceededPayment();
        _gateway
            .Setup(gateway => gateway.ProcessRefundAsync(It.IsAny<GatewayRefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayRefundResult(
                Success: false,
                RefundId: null,
                AmountRefunded: 0m,
                ErrorCode: "stripe_error",
                ErrorMessage: "stripe error",
                ProcessedAt: SystemClock.UtcNow));
        var handler = CreateRefundHandler(payment);

        var result = await handler.Handle(new ProcessRefundCommand(payment.Id, 5m, "requested"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _revenueAuditService.Verify(
            service => service.RecordRevenueEventAsync(
                It.IsAny<RevenueEventType>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<RevenueSource>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _revenueAuditService.Verify(
            service => service.RecordAuditTrailAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessRefund_WithoutAuthenticatedActor_FailsClosed()
    {
        var payment = SucceededPayment();
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(ActorContext.Anonymous);
        SetupGatewayRefundSuccess();
        var handler = CreateRefundHandler(payment);

        var act = () => handler.Handle(new ProcessRefundCommand(payment.Id, 5m, "requested"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RetryPayment_OnAttemptOutcome_WritesPaymentAuditTrail()
    {
        var payment = FailedPayment();
        SetupGatewayPaymentSuccess();
        var handler = CreateRetryHandler(payment);

        await handler.Handle(new RetryPaymentCommand(payment.Id), CancellationToken.None);

        _revenueAuditService.Verify(
            service => service.RecordAuditTrailAsync(
                "Payment",
                payment.Id,
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.Is<string>(reason => reason.Contains("retry", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetryPayment_HonorsConfiguredMaxRetries()
    {
        var payment = FailedPayment(); // RetryCount 0
        SetupGatewayPaymentFailure();
        var handler = CreateRetryHandler(payment, maxRetries: 1);

        var first = await handler.Handle(new RetryPaymentCommand(payment.Id), CancellationToken.None);

        first.Success.Should().BeFalse();
        first.RetryAttempt.Should().Be(1);
        first.MaxRetriesReached.Should().BeTrue();

        var second = await handler.Handle(new RetryPaymentCommand(payment.Id), CancellationToken.None);

        second.Success.Should().BeFalse();
        second.MaxRetriesReached.Should().BeTrue();
        second.FailureReason.Should().Contain("Maximum retry attempts");
    }

    [Fact]
    public async Task RetryPayment_AppliesConfiguredBackoffOnFailure()
    {
        var payment = FailedPayment();
        SetupGatewayPaymentFailure();
        var handler = CreateRetryHandler(payment, maxRetries: 3, backoffBaseMinutes: 30.0, backoffMultiplier: 2.0);
        var before = SystemClock.UtcNow;

        var result = await handler.Handle(new RetryPaymentCommand(payment.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.RetryAttempt.Should().Be(1);
        result.NextRetryAt.Should().NotBeNull();
        // Second failure: 30 minutes * 2^1 = 60 minutes.
        result.NextRetryAt.Should().BeOnOrAfter(before.AddMinutes(59.99));
        result.NextRetryAt.Should().BeBefore(before.AddMinutes(60.01));
    }

    private ProcessRefundCommandHandler CreateRefundHandler(Payment payment)
    {
        var repository = new Mock<IPaymentRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        repository
            .Setup(repo => repo.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment updated, CancellationToken _) => updated);

        return new ProcessRefundCommandHandler(
            repository.Object,
            _gateway.Object,
            _revenueAuditService.Object,
            _actorAccessor.Object,
            NullLogger<ProcessRefundCommandHandler>.Instance);
    }

    private RetryPaymentCommandHandler CreateRetryHandler(
        Payment payment,
        int maxRetries = 3,
        double backoffBaseMinutes = 1.0,
        double backoffMultiplier = 5.0)
    {
        var repository = new Mock<IPaymentRepository>();
        repository
            .Setup(repo => repo.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        repository
            .Setup(repo => repo.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment updated, CancellationToken _) => updated);

        return new RetryPaymentCommandHandler(
            repository.Object,
            _gateway.Object,
            Mock.Of<IPaymentSubscriptionSyncService>(),
            Options.Create(new PaymentRetryOptions
            {
                MaxRetries = maxRetries,
                BackoffBaseMinutes = backoffBaseMinutes,
                BackoffMultiplier = backoffMultiplier
            }),
            _revenueAuditService.Object,
            _actorAccessor.Object,
            NullLogger<RetryPaymentCommandHandler>.Instance);
    }

    private void SetupGatewayRefundSuccess()
    {
        _gateway
            .Setup(gateway => gateway.ProcessRefundAsync(It.IsAny<GatewayRefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayRefundResult(
                Success: true,
                RefundId: "re_123",
                AmountRefunded: 10m,
                ErrorCode: null,
                ErrorMessage: null,
                ProcessedAt: SystemClock.UtcNow));
    }

    private void SetupGatewayPaymentSuccess()
    {
        _gateway
            .Setup(gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                Success: true,
                TransactionId: "pi_retry",
                ExternalPaymentId: "ch_retry",
                ErrorCode: null,
                ErrorMessage: null,
                Status: PaymentStatus.Succeeded,
                ProcessedAt: SystemClock.UtcNow,
                ProviderMapping: new GatewayProviderMapping(
                    "test",
                    "acct_platform",
                    "pi_retry",
                    "payment_intent",
                    "capture")));
    }

    private void SetupGatewayPaymentFailure()
    {
        _gateway
            .Setup(gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                Success: false,
                TransactionId: null,
                ExternalPaymentId: null,
                ErrorCode: "card_declined",
                ErrorMessage: "Your card was declined.",
                Status: PaymentStatus.Failed,
                ProcessedAt: SystemClock.UtcNow));
    }

    private static Payment SucceededPayment()
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            25m,
            "USD",
            $"idem-{Guid.NewGuid():N}",
            subscriptionId: Guid.NewGuid());
        payment.MarkAsProcessing();
        payment.MarkAsSucceeded("pi_123", "ch_123");
        return payment;
    }

    private static Payment FailedPayment()
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            25m,
            "USD",
            $"idem-{Guid.NewGuid():N}",
            subscriptionId: Guid.NewGuid());
        payment.MarkAsProcessing();
        payment.MarkAsFailed("insufficient funds");
        return payment;
    }

    private static RevenueEvent NewRevenueEvent() => new()
    {
        EventType = RevenueEventType.RefundProcessed,
        Amount = 10m,
        Currency = "USD",
        Source = RevenueSource.Subscription,
        ReferenceId = Guid.NewGuid().ToString(),
        Timestamp = SystemClock.UtcNow
    };

    private static ActorContext AuthenticatedActor() => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = Guid.NewGuid().ToString(),
        TenantId = Guid.NewGuid(),
        Roles = new HashSet<string>(),
        Permissions = new HashSet<string>(),
        TypedAttributes = ActorAttributes.Empty,
        AuthScheme = "Test",
        IsAuthenticated = true
    };
}
