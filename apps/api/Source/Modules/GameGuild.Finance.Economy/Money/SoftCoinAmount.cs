namespace GameGuild.Finance.Economy.Money;

public readonly record struct SoftCoinAmount : IComparable<SoftCoinAmount>
{
    public SoftCoinAmount(long units)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(units);
        Units = units;
    }

    public long Units { get; }

    public static SoftCoinAmount Zero => new(0);

    public static SoftCoinAmount operator +(SoftCoinAmount left, SoftCoinAmount right) => new(checked(left.Units + right.Units));

    public static SoftCoinAmount operator -(SoftCoinAmount left, SoftCoinAmount right)
    {
        if (right.Units > left.Units) throw new InvalidOperationException("Soft-coin amounts cannot underflow.");
        return new SoftCoinAmount(left.Units - right.Units);
    }

    public static SoftCoinAmount operator *(SoftCoinAmount amount, long factor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(factor);
        return new SoftCoinAmount(checked(amount.Units * factor));
    }

    public static bool operator <(SoftCoinAmount left, SoftCoinAmount right) => left.Units < right.Units;

    public static bool operator >(SoftCoinAmount left, SoftCoinAmount right) => left.Units > right.Units;

    public static bool operator <=(SoftCoinAmount left, SoftCoinAmount right) => left.Units <= right.Units;

    public static bool operator >=(SoftCoinAmount left, SoftCoinAmount right) => left.Units >= right.Units;

    public int CompareTo(SoftCoinAmount other) => Units.CompareTo(other.Units);
}
