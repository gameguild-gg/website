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
}
