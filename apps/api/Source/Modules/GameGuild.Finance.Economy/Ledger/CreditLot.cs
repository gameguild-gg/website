using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Ledger;

public enum CreditLotState
{
    Active = 1,
    Held = 2,
    Consumed = 3,
    Reversed = 4
}

public sealed class CreditLot
{
    private readonly IReadOnlyList<RootTraceRange> _ranges;

    public CreditLot(
        CreditLotId id,
        WalletId walletId,
        CoinAmount amount,
        ProvenanceKind provenance,
        DateTimeOffset confirmedAt,
        DateTimeOffset originalMaturesAt,
        long journalSequence,
        CreditLotState state,
        IReadOnlyCollection<RootTraceRange> ranges,
        long traceUnitsPerCoinUnit = 1)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        if (!Enum.IsDefined(provenance)) throw new ArgumentOutOfRangeException(nameof(provenance));
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount.Units);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(journalSequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(traceUnitsPerCoinUnit);
        if (originalMaturesAt < confirmedAt)
            throw new ArgumentException("Original maturity cannot precede confirmation.", nameof(originalMaturesAt));

        _ranges = Array.AsReadOnly(ranges.ToArray());
        if (_ranges.Count == 0) throw new ArgumentException("At least one root trace range is required.", nameof(ranges));

        var tracedUnits = _ranges.Aggregate(0L, static (total, range) => checked(total + range.Length));
        if (tracedUnits != checked(amount.Units * traceUnitsPerCoinUnit))
            throw new ArgumentException("Root trace ranges must conserve the credit lot amount exactly.", nameof(ranges));

        Id = id;
        WalletId = walletId;
        Amount = amount;
        Provenance = provenance;
        ConfirmedAt = confirmedAt;
        OriginalMaturesAt = originalMaturesAt;
        JournalSequence = journalSequence;
        State = state;
        TraceUnitsPerCoinUnit = traceUnitsPerCoinUnit;
    }

    public CreditLotId Id { get; }
    public WalletId WalletId { get; }
    public CoinAmount Amount { get; }
    public ProvenanceKind Provenance { get; }
    public DateTimeOffset ConfirmedAt { get; }
    public DateTimeOffset OriginalMaturesAt { get; }
    public long JournalSequence { get; }
    public CreditLotState State { get; }
    public long TraceUnitsPerCoinUnit { get; }
    public IReadOnlyList<RootTraceRange> Ranges => _ranges;
}

public static class CurrencyTraceScale
{
    public const long HardCoinTraceUnitsPerCoin = 1_000;
    public const long SoftCoinTraceUnitsPerCoin = 1;

    public static long For(CurrencyCode currency) => currency switch
    {
        CurrencyCode.HardCoin => HardCoinTraceUnitsPerCoin,
        CurrencyCode.SoftCoin => SoftCoinTraceUnitsPerCoin,
        _ => throw new ArgumentOutOfRangeException(nameof(currency))
    };
}
