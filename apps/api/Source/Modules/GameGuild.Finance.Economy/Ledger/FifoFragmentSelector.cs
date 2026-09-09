using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Ledger;

public static class FifoFragmentSelector
{
    public static FragmentSelectionResult Select(IEnumerable<CreditLot> lots, CoinAmount requested)
    {
        ArgumentNullException.ThrowIfNull(lots);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requested.Units);

        var eligible = lots
            .Where(lot => lot.State == CreditLotState.Active && lot.Amount.Currency == requested.Currency)
            .OrderBy(lot => lot.ConfirmedAt)
            .ThenBy(lot => lot.JournalSequence)
            .ThenBy(lot => lot.Id.Value)
            .ToArray();

        var available = eligible.Aggregate(0L, static (total, lot) => checked(total + lot.Amount.Units));
        if (available < requested.Units)
            throw new InsufficientFragmentsException(requested.Units - available);

        var remainingRequest = requested.Units;
        var selections = new List<FragmentSelection>();

        foreach (var lot in eligible)
        {
            if (remainingRequest == 0) break;

            var unitsFromLot = Math.Min(remainingRequest, lot.Amount.Units);
            var split = SplitRanges(lot.Ranges, checked(unitsFromLot * lot.TraceUnitsPerCoinUnit));
            selections.Add(new FragmentSelection(
                lot.Id,
                new CoinAmount(requested.Currency, unitsFromLot),
                split.Selected,
                split.Remaining,
                lot.TraceUnitsPerCoinUnit));
            remainingRequest -= unitsFromLot;
        }

        return new FragmentSelectionResult(selections);
    }

    private static RangeSelection SplitRanges(IReadOnlyList<RootTraceRange> ranges, long requestedUnits)
    {
        var selected = new List<RootTraceRange>();
        var remaining = new List<RootTraceRange>();
        var unitsLeft = requestedUnits;

        foreach (var range in ranges)
        {
            if (unitsLeft == 0)
            {
                remaining.Add(range);
                continue;
            }

            var unitsFromRange = Math.Min(unitsLeft, range.Length);
            var split = range.Take(unitsFromRange);
            selected.Add(split.Selected);
            if (split.Remaining is { } remainder) remaining.Add(remainder);
            unitsLeft -= unitsFromRange;
        }

        return new RangeSelection(selected, remaining);
    }

    private readonly record struct RangeSelection(
        IReadOnlyList<RootTraceRange> Selected,
        IReadOnlyList<RootTraceRange> Remaining);
}

public sealed record FragmentSelection(
    CreditLotId ParentLotId,
    CoinAmount Amount,
    IReadOnlyList<RootTraceRange> SelectedRanges,
    IReadOnlyList<RootTraceRange> RemainingRanges,
    long TraceUnitsPerCoinUnit = 1);

public sealed record FragmentSelectionResult(IReadOnlyList<FragmentSelection> Selections);

public sealed class InsufficientFragmentsException : InvalidOperationException
{
    public InsufficientFragmentsException(long missingUnits)
        : base($"The wallet is missing {missingUnits} eligible units.")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(missingUnits);
        MissingUnits = missingUnits;
    }

    public long MissingUnits { get; }
}
