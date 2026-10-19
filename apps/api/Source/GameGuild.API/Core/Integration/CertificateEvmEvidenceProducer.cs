using System.Security.Cryptography;
using System.Text;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Certificates;

namespace GameGuild.API.Core.Integration;

/// <summary>Host-owned domain bridge: only stages events captured by the existing transactional outbox.</summary>
public sealed class CertificateEvmEvidenceProducer : ICertificateDurableEvidenceProducer
{
    public const string ProducerName = "learning-certificates-v1";
    public const string AggregateName = "LearningCertificateEvm";
    private readonly EvmCertificatePublicationNetwork network;
    private readonly string canonicalIssuer;
    private readonly IActorContextAccessor actors;
    private readonly TimeProvider clock;

    public CertificateEvmEvidenceProducer(EvmCertificatePublicationNetwork network, string canonicalIssuer,
        IActorContextAccessor actors, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(network); ArgumentNullException.ThrowIfNull(actors); ArgumentNullException.ThrowIfNull(clock);
        ValidateIssuer(canonicalIssuer);
        this.network = network; this.canonicalIssuer = canonicalIssuer; this.actors = actors; this.clock = clock;
    }

    public void StageIssued(Certificate certificate)
    {
        ValidateCertificate(certificate);
        var eventId = AnchorEventId(certificate.Id);
        if (certificate.IntegrationEvents.Any(e => e.EventId == eventId))
        {
            return;
        }
        var payload = BlockchainCertificateCanonicalizer.BuildIssuancePayload(certificate.Id,
            BlockchainCertificateCanonicalizer.ComputeSha256Hex(certificate.RecipientName), certificate.IssuedAt, canonicalIssuer, 1);
        certificate.AddIntegrationEvent(new CertificateEvmAnchorRequestedV1
        {
            EventId = eventId, OccurredAt = clock.GetUtcNow().UtcDateTime, ActorId = ActorId(),
            TenantId = certificate.TenantId ?? DurableIntegrationEventTenants.Platform,
            AggregateType = AggregateName, AggregateId = certificate.Id.ToString("D"),
            DocumentId = certificate.Id, OwnerUserId = certificate.UserId, OriginalTenantId = certificate.TenantId,
            CertificateHash = "0x" + BlockchainCertificateCanonicalizer.ComputeSha256Hex(payload), CanonicalIssuer = canonicalIssuer,
            Profile = network.Profile, ChainId = network.ChainId, ContractAddress = network.ContractAddress, IssuerId = network.IssuerId,
            RuntimeCodeSha256 = network.RuntimeCodeSha256, FinalityMode = network.FinalityMode, MinimumConfirmations = network.MinimumConfirmations
        });
    }

    public void StageRevoked(Certificate certificate)
    {
        ValidateCertificate(certificate);
        if (certificate.Status != CertificateStatus.Revoked)
        {
            throw new InvalidOperationException("A durable revocation intent requires a revoked certificate.");
        }
        var eventId = RevocationEventId(certificate.Id);
        if (certificate.IntegrationEvents.Any(e => e.EventId == eventId))
        {
            return;
        }
        var anchorId = AnchorEventId(certificate.Id);
        certificate.AddIntegrationEvent(new CertificateEvmRevocationRequestedV1
        {
            EventId = eventId, OccurredAt = clock.GetUtcNow().UtcDateTime, ActorId = ActorId(),
            TenantId = certificate.TenantId ?? DurableIntegrationEventTenants.Platform,
            AggregateType = AggregateName, AggregateId = certificate.Id.ToString("D"), CausationId = anchorId,
            DocumentId = certificate.Id, OwnerUserId = certificate.UserId, OriginalTenantId = certificate.TenantId, AnchorEventId = anchorId
        });
    }

    public static Guid AnchorEventId(Guid documentId) => EventId(documentId, "anchor");
    public static Guid RevocationEventId(Guid documentId) => EventId(documentId, "revoke");

    internal static void ValidateIssuer(string issuer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        if (issuer.Length > 200 || issuer.Any(char.IsControl))
        {
            throw new ArgumentException("A bounded public canonical issuer identity is required.", nameof(issuer));
        }
    }

    private static Guid EventId(Guid documentId, string kind) => new(SHA256.HashData(
        Encoding.UTF8.GetBytes($"GameGuild.CertificateEvm.v1/{documentId:D}/{kind}")).AsSpan(0, 16));

    private Guid ActorId()
    {
        var actor = actors.ActorContext;
        return actor.IsAuthenticated && Guid.TryParse(actor.SubjectId, out var id) && id != Guid.Empty
            ? id : DurableIntegrationEventActors.System;
    }

    private static void ValidateCertificate(Certificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        if (certificate.Id == Guid.Empty || certificate.UserId == Guid.Empty || certificate.TenantId == Guid.Empty)
        {
            throw new ArgumentException("A certificate, owner and valid tenant scope are required.", nameof(certificate));
        }
    }
}
