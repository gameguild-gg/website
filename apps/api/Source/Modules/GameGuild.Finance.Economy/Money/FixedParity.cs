namespace GameGuild.Finance.Economy.Money;

public static class FixedParity
{
    public const long HardCoinsPerUsd = 100;
    public const long SoftCoinsPerUsd = 100_000;
    public const long SoftCoinsPerHardCoin = SoftCoinsPerUsd / HardCoinsPerUsd;
    public const long UsdNanosPerUsd = 1_000_000_000;

    public static SoftCoinAmount ToSoft(HardCoinAmount hardCoinAmount) =>
        new(checked(hardCoinAmount.Units * SoftCoinsPerHardCoin));
}
