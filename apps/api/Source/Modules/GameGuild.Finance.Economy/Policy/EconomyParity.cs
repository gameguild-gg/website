namespace GameGuild.Finance.Economy.Policy;

public static class EconomyParity
{
    public const long HardCoinUnitsPerUsd = 100;
    public const long SoftCoinUnitsPerUsd = 100_000;
    public const long SoftCoinUnitsPerHardCoin = SoftCoinUnitsPerUsd / HardCoinUnitsPerUsd;
    public static readonly TimeSpan EarnedHardMaturity = TimeSpan.FromDays(120);
}
