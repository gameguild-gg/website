using System.Numerics;
using GameGuild.Finance.Economy.Policy;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Integrations.AI;

public sealed record AiProviderTokenCost(
    long InputCostUsdNanos,
    long OutputCostUsdNanos,
    long TotalCostUsdNanos);

public sealed record AiProviderRateCard
{
    private const long TokensPerMillion = 1_000_000;

    public AiProviderRateCard(
        string version,
        AiProvider provider,
        string model,
        long inputTokenCostUsdNanosPerMillion,
        long outputTokenCostUsdNanosPerMillion,
        DateTimeOffset observedAt,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        if (!Enum.IsDefined(provider)) throw new ArgumentOutOfRangeException(nameof(provider));
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokenCostUsdNanosPerMillion);
        ArgumentOutOfRangeException.ThrowIfNegative(outputTokenCostUsdNanosPerMillion);
        if (inputTokenCostUsdNanosPerMillion == 0 && outputTokenCostUsdNanosPerMillion == 0)
            throw new ArgumentException("At least one token cost must be positive.", nameof(inputTokenCostUsdNanosPerMillion));
        if (expiresAt <= observedAt)
            throw new ArgumentException("Rate-card expiry must follow observation.", nameof(expiresAt));

        Version = version.Trim();
        Provider = provider;
        Model = model.Trim();
        InputTokenCostUsdNanosPerMillion = inputTokenCostUsdNanosPerMillion;
        OutputTokenCostUsdNanosPerMillion = outputTokenCostUsdNanosPerMillion;
        ObservedAt = observedAt;
        ExpiresAt = expiresAt;
    }

    public string Version { get; }
    public AiProvider Provider { get; }
    public string Model { get; }
    public long InputTokenCostUsdNanosPerMillion { get; }
    public long OutputTokenCostUsdNanosPerMillion { get; }
    public DateTimeOffset ObservedAt { get; }
    public DateTimeOffset ExpiresAt { get; }

    public AiProviderTokenCost CalculateCost(int inputTokens, int outputTokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(outputTokens);

        var input = CeilingDivide(
            (BigInteger)inputTokens * InputTokenCostUsdNanosPerMillion,
            TokensPerMillion);
        var output = CeilingDivide(
            (BigInteger)outputTokens * OutputTokenCostUsdNanosPerMillion,
            TokensPerMillion);
        return new AiProviderTokenCost(
            ToLong(input),
            ToLong(output),
            ToLong(input + output));
    }

    private static BigInteger CeilingDivide(BigInteger numerator, BigInteger denominator)
    {
        var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
        return quotient + (remainder.IsZero ? 0 : 1);
    }

    private static long ToLong(BigInteger value)
    {
        if (value > long.MaxValue)
            throw new OverflowException("AI provider-cost arithmetic exceeded the supported unit range.");
        return (long)value;
    }
}

public sealed record AiServicePriceSnapshot
{
    private const int PpmScale = 1_000_000;
    private const long UsdNanosPerUsd = 1_000_000_000;

    private AiServicePriceSnapshot(
        string serviceCode,
        AiProviderRateCard rateCard,
        int maximumInputTokens,
        int maximumOutputTokens,
        long currentProviderCostUsdNanos,
        long trailingHighPercentileCostUsdNanos,
        long providerFxStressCostUsdNanos,
        long stressedProviderCostUsdNanos,
        int minimumGrossMarginPpm,
        long priceSoftUnits,
        DateTimeOffset observedAt,
        DateTimeOffset expiresAt)
    {
        ServiceCode = serviceCode;
        RateCard = rateCard;
        MaximumInputTokens = maximumInputTokens;
        MaximumOutputTokens = maximumOutputTokens;
        CurrentProviderCostUsdNanos = currentProviderCostUsdNanos;
        TrailingHighPercentileCostUsdNanos = trailingHighPercentileCostUsdNanos;
        ProviderFxStressCostUsdNanos = providerFxStressCostUsdNanos;
        StressedProviderCostUsdNanos = stressedProviderCostUsdNanos;
        MinimumGrossMarginPpm = minimumGrossMarginPpm;
        PriceSoftUnits = priceSoftUnits;
        ObservedAt = observedAt;
        ExpiresAt = expiresAt;
    }

    public string ServiceCode { get; }
    public AiProviderRateCard RateCard { get; }
    public int MaximumInputTokens { get; }
    public int MaximumOutputTokens { get; }
    public long CurrentProviderCostUsdNanos { get; }
    public long TrailingHighPercentileCostUsdNanos { get; }
    public long ProviderFxStressCostUsdNanos { get; }
    public long StressedProviderCostUsdNanos { get; }
    public int MinimumGrossMarginPpm { get; }
    public long PriceSoftUnits { get; }
    public DateTimeOffset ObservedAt { get; }
    public DateTimeOffset ExpiresAt { get; }

    public static AiServicePriceSnapshot Create(
        string serviceCode,
        AiProviderRateCard rateCard,
        int maximumInputTokens,
        int maximumOutputTokens,
        long trailingHighPercentileCostUsdNanos,
        long providerFxStressCostUsdNanos,
        int minimumGrossMarginPpm,
        DateTimeOffset observedAt,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceCode);
        ArgumentNullException.ThrowIfNull(rateCard);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumInputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumOutputTokens);
        if (maximumInputTokens == 0 && maximumOutputTokens == 0)
            throw new ArgumentException("At least one token allowance must be positive.", nameof(maximumInputTokens));
        ArgumentOutOfRangeException.ThrowIfNegative(trailingHighPercentileCostUsdNanos);
        ArgumentOutOfRangeException.ThrowIfNegative(providerFxStressCostUsdNanos);
        if (expiresAt <= observedAt)
            throw new ArgumentException("Service-price expiry must follow observation.", nameof(expiresAt));

        var current = rateCard.CalculateCost(maximumInputTokens, maximumOutputTokens).TotalCostUsdNanos;
        var stressed = ReserveFormula.StressedUnitCostUsdNanos(
            current,
            trailingHighPercentileCostUsdNanos,
            providerFxStressCostUsdNanos);
        if (stressed == 0)
            throw new AiProviderCostUnknownException("A zero-cost service cannot be authorized.");
        var price = ReserveFormula.MinimumServicePriceSoftUnits(stressed, minimumGrossMarginPpm);

        return new AiServicePriceSnapshot(
            serviceCode.Trim(),
            rateCard,
            maximumInputTokens,
            maximumOutputTokens,
            current,
            trailingHighPercentileCostUsdNanos,
            providerFxStressCostUsdNanos,
            stressed,
            minimumGrossMarginPpm,
            price,
            observedAt,
            expiresAt);
    }

    public bool MeetsMargin(long priceSoftUnits)
    {
        if (priceSoftUnits <= 0) return false;
        var revenueUsdNanos = (BigInteger)priceSoftUnits * UsdNanosPerUsd /
                              EconomyParity.SoftCoinUnitsPerUsd;
        return (revenueUsdNanos - StressedProviderCostUsdNanos) * PpmScale >=
               revenueUsdNanos * MinimumGrossMarginPpm;
    }
}

public sealed class AiServiceRateCardCatalog
{
    private readonly object _gate = new();
    private readonly List<AiServicePriceSnapshot> _snapshots = [];

    public IReadOnlyList<AiServicePriceSnapshot> Snapshots
    {
        get
        {
            lock (_gate) return _snapshots.OrderBy(snapshot => snapshot.ObservedAt).ToArray();
        }
    }

    public void Publish(AiServicePriceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            if (_snapshots.Any(existing =>
                    existing.ServiceCode == snapshot.ServiceCode &&
                    existing.RateCard.Provider == snapshot.RateCard.Provider &&
                    existing.RateCard.Model == snapshot.RateCard.Model &&
                    existing.RateCard.Version == snapshot.RateCard.Version))
                throw new InvalidOperationException("The AI service rate-card version already exists.");
            _snapshots.Add(snapshot);
        }
    }

    public AiServicePriceSnapshot Resolve(
        string serviceCode,
        AiProvider provider,
        string model,
        DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceCode);
        if (!Enum.IsDefined(provider)) throw new ArgumentOutOfRangeException(nameof(provider));
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        lock (_gate)
        {
            var snapshot = _snapshots
                .Where(candidate =>
                    candidate.ServiceCode == serviceCode.Trim() &&
                    candidate.RateCard.Provider == provider &&
                    candidate.RateCard.Model == model.Trim() &&
                    candidate.ObservedAt <= at)
                .OrderByDescending(candidate => candidate.ObservedAt)
                .FirstOrDefault();
            if (snapshot is null)
                throw new AiProviderCostUnknownException("No AI service cost feed is available.");
            if (snapshot.ExpiresAt <= at || snapshot.RateCard.ExpiresAt <= at)
                throw new AiProviderCostStaleException("The AI service cost feed is stale.");
            return snapshot;
        }
    }
}

public sealed class AiProviderCostUnknownException(string message) : InvalidOperationException(message);
public sealed class AiProviderCostStaleException(string message) : InvalidOperationException(message);
