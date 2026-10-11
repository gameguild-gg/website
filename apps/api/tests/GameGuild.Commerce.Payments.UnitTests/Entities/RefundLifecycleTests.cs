using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Entities;

public class RefundLifecycleTests
{
    private static Refund PendingRefund(decimal amount = 10m) => Refund.Create(
        tenantId: Guid.NewGuid(),
        paymentId: Guid.NewGuid(),
        amount: amount,
        currency: "usd",
        reason: "duplicate",
        reasonDetails: "charged twice",
        requestedByUserId: Guid.NewGuid(),
        requiresApproval: false,
        idempotencyKey: $"refund_{Guid.NewGuid():N}");

    [Fact]
    public void Create_InitializesPendingStateWithRetrySafeKey()
    {
        var refund = PendingRefund();

        refund.Status.Should().Be(RefundStatus.Pending);
        refund.Currency.Should().Be("USD");
        refund.AttemptCount.Should().Be(0);
        refund.IsTerminal.Should().BeFalse();
        refund.IdempotencyKey.Should().StartWith("refund_");
        refund.IdempotencyKey.Should().NotContainAny(new[] { ":", " " });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_RejectsNonPositiveAmounts(decimal amount)
    {
        var act = () => PendingRefund(amount);

        act.Should().Throw<ArgumentException>().WithMessage("*Refund amount must be positive*");
    }

    [Fact]
    public void Create_RejectsMissingReason()
    {
        var act = () => Refund.Create(
            Guid.NewGuid(), Guid.NewGuid(), 10m, "USD", "  ", null, Guid.NewGuid(), false, "key");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FullLifecycle_PendingToApprovedToProcessingToCompleted()
    {
        var refund = PendingRefund();
        var approver = Guid.NewGuid();

        refund.MarkApproved(approver, "valid complaint");
        refund.Status.Should().Be(RefundStatus.Approved);
        refund.DecidedByUserId.Should().Be(approver);
        refund.DecidedAt.Should().NotBeNull();
        refund.DecisionNotes.Should().Be("valid complaint");

        refund.MarkProcessing();
        refund.Status.Should().Be(RefundStatus.Processing);
        refund.AttemptCount.Should().Be(1);

        refund.MarkCompleted("re_123", "stripe");
        refund.Status.Should().Be(RefundStatus.Completed);
        refund.GatewayRefundId.Should().Be("re_123");
        refund.GatewayName.Should().Be("stripe");
        refund.ProcessedAt.Should().NotBeNull();
        refund.FailureReason.Should().BeNull();
        refund.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void ManualRejection_IsTerminalAndRecordsTheReviewer()
    {
        var refund = PendingRefund();
        var reviewer = Guid.NewGuid();

        refund.MarkRejected(reviewer, "not eligible");

        refund.Status.Should().Be(RefundStatus.Rejected);
        refund.DecidedByUserId.Should().Be(reviewer);
        refund.DecisionNotes.Should().Be("not eligible");
        refund.IsTerminal.Should().BeTrue();
        ((Action)(() => refund.MarkProcessing())).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FailedAttempt_CanBeReExecuted()
    {
        var refund = PendingRefund();
        refund.MarkProcessing();
        refund.MarkFailed("gateway unavailable");

        refund.Status.Should().Be(RefundStatus.Failed);
        refund.FailureReason.Should().Be("gateway unavailable");
        refund.FailedAt.Should().NotBeNull();
        refund.IsTerminal.Should().BeFalse();

        refund.MarkProcessing();
        refund.Status.Should().Be(RefundStatus.Processing);
        refund.AttemptCount.Should().Be(2);
    }

    [Fact]
    public void Pending_CannotTransitionDirectlyToCompleted()
    {
        var refund = PendingRefund();

        var act = () => refund.MarkCompleted("re_123", "stripe");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot transition refund from Pending to Completed*");
    }

    [Fact]
    public void Completed_IsTerminal()
    {
        var refund = PendingRefund();
        refund.MarkProcessing();
        refund.MarkCompleted("re_123", "stripe");

        ((Action)(() => refund.MarkFailed("late failure"))).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Decisions_RequireAnActor()
    {
        var refund = PendingRefund();

        ((Action)(() => refund.MarkApproved(Guid.Empty))).Should().Throw<ArgumentException>();
        ((Action)(() => refund.MarkRejected(Guid.Empty))).Should().Throw<ArgumentException>();
    }
}
