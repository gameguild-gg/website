using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using Fixture = GameGuild.Finance.Economy.Payouts.UnitTests.PayoutCoordinatorScenarioTests.Fixture;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class PayoutCoverageClosureTests
{
    private static readonly DateTimeOffset Time = PayoutCoordinatorScenarioTests.Time;

    [Fact]
    public void OperationStore_RejectsANonSequentialReplacementAgainstTheCurrentVersion()
    {
        var store = new InMemoryPayoutOperationStore();
        var operation = Operation(new Fixture(), PayoutOperationState.Reserved);
        store.Add(operation);

        var update = () => store.Update(operation with { Version = 3 }, expectedVersion: 1);

        update.Should().Throw<PayoutStaleCommandException>();

        var currentVersionMismatch = () => store.RecordProviderEvent(
            "event-current-version",
            "event-hash",
            operation with { Version = 3, State = PayoutOperationState.Succeeded },
            expectedVersion: 2,
            Time);
        currentVersionMismatch.Should().Throw<PayoutStaleCommandException>();
    }

    [Fact]
    public async Task PublicContracts_ExposeEverySecurityBindingAndSupportExplicitProviderBinding()
    {
        var fixture = new Fixture();
        fixture.AddLot(20, ProvenanceKind.EarnedHard, 150, 1);

        var reserved = await fixture.Coordinator.ReserveAsync(fixture.Request(5));
        fixture.Risk.LastRequest.Should().NotBeNull();
        var risk = fixture.Risk.LastRequest!;

        risk.Kyc.Should().BeSameAs(fixture.Kyc.Snapshot);
        risk.ExternalEvidence.Should().HaveCount(2);
        risk.RollingReserve.Should().BeSameAs(fixture.RollingReserve.Snapshot);
        risk.Account.Should().BeSameAs(fixture.Provider.Account);
        risk.EntityCluster.Id.Should().NotBeNullOrWhiteSpace();
        risk.EligibilityHash.Should().Be(reserved.EligibilityHash);
        reserved.ActorId.Should().Be(fixture.ActorId);

        await fixture.Coordinator.DispatchAsync(
            reserved.Id, reserved.Version, reserved.FencingToken, reserved.KillSwitchEpoch, Time.AddMinutes(1));
        fixture.Provider.LastDispatch.Should().NotBeNull();
        var dispatch = fixture.Provider.LastDispatch!;

        dispatch.ExpectedVersion.Should().Be(reserved.Version + 1);
        dispatch.FencingToken.Should().Be(reserved.FencingToken);
        dispatch.KillSwitchEpoch.Should().Be(reserved.KillSwitchEpoch);
        dispatch.Amount.Should().Be(reserved.Amount);
        dispatch.IdempotencyKey.Should().Be(reserved.IdempotencyKey.Value);

        foreach (var state in new[] { PayoutOperationState.Dispatching, PayoutOperationState.Ambiguous })
        {
            var inFlight = Operation(fixture, state);
            var bound = inFlight.BindProviderDispatch("  po_bound  ", Time.AddMinutes(2));
            bound.ProviderPayoutId.Should().Be("po_bound");
            bound.Version.Should().Be(inFlight.Version + 1);
            bound.UpdatedAt.Should().Be(Time.AddMinutes(2));
        }

        var invalidState = () => Operation(fixture, PayoutOperationState.Reserved)
            .BindProviderDispatch("po_invalid", Time);
        invalidState.Should().Throw<InvalidOperationException>();
        var invalidProviderId = () => Operation(fixture, PayoutOperationState.Dispatching)
            .BindProviderDispatch(" ", Time);
        invalidProviderId.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Reservation_RevalidatesAHoldInsertedAfterTheRiskDecision()
    {
        var fixture = new Fixture();
        fixture.AddLot(20, ProvenanceKind.EarnedHard, 150, 1);
        fixture.Risk.OnDecide = _ => fixture.Ledger.Execute(transaction =>
        {
            transaction.PlaceHold(
                HoldId.New(),
                fixture.WalletId,
                new CoinAmount(CurrencyCode.HardCoin, 1),
                HoldReason.RiskReview,
                Time.AddSeconds(1));
            return 0;
        });

        var reserve = async () => await fixture.Coordinator.ReserveAsync(fixture.Request(5));

        await reserve.Should().ThrowAsync<PayoutEligibilityException>();
        fixture.Operations.Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task Reservation_RejectsAChangedFifoSelectionEvenWhenEnoughValueRemains()
    {
        var fixture = new Fixture();
        fixture.AddLot(20, ProvenanceKind.EarnedHard, 150, 2);
        fixture.Risk.OnDecide = _ => fixture.AddLot(20, ProvenanceKind.EarnedHard, 200, 1);

        var reserve = async () => await fixture.Coordinator.ReserveAsync(fixture.Request(5));

        await reserve.Should().ThrowAsync<PayoutStaleCommandException>();
        fixture.Operations.Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task ConcurrentIdenticalReservations_CommitOnceAndReplayTheSameOperation()
    {
        var fixture = new Fixture();
        fixture.AddLot(20, ProvenanceKind.EarnedHard, 150, 1);
        var request = fixture.Request(5);
        using var barrier = new Barrier(2);
        fixture.Risk.OnDecide = _ => barrier.SignalAndWait(TimeSpan.FromSeconds(10));

        var first = Task.Run(async () => await fixture.Coordinator.ReserveAsync(request));
        var second = Task.Run(async () => await fixture.Coordinator.ReserveAsync(request));
        var results = await Task.WhenAll(first, second);

        results[0].Should().BeSameAs(results[1]);
        fixture.Operations.Operations.Should().ContainSingle();
        fixture.Ledger.FragmentReservations.Should().ContainSingle();
    }

    [Fact]
    public async Task ProviderEventWithoutAPreboundProviderId_PassesBindingThenFailsClosedOnMissingFragments()
    {
        var fixture = new Fixture();
        var operation = Operation(fixture, PayoutOperationState.Dispatching);
        fixture.Operations.Add(operation);
        var providerEvent = fixture.Provider.Event(
            operation.Id, PayoutProviderOutcome.Succeeded, "missing-fragments");

        var apply = async () => await fixture.Coordinator.ApplyProviderEventAsync(providerEvent);

        await apply.Should().ThrowAsync<PayoutStaleCommandException>();
    }

    private static PayoutOperation Operation(Fixture fixture, PayoutOperationState state) => new(
        Guid.NewGuid(),
        new IdempotencyKey(Guid.NewGuid().ToString("N")),
        "request",
        fixture.ActorId,
        fixture.PayeeId,
        fixture.WalletId,
        new CoinAmount(CurrencyCode.HardCoin, 1),
        fixture.ProviderAccountId,
        fixture.DestinationHash,
        "provider-binding",
        "eligibility",
        null,
        null,
        state,
        1,
        1,
        1,
        new ReserveVersion(1),
        1,
        new PolicyVersion(1),
        Guid.NewGuid(),
        Time,
        Time,
        fixture.TenantId);
}
