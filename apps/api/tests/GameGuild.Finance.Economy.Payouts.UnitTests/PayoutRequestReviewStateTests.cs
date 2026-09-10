using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class PayoutRequestReviewStateTests
{
    private static readonly DateTimeOffset RequestedAt = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstApprovalMovesTheRequestToAwaitingSecondApproval()
    {
        var request = CreateSubmittedRequest();

        var reviewed = request.Review(Guid.NewGuid(), PayoutRequestState.Approved, RequestedAt.AddMinutes(1));

        reviewed.State.Should().Be((PayoutRequestState)5);
        reviewed.Version.Should().Be(2);
    }

    [Fact]
    public void ASecondAdministratorCompletesTheApproval()
    {
        var request = CreateSubmittedRequest();
        var firstReview = request.Review(Guid.NewGuid(), PayoutRequestState.Approved, RequestedAt.AddMinutes(1));

        var approved = firstReview.Review(Guid.NewGuid(), PayoutRequestState.Approved, RequestedAt.AddMinutes(2));

        approved.State.Should().Be(PayoutRequestState.Approved);
        approved.Version.Should().Be(3);
    }

    [Fact]
    public void AwaitingSecondApprovalWithoutARecordedFirstApproverIsRejected()
    {
        var request = CreateSubmittedRequest() with
        {
            State = PayoutRequestState.AwaitingSecondApproval
        };

        FluentActions.Invoking(() => request.Review(
                Guid.NewGuid(),
                PayoutRequestState.Approved,
                RequestedAt.AddMinutes(1)))
            .Should().Throw<PayoutRequestTransitionException>()
            .WithMessage("*first approver*");
    }

    [Fact]
    public void Review_RejectsInvalidReviewerOutcomeTimestampAndTerminalState()
    {
        var request = CreateSubmittedRequest();
        var reviewer = Guid.NewGuid();

        FluentActions.Invoking(() => request.Review(Guid.Empty, PayoutRequestState.Approved, RequestedAt.AddMinutes(1)))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => request.Review(request.PayeeId, PayoutRequestState.Approved, RequestedAt.AddMinutes(1)))
            .Should().Throw<PayoutRequestTransitionException>()
            .WithMessage("*cannot review their own*");
        FluentActions.Invoking(() => request.Review(reviewer, PayoutRequestState.Cancelled, RequestedAt.AddMinutes(1)))
            .Should().Throw<PayoutRequestTransitionException>()
            .WithMessage("*approve or reject*");
        FluentActions.Invoking(() => request.Review(reviewer, PayoutRequestState.Approved, RequestedAt.AddMinutes(-1)))
            .Should().Throw<ArgumentOutOfRangeException>();

        var rejected = request.Review(reviewer, PayoutRequestState.Rejected, RequestedAt.AddMinutes(1));
        rejected.State.Should().Be(PayoutRequestState.Rejected);
        rejected.Version.Should().Be(2);
        FluentActions.Invoking(() => rejected.Review(Guid.NewGuid(), PayoutRequestState.Approved, RequestedAt.AddMinutes(2)))
            .Should().Throw<PayoutRequestTransitionException>()
            .WithMessage("*Only a submitted*");
    }

    [Fact]
    public void FirstApproverCannotCompleteTheSecondApproval()
    {
        var firstApprover = Guid.NewGuid();
        var awaitingSecondApproval = CreateSubmittedRequest()
            .Review(firstApprover, PayoutRequestState.Approved, RequestedAt.AddMinutes(1));

        FluentActions.Invoking(() => awaitingSecondApproval.Review(
                firstApprover,
                PayoutRequestState.Approved,
                RequestedAt.AddMinutes(2)))
            .Should().Throw<PayoutRequestTransitionException>()
            .WithMessage("*cannot complete*");
    }

    private static PayoutRequest CreateSubmittedRequest() => new(
        Guid.NewGuid(),
        new IdempotencyKey($"payout-request-{Guid.NewGuid():N}"),
        new string('a', 64),
        Guid.NewGuid(),
        new WalletId(Guid.NewGuid()),
        new CoinAmount(CurrencyCode.HardCoin, 100),
        PayoutRequestState.Submitted,
        1,
        RequestedAt,
        RequestedAt);
}
