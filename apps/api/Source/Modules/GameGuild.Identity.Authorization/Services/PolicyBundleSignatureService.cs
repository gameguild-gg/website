using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     ECDSA P-256 policy bundle signing and verification.
/// </summary>
/// <remarks>
///     <para>
///         <b>Envelope:</b> the bundle's <see cref="PolicyBundle.DigitalSignature"/> field stores a
///         versioned JSON envelope carrying the algorithm (<c>ES256</c>), signer key id, signing
///         time, SHA-256 content hash of the canonical payload and the P1363 (IEEE r||s)
///         signature over the signature subject. Unknown envelope versions or algorithms fail
///         closed during verification.
///     </para>
///     <para>
///         <b>Canonical payload:</b> deterministic JSON (fixed property order, shared serializer)
///         binding bundle id, name, tenant/global scope, bundle type, semantic version, policy
///         data, metadata, previous version lineage, effective dates and creator.
///     </para>
/// </remarks>
public sealed class PolicyBundleSignatureService(
    IOptions<PolicyBundleSigningOptions> options,
    ILogger<PolicyBundleSignatureService> logger
) : IPolicyBundleSignatureService
{
    /// <summary>Signature algorithm identifier recorded in the envelope.</summary>
    public const string AlgorithmIdentifier = "ES256";

    /// <summary>Current envelope version.</summary>
    public const string EnvelopeVersion = "1";

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly PolicyBundleSigningOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));

    private readonly ILogger<PolicyBundleSignatureService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public IReadOnlyList<string> ValidateBundleInputs(PolicyBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(bundle.Name))
        {
            errors.Add("Bundle name is required.");
        }

        if (string.IsNullOrWhiteSpace(bundle.Version))
        {
            errors.Add("Bundle semantic version is required.");
        }

        if (string.IsNullOrWhiteSpace(bundle.PolicyData))
        {
            errors.Add("PolicyData is required.");
        }
        else if (!IsValidJsonWithoutDuplicateProperties(bundle.PolicyData, out var policyDataError))
        {
            errors.Add($"PolicyData is not acceptable JSON: {policyDataError}");
        }

        if (!string.IsNullOrEmpty(bundle.Metadata)
            && !IsValidJsonWithoutDuplicateProperties(bundle.Metadata, out var metadataError))
        {
            errors.Add($"Metadata is not acceptable JSON: {metadataError}");
        }

        // Scope consistency: a bundle is either global (IsGlobal, no tenant) or tenant-scoped.
        if (bundle.IsGlobal && bundle.TenantId is not null)
        {
            errors.Add("A global bundle must not carry a tenant id.");
        }

        if (!bundle.IsGlobal && bundle.TenantId is null)
        {
            errors.Add("A tenant-scoped bundle must carry a tenant id.");
        }

        if (bundle.EffectiveFrom.HasValue && bundle.EffectiveUntil.HasValue
            && bundle.EffectiveFrom.Value >= bundle.EffectiveUntil.Value)
        {
            errors.Add("EffectiveFrom must be earlier than EffectiveUntil.");
        }

        var combinedBytes = Encoding.UTF8.GetByteCount(bundle.PolicyData ?? string.Empty)
            + Encoding.UTF8.GetByteCount(bundle.Metadata ?? string.Empty);
        if (combinedBytes > _options.MaxBundleSizeBytes)
        {
            errors.Add(
            $"Combined PolicyData and Metadata size ({combinedBytes} bytes) exceeds the limit of {_options.MaxBundleSizeBytes} bytes.");
        }

        if (bundle.CreatedBy == Guid.Empty)
        {
            errors.Add("CreatedBy is required.");
        }

        return errors;
    }

    /// <inheritdoc />
    public PolicyBundle SignBundle(PolicyBundle bundle, Guid signerUserId)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var errors = ValidateBundleInputs(bundle);
        if (errors.Count > 0)
        {
            throw new PolicyBundleSignatureException(
            $"Policy bundle '{bundle.Name}' failed signing validation: {string.Join(" ", errors)}");
        }

        var activeKey = _options.ActiveKey
            ?? throw new PolicyBundleSignatureException(
                "No active policy bundle signing key is configured; refusing to sign (fail closed).");

        var trustedKey = FindTrustedKeyForPrivateKey(activeKey);
        if (trustedKey is null)
        {
            throw new PolicyBundleSignatureException(
            $"Active signing key '{activeKey.KeyId}' does not match any trusted public key; refusing to sign (fail closed).");
        }

        var signedAt = DateTimeOffset.UtcNow;

        if (!trustedKey.IsTrustedForVerification(signedAt: signedAt, verifiedAt: signedAt))
        {
            throw new PolicyBundleSignatureException(
            $"Active signing key '{trustedKey.KeyId}' is expired or revoked; refusing to sign (fail closed).");
        }

        using var privateKey = ImportPrivateKey(activeKey.PrivateKeyPem);
        var payload = BuildCanonicalPayload(bundle);
        var payloadJson = JsonSerializer.Serialize(payload, CanonicalJsonOptions);
        var contentHash = ComputeContentHash(payloadJson);

        var envelope = new PolicyBundleSignatureEnvelope
        {
            Version = EnvelopeVersion,
            Algorithm = AlgorithmIdentifier,
            KeyId = trustedKey.KeyId,
            ContentHash = contentHash,
            SignedAt = signedAt.UtcDateTime,
            SignedBy = signerUserId,
            Signature = Convert.ToBase64String(
                privateKey.SignData(
                    BuildSignatureSubject(bundle.Id, trustedKey.KeyId, signedAt, contentHash),
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
        };

        bundle.ContentHash = contentHash;
        bundle.DigitalSignature = JsonSerializer.Serialize(envelope, CanonicalJsonOptions);
        bundle.SignedBy = trustedKey.KeyId;
        bundle.SignedAt = envelope.SignedAt;

        _logger.LogInformation(
            "Signed policy bundle {BundleId} ('{BundleName}') with key {KeyId}",
            bundle.Id, bundle.Name, trustedKey.KeyId);

        return bundle;
    }

    /// <inheritdoc />
    public PolicyBundleVerificationResult VerifyBundle(PolicyBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        if (!_options.EnforceSignatureVerification)
        {
            return new PolicyBundleVerificationResult
            {
                IsValid = true,
                Reason = "signature-enforcement-disabled"
            };
        }

        if (string.IsNullOrWhiteSpace(bundle.DigitalSignature))
        {
            return PolicyBundleVerificationResult.Invalid("Bundle carries no signature.");
        }

        PolicyBundleSignatureEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<PolicyBundleSignatureEnvelope>(
                bundle.DigitalSignature,
                CanonicalJsonOptions) ?? throw new JsonException();
        }
        catch (JsonException ex)
        {
            return PolicyBundleVerificationResult.Invalid($"Signature envelope is malformed: {ex.Message}");
        }

        if (!string.Equals(envelope.Version, EnvelopeVersion, StringComparison.Ordinal))
        {
            return PolicyBundleVerificationResult.Invalid(
            $"Unsupported signature envelope version '{envelope.Version}' (expected '{EnvelopeVersion}').");
        }

        if (!string.Equals(envelope.Algorithm, AlgorithmIdentifier, StringComparison.Ordinal))
        {
            return PolicyBundleVerificationResult.Invalid(
            $"Unsupported signature algorithm '{envelope.Algorithm}' (expected '{AlgorithmIdentifier}').");
        }

        var trustedKey = _options.TrustedKeys.FirstOrDefault(k =>
            string.Equals(k.KeyId, envelope.KeyId, StringComparison.Ordinal));
        if (trustedKey is null)
        {
            return PolicyBundleVerificationResult.Invalid($"Signature key '{envelope.KeyId}' is not trusted.");
        }

        var signedAt = new DateTimeOffset(envelope.SignedAt, TimeSpan.Zero);
        if (!trustedKey.IsTrustedForVerification(signedAt, DateTimeOffset.UtcNow))
        {
            return PolicyBundleVerificationResult.Invalid(
            $"Signature key '{envelope.KeyId}' is expired or revoked; signing time {signedAt:O}.");
        }

        var errors = ValidateBundleInputs(bundle);
        if (errors.Count > 0)
        {
            return PolicyBundleVerificationResult.Invalid(
            $"Bundle content failed signing-contract validation: {string.Join(" ", errors)}");
        }

        var payloadJson = JsonSerializer.Serialize(BuildCanonicalPayload(bundle), CanonicalJsonOptions);
        var contentHash = ComputeContentHash(payloadJson);
        if (!string.Equals(contentHash, envelope.ContentHash, StringComparison.Ordinal))
        {
            return PolicyBundleVerificationResult.Invalid(
            "Content hash mismatch: bundle content changed after signing.");
        }

        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(envelope.Signature);
        }
        catch (FormatException)
        {
            return PolicyBundleVerificationResult.Invalid("Signature is not valid base64.");
        }

        try
        {
            using var publicKey = ImportPublicKey(trustedKey.PublicKeyPem);
            var subject = BuildSignatureSubject(bundle.Id, envelope.KeyId, signedAt, envelope.ContentHash);
            var valid = publicKey.VerifyData(
                subject,
                signatureBytes,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return valid
                ? PolicyBundleVerificationResult.Valid(envelope.KeyId, contentHash)
                : PolicyBundleVerificationResult.Invalid("Signature verification failed.");
        }
        catch (CryptographicException ex)
        {
            return PolicyBundleVerificationResult.Invalid($"Signature could not be verified: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return PolicyBundleVerificationResult.Invalid($"Trusted key material is invalid: {ex.Message}");
        }
    }

    private TrustedPolicySigningKey? FindTrustedKeyForPrivateKey(PolicyBundleSigningKeyOptions activeKey)
    {
        using var privateKey = ImportPrivateKey(activeKey.PrivateKeyPem);
        var publicSpki = privateKey.ExportSubjectPublicKeyInfo();

        foreach (var candidate in _options.TrustedKeys)
        {
            if (candidate.RevokedAt is not null)
            {
                continue;
            }

            try
            {
                using var candidateKey = ImportPublicKey(candidate.PublicKeyPem);
                if (candidateKey.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(publicSpki))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                _logger.LogWarning(
                    "Trusted policy signing key {KeyId} has invalid key material and was skipped",
                    candidate.KeyId);
            }
        }

        return null;
    }

    private static PolicyBundleSignedPayload BuildCanonicalPayload(PolicyBundle bundle) => new()
    {
        PayloadVersion = EnvelopeVersion,
        BundleId = bundle.Id,
        Name = bundle.Name,
        Scope = bundle.IsGlobal ? "global" : $"tenant:{bundle.TenantId}",
        BundleType = bundle.BundleType.ToString(),
        Version = bundle.Version,
        PolicyData = bundle.PolicyData,
        Metadata = bundle.Metadata,
        PreviousVersionId = bundle.PreviousVersionId,
        EffectiveFrom = bundle.EffectiveFrom,
        EffectiveUntil = bundle.EffectiveUntil,
        CreatedBy = bundle.CreatedBy
    };

    private static byte[] BuildSignatureSubject(
        Guid bundleId,
        string keyId,
        DateTimeOffset signedAt,
        string contentHash)
        => Encoding.ASCII.GetBytes($"policy-bundle-signature|v{EnvelopeVersion}|{bundleId:N}|{keyId}|{signedAt:O}|{contentHash}");

    private static string ComputeContentHash(string canonicalJson)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexString(hash);
    }

    private static ECDsa ImportPrivateKey(string pem)
    {
        var key = ECDsa.Create();
        key.ImportFromPem(pem);
        return key;
    }

    private static ECDsa ImportPublicKey(string pem)
    {
        var key = ECDsa.Create();
        key.ImportFromPem(pem);
        return key;
    }

    private static bool IsValidJsonWithoutDuplicateProperties(string json, out string error)
    {
        error = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (ContainsDuplicateProperties(document.RootElement))
            {
                error = "duplicate object property names are not allowed.";
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool ContainsDuplicateProperties(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!seen.Add(property.Name))
                        {
                            return true;
                        }

                        if (ContainsDuplicateProperties(property.Value))
                        {
                            return true;
                        }
                    }

                return false;
            }
            case JsonValueKind.Array:
                return element.EnumerateArray().Any(ContainsDuplicateProperties);
            default:
                return false;
        }
    }

    /// <summary>Versioned signature envelope persisted in <see cref="PolicyBundle.DigitalSignature"/>.</summary>
    private sealed record PolicyBundleSignatureEnvelope
    {
        public string Version { get; init; } = EnvelopeVersion;

        public string Algorithm { get; init; } = AlgorithmIdentifier;

        public string KeyId { get; init; } = string.Empty;

        public string ContentHash { get; init; } = string.Empty;

        public DateTime SignedAt { get; init; }

        public Guid SignedBy { get; init; }

        public string Signature { get; init; } = string.Empty;
    }

    /// <summary>Canonical signed payload bound by the signature.</summary>
    private sealed record PolicyBundleSignedPayload
    {
        public string PayloadVersion { get; init; } = EnvelopeVersion;

        public Guid BundleId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Scope { get; init; } = string.Empty;

        public string BundleType { get; init; } = string.Empty;

        public string Version { get; init; } = string.Empty;

        public string PolicyData { get; init; } = string.Empty;

        public string? Metadata { get; init; }

        public Guid? PreviousVersionId { get; init; }

        public DateTime? EffectiveFrom { get; init; }

        public DateTime? EffectiveUntil { get; init; }

        public Guid CreatedBy { get; init; }
    }
}
