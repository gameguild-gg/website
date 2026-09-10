using System.Security.Cryptography;
using System.Text;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Microsoft.Extensions.Options;

namespace GameGuild.Finance.Economy.Risk;

public sealed class EconomyKmsOptions
{
    public const string SectionName = "Modules:Economy:Cryptography:Kms";

    public bool Enabled { get; set; }
    public string KeyId { get; set; } = string.Empty;
    public string SigningAlgorithm { get; set; } = string.Empty;
}

public sealed class AwsKmsEconomyCryptography :
    ICapabilityReceiptSigner,
    ICapabilityPolicySigner,
    ICapabilityPolicySignatureVerifier
{
    private static readonly IReadOnlySet<string> AllowedAlgorithms = new HashSet<string>(StringComparer.Ordinal)
    {
        "ECDSA_SHA_256",
        "ECDSA_SHA_384",
        "ECDSA_SHA_512",
        "RSASSA_PSS_SHA_256",
        "RSASSA_PSS_SHA_384",
        "RSASSA_PSS_SHA_512"
    };

    private readonly IAmazonKeyManagementService _kms;
    private readonly EconomyKmsOptions _options;

    public AwsKmsEconomyCryptography(
        IAmazonKeyManagementService kms,
        IOptions<EconomyKmsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(kms);
        ArgumentNullException.ThrowIfNull(options);
        _kms = kms;
        _options = options.Value;
    }

    public async ValueTask<CapabilityReceiptSignature> SignAsync(
        string canonicalPayload,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPayload);
        EnsureConfigured();
        var algorithm = SigningAlgorithmSpec.FindValue(_options.SigningAlgorithm.Trim());
        using var digest = Digest(canonicalPayload);
        var response = await _kms.SignAsync(new SignRequest
        {
            KeyId = _options.KeyId.Trim(),
            Message = digest,
            MessageType = MessageType.DIGEST,
            SigningAlgorithm = algorithm
        }, cancellationToken);
        if (response.Signature is null || response.Signature.Length == 0 || string.IsNullOrWhiteSpace(response.KeyId))
            throw new EconomyCryptographyUnavailableException("AWS KMS did not return an asymmetric signature.");
        return new CapabilityReceiptSignature(
            response.KeyId.Trim(),
            $"{response.SigningAlgorithm.Value}:{Convert.ToBase64String(response.Signature.ToArray())}");
    }

    public async ValueTask<bool> VerifyAsync(
        string canonicalPayload,
        string keyId,
        string signature,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(canonicalPayload) ||
            string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(signature))
            return false;
        var separator = signature.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || separator == signature.Length - 1) return false;
        var algorithmName = signature[..separator];
        if (!AllowedAlgorithms.Contains(algorithmName)) return false;
        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(signature[(separator + 1)..]);
        }
        catch (FormatException)
        {
            return false;
        }

        try
        {
            using var digest = Digest(canonicalPayload);
            using var signatureStream = new MemoryStream(signatureBytes, writable: false);
            var response = await _kms.VerifyAsync(new VerifyRequest
            {
                KeyId = keyId.Trim(),
                Message = digest,
                MessageType = MessageType.DIGEST,
                Signature = signatureStream,
                SigningAlgorithm = SigningAlgorithmSpec.FindValue(algorithmName)
            }, cancellationToken);
            return response.SignatureValid;
        }
        catch (AmazonKeyManagementServiceException)
        {
            return false;
        }
    }

    private void EnsureConfigured()
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.KeyId) ||
            !AllowedAlgorithms.Contains(_options.SigningAlgorithm.Trim()))
            throw new EconomyCryptographyUnavailableException(
                "Economy asymmetric signing is disabled until a valid AWS KMS SIGN_VERIFY key is configured.");
    }

    private static MemoryStream Digest(string canonicalPayload) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload)), writable: false);
}
