namespace GameGuild.Configuration.ApplicationLayer;

/// <summary>Explicit chain and deployed-registry identity for certificate evidence.</summary>
/// <remarks>These settings alone do not activate a provider or prove deployment/finality.</remarks>
public sealed class EvmCertificateNetworkOptions
{
    public long ChainId { get; set; } = 11155111;
    public string RpcEndpoint { get; set; } = string.Empty;
    public string ContractAddress { get; set; } = string.Empty;
    public string IssuerId { get; set; } = string.Empty;

    /// <summary>SHA-256 of actual deployed runtime bytes, including configured immutable values.</summary>
    public string ExpectedRuntimeCodeSha256 { get; set; } = string.Empty;

    public EvmCertificateFinalityMode FinalityMode { get; set; } = EvmCertificateFinalityMode.Finalized;
    public int MinimumConfirmations { get; set; }
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public int MaximumResponseBytes { get; set; } = 1024 * 1024;

    /// <summary>Returns configuration errors without echoing endpoints or credentials.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (ChainId <= 0)
        {
            errors.Add("ChainId must be positive.");
        }
        if (!Uri.TryCreate(RpcEndpoint, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(endpoint.UserInfo) ||
            !string.IsNullOrEmpty(endpoint.Fragment))
        {
            errors.Add("RpcEndpoint must be an absolute HTTPS URI without user information or fragments.");
        }

        if (!IsNonzeroHexData(ContractAddress, 20))
        {
            errors.Add("ContractAddress must be a nonzero 20-byte EVM address.");
        }
        if (!IsNonzeroHexData(IssuerId, 32))
        {
            errors.Add("IssuerId must be a nonzero bytes32 public identifier.");
        }
        var codeHash = ExpectedRuntimeCodeSha256 ?? string.Empty;
        if (codeHash.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            codeHash = codeHash[2..];
        }
        if (codeHash.Length != 64 || !codeHash.All(Uri.IsHexDigit))
        {
            errors.Add("ExpectedRuntimeCodeSha256 must contain exactly 32 bytes of hexadecimal data.");
        }

        if (!Enum.IsDefined(FinalityMode))
        {
            errors.Add("FinalityMode is unsupported.");
        }
        if (MinimumConfirmations < 0 ||
            (FinalityMode == EvmCertificateFinalityMode.Confirmations && MinimumConfirmations == 0))
        {
            errors.Add("MinimumConfirmations must be positive when the confirmation policy is selected.");
        }

        if (ChainId == 11155111 && FinalityMode != EvmCertificateFinalityMode.Finalized)
        {
            errors.Add("Sepolia requires the explicit finalized-block policy; there is no confirmation-count fallback.");
        }

        if (RequestTimeout < TimeSpan.FromMilliseconds(10) || RequestTimeout > TimeSpan.FromMinutes(2))
        {
            errors.Add("RequestTimeout must be between 10 milliseconds and 2 minutes.");
        }

        if (MaximumResponseBytes < 1024 || MaximumResponseBytes > 4 * 1024 * 1024)
        {
            errors.Add("MaximumResponseBytes must be between 1 KiB and 4 MiB.");
        }

        return errors;
    }

    private static bool IsNonzeroHexData(string? value, int byteCount) =>
        value is not null && value.Length == 2 + byteCount * 2 &&
        value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
        value.Skip(2).All(Uri.IsHexDigit) && value.Skip(2).Any(character => character != '0');
}

public enum EvmCertificateFinalityMode
{
    Finalized,
    Confirmations
}
