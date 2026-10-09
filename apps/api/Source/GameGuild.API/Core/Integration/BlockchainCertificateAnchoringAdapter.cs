using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using GameGuild.Learning.Certificates;
using Microsoft.Extensions.Logging;

namespace GameGuild.API.Core.Integration;

/// <summary>
///     Connects Learning.Certificates' <see cref="ICertificateAnchoring" /> port to the platform's
///     config-gated <see cref="IBlockchainCertificateService" />. Registered by the API composition
///     root only when <c>BlockchainCertificates:Provider</c> is not <c>"none"</c>; the certificates
///     module otherwise keeps its no-op default.
/// </summary>
/// <remarks>
///     PRIVACY: the adapter publishes only the SHA-256 hash of a canonicalized, PII-free payload
///     (see <see cref="BlockchainCertificateCanonicalizer" />). The recipient name is reduced to a
///     digest before leaving this class; the local revocation reason is never published.
/// </remarks>
public sealed class BlockchainCertificateAnchoringAdapter(
    IBlockchainCertificateService blockchainCertificates,
    BlockchainCertificateOptions options,
    ILogger<BlockchainCertificateAnchoringAdapter> logger) : ICertificateAnchoring
{
    /// <summary>Certificate type label recorded on anchors for issued learning certificates.</summary>
    public const string CertificateType = "learning-certificate";

    private const int PayloadVersion = 1;

    /// <inheritdoc />
    public async Task AnchorIssuedCertificateAsync(Certificate certificate, CancellationToken cancellationToken = default)
    {
        var payload = BuildCanonicalPayload(certificate);

        var result = await blockchainCertificates
            .AnchorCertificateAsync(certificate.UserId, payload, CertificateType)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "Certificate {CertificateNumber} anchored on network {Network} (tx {TransactionHash})",
                certificate.CertificateNumber, result.Network, result.TransactionHash);
        }
        else
        {
            logger.LogWarning(
                "Certificate {CertificateNumber} was not anchored: {AnchorError} (non-fatal)",
                certificate.CertificateNumber, result.ErrorMessage ?? "unknown anchoring failure");
        }
    }

    /// <inheritdoc />
    public async Task RecordRevocationAsync(Certificate certificate, string reason, CancellationToken cancellationToken = default)
    {
        // The original certificate hash is recomputed deterministically from the same canonical
        // payload that was anchored at issuance, so the revocation links back to the right anchor.
        var certificateHash = BlockchainCertificateCanonicalizer.ComputeSha256Hex(BuildCanonicalPayload(certificate));

        try
        {
            var revocationTransactionHash = await blockchainCertificates
                .RevokeCertificateAsync(certificateHash, reason)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Certificate {CertificateNumber} revocation anchored (tx {TransactionHash})",
                certificate.CertificateNumber, revocationTransactionHash);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No anchored certificate", StringComparison.Ordinal))
        {
            // The certificate was issued while anchoring was disabled (or pre-dates it); nothing to revoke.
            logger.LogDebug(
                "Certificate {CertificateNumber} has no blockchain anchor to revoke; skipping revocation anchoring",
                certificate.CertificateNumber);
        }
    }

    private string BuildCanonicalPayload(Certificate certificate)
    {
        return BlockchainCertificateCanonicalizer.BuildIssuancePayload(
            certificate.Id,
            BlockchainCertificateCanonicalizer.ComputeSha256Hex(certificate.RecipientName),
            certificate.IssuedAt,
            options.Issuer,
            PayloadVersion);
    }
}
