namespace GameGuild.Finance.Economy.Money;

public readonly record struct HardCoinAmount : IComparable<HardCoinAmount>
{
    public HardCoinAmount(long units)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(units);
        Units = units;
    }

    public long Units { get; }

    public static HardCoinAmount Zero => new(0);

    public static HardCoinAmount operator +(HardCoinAmount left, HardCoinAmount right) => new(checked(left.Units + right.Units));

    public static HardCoinAmount operator -(HardCoinAmount left, HardCoinAmount right)
    {
        if (right.Units > left.Units) throw new InvalidOperationException("Hard-coin amounts cannot underflow.");
        return new HardCoinAmount(left.Units - right.Units);
    }

    public static HardCoinAmount operator *(HardCoinAmount amount, long factor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(factor);
        return new HardCoinAmount(checked(amount.Units * factor));
    }

    public static bool operator <(HardCoinAmount left, HardCoinAmount right) => left.Units < right.Units;

    public static bool operator >(HardCoinAmount left, HardCoinAmount right) => left.Units > right.Units;

    public static bool operator <=(HardCoinAmount left, HardCoinAmount right) => left.Units <= right.Units;

    public static bool operator >=(HardCoinAmount left, HardCoinAmount right) => left.Units >= right.Units;

    public int CompareTo(HardCoinAmount other) => Units.CompareTo(other.Units);
}
