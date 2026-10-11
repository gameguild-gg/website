using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Commands;

/// <summary>
///     Recovery lifecycle integration of the retry path (issue #415): retry-attempt history
///     persistence, dunning suspension when retries are exhausted, reason-guarded
///     reactivation after a successful recovery payment, failure-code-driven scheduling and
///     system-actor attribution for queue dispatches.
/// </summary>
public class RetryPaymentRecoveryLifecycleTests
{
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly Mock<IRevenueAuditService> _revenueAuditService = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();
    private readonly Mock<IPaymentDunningLifecycleService> _dunning = new();
    private readonly Mock<IPaymentRepository> _repository = new();

    public RetryPaymentRecoveryLifecycleTests()
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
    }

    [Fact]
    public async Task RetryFailure_AtBudgetExhaustion_SuspendsTheSubscriptionForDunning()
    {
        var payment = FailedPayment(maxRetries: 1);
        SetupGatewayFailure("card_declined");
        var handler = CreateHandler(payment, 1);

        var result = await handler.Handle(new RetryPaymentCommand(payment.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.MaxRetriesReached.Should().BeTrue();
        _dunning.Verify(
            service => service.SuspendForRetryExhaustionAsync(
                payment.SubscriptionId!.Value,
                payment.TenantId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetryFailure_WithHardDeclineClass_MarksNonRetriableAndSuspends()
    {
        var payment = FailedPayment(maxRetries: 4); // Budget remains, but the decline is terminal.
        SetupGatewayFailure("expired_card");
        var handler = CreateHandler(payment, 4);

        var result = await handler.Handle(new RetryPaymentCommand(payment.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        payment.IsRetriable.Should().BeFalse();
        payment.NextRetryAt.Should().BeNull();
        _dunning.Verify(
            service => service.SuspendForRetryExhaustionAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetryFailure_WithRetriableBudgetLeft_DoesNotSuspend()
    {
        var payment = FailedPayment(maxRetries: 4);
        SetupGatewayFailure("insufficient_funds");
        var handler = CreateHandler(payment, 4);
        var before = SystemClock.UtcNow;

        var result = await handler.Handle(new RetryPaymentCommand(payment.Id), CancellationToken.None);

        result.Success.Should().BeFalse();
        payment.IsRetriable.Should().BeTrue();
        // The failing attempt is retry #1 (RetryCount == 1 after PrepareForRetry), so the
        // insufficient_funds ladder rung is 7 days after this failure (rung 1 = 3 days after
        // the initial failure; no jitter on the test resolver).
        payment.NextRetryAt.Should().BeOnOrAfter(before.AddDays(7).AddSeconds(-1));
        payment.NextRetryAt.Should().BeOnOrBefore(before.AddDays(7).AddSeconds(1));
        _dunning.Verify(
            service => service.SuspendForRetryExhaustionAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RetrySuccess_OnDunningSuspendedSubscription_TriggersGuardedReactivation()
    {
        var payment = FailedPayment(maxRetries: 4);
        SetupGatewaySuccess();
        _dunning
            .Setup(service => service.TryReactivateAfterRecoveryAsync(payment.SubscriptionId!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = CreateHandler(payment, 4);

        var result = await handler.Handle(new RetryPaymentCommand(payment.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
        _dunning.Verify(
            service => service.TryReactivateAfterRecoveryAsync(payment.SubscriptionId!.Value, It.IsAny<CancellationToken>()),
            Times.Once);
        _dunning.Verify(
            service => service.SuspendForRetryExhaustionAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EveryAttempt_PersistsARetryAttemptHistoryRow()
    {
        var payment = FailedPayment(maxRetries: 4);
        SetupGatewayFailure("insufficient_funds");
        var handler = CreateHandler(payment, 4);

        await handler.Handle(new RetryPaymentCommand(payment.Id), CancellationToken.None);

        _repository.Verify(
            repository => repository.AddRetryAttemptAsync(
                It.Is<PaymentRetryAttempt>(attempt =>
                    attempt.PaymentId == payment.Id
                    && attempt.AttemptNumber == 1
                    && attempt.Outcome == PaymentStatus.Failed
                    && attempt.FailureCode == "insufficient_funds"
                    && attempt.Gateway == "stripe"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SuccessfulAttempt_PersistsASucceededHistoryRowWithTransactionReference()
    {
        var payment = FailedPayment(maxRetries: 4);
        SetupGatewaySuccess();
        var handler = CreateHandler(payment, 4);

        await handler.Handle(new RetryPaymentCommand(payment.Id), CancellationToken.None);

        _repository.Verify(
            repository => repository.AddRetryAttemptAsync(
                It.Is<PaymentRetryAttempt>(attempt =>
                    attempt.Outcome == PaymentStatus.Succeeded
                    && attempt.FailureCode == null
                    && attempt.GatewayTransactionId == "tx_retry_1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AttemptHistoryFailure_DoesNotFailTheRetry()
    {
        var payment = FailedPayment(maxRetries: 4);
        SetupGatewaySuccess();
        _repository
            .Setup(repository => repository.AddRetryAttemptAsync(It.IsAny<PaymentRetryAttempt>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("history store down"));
        var handler = CreateHandler(payment, 4);

        var result = await handler.Handle(new RetryPaymentCommand(payment.Id), CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task DunningLifecycleFailure_DoesNotFailTheRetry()
    {
        var payment = FailedPayment(maxRetries: 1);
        SetupGatewayFailure("card_declined");
        _dunning
            .Setup(service => service.SuspendForRetryExhaustionAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("subscription store down"));
        var handler = CreateHandler(payment, 1);

        var result = await handler.Handle(new RetryPaymentCommand(payment.Id), CancellationToken.None);

        result.Success.Should().BeFalse(); // The retry outcome itself is preserved.
        result.MaxRetriesReached.Should().BeTrue();
    }

    [Fact]
    public async Task PaymentWithoutSubscription_SkipsTheRecoveryLifecycle()
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            25m,
            "USD",
            $"idem-recovery-{Guid.NewGuid():N}",
            subscriptionId: null, // One-time payment: no recovery lifecycle applies.
            maxRetries: 1);
        payment.MarkAsProcessing();
        payment.MarkAsFailed("insufficient funds", "insufficient_funds");
        SetupGatewayFailure("card_declined");
        var handler = CreateHandler(payment, 1);

        await handler.Handle(new RetryPaymentCommand(payment.Id), CancellationToken.None);

        _dunning.Verify(
            service => service.SuspendForRetryExhaustionAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _dunning.Verify(
            service => service.TryReactivateAfterRecoveryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task QueueDispatchUnderSystemActor_AttributesAuditToTheRetryQueueSystemActor()
    {
        var payment = FailedPayment(maxRetries: 4);
        SetupGatewaySuccess();
        var systemActor = ActorContextBuilder.ForSystem("PaymentRetryQueue").Build();
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(systemActor);
        var handler = CreateHandler(payment, 4);

        await handler.Handle(new RetryPaymentCommand(payment.Id), CancellationToken.None);

        _revenueAuditService.Verify(
            service => service.RecordAuditTrailAsync(
                "Payment",
                payment.Id,
                It.IsAny<string>(),
                PaymentRetryAuditActors.RetryQueueSystemActorId,
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private RetryPaymentCommandHandler CreateHandler(Payment payment, int maxRetries)
    {
        var options = new PaymentRetryOptions { MaxRetries = maxRetries, JitterRatio = 0.0 };
        // The repository returns the shared payment instance; the handler mutates it in place.
        _repository
            .Setup(repository => repository.GetByIdAsync(payment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);
        _repository
            .Setup(repository => repository.UpdateAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment updated, CancellationToken _) => updated);
        _repository
            .Setup(repository => repository.AddRetryAttemptAsync(It.IsAny<PaymentRetryAttempt>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new RetryPaymentCommandHandler(
            _repository.Object,
            _gateway.Object,
            Mock.Of<IPaymentSubscriptionSyncService>(),
            Options.Create(options),
            new PaymentRetryPolicyResolver(Options.Create(options)),
            _dunning.Object,
            _revenueAuditService.Object,
            _actorAccessor.Object,
            NullLogger<RetryPaymentCommandHandler>.Instance);
    }

    private void SetupGatewaySuccess()
    {
        _gateway
            .Setup(gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                Success: true,
                TransactionId: "tx_retry_1",
                ExternalPaymentId: "ch_retry_1",
                ErrorCode: null,
                ErrorMessage: null,
                Status: PaymentStatus.Succeeded,
                ProcessedAt: SystemClock.UtcNow,
                ProviderMapping: new GatewayProviderMapping(
                    "test",
                    "acct_platform",
                    "pi_retry_1",
                    "payment_intent",
                    "capture")));
    }

    private void SetupGatewayFailure(string errorCode)
    {
        _gateway
            .Setup(gateway => gateway.ProcessPaymentAsync(It.IsAny<GatewayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentResult(
                Success: false,
                TransactionId: null,
                ExternalPaymentId: null,
                ErrorCode: errorCode,
                ErrorMessage: $"Gateway refused the charge ({errorCode})",
                Status: PaymentStatus.Failed,
                ProcessedAt: SystemClock.UtcNow));
    }

    private static Payment FailedPayment(int maxRetries = 4, Guid? subscriptionId = null)
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            25m,
            "USD",
            $"idem-recovery-{Guid.NewGuid():N}",
            subscriptionId: subscriptionId ?? Guid.NewGuid(),
            maxRetries: maxRetries);
        payment.MarkAsProcessing();
        payment.MarkAsFailed("insufficient funds", "insufficient_funds");
        return payment;
    }

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
