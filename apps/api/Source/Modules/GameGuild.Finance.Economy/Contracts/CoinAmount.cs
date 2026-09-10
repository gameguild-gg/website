using GameGuild.Finance.Economy.Money;

namespace GameGuild.Finance.Economy.Contracts;

public readonly record struct CoinAmount
{
    public CoinAmount(CurrencyCode currency, long units)
    {
        if (!Enum.IsDefined(currency)) throw new ArgumentOutOfRangeException(nameof(currency));
        ArgumentOutOfRangeException.ThrowIfNegative(units);
        Currency = currency;
        Units = units;
    }

    public CurrencyCode Currency { get; }
    public long Units { get; }

    public static CoinAmount From(HardCoinAmount amount) => new(CurrencyCode.HardCoin, amount.Units);
    public static CoinAmount From(SoftCoinAmount amount) => new(CurrencyCode.SoftCoin, amount.Units);
}
