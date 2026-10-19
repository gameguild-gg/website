using GameGuild.API.Core.Integration;
using GameGuild.API.Eventing;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Certificates;
using Moq;

namespace GameGuild.API.UnitTests.Integration;

public sealed class CertificateEvmEvidenceProducerTests
{
    [Fact]
    public void IssuanceCapturesItsPublicProfileAndCanonicalDigestWithoutPrivatePayload()
    {
        var certificate = Certificate();
        Producer().StageIssued(certificate);
        var requested = Assert.IsType<CertificateEvmAnchorRequestedV1>(Assert.Single(certificate.IntegrationEvents));
        var canonical = BlockchainCertificateCanonicalizer.BuildIssuancePayload(certificate.Id,
            BlockchainCertificateCanonicalizer.ComputeSha256Hex(certificate.RecipientName), certificate.IssuedAt, "original-issuer", 1);
        Assert.Equal("0x" + BlockchainCertificateCanonicalizer.ComputeSha256Hex(canonical), requested.CertificateHash);
        Assert.Equal(certificate.UserId, requested.OwnerUserId);
        Assert.Equal(certificate.TenantId, requested.OriginalTenantId);
        Assert.Equal("original-profile", requested.Profile);
        Assert.Equal(Network().ContractAddress, requested.ContractAddress);
        var payload = DurableEventSerializer.Serialize(requested);
        Assert.DoesNotContain(certificate.RecipientName, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("private-route", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void RevocationReferencesTheOriginalIssuanceInsteadOfRecomputingANewDigest()
    {
        var certificate = Certificate();
        Producer().StageIssued(certificate);
        var original = Assert.Single(certificate.IntegrationEvents);
        certificate.ClearIntegrationEvents();
        certificate.Revoke("private revocation explanation");
        Producer("changed-issuer").StageRevoked(certificate);
        var revoked = Assert.IsType<CertificateEvmRevocationRequestedV1>(Assert.Single(certificate.IntegrationEvents));
        Assert.Equal(original.EventId, revoked.AnchorEventId);
        Assert.Equal(original.EventId, revoked.CausationId);
        var payload = DurableEventSerializer.Serialize(revoked);
        Assert.DoesNotContain("changed-issuer", payload, StringComparison.Ordinal);
        Assert.DoesNotContain(certificate.RevocationReason!, payload, StringComparison.Ordinal);
        Assert.DoesNotContain(certificate.RecipientName, payload, StringComparison.Ordinal);
    }

    [Fact]
    public void RepeatedStagingDoesNotDuplicateAnEntityEvent()
    {
        var certificate = Certificate();
        Producer().StageIssued(certificate);
        Producer().StageIssued(certificate);
        Assert.Single(certificate.IntegrationEvents);
    }

    [Fact]
    public void ActorIdentityComesFromTheTrustedContext()
    {
        var actor = Guid.NewGuid();
        var certificate = Certificate();
        Producer(actorId: actor).StageIssued(certificate);
        Assert.Equal(actor, Assert.Single(certificate.IntegrationEvents).ActorId);
        Assert.NotEqual(certificate.UserId, actor);
    }

    [Fact]
    public void BackgroundIssuanceUsesTheExplicitSystemActorAndPlatformTenantSentinel()
    {
        var certificate = Certificate(tenantId: null);
        Producer().StageIssued(certificate);
        var requested = Assert.IsType<CertificateEvmAnchorRequestedV1>(Assert.Single(certificate.IntegrationEvents));
        Assert.Null(requested.OriginalTenantId);
        Assert.Equal(DurableIntegrationEventTenants.Platform, requested.TenantId);
        Assert.Equal(DurableIntegrationEventActors.System, requested.ActorId);
    }

    [Fact]
    public void InvalidOwnerCannotStageAnUnownedOperation()
    {
        var certificate = Certificate(ownerId: Guid.Empty);
        Assert.Throws<ArgumentException>(() => Producer().StageIssued(certificate));
        Assert.Empty(certificate.IntegrationEvents);
    }

    [Fact]
    public void RequestIdentityRemainsStableAcrossProducerInstances()
    {
        var certificate = Certificate();
        Producer().StageIssued(certificate);
        var first = Assert.Single(certificate.IntegrationEvents).EventId;
        certificate.ClearIntegrationEvents();
        Producer().StageIssued(certificate);
        Assert.Equal(first, Assert.Single(certificate.IntegrationEvents).EventId);
    }

    [Fact]
    public void DisabledProducerPreservesTheExistingNoEvidenceDefault()
    {
        var certificate = Certificate();
        NoOpCertificateDurableEvidenceProducer.Instance.StageIssued(certificate);
        NoOpCertificateDurableEvidenceProducer.Instance.StageRevoked(certificate);
        Assert.Empty(certificate.IntegrationEvents);
    }

    [Fact]
    public void CertificateFactoryRetainsTheSuppliedTenant()
    {
        var tenant = Guid.NewGuid();
        Assert.Equal(tenant, Certificate(tenantId: tenant).TenantId);
    }

    private static Certificate Certificate(Guid? ownerId = null, Guid? tenantId = null) =>
        GameGuild.Learning.Certificates.Certificate.Issue(Guid.NewGuid(), Guid.NewGuid(), ownerId ?? Guid.NewGuid(), Guid.NewGuid(),
            "Confidential recipient", "Certificate fixture", tenantId: tenantId);

    private static CertificateEvmEvidenceProducer Producer(string issuer = "original-issuer", Guid? actorId = null)
    {
        var actors = new Mock<IActorContextAccessor>();
        actors.SetupGet(a => a.ActorContext).Returns(actorId.HasValue ? new ActorContext
        {
            ActorKind = ActorKind.User, SubjectId = actorId.Value.ToString("D"), IsAuthenticated = true,
            Roles = new HashSet<string>(), Permissions = new HashSet<string>()
        } : ActorContext.Anonymous);
        return new CertificateEvmEvidenceProducer(Network(), issuer, actors.Object, TimeProvider.System);
    }

    internal static EvmCertificatePublicationNetwork Network(string profile = "original-profile", char contract = '1') =>
        EvmCertificatePublicationNetwork.Capture(profile, new EvmCertificateNetworkOptions
        {
            ChainId = 12345, RpcEndpoint = "https://rpc.invalid/private-route", ContractAddress = "0x" + new string(contract, 40),
            IssuerId = "0x" + new string('2', 64), ExpectedRuntimeCodeSha256 = new string('3', 64),
            FinalityMode = EvmCertificateFinalityMode.Confirmations, MinimumConfirmations = 1
        });
}
