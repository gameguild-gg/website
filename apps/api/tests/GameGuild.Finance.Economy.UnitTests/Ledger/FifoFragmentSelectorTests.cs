using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class FifoFragmentSelectorTests
{
    private static readonly DateTimeOffset BaseTime = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

    [Fact]
    public void Select_UsesGlobalConfirmationThenJournalSequenceAndPreservesRemainderRank()
    {
        var later = Lot(10, BaseTime.AddMinutes(1), 1);
        var first = Lot(5, BaseTime, 2);
        var second = Lot(10, BaseTime, 3);

        var result = FifoFragmentSelector.Select([later, second, first], new CoinAmount(CurrencyCode.HardCoin, 12));

        result.Selections.Select(selection => selection.ParentLotId).Should().Equal(first.Id, second.Id);
        result.Selections.Select(selection => selection.Amount.Units).Should().Equal(5, 7);
        result.Selections[1].RemainingRanges.Should().ContainSingle().Which.Start.Should().Be(7);
        result.Selections[1].RemainingRanges[0].Length.Should().Be(3);
    }

    [Fact]
    public void Select_SkipsWrongCurrencyAndIneligibleLots()
    {
        var held = Lot(20, BaseTime, 1, state: CreditLotState.Held);
        var soft = Lot(20, BaseTime, 2, CurrencyCode.SoftCoin);
        var active = Lot(7, BaseTime, 3);

        var result = FifoFragmentSelector.Select([held, soft, active], new CoinAmount(CurrencyCode.HardCoin, 7));

        result.Selections.Should().ContainSingle().Which.ParentLotId.Should().Be(active.Id);
    }

    [Fact]
    public void Select_CrossesMixedRootRangesWithoutReorderingOrCoalescingThem()
    {
        var firstRoot = SourceStampId.New();
        var secondRoot = SourceStampId.New();
        var lot = Lot(
            10,
            BaseTime,
            1,
            ranges:
            [
                new RootTraceRange(firstRoot, 4, 6, 2),
                new RootTraceRange(secondRoot, 0, 4, 9)
            ]);

        var result = FifoFragmentSelector.Select([lot], new CoinAmount(CurrencyCode.HardCoin, 8));

        result.Selections[0].SelectedRanges.Should().Equal(
            new RootTraceRange(firstRoot, 4, 6, 2),
            new RootTraceRange(secondRoot, 0, 2, 9));
        result.Selections[0].RemainingRanges.Should().ContainSingle().Which.Should().Be(new RootTraceRange(secondRoot, 2, 2, 9));
    }

    [Fact]
    public void Select_UsesStableLotIdentityAfterEqualFifoKeys()
    {
        var low = Lot(2, BaseTime, 1, id: new CreditLotId(Guid.Parse("00000000-0000-0000-0000-000000000010")));
        var high = Lot(2, BaseTime, 1, id: new CreditLotId(Guid.Parse("00000000-0000-0000-0000-000000000020")));

        var result = FifoFragmentSelector.Select([high, low], new CoinAmount(CurrencyCode.HardCoin, 2));

        result.Selections.Should().ContainSingle().Which.ParentLotId.Should().Be(low.Id);
    }

    [Fact]
    public void Select_RejectsZeroOrInsufficientAvailability()
    {
        var lot = Lot(3, BaseTime, 1);

        FluentActions.Invoking(() => FifoFragmentSelector.Select([lot], new CoinAmount(CurrencyCode.HardCoin, 0)))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => FifoFragmentSelector.Select([lot], new CoinAmount(CurrencyCode.HardCoin, 4)))
            .Should().Throw<InsufficientFragmentsException>()
            .Which.MissingUnits.Should().Be(1);
    }

    [Fact]
    public void CreditLot_RequiresExactRangeConservationAndImmutableConfirmationData()
    {
        var root = SourceStampId.New();
        FluentActions.Invoking(() => Lot(5, BaseTime, 1, ranges: [new RootTraceRange(root, 0, 4, 0)]))
            .Should().Throw<ArgumentException>();

        var lot = Lot(5, BaseTime, 1, ranges: [new RootTraceRange(root, 0, 5, 0)]);
        lot.ConfirmedAt.Should().Be(BaseTime);
        lot.OriginalMaturesAt.Should().Be(BaseTime.AddDays(120));
        lot.Ranges.Should().ContainSingle();
    }

    private static CreditLot Lot(
        long units,
        DateTimeOffset confirmedAt,
        long journalSequence,
        CurrencyCode currency = CurrencyCode.HardCoin,
        CreditLotState state = CreditLotState.Active,
        IReadOnlyCollection<RootTraceRange>? ranges = null,
        CreditLotId? id = null)
    {
        ranges ??= [new RootTraceRange(SourceStampId.New(), 0, units, 0)];
        return new CreditLot(
            id ?? CreditLotId.New(),
            WalletId.New(),
            new CoinAmount(currency, units),
            currency == CurrencyCode.HardCoin ? ProvenanceKind.EarnedHard : ProvenanceKind.AdRewardSoft,
            confirmedAt,
            confirmedAt.AddDays(120),
            journalSequence,
            state,
            ranges);
    }
}
