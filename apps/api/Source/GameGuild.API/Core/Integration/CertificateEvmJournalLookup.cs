using GameGuild.Identity.Authentication;

namespace GameGuild.API.Core.Integration;

/// <summary>One producer and an exact persisted document/owner/tenant scope; no arbitrary operation-ID lookup from HTTP.</summary>
public sealed class CertificateEvmJournalLookup(EvmCertificateJournal journal) : ICertificateEvmPublicationLookup
{
    public Task<EvmCertificateJournalSnapshot?> FindAsync(Guid documentId, Guid ownerUserId, Guid? tenantId,
        EvmCertificateOperationKind kind, CancellationToken cancellationToken) => journal.FindByDocumentAsync(
            CertificateEvmEvidenceProducer.ProducerName, documentId, ownerUserId, tenantId, kind, cancellationToken);
}
