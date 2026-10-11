using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Entities;

/// <summary>
///     Issue #415 entity behaviors: failure retriability, retry-queue pause/resume around a
///     payment-method change, and the persisted retry-attempt history entry.
/// </summary>
public class PaymentRetryQueueEntityTests
{
    private const string IdempotencyPrefix = "idem-retry-queue-";

    [Fact]
    public void MarkAsFailed_WithNonRetriableClassification_MarksNonRetriableAndLeavesQueue()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();

        payment.MarkAsFailed("card expired", "expired_card", nextRetryAt: null, isRetriable: false);

        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.IsRetriable.Should().BeFalse();
        payment.NextRetryAt.Should().BeNull();
        payment.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void MarkAsFailed_WithExplicitNextRetryAt_OverridesInternalBackoff()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();
        var explicitRetryAt = SystemClock.UtcNow.AddDays(3);

        payment.MarkAsFailed(
            "insufficient funds",
            "insufficient_funds",
            backoffBaseMinutes: 1.0,
            backoffMultiplier: 5.0,
            nextRetryAt: explicitRetryAt);

        payment.NextRetryAt.Should().Be(explicitRetryAt);
        payment.IsRetriable.Should().BeTrue();
    }

    [Fact]
    public void MarkAsFailed_WithoutPolicyParameters_KeepsLegacyBackoffBehavior()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();
        var before = SystemClock.UtcNow;

        payment.MarkAsFailed("declined");

        payment.IsRetriable.Should().BeTrue();
        payment.NextRetryAt.Should().BeOnOrAfter(before);
        payment.NextRetryAt.Should().BeBefore(before.AddMinutes(2));
    }

    [Fact]
    public void PrepareForRetry_RefusesNonRetriablePayments()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();
        payment.MarkAsFailed("card expired", "expired_card", nextRetryAt: null, isRetriable: false);

        var act = () => payment.PrepareForRetry();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*non-retriable*");
    }

    [Fact]
    public void PauseRetryQueue_ClearsTheScheduledNextRetry()
    {
        var payment = FailedPayment();

        payment.PauseRetryQueue();

        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.NextRetryAt.Should().BeNull();
        payment.CanRetry.Should().BeTrue(); // Budget remains — only the schedule is paused.
    }

    [Fact]
    public void PauseRetryQueue_IsIdempotentWhenAlreadyPaused()
    {
        var payment = FailedPayment();
        payment.PauseRetryQueue();

        var act = () => payment.PauseRetryQueue();

        act.Should().NotThrow();
    }

    [Fact]
    public void PauseRetryQueue_RejectsNonFailedPayments()
    {
        var payment = CreatePayment();

        var act = () => payment.PauseRetryQueue();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResumeRetryQueue_ReArmsEligibilityAtTheGivenTime()
    {
        var payment = FailedPayment();
        payment.PauseRetryQueue();
        var resumeAt = SystemClock.UtcNow.AddMinutes(5);

        payment.ResumeRetryQueue(resumeAt);

        payment.NextRetryAt.Should().Be(resumeAt);
    }

    [Fact]
    public void ResumeRetryQueue_SkipsExhaustedPayments()
    {
        var payment = CreatePayment(maxRetries: 1);
        payment.MarkAsProcessing();
        payment.MarkAsFailed("declined");
        payment.PauseRetryQueue();
        payment.PrepareForRetry(); // RetryCount -> 1 (== MaxRetries)
        payment.MarkAsProcessing();
        payment.MarkAsFailed("declined again");
        payment.NextRetryAt.Should().BeNull();

        payment.ResumeRetryQueue(SystemClock.UtcNow);

        payment.NextRetryAt.Should().BeNull(); // Exhausted payments stay out of the queue.
    }

    [Fact]
    public void ResumeRetryQueue_SkipsNonRetriablePayments()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();
        payment.MarkAsFailed("fraud", "fraudulent", nextRetryAt: null, isRetriable: false);

        payment.ResumeRetryQueue(SystemClock.UtcNow);

        payment.NextRetryAt.Should().BeNull();
        payment.IsRetriable.Should().BeFalse();
    }

    [Fact]
    public void ApplyPaymentMethodChange_RepointsFailedPaymentAndIsIdempotent()
    {
        var payment = FailedPayment();

        payment.ApplyPaymentMethodChange("pm_new_card");

        payment.PaymentMethodId.Should().Be("pm_new_card");
        payment.Status.Should().Be(PaymentStatus.Failed);

        var act = () => payment.ApplyPaymentMethodChange("pm_new_card");
        act.Should().NotThrow();
    }

    [Fact]
    public void ApplyPaymentMethodChange_RejectsTerminalPayments()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();
        payment.MarkAsSucceeded("pi_1", "ch_1");
        payment.ProcessRefund(payment.Amount, "re_1", "goodwill");
        payment.Status.Should().Be(PaymentStatus.Refunded);

        var act = () => payment.ApplyPaymentMethodChange("pm_new_card");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ApplyPaymentMethodChange_RejectsBlankIdentifiers()
    {
        var payment = FailedPayment();

        var act = () => payment.ApplyPaymentMethodChange(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RetryAttempt_Create_RecordsAttemptMetadata()
    {
        var paymentId = Guid.NewGuid();
        var scheduledFor = SystemClock.UtcNow.AddHours(-1);
        var executedAt = SystemClock.UtcNow;

        var attempt = PaymentRetryAttempt.Create(
            paymentId,
            attemptNumber: 2,
            scheduledForUtc: scheduledFor,
            executedAtUtc: executedAt,
            outcome: PaymentStatus.Failed,
            gateway: "stripe",
            failureCode: "insufficient_funds",
            failureMessage: "Not enough funds",
            gatewayTransactionId: "tx_2");

        attempt.PaymentId.Should().Be(paymentId);
        attempt.AttemptNumber.Should().Be(2);
        attempt.ScheduledForUtc.Should().Be(scheduledFor);
        attempt.ExecutedAtUtc.Should().Be(executedAt);
        attempt.Outcome.Should().Be(PaymentStatus.Failed);
        attempt.Gateway.Should().Be("stripe");
        attempt.FailureCode.Should().Be("insufficient_funds");
        attempt.GatewayTransactionId.Should().Be("tx_2");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RetryAttempt_Create_RejectsNonPositiveAttemptNumbers(int attemptNumber)
    {
        var act = () => PaymentRetryAttempt.Create(
            Guid.NewGuid(),
            attemptNumber,
            scheduledForUtc: null,
            executedAtUtc: SystemClock.UtcNow,
            outcome: PaymentStatus.Failed,
            gateway: "stripe");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RetryAttempt_Create_RejectsEmptyPaymentAndBlankGateway()
    {
        var actEmptyPayment = () => PaymentRetryAttempt.Create(
            Guid.Empty, 1, null, SystemClock.UtcNow, PaymentStatus.Succeeded, "stripe");
        actEmptyPayment.Should().Throw<ArgumentException>();

        var actBlankGateway = () => PaymentRetryAttempt.Create(
            Guid.NewGuid(), 1, null, SystemClock.UtcNow, PaymentStatus.Succeeded, " ");
        actBlankGateway.Should().Throw<ArgumentException>();
    }

    private static Payment CreatePayment(int maxRetries = 4) => Payment.Create(
        Guid.NewGuid(),
        25m,
        "USD",
        $"{IdempotencyPrefix}{Guid.NewGuid():N}",
        subscriptionId: Guid.NewGuid(),
        maxRetries: maxRetries);

    private static Payment FailedPayment()
    {
        var payment = CreatePayment();
        payment.MarkAsProcessing();
        payment.MarkAsFailed("insufficient funds", "insufficient_funds");
        payment.NextRetryAt.Should().NotBeNull();
        return payment;
    }
}
