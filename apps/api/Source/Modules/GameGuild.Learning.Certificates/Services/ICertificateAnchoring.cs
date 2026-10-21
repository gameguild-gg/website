namespace GameGuild.Learning.Certificates;

/// <summary>
/// Port for publishing auxiliary trust evidence (blockchain anchors) for issued certificates.
/// </summary>
/// <remarks>
///     <para>
///         Learning.Certificates must not depend on platform blockchain services directly, so it
///         exposes this module-local port. The default implementation is a no-op (anchoring disabled);
///         the API host wires a blockchain-backed adapter when
///         <c>BlockchainCertificates:Provider</c> is not <c>"none"</c>. Implementations must treat
///         anchoring as auxiliary evidence: the certificate lifecycle (issuance, verification,
///         revocation) proceeds unchanged regardless of anchoring outcomes.
///     </para>
///     <para>
///         Privacy contract: implementations must only publish a SHA-256 hash of a canonicalized,
///         PII-free payload — never recipient names or other personal data.
///     </para>
/// </remarks>
public interface ICertificateAnchoring
{
    /// <summary>
    ///     Publishes a trust anchor for a newly issued certificate.
    /// </summary>
    /// <param name="certificate">The certificate that was just issued.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task AnchorIssuedCertificateAsync(Certificate certificate, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Publishes a revocation record linking back to the anchor of the original certificate.
    /// </summary>
    /// <param name="certificate">The certificate that was revoked.</param>
    /// <param name="reason">The local revocation reason (must not be published verbatim).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task RecordRevocationAsync(Certificate certificate, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Queries the anchoring backend to prove (or disprove) that a certificate's canonical
    ///     digest was anchored on chain, and reports the on-chain evidence.
    /// </summary>
    /// <param name="certificate">The certificate whose anchor should be verified.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <remarks>
    ///     <para>
    ///         Implementations recompute the canonical SHA-256 digest the same way the anchoring
    ///         path does, look up the anchor for that digest, and confirm the chain record. The
    ///         result is fail-closed: backend errors must surface as
    ///         <see cref="CertificateAnchorVerificationStatus.Unreachable" />, never as verified.
    ///     </para>
    ///     <para>
    ///         This is a read-only query: verification must not mutate the certificate, the anchor
    ///         store, or the chain. Like the anchoring operations it is auxiliary evidence — but
    ///         unlike them its outcome is reported, so callers can distinguish a proof from an
    ///         absence of proof.
    ///     </para>
    /// </remarks>
    Task<CertificateAnchorVerification> VerifyAnchoringAsync(Certificate certificate, CancellationToken cancellationToken = default);
}
