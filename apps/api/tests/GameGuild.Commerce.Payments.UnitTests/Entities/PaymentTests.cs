using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Entities;

/// <summary>
///     E.1 Unit Tests for Payment entity state machine and validation.
///     These tests verify critical invariants:
///     - Payment state transitions follow defined rules
///     - Invalid transitions are rejected
///     - Idempotency is maintained
/// </summary>
public class PaymentTests
{
    #region Payment Creation Tests

    [Fact]
    public void Create_WithValidData_ShouldCreatePayment()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var amount = 99.99m;
        var currency = "USD";
        var idempotencyKey = "payment_123";

        // Act
        var payment = Payment.Create(tenantId, amount, currency, idempotencyKey);

        // Assert
        payment.TenantId.Should().Be(tenantId);
        payment.Amount.Should().Be(amount);
        payment.Currency.Should().Be("USD");
        payment.IdempotencyKey.Should().Be(idempotencyKey);
        payment.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ShouldThrow()
    {
        // Arrange & Act
        var act = () => Payment.Create(Guid.Empty, 100m, "USD", "key");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*TenantId*required*");
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldThrow()
    {
        // Arrange & Act
        var act = () => Payment.Create(Guid.NewGuid(), 0m, "USD", "key");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Amount must be positive*");
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrow()
    {
        // Arrange & Act
        var act = () => Payment.Create(Guid.NewGuid(), -50m, "USD", "key");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Amount must be positive*");
    }

    [Fact]
    public void Create_WithEmptyIdempotencyKey_ShouldThrow()
    {
        // Arrange & Act
        var act = () => Payment.Create(Guid.NewGuid(), 100m, "USD", "");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Idempotency key*required*");
    }

    #endregion

    #region State Machine Valid Transitions Tests

    [Fact]
    public void Pending_CanTransitionTo_Processing()
    {
        // Arrange
        var payment = CreatePendingPayment();

        // Assert
        payment.CanTransitionTo(PaymentStatus.Processing).Should().BeTrue();
    }

    [Fact]
    public void Pending_CanTransitionTo_Cancelled()
    {
        // Arrange
        var payment = CreatePendingPayment();

        // Assert
        payment.CanTransitionTo(PaymentStatus.Cancelled).Should().BeTrue();
    }

    [Fact]
    public void Processing_CanTransitionTo_Succeeded()
    {
        // Arrange
        var payment = CreatePendingPayment();
        payment.MarkAsProcessing();

        // Assert
        payment.CanTransitionTo(PaymentStatus.Succeeded).Should().BeTrue();
    }

    [Fact]
    public void Processing_CanTransitionTo_Failed()
    {
        // Arrange
        var payment = CreatePendingPayment();
        payment.MarkAsProcessing();

        // Assert
        payment.CanTransitionTo(PaymentStatus.Failed).Should().BeTrue();
    }

    [Fact]
    public void Succeeded_CanTransitionTo_Refunded()
    {
        // Arrange
        var payment = CreateSucceededPayment();

        // Assert
        payment.CanTransitionTo(PaymentStatus.Refunded).Should().BeTrue();
    }

    [Fact]
    public void Succeeded_CanTransitionTo_Disputed()
    {
        // Arrange
        var payment = CreateSucceededPayment();

        // Assert
        payment.CanTransitionTo(PaymentStatus.Disputed).Should().BeTrue();
    }

    #endregion

    #region E.1 Critical Invariant Tests - Invalid Transitions

    /// <summary>
    /// E.1 Test: Payment.TransitionTo_InvalidTransition_Throws
    /// Verifies that attempting an invalid state transition throws InvalidOperationException
    /// </summary>
    [Fact]
    public void TransitionTo_InvalidTransition_Throws()
    {
        // Arrange - Cancelled is terminal, cannot transition to Processing
        var payment = CreatePendingPayment();
        payment.Cancel("Test cancellation");
        payment.Status.Should().Be(PaymentStatus.Cancelled);

        // Act & Assert
        var act = () => payment.MarkAsProcessing();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot transition payment*Cancelled*Processing*");
    }

    [Fact]
    public void Pending_CannotTransitionTo_Succeeded()
    {
        // Arrange - Cannot skip Processing state
        var payment = CreatePendingPayment();

        // Assert
        payment.CanTransitionTo(PaymentStatus.Succeeded).Should().BeFalse();
    }

    [Fact]
    public void Pending_CannotTransitionTo_Refunded()
    {
        // Arrange - Cannot refund a payment that hasn't succeeded
        var payment = CreatePendingPayment();

        // Assert
        payment.CanTransitionTo(PaymentStatus.Refunded).Should().BeFalse();
    }

    [Fact]
    public void Cancelled_IsTerminalState()
    {
        // Arrange
        var payment = CreatePendingPayment();
        payment.Cancel("Cancelled by user");

        // Assert - Cannot transition to any state
        payment.CanTransitionTo(PaymentStatus.Pending).Should().BeFalse();
        payment.CanTransitionTo(PaymentStatus.Processing).Should().BeFalse();
        payment.CanTransitionTo(PaymentStatus.Succeeded).Should().BeFalse();
        payment.CanTransitionTo(PaymentStatus.Failed).Should().BeFalse();
        payment.CanTransitionTo(PaymentStatus.Refunded).Should().BeFalse();
    }

    [Fact]
    public void Refunded_IsTerminalState()
    {
        // Arrange
        var payment = CreateSucceededPayment();
        payment.ProcessRefund(payment.Amount, "refund_123", "Customer refund");

        // Assert - Cannot transition from Refunded
        payment.CanTransitionTo(PaymentStatus.Pending).Should().BeFalse();
        payment.CanTransitionTo(PaymentStatus.Processing).Should().BeFalse();
        payment.CanTransitionTo(PaymentStatus.Succeeded).Should().BeFalse();
        payment.CanTransitionTo(PaymentStatus.Failed).Should().BeFalse();
        payment.CanTransitionTo(PaymentStatus.Cancelled).Should().BeFalse();
    }

    [Fact]
    public void Failed_CanRetry_TransitionsBackToPending()
    {
        // Arrange
        var payment = CreatePendingPayment();
        payment.MarkAsProcessing();
        payment.MarkAsFailed("Card declined", "card_declined");

        // Act
        payment.PrepareForRetry();

        // Assert
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.RetryCount.Should().Be(1);
    }

    [Fact]
    public void PrepareForRetry_WhenMaxRetriesReached_ShouldThrow()
    {
        // Arrange
        var payment = CreatePendingPayment();
        
        // Exhaust all retries
        for (int i = 0; i < payment.MaxRetries; i++)
        {
            payment.MarkAsProcessing();
            payment.MarkAsFailed("Card declined");
            payment.PrepareForRetry();
        }

        // One more failure to get back to Failed state
        payment.MarkAsProcessing();
        payment.MarkAsFailed("Card declined");

        // Act & Assert — RetryCount should now equal MaxRetries
        var act = () => payment.PrepareForRetry();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Maximum retry attempts*reached*");
    }

    #endregion

    #region Refund Tests

    [Fact]
    public void ProcessRefund_FullRefund_TransitionsToRefunded()
    {
        // Arrange
        var payment = CreateSucceededPayment();
        var refundId = "re_123";

        // Act
        payment.ProcessRefund(payment.Amount, refundId, "Full refund");

        // Assert
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.RefundedAmount.Should().Be(payment.Amount);
        payment.RefundId.Should().Be(refundId);
        payment.RefundedAt.Should().NotBeNull();
    }

    [Fact]
    public void ProcessRefund_PartialRefund_DoesNotTransitionToRefunded()
    {
        // Arrange
        var payment = CreateSucceededPayment();
        var partialAmount = payment.Amount / 2;

        // Act
        payment.ProcessRefund(partialAmount, "re_partial", "Partial refund");

        // Assert
        // Partial refunds transition to PartiallyRefunded (issue #419), never straight to Refunded.
        payment.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        payment.RefundedAmount.Should().Be(partialAmount);
        payment.NetAmount.Should().Be(payment.Amount - partialAmount);
    }

    [Fact]
    public void ProcessRefund_ExceedingAmount_ShouldThrow()
    {
        // Arrange
        var payment = CreateSucceededPayment();

        // Act & Assert
        var act = () => payment.ProcessRefund(payment.Amount + 100m, "re_123", "Too much");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*refund amount cannot exceed*");
    }

    [Fact]
    public void ProcessRefund_OnPendingPayment_ShouldThrow()
    {
        // Arrange
        var payment = CreatePendingPayment();

        // Act & Assert
        var act = () => payment.ProcessRefund(50m, "re_123", "Cannot refund pending");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Can only refund succeeded*");
    }

    #endregion

    #region Helper Methods

    private static Payment CreatePendingPayment()
    {
        return Payment.Create(
            tenantId: Guid.NewGuid(),
            amount: 99.99m,
            currency: "USD",
            idempotencyKey: Guid.NewGuid().ToString()
        );
    }

    private static Payment CreateSucceededPayment()
    {
        var payment = CreatePendingPayment();
        payment.MarkAsProcessing("txn_123");
        payment.MarkAsSucceeded("pi_stripe_123", "txn_123");
        return payment;
    }

    #endregion
}
