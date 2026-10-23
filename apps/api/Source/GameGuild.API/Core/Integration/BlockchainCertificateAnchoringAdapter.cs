using System.Text.Json;
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

    /// <inheritdoc />
    public async Task<CertificateAnchorVerification> VerifyAnchoringAsync(Certificate certificate, CancellationToken cancellationToken = default)
    {
        // Deterministic digest recomputation: identical to the anchoring path, so the anchor being
        // looked for is exactly the one AnchorIssuedCertificateAsync published.
        var payload = BuildCanonicalPayload(certificate);
        var digest = BlockchainCertificateCanonicalizer.ComputeSha256Hex(payload);

        List<BlockchainCertificateAnchor> userAnchors;
        try
        {
            userAnchors = await blockchainCertificates
                .GetUserCertificatesAsync(certificate.UserId)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Certificate {CertificateNumber} anchor verification could not query the anchoring backend (fail closed)",
                certificate.CertificateNumber);

            return CertificateAnchorVerification.BackendUnreachable(
                certificate.Id.ToString(),
                digest,
                "The anchoring backend could not be queried; on-chain verification is unavailable.");
        }

        var anchor = userAnchors.FirstOrDefault(candidate =>
            string.Equals(candidate.CertificateType, CertificateType, StringComparison.Ordinal)
            && string.Equals(candidate.CertificateHash, digest, StringComparison.OrdinalIgnoreCase));

        if (anchor is null)
        {
            // No anchor for the current digest. If an anchor exists that claims this certificate
            // (by canonical payload id) but carries a different digest, the certificate no longer
            // matches what was anchored — report the mismatch instead of a silent absence.
            var staleAnchor = userAnchors.FirstOrDefault(candidate =>
                string.Equals(candidate.CertificateType, CertificateType, StringComparison.Ordinal)
                && TryGetCanonicalPayloadCertificateId(candidate, out var anchoredCertificateId)
                && anchoredCertificateId == certificate.Id.ToString());

            if (staleAnchor is not null)
            {
                logger.LogWarning(
                    "Certificate {CertificateNumber} anchor digest mismatch: anchored {AnchoredDigest}, recomputed {RecomputedDigest}",
                    certificate.CertificateNumber, staleAnchor.CertificateHash, digest);

                return CertificateAnchorVerification.Mismatch(
                    certificate.Id.ToString(),
                    digest,
                    staleAnchor.CertificateHash,
                    staleAnchor.TransactionHash);
            }

            return CertificateAnchorVerification.NotAnchored(certificate.Id.ToString(), digest);
        }

        // Confirmations: the platform verification resolves the chain record for this digest and
        // transaction — payload integrity, latest revocation state and anchor validity.
        bool confirmed;
        try
        {
            confirmed = await blockchainCertificates
                .VerifyCertificateAsync(digest, anchor.TransactionHash)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Certificate {CertificateNumber} anchor verification failed while confirming the chain record (fail closed)",
                certificate.CertificateNumber);

            return CertificateAnchorVerification.BackendUnreachable(
                certificate.Id.ToString(),
                digest,
                "The anchoring backend could not confirm the chain record; on-chain verification is unavailable.");
        }

        if (confirmed)
        {
            logger.LogInformation(
                "Certificate {CertificateNumber} anchor verified on network {Network} (tx {TransactionHash}, block {BlockNumber})",
                certificate.CertificateNumber, anchor.BlockchainNetwork, anchor.TransactionHash, anchor.BlockNumber);

            return CertificateAnchorVerification.VerifiedAnchor(
                certificate.Id.ToString(),
                digest,
                anchor.CertificateHash,
                anchor.TransactionHash,
                anchor.BlockchainNetwork,
                anchor.BlockNumber,
                anchor.AnchoredAt);
        }

        if (anchor.IsRevoked)
        {
            return CertificateAnchorVerification.RevokedAnchor(
                certificate.Id.ToString(),
                digest,
                anchor.CertificateHash,
                anchor.TransactionHash);
        }

        logger.LogWarning(
            "Certificate {CertificateNumber} chain record did not corroborate the recomputed digest {Digest} (tx {TransactionHash})",
            certificate.CertificateNumber, digest, anchor.TransactionHash);

        return CertificateAnchorVerification.Mismatch(
            certificate.Id.ToString(),
            digest,
            anchor.CertificateHash,
            anchor.TransactionHash,
            "The chain record did not corroborate the certificate's recomputed canonical digest.");
    }

    private static bool TryGetCanonicalPayloadCertificateId(BlockchainCertificateAnchor anchor, out string certificateId)
    {
        certificateId = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(anchor.CertificateData);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (document.RootElement.TryGetProperty(BlockchainCertificateCanonicalizer.IdKey, out var idValue)
                && idValue.ValueKind == JsonValueKind.String)
            {
                var value = idValue.GetString();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    certificateId = value;

                    return true;
                }
            }
        }
        catch (JsonException)
        {
            // Non-canonical stored payloads cannot be tied to a certificate id; treat as no match.
        }

        return false;
    }
}
