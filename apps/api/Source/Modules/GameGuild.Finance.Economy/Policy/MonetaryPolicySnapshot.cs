using System.Collections.ObjectModel;
using System.Numerics;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Policy;

public sealed record AdRewardPolicy
{
    public AdRewardPolicy(int safetyReservePpm, long maximumRewardSoftUnits)
    {
        EnsurePpm(safetyReservePpm, nameof(safetyReservePpm));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRewardSoftUnits);
        SafetyReservePpm = safetyReservePpm;
        MaximumRewardSoftUnits = maximumRewardSoftUnits;
    }

    public int SafetyReservePpm { get; }
    public long MaximumRewardSoftUnits { get; }

    internal static void EnsurePpm(int value, string parameterName)
    {
        if (value is < 0 or >= MonetaryPolicySnapshot.PpmScale)
            throw new ArgumentOutOfRangeException(parameterName);
    }
}

public sealed record EconomyOperationLimits
{
    public EconomyOperationLimits(
        long maximumHardTransferUnits,
        long maximumSoftSpendUnits,
        long maximumAdRewardSoftUnits)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumHardTransferUnits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSoftSpendUnits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumAdRewardSoftUnits);
        MaximumHardTransferUnits = maximumHardTransferUnits;
        MaximumSoftSpendUnits = maximumSoftSpendUnits;
        MaximumAdRewardSoftUnits = maximumAdRewardSoftUnits;
    }

    public long MaximumHardTransferUnits { get; }
    public long MaximumSoftSpendUnits { get; }
    public long MaximumAdRewardSoftUnits { get; }
}

public sealed record ServicePricePolicy
{
    public ServicePricePolicy(string serviceCode, long softUnits, long stressedCostMicrousd)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceCode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(softUnits);
        ArgumentOutOfRangeException.ThrowIfNegative(stressedCostMicrousd);
        ServiceCode = serviceCode.Trim();
        SoftUnits = softUnits;
        StressedCostMicrousd = stressedCostMicrousd;
    }

    public string ServiceCode { get; }
    public long SoftUnits { get; }
    public long StressedCostMicrousd { get; }
}

public sealed record MonetaryPolicySnapshot
{
    internal const int PpmScale = 1_000_000;
    private const long MicrousdPerUsd = 1_000_000;

    public MonetaryPolicySnapshot(
        PolicyVersion version,
        DateTimeOffset effectiveAt,
        DateTimeOffset? endsAt,
        int conversionFeePpm,
        int minimumServiceMarginPpm,
        AdRewardPolicy adRewards,
        EconomyOperationLimits limits,
        IReadOnlyCollection<ServicePricePolicy> servicePrices)
    {
        if (endsAt <= effectiveAt) throw new ArgumentException("Policy end must follow its effective time.", nameof(endsAt));
        AdRewardPolicy.EnsurePpm(conversionFeePpm, nameof(conversionFeePpm));
        AdRewardPolicy.EnsurePpm(minimumServiceMarginPpm, nameof(minimumServiceMarginPpm));
        ArgumentNullException.ThrowIfNull(adRewards);
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(servicePrices);

        var prices = new Dictionary<string, ServicePricePolicy>(StringComparer.Ordinal);
        foreach (var price in servicePrices)
        {
            ArgumentNullException.ThrowIfNull(price);
            if (!MeetsMinimumMargin(price, minimumServiceMarginPpm))
                throw new ArgumentException(
                    $"Service price {price.ServiceCode} does not meet the minimum gross margin.",
                    nameof(servicePrices));
            if (!prices.TryAdd(price.ServiceCode, price))
                throw new ArgumentException($"Service price {price.ServiceCode} is duplicated.", nameof(servicePrices));
        }

        Version = version;
        EffectiveAt = effectiveAt;
        EndsAt = endsAt;
        ConversionFeePpm = conversionFeePpm;
        MinimumServiceMarginPpm = minimumServiceMarginPpm;
        AdRewards = adRewards;
        Limits = limits;
        ServicePrices = new ReadOnlyDictionary<string, ServicePricePolicy>(prices);
    }

    public PolicyVersion Version { get; }
    public DateTimeOffset EffectiveAt { get; }
    public DateTimeOffset? EndsAt { get; }
    public int ConversionFeePpm { get; }
    public int MinimumServiceMarginPpm { get; }
    public AdRewardPolicy AdRewards { get; }
    public EconomyOperationLimits Limits { get; }
    public IReadOnlyDictionary<string, ServicePricePolicy> ServicePrices { get; }
    public TimeSpan EarnedHardMaturity => EconomyParity.EarnedHardMaturity;

    public long ConvertHardToSoft(long hardUnits)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hardUnits);
        var gross = (BigInteger)hardUnits * EconomyParity.SoftCoinUnitsPerHardCoin;
        return ToLong(gross * (PpmScale - ConversionFeePpm) / PpmScale);
    }

    public long QuoteAdRewardSoft(long estimatedGrossMicrousd)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(estimatedGrossMicrousd);
        var grossSoft = (BigInteger)estimatedGrossMicrousd * EconomyParity.SoftCoinUnitsPerUsd / MicrousdPerUsd;
        var reserved = grossSoft * (PpmScale - AdRewards.SafetyReservePpm) / PpmScale;
        var maximum = Math.Min(AdRewards.MaximumRewardSoftUnits, Limits.MaximumAdRewardSoftUnits);
        return Math.Min(ToLong(reserved), maximum);
    }

    private static bool MeetsMinimumMargin(ServicePricePolicy price, int minimumMarginPpm)
    {
        var revenueMicrousd = (BigInteger)price.SoftUnits * MicrousdPerUsd / EconomyParity.SoftCoinUnitsPerUsd;
        return (revenueMicrousd - price.StressedCostMicrousd) * PpmScale >= revenueMicrousd * minimumMarginPpm;
    }

    private static long ToLong(BigInteger value)
    {
        if (value > long.MaxValue)
            throw new OverflowException("Policy arithmetic exceeded the supported unit range.");
        return (long)value;
    }
}

public sealed class MonetaryPolicyCatalog
{
    private readonly object _gate = new();
    private readonly List<MonetaryPolicySnapshot> _policies = [];

    public IReadOnlyList<MonetaryPolicySnapshot> Policies
    {
        get
        {
            lock (_gate) return _policies.OrderBy(policy => policy.EffectiveAt).ToArray();
        }
    }

    public void Add(MonetaryPolicySnapshot policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        lock (_gate)
        {
            if (_policies.Any(existing => existing.Version == policy.Version))
                throw new InvalidOperationException($"Policy version {policy.Version.Value} already exists.");
            if (_policies.Any(existing => Overlaps(existing, policy)))
                throw new InvalidOperationException("Monetary policy effective windows cannot overlap.");
            _policies.Add(policy);
        }
    }

    public MonetaryPolicySnapshot Resolve(DateTimeOffset at)
    {
        lock (_gate)
        {
            return _policies.SingleOrDefault(policy =>
                       policy.EffectiveAt <= at && (policy.EndsAt is null || at < policy.EndsAt.Value)) ??
                   throw new InvalidOperationException("No monetary policy is effective at the requested time.");
        }
    }

    private static bool Overlaps(MonetaryPolicySnapshot left, MonetaryPolicySnapshot right) =>
        left.EffectiveAt < (right.EndsAt ?? DateTimeOffset.MaxValue) &&
        right.EffectiveAt < (left.EndsAt ?? DateTimeOffset.MaxValue);
}
