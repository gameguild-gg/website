using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class PayoutLifecycleContractTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-08-03T12:00:00Z");

    [Fact]
    public void RollingReserve_ComputesCeilingReserveAndNeverReturnsNegativeAvailability()
    {
        new PayoutRollingReserveSnapshot(1, 101, 1, 1_000, Time, Time.AddMinutes(5), "reserve")
            .ReleasableHardUnits.Should().Be(89);
        new PayoutRollingReserveSnapshot(1, 10, 20, 0, Time, Time.AddMinutes(5), "reserve")
            .ReleasableHardUnits.Should().Be(0);
        FluentActions.Invoking(() => new PayoutRollingReserveSnapshot(
                1, long.MaxValue, 0, 10_000, Time, Time.AddMinutes(5), "reserve").ReleasableHardUnits)
            .Should().Throw<OverflowException>();
    }

    [Fact]
    public void Operation_ExhaustivelyEnforcesTheAppendOnlyTransitionMatrix()
    {
        foreach (var current in Enum.GetValues<PayoutOperationState>())
            foreach (var next in Enum.GetValues<PayoutOperationState>())
            {
                var operation = Operation(current);
                var allowed = (current, next) is
                    (PayoutOperationState.Reserved, PayoutOperationState.Dispatching) or
                    (PayoutOperationState.Reserved, PayoutOperationState.Cancelled) or
                    (PayoutOperationState.Dispatching, PayoutOperationState.Ambiguous) or
                    (PayoutOperationState.Dispatching, PayoutOperationState.Succeeded) or
                    (PayoutOperationState.Dispatching, PayoutOperationState.Failed) or
                    (PayoutOperationState.Ambiguous, PayoutOperationState.Succeeded) or
                    (PayoutOperationState.Ambiguous, PayoutOperationState.Failed);
                var transition = () => operation.Transition(next, Time.AddMinutes(1), "snapshot", "po_123");

                if (!allowed)
                {
                    transition.Should().Throw<InvalidOperationException>();
                    continue;
                }

                var changed = transition.Should().NotThrow().Which;
                changed.State.Should().Be(next);
                changed.Version.Should().Be(2);
                changed.DispatchSnapshotHash.Should().Be("snapshot");
                changed.ProviderPayoutId.Should().Be("po_123");
                changed.UpdatedAt.Should().Be(Time.AddMinutes(1));
                operation.State.Should().Be(current);
            }
    }

    [Fact]
    public void Operation_TransitionPreservesPreviouslyBoundProviderValuesWhenNoReplacementIsSupplied()
    {
        var operation = Operation(PayoutOperationState.Dispatching) with
        {
            DispatchSnapshotHash = "bound-snapshot",
            ProviderPayoutId = "po_bound"
        };

        var changed = operation.Transition(PayoutOperationState.Ambiguous, Time.AddMinutes(1));

        changed.DispatchSnapshotHash.Should().Be("bound-snapshot");
        changed.ProviderPayoutId.Should().Be("po_bound");
    }

    [Fact]
    public void ExecutionGate_IsDisabledByDefaultAndStopAdvancesTheKillSwitchEpoch()
    {
        var disabled = new PayoutExecutionGate();
        disabled.IsEnabled.Should().BeFalse();
        disabled.Epoch.Should().Be(1);
        FluentActions.Invoking(disabled.EnsureEnabled).Should().Throw<PayoutExecutionDisabledException>();

        var enabled = new PayoutExecutionGate(true, 8);
        enabled.EnsureEnabled();
        enabled.IsEnabled.Should().BeTrue();
        enabled.Stop().Should().Be(9);
        enabled.IsEnabled.Should().BeFalse();
        enabled.Epoch.Should().Be(9);
        FluentActions.Invoking(() => new PayoutExecutionGate(epoch: 0))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    private static PayoutOperation Operation(PayoutOperationState state) => new(
        Guid.NewGuid(),
        new IdempotencyKey("payout-operation"),
        "request",
        Guid.NewGuid(),
        Guid.NewGuid(),
        WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 10),
        "acct_123",
        "destination",
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
        Time);
}
