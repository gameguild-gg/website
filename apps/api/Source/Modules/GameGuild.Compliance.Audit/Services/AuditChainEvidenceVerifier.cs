using System.Security.Cryptography;

namespace GameGuild.Compliance.Audit;

/// <summary>Verifies captured interval entries using the same canonical bytes as the audit writer.</summary>
public sealed class AuditChainEvidenceVerifier(ICryptographicSigningService signing)
{
    public bool VerifyEntry(TamperEvidentAuditLog entry) => VerifyEntryAndTimestamp(entry).IsValid;

    public (bool IsValid, DateTime SignedTimestampUtc) VerifyEntryAndTimestamp(TamperEvidentAuditLog entry)
    {
        var signedTimestampUtc = entry.Timestamp;
        if (entry.SequenceNumber < 1 || entry.TenantId is null || entry.TenantId == Guid.Empty || entry.Timestamp.Kind != DateTimeKind.Utc) { return (false, signedTimestampUtc); }
        try
        {
            var contentHash = signing.ComputeContentHash(TamperEvidentAuditService.SerializeContent(entry));
            // Older writers signed 100 ns timestamps before PostgreSQL discarded the final digit.
            // Recover only the ten original instants represented by the stored microsecond, and still require the signed hash.
            if (contentHash != entry.ContentHash && entry.Timestamp.Ticks % 10 == 0)
            {
                for (var digit = 1; digit <= 9; digit++)
                {
                    var candidate = entry.Timestamp.AddTicks(digit);
                    var candidateHash = signing.ComputeContentHash(TamperEvidentAuditService.SerializeContent(entry, candidate));
                    if (candidateHash != entry.ContentHash) { continue; }
                    contentHash = candidateHash;
                    signedTimestampUtc = candidate;
                    break;
                }
            }
            var chainHash = signing.ComputeChainHash(contentHash, entry.PreviousHash, entry.SequenceNumber);
            var verified = contentHash == entry.ContentHash && chainHash == entry.ChainHash &&
                signing.VerifySignature(chainHash, entry.DigitalSignature, entry.SigningKeyId);
            return (verified, signedTimestampUtc);
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException or InvalidOperationException)
        {
            return (false, signedTimestampUtc);
        }
    }
}
