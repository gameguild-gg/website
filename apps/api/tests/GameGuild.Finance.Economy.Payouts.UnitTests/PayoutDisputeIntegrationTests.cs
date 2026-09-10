using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Finance.Economy.Ledger;
using Fixture = GameGuild.Finance.Economy.Payouts.UnitTests.PayoutCoordinatorScenarioTests.Fixture;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class PayoutDisputeIntegrationTests
{
    private static readonly DateTimeOffset Time = PayoutCoordinatorScenarioTests.Time;

    [Fact]
    public async Task ProviderTimeout_PreservesTheDispatchClaimForAuthoritativeReconciliation()
    {
        var fixture = new Fixture();
        fixture.AddLot(10, ProvenanceKind.EarnedHard, 150, 1);
        fixture.Provider.DispatchFactory = _ => throw new TimeoutException("provider outcome is unknown");
        var reserved = await fixture.Coordinator.ReserveAsync(fixture.Request(4));

        var dispatch = async () => await fixture.Coordinator.DispatchAsync(
            reserved.Id,
            reserved.Version,
            reserved.FencingToken,
            reserved.KillSwitchEpoch,
            Time.AddMinutes(1));

        await dispatch.Should().ThrowAsync<TimeoutException>();
        var claimed = fixture.Operations.Get(reserved.Id);
        claimed.State.Should().Be(PayoutOperationState.Dispatching);
        claimed.ProviderPayoutId.Should().BeNull();
        fixture.Ledger.GetFragmentReservations(reserved.Id)
            .Should().OnlyContain(item => item.Status == FragmentReservationStatus.Dispatching);
    }

    [Fact]
    public async Task DisputeLostAfterPayoutSuccess_CreatesDebtWithoutRecreatingConsumedValue()
    {
        var fixture = new Fixture();
        var sourceId = AddConfirmedMatureEarnedRoot(fixture, 10);
        fixture.Provider.DispatchOutcome = PayoutProviderOutcome.Succeeded;
        var reserved = await fixture.Coordinator.ReserveAsync(fixture.Request(10));

        var paid = await fixture.Coordinator.DispatchAsync(
            reserved.Id,
            reserved.Version,
            reserved.FencingToken,
            reserved.KillSwitchEpoch,
            Time.AddMinutes(1));

        paid.State.Should().Be(PayoutOperationState.Succeeded);
        fixture.Ledger.GetFragmentReservations(paid.Id)
            .Should().OnlyContain(item => item.Status == FragmentReservationStatus.Consumed);

        var posting = new TransactionalPostingService(fixture.Ledger, fences: fixture.RootFences);
        var disputes = new ProviderDisputeWorkflow(fixture.Ledger, posting, fixture.RootFences);
        disputes.Handle(Dispute(sourceId, "evt-open-after-payout", 1, ProviderDisputeStatus.Open));
        var lost = disputes.Handle(Dispute(sourceId, "evt-lost-after-payout", 2, ProviderDisputeStatus.Lost));

        lost.Reversal.Should().NotBeNull();
        lost.Reversal!.State.ResponsibleDebtHardUnits.Should().Be(10);
        fixture.Ledger.GetDebt(fixture.WalletId).OutstandingHardUnits.Should().Be(10);
        fixture.Ledger.CreditLots.Should().ContainSingle();
        fixture.Ledger.FragmentConsumptions.Should().ContainSingle();
    }

    private static SourceStampId AddConfirmedMatureEarnedRoot(Fixture fixture, long units)
    {
        var sourceId = SourceStampId.New();
        var observedAt = Time.AddDays(-150);
        var confirmedAt = observedAt.AddSeconds(1);
        var providerLeg = new ProviderMonetaryLeg(
            "stripe",
            "test",
            fixture.ProviderAccountId,
            $"earned_{sourceId.Value:N}",
            "creator-revenue");
        var claim = HardCoinFundingClaim
            .Observe(sourceId, fixture.WalletId, providerLeg, "earned-observed", units, observedAt)
            .Transition(SourceConfirmationState.Confirmed, "earned-confirmed", confirmedAt);
        var observed = SourceEvidence.Observe(
            sourceId,
            providerLeg.Provider,
            providerLeg.Key,
            "earned-observed",
            observedAt);
        var confirmed = observed.Confirm(confirmedAt);
        var lot = ConfirmedCreditFactory.CreateRootLot(
            CreditLotId.New(),
            fixture.WalletId,
            new CoinAmount(CurrencyCode.HardCoin, units),
            ProvenanceKind.EarnedHard,
            confirmed,
            journalSequence: 1);
        fixture.Ledger.Execute(transaction =>
        {
            transaction.AddSource(observed);
            transaction.AddSource(confirmed);
            transaction.AddFundingClaim(claim);
            transaction.AddCreditLot(lot);
            return 0;
        });
        return sourceId;
    }

    private static ProviderDisputeNotification Dispute(
        SourceStampId sourceId,
        string eventId,
        long sequence,
        ProviderDisputeStatus status) => new(
        eventId,
        "dp-after-payout",
        sourceId,
        sequence,
        10,
        status,
        ProviderReversalDisposition.ResponsibleDebt,
        $"provider-{status.ToString().ToLowerInvariant()}",
        new ReserveVersion(1),
        new PolicyVersion(1),
        Time.AddMinutes(sequence + 1));
}
