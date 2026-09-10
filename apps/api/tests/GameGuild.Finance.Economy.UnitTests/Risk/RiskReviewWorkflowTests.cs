using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class RiskReviewWorkflowTests
{
    private static readonly DateTimeOffset Time = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DualApprovalRequiresTwoDifferentIndependentReviewers()
    {
        var ledger = new RiskReviewLedger();
        var review = ledger.Submit(
            Guid.NewGuid(), Decision(), Guid.NewGuid(), ["evidence"], Time, 2, null);
        var first = Guid.NewGuid();

        ledger.Approve(
            review.Id, first, RiskManualDecisionCode.EvidenceVerified, "first approval", Time.AddMinutes(1))
            .Status.Should().Be(RiskReviewStatus.Pending);
        FluentActions.Invoking(() => ledger.Approve(
                review.Id, first, RiskManualDecisionCode.EvidenceVerified, "duplicate", Time.AddMinutes(2)))
            .Should().Throw<InvalidOperationException>();
        ledger.Approve(
            review.Id, Guid.NewGuid(), RiskManualDecisionCode.RiskAccepted, "second approval", Time.AddMinutes(2))
            .Status.Should().Be(RiskReviewStatus.Approved);

        ledger.Events.Select(item => item.Kind).Should().Equal(
            RiskReviewEventKind.Submitted,
            RiskReviewEventKind.ApprovalRecorded,
            RiskReviewEventKind.Approved);
    }

    [Fact]
    public void RejectedCaseCanBeAppealedWithImmutableEvidence()
    {
        var ledger = new RiskReviewLedger();
        var original = ledger.Submit(Guid.NewGuid(), Decision(), Guid.NewGuid(), ["original"], Time);
        ledger.Reject(
            original.Id, Guid.NewGuid(), RiskManualDecisionCode.FraudConfirmed, "rejected", Time.AddMinutes(1));

        var appeal = ledger.Appeal(
            Guid.NewGuid(), original.Id, Decision(), Guid.NewGuid(), ["appeal"], Time.AddMinutes(2));

        appeal.AppealOf.Should().Be(original.Id);
        appeal.RequiredApprovals.Should().Be(2);
        ledger.Events[^1].Kind.Should().Be(RiskReviewEventKind.AppealSubmitted);
        ledger.Events[^1].EvidenceHashes.Should().Equal("appeal");
        FluentActions.Invoking(() => ledger.Appeal(
                Guid.NewGuid(), appeal.Id, Decision(), Guid.NewGuid(), ["invalid"], Time.AddMinutes(3)))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => ledger.Appeal(
                Guid.NewGuid(), Guid.NewGuid(), Decision(), Guid.NewGuid(), ["unknown"], Time.AddMinutes(3)))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReviewWorkflowValidatesApprovalCountAndManualDecisionCode()
    {
        var ledger = new RiskReviewLedger();
        FluentActions.Invoking(() => ledger.Submit(
                Guid.NewGuid(), Decision(), Guid.NewGuid(), ["evidence"], Time, 3, null))
            .Should().Throw<ArgumentOutOfRangeException>();

        var review = ledger.Submit(Guid.NewGuid(), Decision(), Guid.NewGuid(), ["evidence"], Time);
        FluentActions.Invoking(() => ledger.Approve(
                review.Id, Guid.NewGuid(), (RiskManualDecisionCode)99, "invalid", Time.AddMinutes(1)))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    private static RiskDecisionSnapshot Decision()
    {
        var context = new ProtectedOperationContext(
            new IdempotencyKey(Guid.NewGuid().ToString("N")), Guid.NewGuid(), PostingTemplateKind.PayoutReservation,
            WalletId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 1),
            [new RiskCurrencyLeg(CurrencyCode.HardCoin, 1)], [SourceStampId.New()], "provider",
            new PolicyVersion(1), new ReserveVersion(1), 1, 1, 1, "graph");
        return RiskDecisionSnapshot.Create(
            Guid.NewGuid(), RiskOutcome.Review, context, Time, Time.AddMinutes(10),
            [RiskReasonCode.ManualReviewRequired]);
    }
}
