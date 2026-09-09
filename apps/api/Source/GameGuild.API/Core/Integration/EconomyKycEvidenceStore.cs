using GameGuild.Compliance.KYC;
using GameGuild.Economy.Risk;

namespace GameGuild.API.Core.Integration;

/// <summary>Connects the shared KYC module to the GameGuild economy compliance ledger.</summary>
public sealed class EconomyKycEvidenceStore(IComplianceEvidenceStore store) : IKycEvidenceStore
{
    public async ValueTask<KycEvidenceIngestionResult> IngestAsync(KycEvidenceSubmission evidence, CancellationToken cancellationToken)
    {
        var envelope = ComplianceEvidenceEnvelope.Create(
            evidence.Provider, evidence.Environment, evidence.ProviderEventId, evidence.TenantId,
            evidence.SubjectHash, evidence.Version, evidence.Result switch
            {
                KycEvidenceResult.Approved => ComplianceEvidenceResult.Approved,
                KycEvidenceResult.Rejected => ComplianceEvidenceResult.Rejected,
                KycEvidenceResult.NeedsReview => ComplianceEvidenceResult.NeedsReview,
                KycEvidenceResult.Unavailable => ComplianceEvidenceResult.Unavailable,
                _ => throw new ArgumentOutOfRangeException(nameof(evidence))
            }, evidence.IssuedAt, evidence.ExpiresAt, evidence.PolicyVersion, evidence.PayloadHash,
            evidence.SignatureVerified, evidence.RawObjectReference, evidence.ReceivedAt, evidence.JurisdictionCode);
        var result = await store.IngestAsync(envelope, cancellationToken).ConfigureAwait(false);
        return new KycEvidenceIngestionResult(result.Status switch
        {
            ComplianceEvidenceIngestionStatus.Published => KycEvidenceIngestionStatus.Published,
            ComplianceEvidenceIngestionStatus.Duplicate => KycEvidenceIngestionStatus.Duplicate,
            ComplianceEvidenceIngestionStatus.Deferred => KycEvidenceIngestionStatus.Deferred,
            ComplianceEvidenceIngestionStatus.Rejected => KycEvidenceIngestionStatus.Rejected,
            _ => throw new InvalidOperationException("Unsupported compliance ingestion status.")
        }, result.EvidenceId);
    }
}
