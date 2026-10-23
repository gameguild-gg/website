namespace GameGuild.Learning.Certificates;

/// <summary>
///     Default <see cref="ICertificateAnchoring" /> implementation used while blockchain anchoring
///     is disabled. Performs no work and publishes nothing — existing deployment behavior.
/// </summary>
public sealed class NoOpCertificateAnchoring : ICertificateAnchoring
{
    /// <summary>
    ///     Shared stateless instance.
    /// </summary>
    public static NoOpCertificateAnchoring Instance { get; } = new();

    /// <inheritdoc />
    public Task AnchorIssuedCertificateAsync(Certificate certificate, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordRevocationAsync(Certificate certificate, string reason, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<CertificateAnchorVerification> VerifyAnchoringAsync(Certificate certificate, CancellationToken cancellationToken = default)
    {
        // Nothing was ever anchored through this implementation, so there is no on-chain record
        // to prove. Honest absence of proof — not an error, and never a confirmation.
        return Task.FromResult(CertificateAnchorVerification.NotAnchored(certificate.Id.ToString()));
    }
}
