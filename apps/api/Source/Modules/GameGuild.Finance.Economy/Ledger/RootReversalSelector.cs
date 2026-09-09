using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Ledger;

public static class RootReversalSelector
{
    public static RootReversalSelection Select(
        SourceStampId root,
        long cumulativeTargetUnits,
        IReadOnlyCollection<RootTraceRange> alreadyReversed,
        IEnumerable<CreditLot> activeLots)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cumulativeTargetUnits);
        ArgumentNullException.ThrowIfNull(alreadyReversed);
        ArgumentNullException.ThrowIfNull(activeLots);

        var history = alreadyReversed.OrderBy(range => range.Start).ToArray();
        if (history.Any(range => range.Root != root))
            throw new LineageConservationException("Reversal history must belong to the selected root.");
        LineageAllocator.EnsureNonOverlapping(history);

        var reversedUnits = history.Aggregate(0L, static (total, range) => checked(total + range.Length));
        if (cumulativeTargetUnits < reversedUnits)
            throw new ArgumentOutOfRangeException(nameof(cumulativeTargetUnits));

        var trace = activeLots
            .Where(lot => lot.State == CreditLotState.Active)
            .SelectMany(lot => lot.Ranges
                .Where(range => range.Root == root)
                .Select(range => new AvailableRootRange(lot, range)))
            .OrderBy(item => item.Range.Start)
            .ThenBy(item => item.Lot.ConfirmedAt)
            .ThenBy(item => item.Lot.JournalSequence)
            .ThenBy(item => item.Lot.Id.Value)
            .ToArray();

        LineageAllocator.EnsureNonOverlapping(trace.Select(item => item.Range).ToArray());
        EnsureHistoryIsTraced(history, trace.Select(item => item.Range).ToArray());

        var unitsNeeded = cumulativeTargetUnits - reversedUnits;
        var newFragments = new List<RootReversalFragment>();

        foreach (var item in trace)
        {
            if (unitsNeeded == 0) break;

            foreach (var available in Subtract(item.Range, history))
            {
                if (unitsNeeded == 0) break;
                var selectedUnits = Math.Min(unitsNeeded, available.Length);
                var selected = available.Take(selectedUnits).Selected;
                newFragments.Add(new RootReversalFragment(item.Lot.Id, [selected]));
                unitsNeeded -= selectedUnits;
            }
        }

        if (unitsNeeded != 0) throw new InsufficientFragmentsException(unitsNeeded);

        var all = history.Concat(newFragments.SelectMany(fragment => fragment.Ranges))
            .OrderBy(range => range.Start)
            .ToArray();
        LineageAllocator.EnsureNonOverlapping(all);
        return new RootReversalSelection(newFragments, all);
    }

    private static IReadOnlyList<RootTraceRange> Subtract(
        RootTraceRange source,
        IReadOnlyCollection<RootTraceRange> exclusions)
    {
        var result = new List<RootTraceRange>();
        var cursor = source.Start;

        foreach (var exclusion in exclusions
                     .Where(range => range.EndExclusive > source.Start && range.Start < source.EndExclusive)
                     .OrderBy(range => range.Start))
        {
            if (exclusion.Start > cursor)
                result.Add(new RootTraceRange(source.Root, cursor, exclusion.Start - cursor, source.Epoch));
            cursor = Math.Max(cursor, exclusion.EndExclusive);
            if (cursor >= source.EndExclusive) break;
        }

        if (cursor < source.EndExclusive)
            result.Add(new RootTraceRange(source.Root, cursor, source.EndExclusive - cursor, source.Epoch));

        return result;
    }

    private static void EnsureHistoryIsTraced(
        IEnumerable<RootTraceRange> history,
        IReadOnlyCollection<RootTraceRange> trace)
    {
        foreach (var reversed in history)
        {
            var covered = trace.Any(candidate =>
                candidate.Start <= reversed.Start && candidate.EndExclusive >= reversed.EndExclusive);
            if (!covered) throw new LineageConservationException("Reversal history must be contained in known root trace ranges.");
        }
    }

    private sealed record AvailableRootRange(CreditLot Lot, RootTraceRange Range);
}

public sealed record RootReversalFragment(CreditLotId ParentLotId, IReadOnlyList<RootTraceRange> Ranges);

public sealed record RootReversalSelection(
    IReadOnlyList<RootReversalFragment> NewFragments,
    IReadOnlyList<RootTraceRange> AllReversedRanges);
