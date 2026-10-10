using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Entities;

public class PaymentRetryPolicyEntityTests
{
    private static Payment CreateFailedPayment(int? maxRetries = null)
    {
        var payment = Payment.Create(
            Guid.NewGuid(),
            25m,
            "USD",
            $"idem-{Guid.NewGuid():N}",
            maxRetries: maxRetries);
        payment.MarkAsProcessing();
        return payment;
    }

    [Fact]
    public void Create_WithoutMaxRetries_KeepsLegacyDefault()
    {
        var payment = Payment.Create(Guid.NewGuid(), 25m, "USD", "idem-1");

        payment.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void Create_WithMaxRetries_PersistsConfiguredRetryBudget()
    {
        var payment = Payment.Create(Guid.NewGuid(), 25m, "USD", "idem-2", maxRetries: 5);

        payment.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void MarkAsFailed_WithoutRetryParameters_AppliesLegacyBackoff()
    {
        var payment = CreateFailedPayment();
        var before = SystemClock.UtcNow;

        payment.MarkAsFailed("card declined");

        payment.RetryCount.Should().Be(0);
        payment.MaxRetries.Should().Be(3);
        payment.NextRetryAt.Should().NotBeNull();
        payment.NextRetryAt.Should().BeOnOrAfter(before.AddMinutes(0.99));
        payment.NextRetryAt.Should().BeBefore(before.AddMinutes(1.01));
    }

    [Fact]
    public void MarkAsFailed_WithCustomParameters_AppliesConfiguredSchedule()
    {
        var payment = CreateFailedPayment(maxRetries: 2);
        var before = SystemClock.UtcNow;

        payment.MarkAsFailed(
            "card declined",
            maxRetries: 2,
            backoffBaseMinutes: 10.0,
            backoffMultiplier: 3.0);

        payment.MaxRetries.Should().Be(2);
        payment.NextRetryAt.Should().NotBeNull();
        payment.NextRetryAt.Should().BeOnOrAfter(before.AddMinutes(9.99));
        payment.NextRetryAt.Should().BeBefore(before.AddMinutes(10.01));
    }

    [Fact]
    public void MarkAsFailed_AfterConsecutiveFailures_EscalatesBackoffPerRetryCount()
    {
        var payment = CreateFailedPayment(maxRetries: 3);
        payment.MarkAsFailed("first failure", maxRetries: 3, backoffBaseMinutes: 1.0, backoffMultiplier: 5.0);
        var firstRetryDelay = payment.NextRetryAt!.Value;

        payment.PrepareForRetry();
        payment.MarkAsProcessing();
        var secondFailureAt = SystemClock.UtcNow;
        payment.MarkAsFailed("second failure", maxRetries: 3, backoffBaseMinutes: 1.0, backoffMultiplier: 5.0);

        payment.RetryCount.Should().Be(1);
        payment.NextRetryAt.Should().BeOnOrAfter(secondFailureAt.AddMinutes(4.99));
        payment.NextRetryAt.Should().BeOnOrAfter(firstRetryDelay);
    }

    [Fact]
    public void MarkAsFailed_WhenRetryBudgetExhausted_DoesNotScheduleNextRetry()
    {
        var payment = CreateFailedPayment(maxRetries: 1);
        payment.MarkAsFailed("first failure", maxRetries: 1);
        payment.PrepareForRetry();
        payment.MarkAsProcessing();

        payment.MarkAsFailed("final failure", maxRetries: 1);

        payment.MaxRetriesReached.Should().BeTrue();
        payment.NextRetryAt.Should().BeNull();
        payment.CanRetry.Should().BeFalse();
    }

    [Fact]
    public void MarkAsFailed_UpdatesPersistedMaxRetries_WhenConfigurationChangesIt()
    {
        var payment = CreateFailedPayment(); // legacy budget 3 persisted at creation

        payment.MarkAsFailed("card declined", maxRetries: 7);

        payment.MaxRetries.Should().Be(7);
    }
}
