using System.Numerics;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Treasury;

public sealed record TreasuryBufferPolicy(
    PolicyVersion Version,
    TreasuryBufferRule ChargebackRefund,
    TreasuryBufferRule PayoutSettlement,
    TreasuryBufferRule HardOperatingLiquidity,
    TreasuryBufferRule AdEstimateVariance,
    TreasuryBufferRule FraudLoss,
    TreasuryBufferRule ProviderFx,
    TreasuryBufferRule SoftOperatingLiquidity,
    DateTimeOffset ObservedAt,
    DateTimeOffset ExpiresAt,
    string Owner)
{
    private const int PpmScale = 1_000_000;

    public ReserveBufferPosition Calculate(
        ReserveLiabilityPosition liabilities,
        TreasuryBufferExposure exposure,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(liabilities);
        ArgumentNullException.ThrowIfNull(exposure);
        if (ObservedAt > now || ExpiresAt <= now || string.IsNullOrWhiteSpace(Owner))
            throw new ReserveInputUnknownException("Treasury buffer policy is stale or invalid.");
        var hardBasis = ReserveFormula.HardFaceValueUsdMinor(liabilities.OutstandingHardUnits);
        var softBasis = ReserveFormula.SoftFaceValueUsdNanos(liabilities.OutstandingSoftUnits);
        return new ReserveBufferPosition(
            Evaluate(ChargebackRefund, hardBasis, exposure.ChargebackRefundUsdMinor),
            Evaluate(PayoutSettlement, hardBasis, exposure.PayoutSettlementUsdMinor),
            Evaluate(HardOperatingLiquidity, hardBasis, exposure.HardOperatingLiquidityUsdMinor),
            Evaluate(AdEstimateVariance, softBasis, exposure.AdEstimateVarianceUsdNanos),
            Evaluate(FraudLoss, softBasis, exposure.FraudLossUsdNanos),
            Evaluate(ProviderFx, softBasis, exposure.ProviderFxUsdNanos),
            Evaluate(SoftOperatingLiquidity, softBasis, exposure.SoftOperatingLiquidityUsdNanos));
    }

    private static long Evaluate(TreasuryBufferRule rule, long basis, long observed)
    {
        if (rule is null || rule.AbsoluteFloor < 0 || rule.PercentageFloorPpm is < 0 or >= PpmScale || observed < 0)
            throw new ReserveInputUnknownException("Treasury buffer evidence or rule is invalid.");
        var percentage = CeilingDivide((BigInteger)basis * rule.PercentageFloorPpm, PpmScale);
        return (long)BigInteger.Max(observed, BigInteger.Max(rule.AbsoluteFloor, percentage));
    }

    private static BigInteger CeilingDivide(BigInteger numerator, BigInteger denominator) =>
        BigInteger.DivRem(numerator, denominator, out var remainder) + (remainder.IsZero ? 0 : 1);

}
