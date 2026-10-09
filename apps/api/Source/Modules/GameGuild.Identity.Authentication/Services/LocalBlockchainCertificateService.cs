using GameGuild.Configuration.ApplicationLayer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Deterministic local/dev implementation of <see cref="IBlockchainCertificateService" />.
/// </summary>
/// <remarks>
///     <para>
///         SAFE DEFAULT SCOPE: this provider contacts NO external network and manages no keys.
///         It persists anchors in the existing <c>BlockchainCertificateAnchor</c> table through the
///         shared <c>IApplicationDbContext</c>. Only a SHA-256 hash of a canonicalized, PII-free
///         payload is ever recorded as the anchored digest (see
///         <see cref="BlockchainCertificateCanonicalizer" />); the stored <c>CertificateData</c>
///         column holds the canonical payload itself, which by construction contains no personal data.
///     </para>
///     <para>
///         REVOCATION SEMANTICS: revoking does not mutate the published digest. It (a) marks the
///         original anchor row revoked for local bookkeeping and (b) appends a SECOND anchor record
///         whose <c>CertificateType</c> is <c>"revocation:&lt;original certificate hash&gt;"</c> and whose
///         payload links back to the original anchor. <see cref="VerifyCertificateAsync" /> resolves the
///         LATEST anchor state, so a certificate is valid only while no revocation anchor exists for it.
///         The revocation reason stays in local storage only — it is never part of an anchored payload.
///     </para>
///     <para>
///         DETERMINISM: transaction hashes and block numbers are derived from the canonical payload,
///         making anchor results reproducible in development and tests. DEFERRED OPS DECISIONS: real
///         chain selection, issuer key custody, transaction funding and finality policy are
///         deliberately out of scope — see <c>BlockchainCertificateOptions</c> remarks and
///         docs/api/blockchain-certificate-anchoring.md.
///     </para>
/// </remarks>
public sealed class LocalBlockchainCertificateService(
    IApplicationDbContext context,
    BlockchainCertificateOptions options,
    ILogger<LocalBlockchainCertificateService> logger) : IBlockchainCertificateService
{
    /// <summary>
    ///     Prefix used on the revocation anchor's <c>CertificateType</c> to link it to the
    ///     certificate hash it revokes.
    /// </summary>
    public const string RevocationCertificateTypePrefix = "revocation:";

    private const string AnchorTransactionScope = "local-anchor";
    private const string RevocationTransactionScope = "local-revocation";

    /// <inheritdoc />
    public async Task<BlockchainAnchorResult> AnchorCertificateAsync(Guid userId, string certificateData, string certificateType)
    {
        var canonicalPayload = BlockchainCertificateCanonicalizer.CanonicalizeIssuancePayload(certificateData);
        if (canonicalPayload is null)
        {
            logger.LogWarning(
                "Rejected certificate anchoring for user {UserId}: payload is not canonical JSON with the required keys ({Keys}); nothing was anchored",
                userId,
                string.Join(", ", BlockchainCertificateCanonicalizer.IdKey, BlockchainCertificateCanonicalizer.IssuedAtKey,
                    BlockchainCertificateCanonicalizer.IssuerKey, BlockchainCertificateCanonicalizer.RecipientHashKey,
                    BlockchainCertificateCanonicalizer.VersionKey));

            return BlockchainCertificateAnchorResult.Failure(
                "Certificate payload must be a JSON object containing exactly the canonical keys " +
                "'id', 'issued-at', 'issuer', 'recipient-hash' and 'version' (unknown fields are dropped; personal data is never anchored).");
        }

        var certificateHash = BlockchainCertificateCanonicalizer.ComputeSha256Hex(canonicalPayload);
        var transactionHash = BlockchainCertificateCanonicalizer.ComputeDeterministicTransactionHash(AnchorTransactionScope, canonicalPayload);

        var anchors = context.Set<BlockchainCertificateAnchor>();
        var existing = await anchors
            .FirstOrDefaultAsync(anchor => anchor.CertificateHash == certificateHash)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            // Deterministic idempotency: the same canonical payload always maps to the same anchor.
            logger.LogDebug("Certificate hash {CertificateHash} is already anchored (tx {TransactionHash})", certificateHash, existing.TransactionHash);

            return BlockchainCertificateAnchorResult.Success(
                existing.TransactionHash,
                existing.BlockchainNetwork,
                existing.CertificateHash,
                existing.BlockNumber,
                existing.AnchoredAt);
        }

        var anchor = new BlockchainCertificateAnchor
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CertificateType = certificateType,
            CertificateHash = certificateHash,
            CertificateData = canonicalPayload,
            TransactionHash = transactionHash,
            BlockchainNetwork = options.LocalNetworkName,
            BlockNumber = ComputeDeterministicBlockNumber(certificateHash),
            AnchoredAt = SystemClock.UtcNow,
            IsRevoked = false,
            Metadata = BuildAnchorMetadata()
        };

        anchors.Add(anchor);
        await context.SaveChangesAsync().ConfigureAwait(false);

        logger.LogInformation(
            "Anchored certificate for user {UserId} on network {Network} (tx {TransactionHash})",
            userId, anchor.BlockchainNetwork, anchor.TransactionHash);

        return BlockchainCertificateAnchorResult.Success(
            anchor.TransactionHash,
            anchor.BlockchainNetwork,
            anchor.CertificateHash,
            anchor.BlockNumber,
            anchor.AnchoredAt);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyCertificateAsync(string certificateHash, string transactionHash)
    {
        if (string.IsNullOrWhiteSpace(certificateHash) || string.IsNullOrWhiteSpace(transactionHash))
        {
            return false;
        }

        var normalizedHash = certificateHash.ToLowerInvariant();
        var anchor = await FindByCertificateHashAsync(normalizedHash).ConfigureAwait(false);

        if (anchor is null || !string.Equals(anchor.TransactionHash, transactionHash, StringComparison.Ordinal))
        {
            return false;
        }

        // Integrity: the stored canonical payload must still hash to the anchored digest.
        if (!string.Equals(
                BlockchainCertificateCanonicalizer.ComputeSha256Hex(anchor.CertificateData),
                anchor.CertificateHash,
                StringComparison.Ordinal))
        {
            return false;
        }

        // Latest anchor state: a revocation record referencing this certificate invalidates it.
        var isRevoked = await context.Set<BlockchainCertificateAnchor>()
            .AnyAsync(candidate => candidate.CertificateType == RevocationCertificateTypePrefix + normalizedHash)
            .ConfigureAwait(false);

        if (isRevoked || anchor.IsRevoked)
        {
            return false;
        }

        return anchor.ExpiresAt is null || anchor.ExpiresAt.Value > SystemClock.UtcNow;
    }

    /// <inheritdoc />
    public async Task<List<BlockchainCertificateAnchor>> GetUserCertificatesAsync(Guid userId)
    {
        return await context.Set<BlockchainCertificateAnchor>()
            .Where(anchor => anchor.UserId == userId)
            .OrderByDescending(anchor => anchor.AnchoredAt)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> RevokeCertificateAsync(string certificateHash, string reason)
    {
        if (string.IsNullOrWhiteSpace(certificateHash))
        {
            throw new ArgumentException("Certificate hash is required.", nameof(certificateHash));
        }

        var normalizedHash = certificateHash.ToLowerInvariant();
        var anchor = await FindByCertificateHashAsync(normalizedHash).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"No anchored certificate matches hash '{normalizedHash}'. Only anchored certificates can be revoked.");

        if (anchor.IsRevoked && !string.IsNullOrEmpty(anchor.RevocationTransactionHash))
        {
            // Deterministic idempotency: revoking twice yields the original revocation transaction.
            return anchor.RevocationTransactionHash;
        }

        var revokedAt = SystemClock.UtcNow;
        var revocationPayload = BlockchainCertificateCanonicalizer.BuildRevocationPayload(anchor.Id, normalizedHash, revokedAt);
        var revocationTransactionHash = BlockchainCertificateCanonicalizer
            .ComputeDeterministicTransactionHash(RevocationTransactionScope, revocationPayload);

        var anchors = context.Set<BlockchainCertificateAnchor>();
        var revocationAnchor = new BlockchainCertificateAnchor
        {
            Id = Guid.NewGuid(),
            UserId = anchor.UserId,
            CertificateType = RevocationCertificateTypePrefix + normalizedHash,
            CertificateHash = BlockchainCertificateCanonicalizer.ComputeSha256Hex(revocationPayload),
            CertificateData = revocationPayload,
            TransactionHash = revocationTransactionHash,
            BlockchainNetwork = options.LocalNetworkName,
            BlockNumber = ComputeDeterministicBlockNumber(revocationPayload),
            AnchoredAt = revokedAt,
            IsRevoked = false,
            Metadata = BuildAnchorMetadata()
        };

        anchors.Add(revocationAnchor);

        anchor.IsRevoked = true;
        anchor.RevokedAt = revokedAt;
        anchor.RevocationReason = reason; // Local bookkeeping only — never part of an anchored payload.
        anchor.RevocationTransactionHash = revocationTransactionHash;
        anchors.Update(anchor);

        await context.SaveChangesAsync().ConfigureAwait(false);

        logger.LogInformation(
            "Recorded revocation anchor for certificate hash {CertificateHash} (tx {TransactionHash})",
            normalizedHash, revocationTransactionHash);

        return revocationTransactionHash;
    }

    /// <inheritdoc />
    public Task<VerifiableCredential> GenerateVerifiableCredentialAsync(Guid userId, string credentialType, Dictionary<string, object> claims)
    {
        // Signing verifiable credentials requires issuer key custody, which is a deferred
        // operations decision (see BlockchainCertificateOptions remarks).
        throw new NotSupportedException(
            "Generating signed verifiable credentials requires issuer key custody decisions that are deferred; " +
            "hash anchoring via AnchorCertificateAsync is the supported path.");
    }

    private Task<BlockchainCertificateAnchor?> FindByCertificateHashAsync(string certificateHash)
    {
        return context.Set<BlockchainCertificateAnchor>()
            .FirstOrDefaultAsync(anchor => anchor.CertificateHash == certificateHash);
    }

    private static long ComputeDeterministicBlockNumber(string source)
    {
        var digest = BlockchainCertificateCanonicalizer.ComputeSha256Hex(source)[..16];

        return Convert.ToInt64(digest, 16) & long.MaxValue;
    }

    private static string BuildAnchorMetadata()
    {
        return $$"""
            {"provider":"{{BlockchainCertificateOptions.ProviderLocal}}","canonical-algorithm":"sha256-sorted-json-v1","finality":"local-only"}
            """;
    }
}
