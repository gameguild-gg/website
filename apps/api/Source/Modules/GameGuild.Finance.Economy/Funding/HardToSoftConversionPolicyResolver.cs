using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Funding;

public sealed record HardToSoftConversionPolicyAuthorization(
    string JurisdictionCode,
    long PolicyVersion,
    string PolicyHash,
    long FeeHardCoinUnits,
    long MaximumHardCoinUnitsPerDay,
    int DecisionLifetimeSeconds);

public interface IHardToSoftConversionPolicyResolver
{
    ValueTask<HardToSoftConversionPolicyAuthorization> ResolveAsync(
        Guid tenantId,
        Guid actorId,
        long principalHardCoinUnits,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken);
}

public sealed class HardToSoftConversionPolicyResolver(
    IEconomyJurisdictionResolver jurisdictionResolver,
    IEconomyCapabilityPolicyStore policyStore,
    ICapabilityPolicySignatureVerifier signatureVerifier) : IHardToSoftConversionPolicyResolver
{
    private const int PpmScale = 1_000_000;

    public async ValueTask<HardToSoftConversionPolicyAuthorization> ResolveAsync(
        Guid tenantId,
        Guid actorId,
        long principalHardCoinUnits,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(principalHardCoinUnits);
        var jurisdiction = await jurisdictionResolver.ResolveAsync(
            tenantId, actorId, null, null, evaluatedAt, cancellationToken);
        var policy = await policyStore.CurrentAsync(
            tenantId,
            EconomyValueMovementCapability.ConvertHardToSoft,
            jurisdiction.JurisdictionCode,
            cancellationToken);
        if (policy is null ||
            policy.TenantId != tenantId ||
            policy.Capability != EconomyValueMovementCapability.ConvertHardToSoft ||
            policy.JurisdictionCode != jurisdiction.JurisdictionCode ||
            policy.State != EconomyCapabilityPolicyState.Active ||
            policy.EffectiveAt > evaluatedAt ||
            policy.ExpiresAt <= evaluatedAt ||
            !string.Equals(Hash(policy.CanonicalPayload), policy.PayloadHash, StringComparison.Ordinal) ||
            !await signatureVerifier.VerifyAsync(
                policy.CanonicalPayload,
                policy.KeyId,
                policy.Signature,
                cancellationToken))
        {
            throw Disabled("A current signed conversion policy is required.");
        }

        try
        {
            using var document = JsonDocument.Parse(policy.CanonicalPayload);
            var root = document.RootElement;
            var feePpm = root.GetProperty("conversionFeePpm").GetInt32();
            var maximum = root.GetProperty("maximumHardCoinUnitsPerDay").GetInt64();
            var lifetime = root.GetProperty("decisionLifetimeSeconds").GetInt32();
            if (feePpm is < 0 or >= PpmScale || maximum <= 0 || lifetime is < 30 or > 900)
                throw new JsonException("Conversion policy values are outside their safe ranges.");

            var fee = ToLongCeiling((BigInteger)principalHardCoinUnits * feePpm, PpmScale);
            if ((BigInteger)principalHardCoinUnits + fee > maximum)
                throw Disabled("The requested conversion exceeds the signed daily limit.");
            return new HardToSoftConversionPolicyAuthorization(
                jurisdiction.JurisdictionCode,
                policy.Version,
                policy.PayloadHash,
                fee,
                maximum,
                lifetime);
        }
        catch (EconomySelfServiceCommandRejectedException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or
                                           FormatException or OverflowException or KeyNotFoundException)
        {
            throw Disabled("The active conversion policy payload is invalid.", exception);
        }
    }

    internal static long ToLongCeiling(BigInteger numerator, int denominator)
    {
        var value = (numerator + denominator - 1) / denominator;
        if (value > long.MaxValue) throw new OverflowException();
        return (long)value;
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static EconomySelfServiceCommandRejectedException Disabled(
        string message,
        Exception? innerException = null) => new(message, innerException);
}
