namespace GameGuild.Identity.Authorization;

/// <summary>
///     Configuration options for policy bundle signing and signature verification.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fail-closed default:</b> <see cref="EnforceSignatureVerification"/> defaults to <c>true</c>.
///         Published policy bundles without a valid signature are never served to the dynamic
///         authorization policy provider unless an operator explicitly disables enforcement
///         (local development only).
///     </para>
///     <para>
///         Trusted keys support validity windows (<see cref="TrustedPolicySigningKey.NotBefore"/> /
///         <see cref="TrustedPolicySigningKey.NotAfter"/>), rotation (multiple trusted keys coexist;
///         each signature records the key that produced it) and immediate revocation
///         (<see cref="TrustedPolicySigningKey.RevokedAt"/> — signatures produced by a revoked key
///         stop verifying at once).
///     </para>
/// </remarks>
public sealed class PolicyBundleSigningOptions
{
    /// <summary>
    ///     Configuration section name.
    /// </summary>
    public const string SectionName = "Authorization:PolicyBundleSigning";

    /// <summary>
    ///     Gets or sets a value indicating whether signature verification is enforced.
    ///     Defaults to <c>true</c> (fail closed). Only disable for local development.
    /// </summary>
    public bool EnforceSignatureVerification { get; set; } = true;

    /// <summary>
    ///     Gets or sets the maximum combined size (policy data + metadata) accepted for a bundle, in bytes.
    /// </summary>
    public int MaxBundleSizeBytes { get; set; } = 1024 * 1024; // 1 MiB

    /// <summary>
    ///     Gets or sets the trusted public keys. Signatures are verified against the key recorded
    ///     in the signature envelope; untrusted, expired or revoked keys fail verification.
    /// </summary>
    public List<TrustedPolicySigningKey> TrustedKeys { get; set; } = [];

    /// <summary>
    ///     Gets or sets the active signing key (private). The public part of this key must be
    ///     present in <see cref="TrustedKeys"/>, otherwise signing fails closed.
    /// </summary>
    public PolicyBundleSigningKeyOptions? ActiveKey { get; set; }
}

/// <summary>
///     A trusted policy-signing public key with validity window and revocation support.
/// </summary>
public sealed record TrustedPolicySigningKey
{
    /// <summary>
    ///     Gets the stable identifier of the key recorded in signature envelopes.
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    ///     Gets the ECDSA P-256 public key in PEM form (SUBJECT PUBLIC KEY INFO).
    /// </summary>
    public required string PublicKeyPem { get; init; }

    /// <summary>
    ///     Gets the optional instant from which the key is trusted.
    /// </summary>
    public DateTimeOffset? NotBefore { get; init; }

    /// <summary>
    ///     Gets the optional instant after which the key is no longer trusted.
    /// </summary>
    public DateTimeOffset? NotAfter { get; init; }

    /// <summary>
    ///     Gets the optional revocation instant. Once reached, every signature made by this key
    ///     fails verification immediately, regardless of when it was produced.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>
    ///     Determines whether the key is trusted: the validity window must cover
    ///     <paramref name="signedAt"/>, and revocation is evaluated against
    ///     <paramref name="verifiedAt"/> (immediate revocation of all signatures).
    /// </summary>
    /// <param name="signedAt">The instant the signature was produced.</param>
    /// <param name="verifiedAt">The instant verification runs (normally now).</param>
    /// <returns>True when the key is trusted for this verification.</returns>
    public bool IsTrustedForVerification(DateTimeOffset signedAt, DateTimeOffset verifiedAt) =>
        (NotBefore is null || signedAt >= NotBefore.Value)
        && (NotAfter is null || signedAt <= NotAfter.Value)
        && (RevokedAt is null || verifiedAt < RevokedAt.Value);
}

/// <summary>
///     Signing key material (private) used by authorized signers.
/// </summary>
public sealed record PolicyBundleSigningKeyOptions
{
    /// <summary>
    ///     Gets the stable identifier of the key recorded in signature envelopes.
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    ///     Gets the ECDSA P-256 private key in PEM form (PKCS#8).
    /// </summary>
    public required string PrivateKeyPem { get; init; }
}

/// <summary>
///     Result of a policy bundle signature verification.
/// </summary>
public sealed record PolicyBundleVerificationResult
{
    /// <summary>
    ///     Gets a value indicating whether the bundle carries a valid signature
    ///     from a trusted, non-revoked key over its current content.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    ///     Gets the key identifier that verified the signature, when valid.
    /// </summary>
    public string? KeyId { get; init; }

    /// <summary>
    ///     Gets the hex-encoded SHA-256 content hash of the canonical signed payload, when computed.
    /// </summary>
    public string? ContentHash { get; init; }

    /// <summary>
    ///     Gets the failure or exemption reason (never null when <see cref="IsValid"/> is false).
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    ///     Creates a successful result.
    /// </summary>
    /// <param name="keyId">The verifying key identifier.</param>
    /// <param name="contentHash">The canonical payload content hash.</param>
    /// <returns>The successful verification result.</returns>
    public static PolicyBundleVerificationResult Valid(string keyId, string contentHash) =>
        new() { IsValid = true, KeyId = keyId, ContentHash = contentHash };

    /// <summary>
    ///     Creates a failed result with a reason.
    /// </summary>
    /// <param name="reason">The failure reason.</param>
    /// <returns>The failed verification result.</returns>
    public static PolicyBundleVerificationResult Invalid(string reason) =>
        new() { IsValid = false, Reason = reason };
}

/// <summary>
///     Thrown when a policy bundle operation must fail closed because a required signature is
///     missing or invalid, or because bundle inputs violate the signing contract.
/// </summary>
public sealed class PolicyBundleSignatureException(
    string message,
    Exception? innerException = null)
    : Exception(message, innerException);
