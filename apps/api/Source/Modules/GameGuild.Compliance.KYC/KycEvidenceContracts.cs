namespace GameGuild.Compliance.KYC;

public enum KycEvidenceResult { Approved = 1, Rejected = 2, NeedsReview = 3, Unavailable = 4 }
public enum KycEvidenceIngestionStatus { Published = 1, Duplicate = 2, Deferred = 3, Rejected = 4 }

/// <summary>Provider-neutral evidence passed to the host's compliance ledger, without economy dependencies.</summary>
public sealed record KycEvidenceSubmission(
    string Provider,
    string Environment,
    string ProviderEventId,
    Guid TenantId,
    string SubjectHash,
    long Version,
    KycEvidenceResult Result,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    long PolicyVersion,
    string PayloadHash,
    bool SignatureVerified,
    string RawObjectReference,
    DateTimeOffset ReceivedAt,
    string? JurisdictionCode = null);

public sealed record KycEvidenceIngestionResult(KycEvidenceIngestionStatus Status, Guid? EvidenceId);

public interface IKycEvidenceStore
{
    ValueTask<KycEvidenceIngestionResult> IngestAsync(KycEvidenceSubmission evidence, CancellationToken cancellationToken);
}

/// <summary>Never report verified evidence as published when the host has no compliance ledger configured.</summary>
public sealed class UnavailableKycEvidenceStore : IKycEvidenceStore
{
    public ValueTask<KycEvidenceIngestionResult> IngestAsync(KycEvidenceSubmission evidence, CancellationToken cancellationToken)
        => throw new SumSubNotConfiguredException("A durable KYC evidence store must be configured before activating SumSub orchestration.");
}

public sealed class KycEvidenceConflictException(string message) : InvalidOperationException(message);
