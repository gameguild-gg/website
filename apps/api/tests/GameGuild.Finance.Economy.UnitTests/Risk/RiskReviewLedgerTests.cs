using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class RiskReviewLedgerTests
{
    private static readonly DateTimeOffset Time = new(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReviewRequiresIndependentApprovalAndPreservesAppendOnlyEvidence()
    {
        var ledger = new RiskReviewLedger();
        var submitter = Guid.NewGuid();
        var context = Context();
        var decision = RiskDecisionSnapshot.Create(
            Guid.NewGuid(), RiskOutcome.Review, context, Time, Time.AddMinutes(5), [RiskReasonCode.ManualReviewRequired]);
        var review = ledger.Submit(Guid.NewGuid(), decision, submitter, ["evidence-a", "evidence-b"], Time);
        review.DecisionId.Should().Be(decision.Id);

        FluentActions.Invoking(() => ledger.Approve(review.Id, submitter, "same actor", Time.AddMinutes(1)))
            .Should().Throw<InvalidOperationException>();
        var reviewer = Guid.NewGuid();
        ledger.Approve(review.Id, reviewer, "verified", Time.AddMinutes(1));

        ledger.Current(review.Id).Status.Should().Be(RiskReviewStatus.Approved);
        ledger.Events.Should().HaveCount(2);
        var submitted = ledger.Events[0];
        submitted.Sequence.Should().Be(1);
        submitted.ReviewId.Should().Be(review.Id);
        submitted.ActorId.Should().Be(submitter);
        submitted.EvidenceHashes.Should().Equal("evidence-a", "evidence-b");
        submitted.Resolution.Should().BeNull();
        submitted.DecisionCode.Should().BeNull();
        ledger.Events[1].Resolution.Should().Be("verified");
        ledger.Events[1].DecisionCode.Should().Be(RiskManualDecisionCode.EvidenceVerified);
        FluentActions.Invoking(() => ledger.Reject(review.Id, Guid.NewGuid(), "late", Time.AddMinutes(2)))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReviewLedgerRejectsNonReviewDecisionMissingCaseAndBackdatedResolution()
    {
        var ledger = new RiskReviewLedger();
        var context = Context();
        var allow = RiskDecisionSnapshot.Create(
            Guid.NewGuid(), RiskOutcome.Allow, context, Time, Time.AddMinutes(5), [RiskReasonCode.WithinLimits]);
        FluentActions.Invoking(() => ledger.Submit(Guid.NewGuid(), allow, Guid.NewGuid(), ["evidence"], Time))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => ledger.Current(Guid.NewGuid())).Should().Throw<KeyNotFoundException>();

        var review = ledger.Submit(
            Guid.NewGuid(),
            RiskDecisionSnapshot.Create(
                Guid.NewGuid(), RiskOutcome.Review, context, Time, Time.AddMinutes(5), [RiskReasonCode.ManualReviewRequired]),
            Guid.NewGuid(), ["evidence"], Time);
        FluentActions.Invoking(() => ledger.Reject(review.Id, Guid.NewGuid(), "backdated", Time.AddTicks(-1)))
            .Should().Throw<ArgumentException>();
    }

    private static ProtectedOperationContext Context() =>
        new(
            new IdempotencyKey("review"), Guid.NewGuid(), PostingTemplateKind.PayoutReservation,
            WalletId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 1),
            [new RiskCurrencyLeg(CurrencyCode.HardCoin, 1)], [SourceStampId.New()], "provider",
            new PolicyVersion(1), new ReserveVersion(1), 1, 1, 1, "graph");
}
