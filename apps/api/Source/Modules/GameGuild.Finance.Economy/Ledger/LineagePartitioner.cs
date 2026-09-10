using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Ledger;

public static class LineagePartitioner
{
    public static IReadOnlyList<LineagePartition> Partition(
        IReadOnlyList<FragmentSelection> sources,
        IReadOnlyList<CoinAmount> outputAmounts)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(outputAmounts);
        if (sources.Count == 0 || outputAmounts.Count == 0)
            throw new LineageConservationException("Lineage partitioning requires sources and outputs.");

        var currency = sources[0].Amount.Currency;
        var scale = sources[0].TraceUnitsPerCoinUnit;
        if (sources.Any(source => source.Amount.Currency != currency || source.TraceUnitsPerCoinUnit != scale))
            throw new LineageConservationException("Lineage sources must share currency and trace scale.");
        if (outputAmounts.Any(output => output.Currency != currency))
            throw new LineageConservationException("Lineage outputs must use the source currency.");
        if (outputAmounts.Any(output => output.Units == 0))
            throw new LineageConservationException("Lineage outputs must contain positive coin units.");

        LineageAllocator.EnsureNonOverlapping(
            sources.SelectMany(source => source.SelectedRanges).ToArray());

        var sourceUnits = sources.Aggregate(0L, static (total, source) => checked(total + source.Amount.Units));
        var outputUnits = outputAmounts.Aggregate(0L, static (total, output) => checked(total + output.Units));
        if (sourceUnits != outputUnits)
            throw new LineageConservationException("Lineage output amounts must consume all source units exactly.");

        var cursors = sources.Select(source => new SelectionCursor(source)).ToArray();
        var cursorIndex = 0;
        var partitions = new List<LineagePartition>();

        foreach (var output in outputAmounts)
        {
            var traceUnitsNeeded = checked(output.Units * scale);
            var allocations = new List<FragmentSelection>();

            while (traceUnitsNeeded > 0)
            {
                var cursor = cursors[cursorIndex];
                var traceUnits = Math.Min(traceUnitsNeeded, cursor.RemainingTraceUnits);
                var ranges = cursor.Take(traceUnits);
                allocations.Add(new FragmentSelection(
                    cursor.Source.ParentLotId,
                    new CoinAmount(currency, traceUnits / scale),
                    ranges,
                    [],
                    scale));
                traceUnitsNeeded -= traceUnits;
                if (cursor.RemainingTraceUnits == 0) cursorIndex++;
            }

            partitions.Add(new LineagePartition(output, allocations));
        }

        return partitions;
    }

    private sealed class SelectionCursor
    {
        private readonly IReadOnlyList<RootTraceRange> _ranges;
        private int _rangeIndex;
        private RootTraceRange? _remainder;

        internal SelectionCursor(FragmentSelection source)
        {
            Source = source;
            _ranges = source.SelectedRanges;
            RemainingTraceUnits = source.SelectedRanges.Aggregate(
                0L,
                static (total, range) => checked(total + range.Length));
            if (RemainingTraceUnits != checked(source.Amount.Units * source.TraceUnitsPerCoinUnit))
                throw new LineageConservationException("Source selection trace units do not conserve its amount.");
        }

        internal FragmentSelection Source { get; }
        internal long RemainingTraceUnits { get; private set; }

        internal IReadOnlyList<RootTraceRange> Take(long traceUnits)
        {
            var selected = new List<RootTraceRange>();
            var remaining = traceUnits;

            while (remaining > 0)
            {
                var range = _remainder ?? _ranges[_rangeIndex++];
                _remainder = null;
                var units = Math.Min(remaining, range.Length);
                var split = range.Take(units);
                selected.Add(split.Selected);
                if (split.Remaining is { } remainder) _remainder = remainder;
                remaining -= units;
                RemainingTraceUnits -= units;
            }

            return selected;
        }
    }
}

public sealed record LineagePartition(CoinAmount Amount, IReadOnlyList<FragmentSelection> Selections);
