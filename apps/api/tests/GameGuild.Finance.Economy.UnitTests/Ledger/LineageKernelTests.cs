using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class LineageKernelTests
{
    private static readonly DateTimeOffset ConfirmedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

    [Fact]
    public void CreateDerivedLot_ConservesEverySelectedUnitAndRootRange()
    {
        var root = SourceStampId.New();
        var parent = Lot(10, [new RootTraceRange(root, 0, 10, 0)]);
        var selected = FifoFragmentSelector.Select([parent], new CoinAmount(CurrencyCode.HardCoin, 6));
        var fences = new RootReversalFenceRegistry();
        var snapshot = fences.Capture([root]);

        var result = LineageAllocator.CreateDerivedLot(
            CreditLotId.New(),
            WalletId.New(),
            ProvenanceKind.RefundRestoration,
            ConfirmedAt,
            ConfirmedAt.AddDays(120),
            2,
            selected.Selections,
            snapshot,
            fences);

        result.Lot.Amount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 6));
        result.Lot.Ranges.Should().Equal(new RootTraceRange(root, 0, 6, 0));
        result.Parents.Should().ContainSingle().Which.ParentLotId.Should().Be(parent.Id);
        result.Parents[0].Ranges.Should().Equal(result.Lot.Ranges);
    }

    [Fact]
    public void CreateDerivedLot_PreservesMixedRootsInSelectionOrder()
    {
        var firstRoot = SourceStampId.New();
        var secondRoot = SourceStampId.New();
        var parent = Lot(8,
        [
            new RootTraceRange(firstRoot, 10, 3, 1),
            new RootTraceRange(secondRoot, 4, 5, 2)
        ]);
        var selected = FifoFragmentSelector.Select([parent], new CoinAmount(CurrencyCode.HardCoin, 7));
        var fences = new RootReversalFenceRegistry();
        var snapshot = fences.Capture([firstRoot, secondRoot]);

        var result = LineageAllocator.CreateDerivedLot(
            CreditLotId.New(), WalletId.New(), ProvenanceKind.EscrowReturn,
            ConfirmedAt, ConfirmedAt.AddDays(120), 2,
            selected.Selections, snapshot, fences);

        result.Lot.Ranges.Should().Equal(
            new RootTraceRange(firstRoot, 10, 3, 1),
            new RootTraceRange(secondRoot, 4, 4, 2));
    }

    [Fact]
    public void CreateDerivedLot_RejectsStaleOrActiveRootFence()
    {
        var root = SourceStampId.New();
        var parent = Lot(5, [new RootTraceRange(root, 0, 5, 0)]);
        var selected = FifoFragmentSelector.Select([parent], new CoinAmount(CurrencyCode.HardCoin, 5));
        var fences = new RootReversalFenceRegistry();
        var stale = fences.Capture([root]);
        var epoch = fences.BeginReversal(root);

        FluentActions.Invoking(() => Create(selected.Selections, stale, fences))
            .Should().Throw<StaleRootFenceException>();

        var active = fences.Capture([root]);
        FluentActions.Invoking(() => Create(selected.Selections, active, fences))
            .Should().Throw<RootReversalInProgressException>();

        fences.CompleteReversal(root, epoch);
        var current = fences.Capture([root]);
        Create(selected.Selections, current, fences).Lot.Amount.Units.Should().Be(5);
    }

    [Fact]
    public void RootFence_RejectsCompletionWithWrongEpochOrInactiveRoot()
    {
        var root = SourceStampId.New();
        var fences = new RootReversalFenceRegistry();
        var epoch = fences.BeginReversal(root);

        FluentActions.Invoking(() => fences.CompleteReversal(root, epoch + 1))
            .Should().Throw<StaleRootFenceException>();
        fences.CompleteReversal(root, epoch);
        FluentActions.Invoking(() => fences.CompleteReversal(root, epoch))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Lineage_RejectsAmountMismatchCurrencyMismatchAndOverlappingRootRanges()
    {
        var root = SourceStampId.New();
        var secondRoot = SourceStampId.New();
        var parent = Lot(5, [new RootTraceRange(root, 0, 5, 0)]);
        var fences = new RootReversalFenceRegistry();
        var snapshot = fences.Capture([root, secondRoot]);
        var validHard = new FragmentSelection(
            parent.Id,
            new CoinAmount(CurrencyCode.HardCoin, 5),
            [new RootTraceRange(root, 0, 5, 0)],
            []);
        var overlap = new FragmentSelection(
            parent.Id,
            new CoinAmount(CurrencyCode.HardCoin, 6),
            [new RootTraceRange(root, 0, 3, 0), new RootTraceRange(root, 2, 3, 0)],
            []);
        var wrongCurrency = new FragmentSelection(
            parent.Id,
            new CoinAmount(CurrencyCode.SoftCoin, 5),
            [new RootTraceRange(secondRoot, 0, 5, 0)],
            []);
        var mismatch = new FragmentSelection(
            parent.Id,
            new CoinAmount(CurrencyCode.HardCoin, 5),
            [new RootTraceRange(root, 0, 4, 0)],
            []);

        FluentActions.Invoking(() => Create([overlap], snapshot, fences)).Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => LineageAllocator.CreateDerivedLot(
                CreditLotId.New(), WalletId.New(), ProvenanceKind.ConvertedSoft,
                ConfirmedAt, ConfirmedAt.AddDays(120), 2,
                [validHard, wrongCurrency], snapshot, fences))
            .Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => Create([mismatch], snapshot, fences)).Should().Throw<LineageConservationException>();
    }

    [Fact]
    public void Retirement_RecordsExactSelectedFragmentsWithoutCreatingBalance()
    {
        var root = SourceStampId.New();
        var parent = Lot(5, [new RootTraceRange(root, 3, 5, 1)]);
        var selected = FifoFragmentSelector.Select([parent], new CoinAmount(CurrencyCode.HardCoin, 4));
        var fences = new RootReversalFenceRegistry();
        var snapshot = fences.Capture([root]);

        var retirement = LineageAllocator.CreateRetirement(PostingId.New(), selected.Selections, snapshot, fences);

        retirement.Amount.Should().Be(new CoinAmount(CurrencyCode.HardCoin, 4));
        retirement.Parents.Should().ContainSingle();
        retirement.Parents[0].Ranges.Should().Equal(new RootTraceRange(root, 3, 4, 1));
    }

    [Fact]
    public void CumulativeReversal_SelectsOnlyNewNonOverlappingIntervalsFromMixedRootLots()
    {
        var target = SourceStampId.New();
        var unrelated = SourceStampId.New();
        var first = Lot(7,
        [
            new RootTraceRange(target, 0, 4, 0),
            new RootTraceRange(unrelated, 0, 3, 0)
        ], sequence: 1);
        var second = Lot(6,
        [
            new RootTraceRange(target, 4, 6, 0)
        ], sequence: 2);

        var initial = RootReversalSelector.Select(target, 3, [], [second, first]);
        var extended = RootReversalSelector.Select(target, 8, initial.AllReversedRanges, [second, first]);
        var repeated = RootReversalSelector.Select(target, 8, extended.AllReversedRanges, [second, first]);

        initial.NewFragments.SelectMany(fragment => fragment.Ranges)
            .Should().Equal(new RootTraceRange(target, 0, 3, 0));
        extended.NewFragments.SelectMany(fragment => fragment.Ranges)
            .Should().Equal(
                new RootTraceRange(target, 3, 1, 0),
                new RootTraceRange(target, 4, 4, 0));
        extended.AllReversedRanges.Sum(range => range.Length).Should().Be(8);
        repeated.NewFragments.Should().BeEmpty();
        repeated.AllReversedRanges.Should().Equal(extended.AllReversedRanges);
    }

    [Fact]
    public void CumulativeReversal_RejectsOverlappingHistoryOrTargetBeyondTrace()
    {
        var root = SourceStampId.New();
        var lot = Lot(5, [new RootTraceRange(root, 0, 5, 0)]);

        FluentActions.Invoking(() => RootReversalSelector.Select(
                root,
                4,
                [new RootTraceRange(root, 0, 3, 0), new RootTraceRange(root, 2, 2, 0)],
                [lot]))
            .Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => RootReversalSelector.Select(root, 6, [], [lot]))
            .Should().Throw<InsufficientFragmentsException>();
    }

    [Fact]
    public void ConversionLineage_PreservesNormalizedTraceAtExactHardToSoftParity()
    {
        var root = SourceStampId.New();
        var parent = new CreditLot(
            CreditLotId.New(), WalletId.New(),
            new CoinAmount(CurrencyCode.HardCoin, 10),
            ProvenanceKind.PurchasedHard,
            ConfirmedAt,
            ConfirmedAt.AddDays(120),
            1,
            CreditLotState.Active,
            [new RootTraceRange(root, 0, 10_000, 0)],
            CurrencyTraceScale.HardCoinTraceUnitsPerCoin);
        var selected = FifoFragmentSelector.Select(
            [parent], new CoinAmount(CurrencyCode.HardCoin, 10));
        var fences = new RootReversalFenceRegistry();
        var snapshot = fences.Capture([root]);

        var converted = LineageAllocator.CreateConvertedSoftLot(
            CreditLotId.New(), WalletId.New(),
            new CoinAmount(CurrencyCode.SoftCoin, 10_000),
            ConfirmedAt, ConfirmedAt.AddDays(120), 2,
            selected.Selections, snapshot, fences);
        var oneSoftCoin = FifoFragmentSelector.Select(
            [converted.Lot], new CoinAmount(CurrencyCode.SoftCoin, 1));

        converted.Lot.TraceUnitsPerCoinUnit.Should().Be(1);
        converted.Lot.Ranges.Should().Equal(new RootTraceRange(root, 0, 10_000, 0));
        oneSoftCoin.Selections[0].SelectedRanges.Should().Equal(new RootTraceRange(root, 0, 1, 0));
    }

    [Fact]
    public void Partition_SplitsRecipientFeeEscrowAndRetirementWithoutLossOrOverlap()
    {
        var root = SourceStampId.New();
        var selection = new FragmentSelection(
            CreditLotId.New(),
            new CoinAmount(CurrencyCode.HardCoin, 10),
            [new RootTraceRange(root, 0, 10_000, 0)],
            [],
            CurrencyTraceScale.HardCoinTraceUnitsPerCoin);

        var partitions = LineagePartitioner.Partition(
            [selection],
            [
                new CoinAmount(CurrencyCode.HardCoin, 6),
                new CoinAmount(CurrencyCode.HardCoin, 1),
                new CoinAmount(CurrencyCode.HardCoin, 2),
                new CoinAmount(CurrencyCode.HardCoin, 1)
            ]);

        partitions.Should().HaveCount(4);
        partitions.SelectMany(partition => partition.Selections)
            .SelectMany(allocation => allocation.SelectedRanges)
            .Should().Equal(
                new RootTraceRange(root, 0, 6_000, 0),
                new RootTraceRange(root, 6_000, 1_000, 0),
                new RootTraceRange(root, 7_000, 2_000, 0),
                new RootTraceRange(root, 9_000, 1_000, 0));
        partitions.Select(partition => partition.Amount.Units).Should().Equal(6, 1, 2, 1);
    }

    [Fact]
    public void Partition_RejectsNonConservingOrMixedCurrencyOutputs()
    {
        var selection = new FragmentSelection(
            CreditLotId.New(),
            new CoinAmount(CurrencyCode.HardCoin, 2),
            [new RootTraceRange(SourceStampId.New(), 0, 2, 0)],
            []);

        FluentActions.Invoking(() => LineagePartitioner.Partition(
                [selection], [new CoinAmount(CurrencyCode.HardCoin, 1)]))
            .Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => LineagePartitioner.Partition(
                [selection], [new CoinAmount(CurrencyCode.SoftCoin, 2)]))
            .Should().Throw<LineageConservationException>();
    }

    [Fact]
    public void ConversionLineage_RejectsWrongCurrenciesParityTraceScaleAndMixedParentScale()
    {
        var hardRoot = SourceStampId.New();
        var softRoot = SourceStampId.New();
        var hard = new FragmentSelection(
            CreditLotId.New(), new CoinAmount(CurrencyCode.HardCoin, 1),
            [new RootTraceRange(hardRoot, 0, 1_000, 0)], [], 1_000);
        var soft = new FragmentSelection(
            CreditLotId.New(), new CoinAmount(CurrencyCode.SoftCoin, 1_000),
            [new RootTraceRange(softRoot, 0, 1_000, 0)], [], 1);
        var unnormalizedHard = new FragmentSelection(
            CreditLotId.New(), new CoinAmount(CurrencyCode.HardCoin, 1),
            [new RootTraceRange(hardRoot, 1_000, 1, 0)], [], 1);
        var fences = new RootReversalFenceRegistry();
        var snapshot = fences.Capture([hardRoot, softRoot]);

        FluentActions.Invoking(() => Convert([soft], new CoinAmount(CurrencyCode.SoftCoin, 1_000), snapshot, fences))
            .Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => Convert([hard], new CoinAmount(CurrencyCode.HardCoin, 1), snapshot, fences))
            .Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => Convert([hard], new CoinAmount(CurrencyCode.SoftCoin, 999), snapshot, fences))
            .Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => Convert([unnormalizedHard], new CoinAmount(CurrencyCode.SoftCoin, 1_000), snapshot, fences))
            .Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => LineageAllocator.CreateDerivedLot(
                CreditLotId.New(), WalletId.New(), ProvenanceKind.EarnedHard,
                ConfirmedAt, ConfirmedAt.AddDays(120), 2,
                [hard, unnormalizedHard], snapshot, fences))
            .Should().Throw<LineageConservationException>();
    }

    [Fact]
    public void Partition_RejectsEmptyMixedScaleAndMalformedTraceSources()
    {
        var root = SourceStampId.New();
        var hard = new FragmentSelection(
            CreditLotId.New(), new CoinAmount(CurrencyCode.HardCoin, 1),
            [new RootTraceRange(root, 0, 1_000, 0)], [], 1_000);
        var soft = new FragmentSelection(
            CreditLotId.New(), new CoinAmount(CurrencyCode.SoftCoin, 1_000),
            [new RootTraceRange(SourceStampId.New(), 0, 1_000, 0)], [], 1);
        var mixedScale = hard with
        {
            ParentLotId = CreditLotId.New(),
            SelectedRanges = [new RootTraceRange(SourceStampId.New(), 0, 1, 0)],
            TraceUnitsPerCoinUnit = 1
        };
        var malformed = hard with
        {
            SelectedRanges = [new RootTraceRange(root, 0, 999, 0)]
        };

        FluentActions.Invoking(() => LineagePartitioner.Partition([], [new CoinAmount(CurrencyCode.HardCoin, 1)]))
            .Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => LineagePartitioner.Partition([hard], []))
            .Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => LineagePartitioner.Partition([hard, soft], [new CoinAmount(CurrencyCode.HardCoin, 2)]))
            .Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => LineagePartitioner.Partition([hard, mixedScale], [new CoinAmount(CurrencyCode.HardCoin, 2)]))
            .Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => LineagePartitioner.Partition([malformed], [new CoinAmount(CurrencyCode.HardCoin, 1)]))
            .Should().Throw<LineageConservationException>();
    }

    [Fact]
    public void Partition_RejectsZeroOutputsAndOverlappingSourceTrace()
    {
        var root = SourceStampId.New();
        var valid = new FragmentSelection(
            CreditLotId.New(), new CoinAmount(CurrencyCode.HardCoin, 2),
            [new RootTraceRange(root, 0, 2, 0)], []);
        var overlapping = new FragmentSelection(
            CreditLotId.New(), new CoinAmount(CurrencyCode.HardCoin, 2),
            [new RootTraceRange(root, 1, 2, 0)], []);

        FluentActions.Invoking(() => LineagePartitioner.Partition(
                [valid],
                [new CoinAmount(CurrencyCode.HardCoin, 0), new CoinAmount(CurrencyCode.HardCoin, 2)]))
            .Should().Throw<LineageConservationException>();
        FluentActions.Invoking(() => LineagePartitioner.Partition(
                [valid, overlapping],
                [new CoinAmount(CurrencyCode.HardCoin, 4)]))
            .Should().Throw<LineageConservationException>();
    }

    private static DerivedCreditLot Convert(
        IReadOnlyList<FragmentSelection> selections,
        CoinAmount output,
        RootFenceSnapshot snapshot,
        RootReversalFenceRegistry fences) =>
        LineageAllocator.CreateConvertedSoftLot(
            CreditLotId.New(), WalletId.New(), output,
            ConfirmedAt, ConfirmedAt.AddDays(120), 2,
            selections, snapshot, fences);

    private static DerivedCreditLot Create(
        IReadOnlyList<FragmentSelection> selections,
        RootFenceSnapshot snapshot,
        RootReversalFenceRegistry fences) =>
        LineageAllocator.CreateDerivedLot(
            CreditLotId.New(), WalletId.New(), ProvenanceKind.RefundRestoration,
            ConfirmedAt, ConfirmedAt.AddDays(120), 2,
            selections, snapshot, fences);

    private static CreditLot Lot(
        long units,
        IReadOnlyCollection<RootTraceRange> ranges,
        long sequence = 1) =>
        new(
            CreditLotId.New(), WalletId.New(),
            new CoinAmount(CurrencyCode.HardCoin, units),
            ProvenanceKind.EarnedHard,
            ConfirmedAt,
            ConfirmedAt.AddDays(120),
            sequence,
            CreditLotState.Active,
            ranges);
}
