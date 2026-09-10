using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Ledger;

public readonly record struct RootTraceRange
{
    public RootTraceRange(SourceStampId root, long start, long length, long epoch)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentOutOfRangeException.ThrowIfNegative(epoch);

        Root = root;
        Start = start;
        Length = length;
        Epoch = epoch;
        _ = checked(start + length);
    }

    public SourceStampId Root { get; }
    public long Start { get; }
    public long Length { get; }
    public long Epoch { get; }
    public long EndExclusive => checked(Start + Length);

    public RootTraceSplit Take(long units)
    {
        if (units <= 0 || units > Length) throw new ArgumentOutOfRangeException(nameof(units));

        var selected = new RootTraceRange(Root, Start, units, Epoch);
        var remainingLength = Length - units;
        var remaining = remainingLength == 0
            ? (RootTraceRange?)null
            : new RootTraceRange(Root, checked(Start + units), remainingLength, Epoch);

        return new RootTraceSplit(selected, remaining);
    }
}

public readonly record struct RootTraceSplit(RootTraceRange Selected, RootTraceRange? Remaining);
