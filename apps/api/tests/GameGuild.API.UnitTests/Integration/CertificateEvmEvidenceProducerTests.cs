using GameGuild.API.Core.Integration;
using GameGuild.API.Eventing;
using GameGuild.API.Database;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Certificates;
using GameGuild.Learning;
using GameGuild.Learning.Abstractions;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        Assert.Equal("Learning.Certificates.EvmAnchorRequested.v1", requested.EventName);
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
        Assert.Equal("Learning.Certificates.EvmRevocationRequested.v1", revoked.EventName);
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
        var certificate = CertificateInTenant(null);
        Producer().StageIssued(certificate);
        var requested = Assert.IsType<CertificateEvmAnchorRequestedV1>(Assert.Single(certificate.IntegrationEvents));
        Assert.Null(requested.OriginalTenantId);
        Assert.Equal(DurableIntegrationEventTenants.Platform, requested.TenantId);
        Assert.Equal(DurableIntegrationEventActors.System, requested.ActorId);
    }

    [Fact]
    public void InvalidOwnerCannotStageAnUnownedOperation()
    {
        var certificate = CertificateForOwner(Guid.Empty);
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
        Assert.Equal(tenant, CertificateInTenant(tenant).TenantId);
    }

    [Fact]
    public async Task ExplicitCompositionUsesTheProducerEvenWithoutOptionalContextOrAuxiliaryAdapters()
    {
        await using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, enableEntityChangeAuditCapture: false);
        var marker = Guid.NewGuid().ToString("N");
        var user = User.Create($"certificate-{marker}@example.test", "Composition recipient"); user.Username = marker;
        var template = CertificateTemplate.Create(Guid.NewGuid(), "Composition fixture", "<div>Fixture</div>");
        context.AddRange(user, template); await context.SaveChangesAsync();
        var actors = new Mock<IActorContextAccessor>(); actors.SetupGet(a => a.ActorContext).Returns(ActorContext.Anonymous);
        var audit = new Mock<ILearningAuditEventSink>();
        audit.Setup(a => a.RecordAsync(It.IsAny<LearningAuditEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var services = new ServiceCollection();
        services.AddSingleton(context); services.AddSingleton<IApplicationDbContext>(context);
        services.AddSingleton<ILogger<CertificateService>>(NullLogger<CertificateService>.Instance);
        services.AddSingleton(audit.Object); services.AddSingleton(actors.Object);
        services.AddCertificateEvmDurableEvidence(Network(), "original-issuer");
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICertificateService>();
        Assert.Same(service, scope.ServiceProvider.GetRequiredService<ICertificateIssuanceService>());
        var result = await service.IssueCertificateAsync(template.Id, Guid.NewGuid(), user.Id, template.CourseId);
        Assert.True(result.IsSuccess);
        Assert.True(await context.Set<OutboxMessage>().AnyAsync(m => m.AggregateId == result.Value.Id.ToString("D") &&
            m.EventName == CertificateEvmAnchorRequestedV1.Name));
    }

    private static Certificate Certificate() => NewCertificate(Guid.NewGuid(), null);
    private static Certificate CertificateForOwner(Guid ownerId) => NewCertificate(ownerId, null);
    private static Certificate CertificateInTenant(Guid? tenantId) => NewCertificate(Guid.NewGuid(), tenantId);
    private static Certificate NewCertificate(Guid ownerId, Guid? tenantId) =>
        GameGuild.Learning.Certificates.Certificate.Issue(Guid.NewGuid(), Guid.NewGuid(), ownerId, Guid.NewGuid(),
            "Confidential recipient", "Certificate fixture", expiresAt: null, tenantId: tenantId);

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
