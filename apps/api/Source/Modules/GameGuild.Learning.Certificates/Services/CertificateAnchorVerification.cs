namespace GameGuild.Learning.Certificates;

/// <summary>
///     Outcome of querying the anchoring backend for the on-chain record of an issued certificate.
/// </summary>
public enum CertificateAnchorVerificationStatus
{
    /// <summary>
    ///     The recomputed canonical digest matches an anchor whose chain record (transaction,
    ///     payload integrity and latest revocation state) corroborates the certificate.
    /// </summary>
    Verified = 0,

    /// <summary>
    ///     An anchor exists for this certificate but its recorded digest differs from the
    ///     recomputed canonical digest, or the chain record failed to corroborate it. Fail-closed:
    ///     the certificate must not be treated as anchored.
    /// </summary>
    DigestMismatch = 1,

    /// <summary>
    ///     The certificate was anchored, but a revocation record on the anchoring backend
    ///     invalidates the anchor.
    /// </summary>
    Revoked = 2,

    /// <summary>
    ///     No anchor exists for the certificate's recomputed canonical digest (anchoring was
    ///     disabled at issuance, or the certificate predates anchoring).
    /// </summary>
    NotAnchored = 3,

    /// <summary>
    ///     The anchoring backend could not be queried. Fail-closed: verification errors are never
    ///     reported as <see cref="Verified" />.
    /// </summary>
    Unreachable = 4
}

/// <summary>
///     Result of on-chain anchor verification for a single issued certificate. Querying the chain
///     proves (or disproves) that the certificate's canonical digest was anchored.
/// </summary>
/// <remarks>
///     <para>
///         PRIVACY: contains only PII-free evidence — canonical digests, transaction/network
///         identifiers, block numbers and anchor timestamps. Recipient names and revocation
///         reasons never appear here.
///     </para>
/// </remarks>
public sealed record CertificateAnchorVerification(
    CertificateAnchorVerificationStatus Status,
    string CertificateId,
    string? Digest,
    string? AnchoredDigest,
    string? TransactionHash,
    string? Network,
    long? BlockNumber,
    DateTime? AnchoredAt,
    string? Detail = null)
{
    /// <summary>Creates a successful verification carrying the on-chain anchor evidence.</summary>
    public static CertificateAnchorVerification VerifiedAnchor(
        string certificateId,
        string digest,
        string anchoredDigest,
        string transactionHash,
        string network,
        long? blockNumber,
        DateTime anchoredAt) => new(
            CertificateAnchorVerificationStatus.Verified,
            certificateId,
            digest,
            anchoredDigest,
            transactionHash,
            network,
            blockNumber,
            anchoredAt);

    /// <summary>Creates a mismatch result: the anchored digest does not corroborate the certificate.</summary>
    public static CertificateAnchorVerification Mismatch(
        string certificateId,
        string? digest,
        string? anchoredDigest,
        string? transactionHash = null,
        string? detail = null) => new(
            CertificateAnchorVerificationStatus.DigestMismatch,
            certificateId,
            digest,
            anchoredDigest,
            transactionHash,
            null,
            null,
            null,
            detail ?? "The anchored digest does not match the certificate's recomputed canonical digest.");

    /// <summary>Creates a revoked result: an anchor exists but a revocation record invalidates it.</summary>
    public static CertificateAnchorVerification RevokedAnchor(
        string certificateId,
        string digest,
        string anchoredDigest,
        string transactionHash) => new(
            CertificateAnchorVerificationStatus.Revoked,
            certificateId,
            digest,
            anchoredDigest,
            transactionHash,
            null,
            null,
            null,
            "The certificate anchor has a revocation record on the anchoring backend.");

    /// <summary>Creates a not-anchored result: no anchor exists for the recomputed digest.</summary>
    public static CertificateAnchorVerification NotAnchored(
        string certificateId,
        string? digest = null) => new(
            CertificateAnchorVerificationStatus.NotAnchored,
            certificateId,
            digest,
            null,
            null,
            null,
            null,
            null,
            "No anchor exists for this certificate's canonical digest (anchoring may have been disabled at issuance).");

    /// <summary>Creates an unreachable result: the anchoring backend could not be queried (fail closed).</summary>
    public static CertificateAnchorVerification BackendUnreachable(
        string certificateId,
        string? digest,
        string detail) => new(
            CertificateAnchorVerificationStatus.Unreachable,
            certificateId,
            digest,
            null,
            null,
            null,
            null,
            null,
            detail);

    /// <summary>
    ///     True only when the on-chain record proves the certificate's recomputed canonical digest
    ///     was anchored and is still corroborated by the chain state.
    /// </summary>
    public bool IsVerified => Status == CertificateAnchorVerificationStatus.Verified;
}
