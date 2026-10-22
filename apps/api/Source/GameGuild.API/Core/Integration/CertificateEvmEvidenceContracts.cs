using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using GameGuild.Learning.Certificates;

namespace GameGuild.API.Core.Integration;

/// <summary>Persisted document scope supplied after the host's actor/tenant authorization check.</summary>
public sealed record CertificateEvmDocument(Guid Id, Guid OwnerUserId, Guid? TenantId, CertificateStatus Status, DateTime? ExpiresAt);

public enum CertificateEvmValidity { Unknown, Valid, Invalid }
public enum CertificateEvmEvidenceReadStatus
{
    Disabled, NoJournalEntry, Cached, NoRecordedTransaction, ProfileUnavailable, Observed, AmbiguousIdentity, Unavailable
}

/// <summary>Public operation projection. Excludes owner/tenant IDs, signer references, budgets, nonces, raw envelopes and leases.</summary>
public sealed record CertificateEvmOperationView(Guid OperationId, string Profile, long ChainId, string ContractAddress,
    string IssuerId, string RuntimeCodeSha256, EvmCertificateFinalityMode FinalityMode, int MinimumConfirmations,
    string CertificateHash, EvmCertificateOperationKind Kind, EvmCertificatePublicationStatus Status,
    string? TransactionHash, EvmCertificateJournalFailure? LastFailure, DateTimeOffset CreatedAt);

/// <summary>Local validity, durable publication state and fresh chain observation are independent facts.</summary>
public sealed record CertificateEvmEvidenceResponse(Guid CertificateId, CertificateStatus ApplicationStatus,
    bool LocallyValid, bool Configured, bool FreshProofRequested, CertificateEvmEvidenceReadStatus ReadStatus, CertificateEvmValidity Validity,
    CertificateEvmOperationView? Anchor, CertificateEvmOperationView? Revocation, EvmCertificateProofResult? ChainProof);

public interface ICertificateEvmEvidenceReader
{
    Task<CertificateEvmEvidenceResponse> ReadAsync(CertificateEvmDocument document, bool freshProof, CancellationToken cancellationToken);
}

public interface ICertificateEvmPublicationLookup
{
    Task<EvmCertificateJournalSnapshot?> FindAsync(Guid documentId, Guid ownerUserId, Guid? tenantId,
        EvmCertificateOperationKind kind, CancellationToken cancellationToken);
}
