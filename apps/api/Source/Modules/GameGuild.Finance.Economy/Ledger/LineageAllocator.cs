using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Ledger;

public static class LineageAllocator
{
    public static DerivedCreditLot CreateDerivedLot(
        CreditLotId outputLotId,
        WalletId outputWalletId,
        ProvenanceKind provenance,
        DateTimeOffset confirmedAt,
        DateTimeOffset originalMaturesAt,
        long journalSequence,
        IReadOnlyList<FragmentSelection> selections,
        RootFenceSnapshot fenceSnapshot,
        RootReversalFenceRegistry fences)
    {
        var validated = Validate(selections, fenceSnapshot, fences);
        var lot = new CreditLot(
            outputLotId,
            outputWalletId,
            new CoinAmount(validated.Currency, validated.TotalUnits),
            provenance,
            confirmedAt,
            originalMaturesAt,
            journalSequence,
            CreditLotState.Active,
            validated.Ranges,
            validated.TraceUnitsPerCoinUnit);

        return new DerivedCreditLot(lot, validated.Parents);
    }

    public static DerivedCreditLot CreateConvertedSoftLot(
        CreditLotId outputLotId,
        WalletId outputWalletId,
        CoinAmount outputAmount,
        DateTimeOffset confirmedAt,
        DateTimeOffset originalMaturesAt,
        long journalSequence,
        IReadOnlyList<FragmentSelection> selections,
        RootFenceSnapshot fenceSnapshot,
        RootReversalFenceRegistry fences)
    {
        var validated = Validate(selections, fenceSnapshot, fences);
        if (validated.Currency != CurrencyCode.HardCoin || outputAmount.Currency != CurrencyCode.SoftCoin)
            throw new LineageConservationException("Conversion lineage supports HardCoin to SoftCoin only.");
        if (checked(validated.TotalUnits * Money.FixedParity.SoftCoinsPerHardCoin) != outputAmount.Units)
            throw new LineageConservationException("Conversion output must preserve exact fixed parity.");
        if (validated.TraceUnits != checked(outputAmount.Units * CurrencyTraceScale.SoftCoinTraceUnitsPerCoin))
            throw new LineageConservationException("Converted output must preserve every normalized root trace unit.");

        var lot = new CreditLot(
            outputLotId,
            outputWalletId,
            outputAmount,
            ProvenanceKind.ConvertedSoft,
            confirmedAt,
            originalMaturesAt,
            journalSequence,
            CreditLotState.Active,
            validated.Ranges,
            CurrencyTraceScale.SoftCoinTraceUnitsPerCoin);
        return new DerivedCreditLot(lot, validated.Parents);
    }

    public static FragmentRetirement CreateRetirement(
        PostingId postingId,
        IReadOnlyList<FragmentSelection> selections,
        RootFenceSnapshot fenceSnapshot,
        RootReversalFenceRegistry fences)
    {
        var validated = Validate(selections, fenceSnapshot, fences);
        return new FragmentRetirement(
            postingId,
            new CoinAmount(validated.Currency, validated.TotalUnits),
            validated.Parents);
    }

    private static ValidatedLineage Validate(
        IReadOnlyList<FragmentSelection> selections,
        RootFenceSnapshot fenceSnapshot,
        RootReversalFenceRegistry fences)
    {
        ArgumentNullException.ThrowIfNull(selections);
        ArgumentNullException.ThrowIfNull(fenceSnapshot);
        ArgumentNullException.ThrowIfNull(fences);
        if (selections.Count == 0) throw new LineageConservationException("At least one selected parent is required.");

        var currency = selections[0].Amount.Currency;
        var ranges = new List<RootTraceRange>();
        var parents = new List<ParentFragmentLineage>();
        var totalUnits = 0L;
        var traceUnits = 0L;
        var traceUnitsPerCoinUnit = selections[0].TraceUnitsPerCoinUnit;

        foreach (var selection in selections)
        {
            if (selection.Amount.Currency != currency)
                throw new LineageConservationException("All selected fragments must use one currency.");
            if (selection.TraceUnitsPerCoinUnit != traceUnitsPerCoinUnit)
                throw new LineageConservationException("A derived lot cannot mix trace-unit scales.");

            var tracedUnits = selection.SelectedRanges.Aggregate(
                0L,
                static (total, range) => checked(total + range.Length));
            if (tracedUnits != checked(selection.Amount.Units * selection.TraceUnitsPerCoinUnit))
                throw new LineageConservationException("Every parent allocation must conserve its selected amount exactly.");

            totalUnits = checked(totalUnits + selection.Amount.Units);
            traceUnits = checked(traceUnits + tracedUnits);
            ranges.AddRange(selection.SelectedRanges);
            parents.Add(new ParentFragmentLineage(selection.ParentLotId, selection.Amount, selection.SelectedRanges.ToArray()));
        }

        EnsureNonOverlapping(ranges);
        fences.EnsureAllocatable(fenceSnapshot, ranges.Select(range => range.Root));
        return new ValidatedLineage(currency, totalUnits, traceUnits, traceUnitsPerCoinUnit, ranges, parents);
    }

    internal static void EnsureNonOverlapping(IReadOnlyCollection<RootTraceRange> ranges)
    {
        foreach (var group in ranges.GroupBy(range => range.Root))
        {
            var ordered = group.OrderBy(range => range.Start).ThenBy(range => range.EndExclusive).ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                if (ordered[index].Start < ordered[index - 1].EndExclusive)
                    throw new LineageConservationException("Root trace ranges cannot overlap.");
            }
        }
    }

    private sealed record ValidatedLineage(
        CurrencyCode Currency,
        long TotalUnits,
        long TraceUnits,
        long TraceUnitsPerCoinUnit,
        IReadOnlyList<RootTraceRange> Ranges,
        IReadOnlyList<ParentFragmentLineage> Parents);
}

public sealed class ParentFragmentLineage
{
    public ParentFragmentLineage(
        CreditLotId parentLotId,
        CoinAmount amount,
        IReadOnlyList<RootTraceRange> ranges)
    {
        ParentLotId = parentLotId;
        Amount = amount;
        Ranges = Array.AsReadOnly(ranges.ToArray());
    }

    public CreditLotId ParentLotId { get; }
    public CoinAmount Amount { get; }
    public IReadOnlyList<RootTraceRange> Ranges { get; }
}

public sealed class DerivedCreditLot
{
    public DerivedCreditLot(CreditLot lot, IReadOnlyList<ParentFragmentLineage> parents)
    {
        Lot = lot;
        Parents = Array.AsReadOnly(parents.ToArray());
    }

    public CreditLot Lot { get; }
    public IReadOnlyList<ParentFragmentLineage> Parents { get; }
}

public sealed class FragmentRetirement
{
    public FragmentRetirement(
        PostingId postingId,
        CoinAmount amount,
        IReadOnlyList<ParentFragmentLineage> parents)
    {
        PostingId = postingId;
        Amount = amount;
        Parents = Array.AsReadOnly(parents.ToArray());
    }

    public PostingId PostingId { get; }
    public CoinAmount Amount { get; }
    public IReadOnlyList<ParentFragmentLineage> Parents { get; }
}

public sealed class LineageConservationException : InvalidOperationException
{
    public LineageConservationException(string message) : base(message)
    {
    }
}
