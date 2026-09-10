namespace GameGuild.Finance.Economy.Money;

public readonly record struct UsdNanoAmount : IComparable<UsdNanoAmount>
{
    public UsdNanoAmount(long nanos)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nanos);
        Nanos = nanos;
    }

    public long Nanos { get; }

    public static UsdNanoAmount Zero => new(0);

    public static UsdNanoAmount operator +(UsdNanoAmount left, UsdNanoAmount right) => new(checked(left.Nanos + right.Nanos));

    public static UsdNanoAmount operator -(UsdNanoAmount left, UsdNanoAmount right)
    {
        if (right.Nanos > left.Nanos) throw new InvalidOperationException("USD nano amounts cannot underflow.");
        return new UsdNanoAmount(left.Nanos - right.Nanos);
    }

    public static UsdNanoAmount operator *(UsdNanoAmount amount, long factor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(factor);
        return new UsdNanoAmount(checked(amount.Nanos * factor));
    }

    public static bool operator <(UsdNanoAmount left, UsdNanoAmount right) => left.Nanos < right.Nanos;

    public static bool operator >(UsdNanoAmount left, UsdNanoAmount right) => left.Nanos > right.Nanos;

    public static bool operator <=(UsdNanoAmount left, UsdNanoAmount right) => left.Nanos <= right.Nanos;

    public static bool operator >=(UsdNanoAmount left, UsdNanoAmount right) => left.Nanos >= right.Nanos;

    public int CompareTo(UsdNanoAmount other) => Nanos.CompareTo(other.Nanos);
}
