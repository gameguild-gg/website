using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class FragmentReservationTests
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-08-03T12:00:00Z");

    [Fact]
    public void Reservation_TransitionsOnlyThroughAllowedAppendOnlyStates()
    {
        var reservation = CreateReservation();
        var dispatching = reservation.Transition(FragmentReservationStatus.Dispatching, Time.AddMinutes(1));
        var consumed = dispatching.Transition(FragmentReservationStatus.Consumed, Time.AddMinutes(2));

        dispatching.OperationVersion.Should().Be(2);
        dispatching.TerminalAt.Should().BeNull();
        consumed.OperationVersion.Should().Be(3);
        consumed.TerminalAt.Should().Be(Time.AddMinutes(2));
        reservation.Status.Should().Be(FragmentReservationStatus.Reserved);
        FluentActions.Invoking(() => reservation.Transition(FragmentReservationStatus.Consumed, Time.AddMinutes(1)))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => consumed.Transition(FragmentReservationStatus.Released, Time.AddMinutes(3)))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reservation_ExhaustivelyEnforcesTheTransitionMatrix()
    {
        foreach (var current in Enum.GetValues<FragmentReservationStatus>())
            foreach (var next in Enum.GetValues<FragmentReservationStatus>())
            {
                var reservation = CreateReservation(status: current);
                var allowed = (current, next) is
                    (FragmentReservationStatus.Reserved, FragmentReservationStatus.Dispatching) or
                    (FragmentReservationStatus.Reserved, FragmentReservationStatus.Released) or
                    (FragmentReservationStatus.Dispatching, FragmentReservationStatus.Consumed) or
                    (FragmentReservationStatus.Dispatching, FragmentReservationStatus.Released);
                var transition = () => reservation.Transition(next, Time.AddMinutes(1));

                if (allowed)
                    transition.Should().NotThrow();
                else
                    transition.Should().Throw<InvalidOperationException>();
            }
    }

    [Fact]
    public void Reservation_ValidatesIdentityEnumsVersionsTimingAndConservation()
    {
        var valid = CreateReservation();
        var args = new object?[]
        {
            valid.Id, valid.OperationId, valid.Purpose, valid.LotId, valid.WalletId, valid.Amount,
            valid.Ranges, valid.OperationVersion, valid.FencingToken, valid.KillSwitchEpoch,
            valid.Status, valid.ReservedAt, valid.TerminalAt
        };

        Act(args, 0, Guid.Empty).Should().Throw<ArgumentException>();
        Act(args, 1, Guid.Empty).Should().Throw<ArgumentException>();
        Act(args, 2, (FragmentReservationPurpose)0).Should().Throw<ArgumentOutOfRangeException>();
        Act(args, 6, null).Should().Throw<ArgumentNullException>();
        Act(args, 6, Array.Empty<RootTraceRange>()).Should().Throw<ArgumentException>();
        Act(args, 7, 0L).Should().Throw<ArgumentOutOfRangeException>();
        Act(args, 8, 0L).Should().Throw<ArgumentOutOfRangeException>();
        Act(args, 9, 0L).Should().Throw<ArgumentOutOfRangeException>();
        Act(args, 10, (FragmentReservationStatus)0).Should().Throw<ArgumentOutOfRangeException>();
        Act(args, 12, Time).Should().Throw<ArgumentException>();
        Act(args, 10, FragmentReservationStatus.Consumed).Should().Throw<ArgumentException>();
        var terminalArgs = (object?[])args.Clone();
        terminalArgs[10] = FragmentReservationStatus.Consumed;
        terminalArgs[12] = Time.AddMinutes(-1);
        Construct(terminalArgs).Should().Throw<ArgumentException>();
        Act(args, 6, new[] { new RootTraceRange(valid.Ranges[0].Root, 0, 999, 0) })
            .Should().Throw<LineageConservationException>();

        valid.Ranges.Should().NotBeAssignableTo<RootTraceRange[]>();
        CreateReservation(purpose: FragmentReservationPurpose.AdminWithdrawal).Purpose
            .Should().Be(FragmentReservationPurpose.AdminWithdrawal);
        valid.Transition(FragmentReservationStatus.Released, Time.AddMinutes(1)).Status
            .Should().Be(FragmentReservationStatus.Released);
    }

    [Fact]
    public void Store_ReservesExactRangesAndReleasesPredispatchOperationsForReversal()
    {
        var store = StoreWithLot(out var lot);
        var first = CreateReservation(lot: lot, units: 4);
        store.Execute(transaction => { transaction.AddFragmentReservation(first); return 0; });

        store.GetAvailableLots(lot.WalletId, CurrencyCode.HardCoin).Single().Amount.Units.Should().Be(6);
        store.GetFragmentReservations(first.OperationId).Should().ContainSingle();
        store.FragmentReservations.Should().ContainSingle();
        store.Execute(transaction => transaction.ReleaseReservedFragmentsForRoot(lot.Ranges[0].Root, Time.AddMinutes(1)))
            .Should().Equal(first.OperationId);
        store.FragmentReservations.Single().Status.Should().Be(FragmentReservationStatus.Released);
        store.GetAvailableLots(lot.WalletId, CurrencyCode.HardCoin).Single().Amount.Units.Should().Be(10);
        store.Execute(transaction => transaction.ReleaseReservedFragmentsForRoot(SourceStampId.New(), Time.AddMinutes(2)))
            .Should().BeEmpty();
    }

    [Fact]
    public void Store_RejectsDuplicatesMismatchesOverlapsAndMissingTransitionsTransactionally()
    {
        var store = StoreWithLot(out var lot);
        var reservation = CreateReservation(lot: lot, units: 4);
        store.Execute(transaction => { transaction.AddFragmentReservation(reservation); return 0; });

        FluentActions.Invoking(() => store.Execute(transaction =>
            { transaction.AddFragmentReservation(reservation); return 0; }))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => store.Execute(transaction =>
            {
                transaction.AddFragmentReservation(CreateReservation(lot: lot, units: 2));
                return 0;
            })).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => store.Execute(transaction =>
            {
                var wrongWallet = CreateReservation(lot: lot, units: 2, walletId: WalletId.New());
                transaction.AddFragmentReservation(wrongWallet);
                return 0;
            })).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => store.Execute(transaction =>
            {
                var wrongRoot = CreateReservation(lot: lot, units: 1, root: SourceStampId.New(), rangeStart: 4_000);
                transaction.AddFragmentReservation(wrongRoot);
                return 0;
            })).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => store.Execute(transaction =>
            {
                var wrongEpoch = CreateReservation(lot: lot, units: 1, rangeStart: 4_000, epoch: 1);
                transaction.AddFragmentReservation(wrongEpoch);
                return 0;
            })).Should().Throw<InvalidOperationException>();
        var offsetStore = StoreWithLot(out var offsetLot, rangeStart: 1_000);
        FluentActions.Invoking(() => offsetStore.Execute(transaction =>
            {
                transaction.AddFragmentReservation(CreateReservation(lot: offsetLot, rangeStart: 0));
                return 0;
            })).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => store.Execute(transaction =>
            {
                var outside = CreateReservation(lot: lot, units: 2, rangeStart: 11_000);
                transaction.AddFragmentReservation(outside);
                return 0;
            })).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => store.Execute(transaction =>
                transaction.TransitionFragmentReservations(Guid.NewGuid(), FragmentReservationStatus.Reserved,
                    FragmentReservationStatus.Dispatching, Time)))
            .Should().Throw<KeyNotFoundException>();
        FluentActions.Invoking(() => store.Execute(transaction =>
                transaction.TransitionFragmentReservations(reservation.OperationId,
                    FragmentReservationStatus.Dispatching, FragmentReservationStatus.Consumed, Time)))
            .Should().Throw<InvalidOperationException>();
        store.FragmentReservations.Should().ContainSingle();
    }

    [Fact]
    public void Store_DispatchingRangesStayUnavailableUntilConsumedOrReleased()
    {
        var store = StoreWithLot(out var lot);
        var reservation = CreateReservation(lot: lot, units: 4);
        store.Execute(transaction =>
        {
            transaction.AddFragmentReservation(reservation);
            return transaction.TransitionFragmentReservations(reservation.OperationId,
                FragmentReservationStatus.Reserved, FragmentReservationStatus.Dispatching, Time.AddMinutes(1));
        }).Should().ContainSingle().Which.Status.Should().Be(FragmentReservationStatus.Dispatching);

        store.Execute(transaction => transaction.ReleaseReservedFragmentsForRoot(lot.Ranges[0].Root, Time.AddMinutes(1)))
            .Should().BeEmpty();
        store.GetAvailableLots(lot.WalletId, CurrencyCode.HardCoin).Single().Amount.Units.Should().Be(6);
        store.Execute(transaction => transaction.TransitionFragmentReservations(reservation.OperationId,
            FragmentReservationStatus.Dispatching, FragmentReservationStatus.Released, Time.AddMinutes(2)));
        store.GetAvailableLots(lot.WalletId, CurrencyCode.HardCoin).Single().Amount.Units.Should().Be(10);
    }

    private static InMemoryLedgerKernelStore StoreWithLot(out CreditLot lot, long rangeStart = 0)
    {
        var store = new InMemoryLedgerKernelStore();
        var root = SourceStampId.New();
        var created = new CreditLot(CreditLotId.New(), WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, 10),
            ProvenanceKind.EarnedHard, Time.AddDays(-121), Time.AddDays(-1), 1,
            CreditLotState.Active, [new RootTraceRange(root, rangeStart, 10_000, 0)],
            CurrencyTraceScale.HardCoinTraceUnitsPerCoin);
        store.Execute(transaction => { transaction.AddCreditLot(created); return 0; });
        lot = created;
        return store;
    }

    private static ValueFragmentReservation CreateReservation(
        FragmentReservationPurpose purpose = FragmentReservationPurpose.Payout,
        CreditLot? lot = null,
        long units = 1,
        WalletId? walletId = null,
        long rangeStart = 0,
        SourceStampId? root = null,
        long epoch = 0,
        FragmentReservationStatus status = FragmentReservationStatus.Reserved) =>
        new(Guid.NewGuid(), Guid.NewGuid(), purpose, lot?.Id ?? CreditLotId.New(),
            walletId ?? lot?.WalletId ?? WalletId.New(), new CoinAmount(CurrencyCode.HardCoin, units),
            [new RootTraceRange(root ?? lot?.Ranges[0].Root ?? SourceStampId.New(), rangeStart,
                checked(units * CurrencyTraceScale.HardCoinTraceUnitsPerCoin), epoch)],
            1, 1, 1, status, Time,
            status is FragmentReservationStatus.Consumed or FragmentReservationStatus.Released ? Time : null);

    private static Action Act(object?[] source, int index, object? value) => () =>
    {
        var args = (object?[])source.Clone();
        args[index] = value;
        Construct(args)();
    };

    private static Action Construct(object?[] args) => () => _ = new ValueFragmentReservation(
        (Guid)args[0]!, (Guid)args[1]!, (FragmentReservationPurpose)args[2]!,
        (CreditLotId)args[3]!, (WalletId)args[4]!, (CoinAmount)args[5]!,
        (IReadOnlyCollection<RootTraceRange>)args[6]!, (long)args[7]!, (long)args[8]!, (long)args[9]!,
        (FragmentReservationStatus)args[10]!, (DateTimeOffset)args[11]!, (DateTimeOffset?)args[12]);
}
