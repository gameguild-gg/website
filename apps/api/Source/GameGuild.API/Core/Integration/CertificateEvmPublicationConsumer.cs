using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.Identity.Authentication;
using GameGuild.Learning.Certificates;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.Core.Integration;

/// <summary>Transfers trusted outbox intents to the independent idempotent journal; never signs or contacts RPC.</summary>
public sealed class CertificateEvmPublicationConsumer(ApplicationDbContext context, EvmCertificateJournal journal,
    EvmCertificatePublicationNetwork configuredNetwork) : IIntegrationEventHandler<CertificateEvmAnchorRequestedV1>,
    IIntegrationEventHandler<CertificateEvmRevocationRequestedV1>
{
    public async Task HandleAsync(CertificateEvmAnchorRequestedV1 requested, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ValidateEnvelope(requested, requested.DocumentId, requested.OwnerUserId, requested.OriginalTenantId,
            CertificateEvmEvidenceProducer.AnchorEventId(requested.DocumentId));
        await RequireCertificateAsync(requested.DocumentId, requested.OwnerUserId, requested.OriginalTenantId, cancellationToken).ConfigureAwait(false);
        if (!MatchesConfiguredNetwork(requested))
        {
            throw new InvalidOperationException("The original certificate publication profile is not configured in this consumer.");
        }
        CertificateEvmEvidenceProducer.ValidateIssuer(requested.CanonicalIssuer);
        await journal.EnqueueAsync(new(configuredNetwork, requested.DocumentId, requested.OwnerUserId, requested.OriginalTenantId,
            CertificateEvmEvidenceProducer.ProducerName, requested.CertificateHash, EvmCertificateOperationKind.Anchor), cancellationToken).ConfigureAwait(false);
    }

    public async Task HandleAsync(CertificateEvmRevocationRequestedV1 requested, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ValidateEnvelope(requested, requested.DocumentId, requested.OwnerUserId, requested.OriginalTenantId,
            CertificateEvmEvidenceProducer.RevocationEventId(requested.DocumentId));
        if (requested.AnchorEventId != CertificateEvmEvidenceProducer.AnchorEventId(requested.DocumentId) ||
            requested.CausationId != requested.AnchorEventId)
        {
            throw new InvalidOperationException("The original certificate publication reference is invalid.");
        }
        await RequireCertificateAsync(requested.DocumentId, requested.OwnerUserId, requested.OriginalTenantId, cancellationToken).ConfigureAwait(false);
        var original = await FindOriginalAsync(requested, cancellationToken).ConfigureAwait(false);
        if (original is null)
        {
            // The anchor may not have reached its consumer yet. Recover its captured identity from the same durable outbox.
            var message = await context.Set<OutboxMessage>().AsNoTracking()
                .SingleOrDefaultAsync(m => m.EventId == requested.AnchorEventId, cancellationToken).ConfigureAwait(false);
            if (message is null || DurableEventSerializer.Deserialize(message) is not CertificateEvmAnchorRequestedV1 anchor ||
                anchor.DocumentId != requested.DocumentId || anchor.OwnerUserId != requested.OwnerUserId || anchor.OriginalTenantId != requested.OriginalTenantId)
            {
                throw new InvalidOperationException("The original certificate publication identity is unavailable; operator review is required.");
            }
            await HandleAsync(anchor, cancellationToken).ConfigureAwait(false);
            original = await FindOriginalAsync(requested, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The original certificate publication identity could not be persisted.");
        }
        // A changed issuer/profile, recipient name or revocation reason must never create another digest.
        await journal.EnqueueAsync(new(original.Network, original.DocumentId, original.OwnerUserId, original.TenantId,
            CertificateEvmEvidenceProducer.ProducerName, original.CertificateHash, EvmCertificateOperationKind.Revoke), cancellationToken).ConfigureAwait(false);
    }

    private Task<EvmCertificateJournalSnapshot?> FindOriginalAsync(CertificateEvmRevocationRequestedV1 requested, CancellationToken ct) =>
        journal.FindByDocumentAsync(CertificateEvmEvidenceProducer.ProducerName, requested.DocumentId,
            requested.OwnerUserId, requested.OriginalTenantId, EvmCertificateOperationKind.Anchor, ct);

    private async Task RequireCertificateAsync(Guid id, Guid owner, Guid? tenant, CancellationToken ct)
    {
        var certificate = await context.Set<Certificate>().IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false);
        if (certificate is null || certificate.UserId != owner || certificate.TenantId != tenant)
        {
            throw new InvalidOperationException("The certificate publication owner or tenant scope does not match the persisted document.");
        }
    }

    private bool MatchesConfiguredNetwork(CertificateEvmAnchorRequestedV1 e) => e.Profile == configuredNetwork.Profile &&
        e.ChainId == configuredNetwork.ChainId && e.ContractAddress == configuredNetwork.ContractAddress &&
        e.IssuerId == configuredNetwork.IssuerId && e.RuntimeCodeSha256 == configuredNetwork.RuntimeCodeSha256 &&
        e.FinalityMode == configuredNetwork.FinalityMode && e.MinimumConfirmations == configuredNetwork.MinimumConfirmations;

    private static void ValidateEnvelope(IDurableIntegrationEvent e, Guid document, Guid owner, Guid? tenant, Guid expectedId)
    {
        DurableIntegrationEventValidator.Validate(e);
        if (document == Guid.Empty || owner == Guid.Empty || tenant == Guid.Empty || e.EventId != expectedId ||
            e.AggregateType != CertificateEvmEvidenceProducer.AggregateName || e.AggregateId != document.ToString("D") ||
            e.TenantId != (tenant ?? DurableIntegrationEventTenants.Platform))
        {
            throw new InvalidOperationException("The durable certificate publication identity is invalid.");
        }
    }
}
