using System.Numerics;
using GameGuild.Finance.Economy.Policy;

namespace GameGuild.Finance.Economy.Reserves;

public static class ReserveFormula
{
    private const long UsdNanosPerUsd = 1_000_000_000;
    private const long UsdNanosPerCent = 10_000_000;
    private const int PpmScale = 1_000_000;

    public static long HardFaceValueUsdMinor(long outstandingHardUnits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(outstandingHardUnits);
        return outstandingHardUnits;
    }

    public static long SoftFaceValueUsdNanos(long outstandingSoftUnits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(outstandingSoftUnits);
        return CeilingDivide(
            (BigInteger)outstandingSoftUnits * UsdNanosPerUsd,
            EconomyParity.SoftCoinUnitsPerUsd);
    }

    public static long RequiredHardReserveUsdMinor(
        long hardFaceValueUsdMinor,
        long chargebackRefundBufferUsdMinor,
        long payoutSettlementBufferUsdMinor,
        long operatingLiquidityBufferUsdMinor)
    {
        EnsureNonNegative(
            hardFaceValueUsdMinor,
            chargebackRefundBufferUsdMinor,
            payoutSettlementBufferUsdMinor,
            operatingLiquidityBufferUsdMinor);
        return ToLong(
            (BigInteger)hardFaceValueUsdMinor + chargebackRefundBufferUsdMinor +
            payoutSettlementBufferUsdMinor + operatingLiquidityBufferUsdMinor);
    }

    public static long StressedUnitCostUsdNanos(
        long currentProviderCostUsdNanos,
        long trailingHighPercentileCostUsdNanos,
        long providerFxStressCostUsdNanos)
    {
        EnsureNonNegative(
            currentProviderCostUsdNanos,
            trailingHighPercentileCostUsdNanos,
            providerFxStressCostUsdNanos);
        return Math.Max(
            currentProviderCostUsdNanos,
            Math.Max(trailingHighPercentileCostUsdNanos, providerFxStressCostUsdNanos));
    }

    public static long MinimumServicePriceSoftUnits(long stressedUnitCostUsdNanos, int targetGrossMarginPpm)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stressedUnitCostUsdNanos);
        if (targetGrossMarginPpm is < 0 or >= PpmScale)
            throw new ArgumentOutOfRangeException(nameof(targetGrossMarginPpm));
        if (stressedUnitCostUsdNanos == 0) return 0;

        return CeilingDivide(
            (BigInteger)stressedUnitCostUsdNanos * EconomyParity.SoftCoinUnitsPerUsd * PpmScale,
            (BigInteger)UsdNanosPerUsd * (PpmScale - targetGrossMarginPpm));
    }

    public static long StressedExpectedRedemptionCostUsdNanos(
        long outstandingSoftUnits,
        long unreservedSoftUnits,
        long irreversibleInFlightProviderCostUsdNanos,
        IReadOnlyCollection<ReserveServiceObservation> services,
        DateTimeOffset now)
    {
        EnsureNonNegative(
            outstandingSoftUnits,
            unreservedSoftUnits,
            irreversibleInFlightProviderCostUsdNanos);
        ArgumentNullException.ThrowIfNull(services);

        var enabled = new Dictionary<string, (ReserveServiceObservation Service, long Cost)>(StringComparer.Ordinal);
        BigInteger reservedTotal = 0;
        foreach (var service in services)
        {
            if (service is null ||
                string.IsNullOrWhiteSpace(service.ServiceCode) ||
                service.CurrentServicePriceSoftUnits <= 0 ||
                service.ReservedSoftUnits < 0 ||
                service.ObservedAt > now ||
                service.ExpiresAt <= now ||
                service.ExpiresAt <= service.ObservedAt ||
                service.CurrentProviderCostUsdNanos < 0 ||
                service.TrailingHighPercentileCostUsdNanos < 0 ||
                service.ProviderFxStressCostUsdNanos < 0)
                throw new ReserveInputUnknownException("Service reserve input is missing, stale, or invalid.");

            if (!service.Enabled)
            {
                if (service.ReservedSoftUnits > 0)
                    throw new ReserveInputUnknownException("A disabled service still has an open authorization.");
                continue;
            }

            var code = service.ServiceCode.Trim();
            var cost = StressedUnitCostUsdNanos(
                service.CurrentProviderCostUsdNanos,
                service.TrailingHighPercentileCostUsdNanos,
                service.ProviderFxStressCostUsdNanos);
            if (!enabled.TryAdd(code, (service, cost)))
                throw new ReserveInputUnknownException($"Service reserve input {code} is duplicated.");
            reservedTotal += service.ReservedSoftUnits;
        }

        if (outstandingSoftUnits > 0 && enabled.Count == 0)
            throw new ReserveInputUnknownException("No enabled service reserve input is available.");
        if ((BigInteger)unreservedSoftUnits + reservedTotal != outstandingSoftUnits)
            throw new ReserveInputUnknownException("Reserved and unreserved soft units must equal outstanding soft units.");

        BigInteger result = irreversibleInFlightProviderCostUsdNanos;
        if (unreservedSoftUnits > 0)
        {
            var worst = enabled.Values.Aggregate((left, right) =>
                (BigInteger)left.Cost * right.Service.CurrentServicePriceSoftUnits >=
                (BigInteger)right.Cost * left.Service.CurrentServicePriceSoftUnits
                    ? left
                    : right);
            result += CeilingDivideBigInteger(
                (BigInteger)unreservedSoftUnits * worst.Cost,
                worst.Service.CurrentServicePriceSoftUnits);
        }

        foreach (var (service, cost) in enabled.Values)
        {
            if (service.ReservedSoftUnits == 0) continue;
            result += CeilingDivideBigInteger(
                (BigInteger)service.ReservedSoftUnits * cost,
                service.CurrentServicePriceSoftUnits);
        }

        return ToLong(result);
    }

    public static long RequiredSoftReserveUsdNanos(
        long softFaceValueUsdNanos,
        long stressedExpectedRedemptionCostUsdNanos,
        long adEstimateVarianceBufferUsdNanos,
        long fraudLossBudgetUsdNanos,
        long providerFxBufferUsdNanos,
        long operatingLiquidityBufferUsdNanos)
    {
        EnsureNonNegative(
            softFaceValueUsdNanos,
            stressedExpectedRedemptionCostUsdNanos,
            adEstimateVarianceBufferUsdNanos,
            fraudLossBudgetUsdNanos,
            providerFxBufferUsdNanos,
            operatingLiquidityBufferUsdNanos);
        var total = (BigInteger)Math.Max(softFaceValueUsdNanos, stressedExpectedRedemptionCostUsdNanos) +
                    adEstimateVarianceBufferUsdNanos + fraudLossBudgetUsdNanos +
                    providerFxBufferUsdNanos + operatingLiquidityBufferUsdNanos;
        return CeilingDivide(total, UsdNanosPerCent) * UsdNanosPerCent;
    }

    private static long CeilingDivide(BigInteger numerator, BigInteger denominator) =>
        ToLong(CeilingDivideBigInteger(numerator, denominator));

    private static BigInteger CeilingDivideBigInteger(BigInteger numerator, BigInteger denominator)
    {
        return BigInteger.DivRem(numerator, denominator, out var remainder) + (remainder.IsZero ? 0 : 1);
    }

    private static long ToLong(BigInteger value)
    {
        if (value > long.MaxValue)
            throw new OverflowException("Reserve arithmetic exceeded the supported unit range.");
        return (long)value;
    }

    private static void EnsureNonNegative(params long[] values)
    {
        if (values.Any(value => value < 0)) throw new ArgumentOutOfRangeException(nameof(values));
    }
}
