using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.AdRewards;

public sealed record AdRewardQuote(
    WalletId WalletId,
    IdempotencyKey IdempotencyKey,
    string Network,
    PolicyVersion PolicyVersion,
    long ImpressionCount,
    long EstimatedNetEcpmUsdNanos,
    int ContractedRevenueSharePpm,
    int SafetyBufferPpm,
    long FixedSoftCoinsPerUsd,
    long RewardSoftUnits,
    Int128 PreviousRemainder,
    Int128 NextRemainder,
    string InputFingerprint);

public sealed class AdRewardRationalAccumulator
{
    public const long SoftCoinsPerUsd = 100_000;
    public static readonly Int128 CanonicalDenominator =
        (Int128)1_000 * 1_000_000 * 1_000_000 * 1_000_000_000;

    private readonly object _gate = new();
    private readonly Dictionary<WalletId, Int128> _remainders = [];
    private readonly Dictionary<string, AdRewardQuote> _quotes = new(StringComparer.Ordinal);

    public AdRewardQuote Accrue(
        WalletId walletId,
        IdempotencyKey idempotencyKey,
        AdNetworkPolicy policy,
        long impressionCount)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(impressionCount);
        var fingerprint = Fingerprint(walletId, policy, impressionCount);

        lock (_gate)
        {
            if (_quotes.TryGetValue(idempotencyKey.Value, out var existing))
            {
                if (existing.InputFingerprint == fingerprint) return existing;
                throw new AdRewardIdempotencyConflictException(
                    "The ad reward idempotency key is already bound to different inputs.");
            }

            var quote = BuildQuote(
                walletId, idempotencyKey, policy, impressionCount,
                _remainders.GetValueOrDefault(walletId), fingerprint);
            _remainders[walletId] = quote.NextRemainder;
            _quotes.Add(idempotencyKey.Value, quote);
            return quote;
        }
    }

    public AdRewardQuote Preview(
        WalletId walletId,
        IdempotencyKey idempotencyKey,
        AdNetworkPolicy policy,
        long impressionCount)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(impressionCount);
        var fingerprint = Fingerprint(walletId, policy, impressionCount);
        lock (_gate)
        {
            if (_quotes.TryGetValue(idempotencyKey.Value, out var existing))
            {
                if (existing.InputFingerprint == fingerprint) return existing;
                throw new AdRewardIdempotencyConflictException(
                    "The ad reward idempotency key is already bound to different inputs.");
            }
            return BuildQuote(
                walletId, idempotencyKey, policy, impressionCount,
                _remainders.GetValueOrDefault(walletId), fingerprint);
        }
    }

    public Int128 RemainderFor(WalletId walletId)
    {
        lock (_gate) return _remainders.GetValueOrDefault(walletId);
    }

    public static AdRewardQuote Calculate(
        WalletId walletId,
        IdempotencyKey idempotencyKey,
        AdNetworkPolicy policy,
        long impressionCount,
        Int128 previousRemainder)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(impressionCount);
        if (previousRemainder < 0 || previousRemainder >= CanonicalDenominator)
            throw new ArgumentOutOfRangeException(nameof(previousRemainder));
        return BuildQuote(
            walletId,
            idempotencyKey,
            policy,
            impressionCount,
            previousRemainder,
            Fingerprint(walletId, policy, impressionCount));
    }

    private static string Fingerprint(WalletId walletId, AdNetworkPolicy policy, long impressionCount)
    {
        var canonical = string.Join('|',
            walletId.Value.ToString("N"),
            policy.Network,
            policy.Version.Value.ToString(CultureInfo.InvariantCulture),
            policy.EstimatedNetEcpmUsdNanos.ToString(CultureInfo.InvariantCulture),
            policy.ContractedRevenueSharePpm.ToString(CultureInfo.InvariantCulture),
            policy.SafetyBufferPpm.ToString(CultureInfo.InvariantCulture),
            policy.MaximumRewardSoftUnits.ToString(CultureInfo.InvariantCulture),
            impressionCount.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static AdRewardQuote BuildQuote(
        WalletId walletId,
        IdempotencyKey idempotencyKey,
        AdNetworkPolicy policy,
        long impressionCount,
        Int128 previous,
        string fingerprint)
    {
        var contribution = checked(
            (Int128)policy.EstimatedNetEcpmUsdNanos *
            policy.ContractedRevenueSharePpm *
            (1_000_000 - policy.SafetyBufferPpm) *
            SoftCoinsPerUsd *
            impressionCount);
        var numerator = checked(contribution + previous);
        var reward = numerator / CanonicalDenominator;
        var next = numerator % CanonicalDenominator;
        var rewardUnits = (long)reward;
        if (rewardUnits > policy.MaximumRewardSoftUnits)
            throw new AdRewardLimitExceededException("Ad reward exceeds the policy session cap.");
        return new AdRewardQuote(
            walletId,
            idempotencyKey,
            policy.Network,
            policy.Version,
            impressionCount,
            policy.EstimatedNetEcpmUsdNanos,
            policy.ContractedRevenueSharePpm,
            policy.SafetyBufferPpm,
            SoftCoinsPerUsd,
            rewardUnits,
            previous,
            next,
            fingerprint);
    }
}

public sealed class AdRewardIdempotencyConflictException(string message) : InvalidOperationException(message);
public sealed class AdRewardLimitExceededException(string message) : InvalidOperationException(message);
