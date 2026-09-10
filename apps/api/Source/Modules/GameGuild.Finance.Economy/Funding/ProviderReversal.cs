using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.Funding;

public enum ProviderReversalDisposition
{
    ResponsibleDebt = 1,
    PlatformLoss = 2
}

public sealed record ReverseTopUpCommand(
    PostingId PostingIdSeed,
    IdempotencyKey IdempotencyKey,
    SourceStampId SourceId,
    long CumulativeProviderHardUnits,
    ProviderReversalDisposition IrrecoverableDisposition,
    string Evidence,
    ReserveVersion ReserveVersion,
    PolicyVersion PolicyVersion,
    DateTimeOffset OccurredAt);

public sealed record ProviderReversalState(
    SourceStampId SourceId,
    long AuthoritativeHardUnits,
    long CumulativeProviderHardUnits,
    long RecoveredHardUnits,
    long RecoveredConvertedSoftUnits,
    long ResponsibleDebtHardUnits,
    long PlatformLossHardUnits,
    IReadOnlyList<RootTraceRange> ReversedRanges)
{
    public long PartitionedHardEquivalentUnits => checked(
        RecoveredHardUnits +
        (RecoveredConvertedSoftUnits / Money.FixedParity.SoftCoinsPerHardCoin) +
        ResponsibleDebtHardUnits +
        PlatformLossHardUnits);
}

public sealed record ProviderReversalResult(
    IReadOnlyList<PostingResult> Postings,
    ProviderReversalState State);

public sealed record ProviderReversalFragment(
    CreditLot Lot,
    CoinAmount Amount,
    IReadOnlyList<RootTraceRange> Ranges);

public sealed record ProviderReversalPlan(
    IReadOnlyList<ProviderReversalFragment> Fragments,
    IReadOnlyList<RootTraceRange> AllReversedRanges,
    long UnrecoverableTraceUnits);

public static class ProviderReversalPlanner
{
    public static ProviderReversalPlan Plan(
        SourceStampId root,
        long cumulativeTargetTraceUnits,
        IReadOnlyCollection<RootTraceRange> alreadyReversed,
        IReadOnlyCollection<CreditLot> availableLots)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cumulativeTargetTraceUnits);
        ArgumentNullException.ThrowIfNull(alreadyReversed);
        ArgumentNullException.ThrowIfNull(availableLots);
        var history = alreadyReversed.OrderBy(range => range.Start).ToArray();
        if (history.Any(range => range.Root != root))
            throw new LineageConservationException("Reversal history must belong to the selected root.");
        LineageAllocator.EnsureNonOverlapping(history);
        var reversed = history.Sum(range => range.Length);
        if (cumulativeTargetTraceUnits < reversed)
            throw new ArgumentOutOfRangeException(nameof(cumulativeTargetTraceUnits));

        var remaining = cumulativeTargetTraceUnits - reversed;
        var fragments = new List<ProviderReversalFragment>();
        foreach (var lot in availableLots
                     .Where(lot => lot.State == CreditLotState.Active)
                     .OrderBy(lot => lot.Ranges.Min(range => range.Start))
                     .ThenBy(lot => lot.ConfirmedAt)
                     .ThenBy(lot => lot.JournalSequence))
        {
            if (remaining == 0) break;
            var selectedRanges = new List<RootTraceRange>();
            foreach (var range in lot.Ranges.Where(range => range.Root == root).OrderBy(range => range.Start))
            {
                if (remaining == 0) break;
                foreach (var available in Subtract(range, history))
                {
                    var coinUnits = Math.Min(
                        remaining / lot.TraceUnitsPerCoinUnit,
                        available.Length / lot.TraceUnitsPerCoinUnit);
                    if (coinUnits == 0) continue;
                    var traceUnits = checked(coinUnits * lot.TraceUnitsPerCoinUnit);
                    selectedRanges.Add(available.Take(traceUnits).Selected);
                    remaining -= traceUnits;
                    if (remaining == 0) break;
                }
            }

            if (selectedRanges.Count == 0) continue;
            var selectedTrace = selectedRanges.Sum(range => range.Length);
            fragments.Add(new ProviderReversalFragment(
                lot,
                new CoinAmount(lot.Amount.Currency, selectedTrace / lot.TraceUnitsPerCoinUnit),
                selectedRanges));
        }

        if (remaining % CurrencyTraceScale.HardCoinTraceUnitsPerCoin != 0)
            throw new UnrecoverableParityFractionException(
                "Provider reversal remainder cannot be represented as whole HardCoin units.");
        var all = history.Concat(fragments.SelectMany(fragment => fragment.Ranges))
            .OrderBy(range => range.Start)
            .ToArray();
        LineageAllocator.EnsureNonOverlapping(all);
        return new ProviderReversalPlan(fragments, all, remaining);
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
}

public sealed class ProviderMonetaryTotalExceededException(string message) : InvalidOperationException(message);
public sealed class UnrecoverableParityFractionException(string message) : InvalidOperationException(message);
