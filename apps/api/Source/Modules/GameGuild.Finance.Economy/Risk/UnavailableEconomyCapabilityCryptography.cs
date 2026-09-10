namespace GameGuild.Finance.Economy.Risk;

public sealed class UnavailableEconomyCapabilityCryptography
    : ICapabilityReceiptSigner, ICapabilityPolicySigner, ICapabilityPolicySignatureVerifier
{
    public ValueTask<CapabilityReceiptSignature> SignAsync(
        string canonicalPayload,
        CancellationToken cancellationToken) =>
        ValueTask.FromException<CapabilityReceiptSignature>(
            new EconomyCryptographyUnavailableException(
                "Economy receipt signing is disabled until an asymmetric KMS adapter is configured."));

    public ValueTask<bool> VerifyAsync(
        string canonicalPayload,
        string keyId,
        string signature,
        CancellationToken cancellationToken) => ValueTask.FromResult(false);
}

public sealed class EconomyCryptographyUnavailableException(string message) : InvalidOperationException(message);
