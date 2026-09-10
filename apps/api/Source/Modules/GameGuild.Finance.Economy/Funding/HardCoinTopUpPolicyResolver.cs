using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.Funding;

public sealed record HardCoinTopUpPolicyAuthorization(
    string JurisdictionCode,
    long PolicyVersion,
    string PolicyHash,
    long HardCoinUnits,
    long UsdMinorUnits,
    string Provider);

public interface IHardCoinTopUpPolicyResolver
{
    ValueTask<HardCoinTopUpPolicyAuthorization> ResolveAsync(
        Guid tenantId,
        Guid actorId,
        long hardCoinUnits,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken);
}

public sealed class HardCoinTopUpPolicyResolver(
    IEconomyJurisdictionResolver jurisdictionResolver,
    IEconomyCapabilityPolicyStore policyStore,
    ICapabilityPolicySignatureVerifier signatureVerifier) : IHardCoinTopUpPolicyResolver
{
    private const string SupportedProvider = "stripe";
    private const long UsdMinorUnitsPerUsd = 100;

    public async ValueTask<HardCoinTopUpPolicyAuthorization> ResolveAsync(
        Guid tenantId,
        Guid actorId,
        long hardCoinUnits,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hardCoinUnits);
        var jurisdiction = await jurisdictionResolver.ResolveAsync(
            tenantId, actorId, null, null, evaluatedAt, cancellationToken);
        var policy = await policyStore.CurrentAsync(
            tenantId,
            EconomyValueMovementCapability.ConfirmHardCoinFunding,
            jurisdiction.JurisdictionCode,
            cancellationToken);
        if (policy is null ||
            policy.TenantId != tenantId ||
            policy.Capability != EconomyValueMovementCapability.ConfirmHardCoinFunding ||
            policy.JurisdictionCode != jurisdiction.JurisdictionCode ||
            policy.State != EconomyCapabilityPolicyState.Active ||
            policy.EffectiveAt > evaluatedAt ||
            policy.ExpiresAt <= evaluatedAt ||
            !policy.ProviderReady ||
            !string.Equals(Hash(policy.CanonicalPayload), policy.PayloadHash, StringComparison.Ordinal) ||
            !await signatureVerifier.VerifyAsync(
                policy.CanonicalPayload,
                policy.KeyId,
                policy.Signature,
                cancellationToken))
        {
            throw Disabled("A current signed Stripe top-up policy is required.");
        }

        try
        {
            using var document = JsonDocument.Parse(policy.CanonicalPayload);
            var root = document.RootElement;
            var minimum = root.GetProperty("minimumHardCoinUnits").GetInt64();
            var maximum = root.GetProperty("maximumHardCoinUnits").GetInt64();
            var provider = root.GetProperty("provider").GetString();
            if (minimum <= 0 || maximum < minimum ||
                !string.Equals(provider, SupportedProvider, StringComparison.Ordinal))
                throw new JsonException("Top-up policy values are outside their safe ranges.");
            if (hardCoinUnits < minimum || hardCoinUnits > maximum)
                throw Disabled("The requested top-up is outside the signed amount window.");

            var usdNumerator = checked(hardCoinUnits * UsdMinorUnitsPerUsd);
            var usdMinorUnits = usdNumerator / Policy.EconomyParity.HardCoinUnitsPerUsd;

            return new HardCoinTopUpPolicyAuthorization(
                jurisdiction.JurisdictionCode,
                policy.Version,
                policy.PayloadHash,
                hardCoinUnits,
                usdMinorUnits,
                provider!);
        }
        catch (EconomySelfServiceCommandRejectedException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or
                                           FormatException or OverflowException or KeyNotFoundException)
        {
            throw Disabled("The active top-up policy payload is invalid.", exception);
        }
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static EconomySelfServiceCommandRejectedException Disabled(
        string message,
        Exception? innerException = null) => new(message, innerException);
}
