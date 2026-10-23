using GameGuild.API.Core.Integration;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.API.UnitTests.Integration;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Users;
using GameGuild.Learning;
using GameGuild.Learning.Certificates;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Course = GameGuild.Learning.Courses.Program;

namespace GameGuild.API.UnitTests.Database;

/// <summary>Actual production EF model, certificate service, outbox and dedicated journal on PostgreSQL.
/// These cases do not establish HTTP authorization, RPC publication or external testnet acceptance.</summary>
public sealed class CertificateEvmOutboxPostgreSqlTests(CertificateEvmOutboxPostgreSqlFixture fixture)
    : IClassFixture<CertificateEvmOutboxPostgreSqlFixture>
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private EvmCertificateJournal Journal => new(fixture, TimeProvider.System);

    [Fact]
    public async Task IssuancePersistsItsIntentWithTheCertificateAndAFreshConsumerDeliversIdempotently()
    {
        var issued = await Issue();
        await using var restarted = fixture.CreateContext();
        var stored = await restarted.Set<Certificate>().SingleAsync(c => c.Id == issued.Id, Ct);
        var message = await restarted.Set<OutboxMessage>().SingleAsync(m => m.AggregateId == issued.Id.ToString("D"), Ct);
        var request = Assert.IsType<CertificateEvmAnchorRequestedV1>(DurableEventSerializer.Deserialize(message));
        Assert.Equal(issued.TenantId, stored.TenantId);
        Assert.Equal(issued.UserId, request.OwnerUserId);
        var consumer = Consumer(restarted);
        await consumer.HandleAsync(request, Ct);
        var first = Assert.IsType<EvmCertificateJournalSnapshot>(await Original(issued));
        await consumer.HandleAsync(request, Ct);
        var second = Assert.IsType<EvmCertificateJournalSnapshot>(await Original(issued));
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(EvmCertificatePublicationStatus.Queued, second.Status);
        Assert.Equal(request.CertificateHash, second.CertificateHash);
        Assert.Null(second.Nonce);
        Assert.Null(second.TransactionHash);
    }

    [Fact]
    public async Task AuditFailureRollsBackBothCertificateAndOutbox()
    {
        await using var context = fixture.CreateContext();
        var seeded = await Seed(context);
        var result = await Service(context, failAudit: true).IssueCertificateAsync(seeded.Template, seeded.Enrollment, seeded.Owner, seeded.Course, seeded.Tenant);
        Assert.False(result.IsSuccess);
        await using var fresh = fixture.CreateContext();
        Assert.False(await fresh.Set<Certificate>().AnyAsync(c => c.EnrollmentId == seeded.Enrollment, Ct));
        Assert.False(await fresh.Set<OutboxMessage>().AnyAsync(m => m.EventName == CertificateEvmAnchorRequestedV1.Name &&
            m.ActorId == DurableIntegrationEventActors.System && m.TenantId == seeded.Tenant, Ct));
    }

    [Fact]
    public async Task NetworkFailureAfterCommitCannotLoseTheDurableIssuanceIntent()
    {
        await using var context = fixture.CreateContext();
        var seeded = await Seed(context);
        var result = await Service(context, auxiliary: new OfflineAuxiliaryEvidence()).IssueCertificateAsync(
            seeded.Template, seeded.Enrollment, seeded.Owner, seeded.Course, seeded.Tenant);
        Assert.True(result.IsSuccess);
        await using var fresh = fixture.CreateContext();
        Assert.True(await fresh.Set<Certificate>().AnyAsync(c => c.Id == result.Value.Id, Ct));
        var message = await fresh.Set<OutboxMessage>().SingleAsync(m => m.AggregateId == result.Value.Id.ToString("D"), Ct);
        await Consumer(fresh).HandleAsync(Assert.IsType<CertificateEvmAnchorRequestedV1>(DurableEventSerializer.Deserialize(message)), Ct);
        Assert.NotNull(await Original(result.Value));
    }

    [Fact]
    public async Task RevocationAfterAConfigurationChangeUsesTheOriginalJournalDigestAndNetwork()
    {
        var issued = await Issue();
        await using (var before = fixture.CreateContext())
        {
            var message = await before.Set<OutboxMessage>().SingleAsync(m => m.AggregateId == issued.Id.ToString("D"), Ct);
            await Consumer(before).HandleAsync(Assert.IsType<CertificateEvmAnchorRequestedV1>(DurableEventSerializer.Deserialize(message)), Ct);
        }
        var original = Assert.IsType<EvmCertificateJournalSnapshot>(await Original(issued));
        await using (var changed = fixture.CreateContext())
        {
            var result = await Service(changed, changedProfile: true).RevokeCertificateAsync(issued.Id, "private revocation explanation");
            Assert.True(result.IsSuccess);
        }
        await using var fresh = fixture.CreateContext();
        var revoke = await fresh.Set<OutboxMessage>().SingleAsync(m => m.AggregateId == issued.Id.ToString("D") &&
            m.EventName == CertificateEvmRevocationRequestedV1.Name, Ct);
        Assert.DoesNotContain("private revocation explanation", revoke.Payload, StringComparison.Ordinal);
        await Consumer(fresh).HandleAsync(Assert.IsType<CertificateEvmRevocationRequestedV1>(DurableEventSerializer.Deserialize(revoke)), Ct);
        var queued = Assert.IsType<EvmCertificateJournalSnapshot>(await Journal.FindByDocumentAsync(
            CertificateEvmEvidenceProducer.ProducerName, issued.Id, issued.UserId, issued.TenantId, EvmCertificateOperationKind.Revoke, Ct));
        Assert.Equal(original.CertificateHash, queued.CertificateHash);
        Assert.Equal(original.Network.Profile, queued.Network.Profile);
        Assert.Equal(original.Network.ContractAddress, queued.Network.ContractAddress);
        Assert.Null(queued.TransactionHash);
    }

    [Fact]
    public async Task RevocationDeliveryBeforeAnchorDeliveryQueuesBothOriginalIntentsWithoutRecomputing()
    {
        var issued = await Issue();
        await using (var context = fixture.CreateContext())
        {
            Assert.True((await Service(context).RevokeCertificateAsync(issued.Id, "private explanation")).IsSuccess);
        }
        await using var fresh = fixture.CreateContext();
        var anchor = await fresh.Set<OutboxMessage>().SingleAsync(m => m.AggregateId == issued.Id.ToString("D") && m.EventName == CertificateEvmAnchorRequestedV1.Name, Ct);
        var revoke = await fresh.Set<OutboxMessage>().SingleAsync(m => m.AggregateId == issued.Id.ToString("D") && m.EventName == CertificateEvmRevocationRequestedV1.Name, Ct);
        var consumer = Consumer(fresh);
        await consumer.HandleAsync(Assert.IsType<CertificateEvmRevocationRequestedV1>(DurableEventSerializer.Deserialize(revoke)), Ct);
        var original = Assert.IsType<EvmCertificateJournalSnapshot>(await Original(issued));
        var revoked = Assert.IsType<EvmCertificateJournalSnapshot>(await Journal.FindByDocumentAsync(
            CertificateEvmEvidenceProducer.ProducerName, issued.Id, issued.UserId, issued.TenantId, EvmCertificateOperationKind.Revoke, Ct));
        Assert.Equal(original.CertificateHash, revoked.CertificateHash);
        Assert.Null(await Journal.TryAcquireAsync(revoked.Id, TimeSpan.FromMinutes(2), Ct));
        await consumer.HandleAsync(Assert.IsType<CertificateEvmAnchorRequestedV1>(DurableEventSerializer.Deserialize(anchor)), Ct);
        Assert.Equal(original.Id, (await Original(issued))!.Id);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("tenant")]
    [InlineData("profile")]
    public async Task AChangedIdentityCannotPublishAnotherOwnersCertificate(string field)
    {
        var issued = await Issue();
        await using var context = fixture.CreateContext();
        var message = await context.Set<OutboxMessage>().SingleAsync(m => m.AggregateId == issued.Id.ToString("D"), Ct);
        var original = Assert.IsType<CertificateEvmAnchorRequestedV1>(DurableEventSerializer.Deserialize(message));
        var changed = field switch
        {
            "owner" => original with { OwnerUserId = Guid.NewGuid() },
            "tenant" => original with { OriginalTenantId = Guid.NewGuid() },
            _ => original with { Profile = "another-profile" }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Consumer(context).HandleAsync(changed, Ct));
        Assert.Null(await Original(issued));
    }

    [Fact]
    public async Task AnOuterConsumerRollbackDoesNotDuplicateAnAlreadyCommittedJournalIntentOnReplay()
    {
        var issued = await Issue();
        await using (var first = fixture.CreateContext())
        {
            await using var transaction = await first.Database.BeginTransactionAsync(Ct);
            var message = await first.Set<OutboxMessage>().SingleAsync(m => m.AggregateId == issued.Id.ToString("D"), Ct);
            await Consumer(first).HandleAsync(Assert.IsType<CertificateEvmAnchorRequestedV1>(DurableEventSerializer.Deserialize(message)), Ct);
            message.CompletedAtUtc = DateTimeOffset.UtcNow;
            await first.SaveChangesAsync(Ct);
            await transaction.RollbackAsync(Ct);
        }
        var accepted = Assert.IsType<EvmCertificateJournalSnapshot>(await Original(issued));
        await using var fresh = fixture.CreateContext();
        var retry = await fresh.Set<OutboxMessage>().SingleAsync(m => m.AggregateId == issued.Id.ToString("D"), Ct);
        Assert.Null(retry.CompletedAtUtc);
        await Consumer(fresh).HandleAsync(Assert.IsType<CertificateEvmAnchorRequestedV1>(DurableEventSerializer.Deserialize(retry)), Ct);
        Assert.Equal(accepted.Id, (await Original(issued))!.Id);
        Assert.Single(await Journal.ListEventsAsync(accepted.Id, Ct));
    }

    [Fact]
    public async Task DisabledEvidencePreservesIssuanceWithoutAnEvmOutboxMessage()
    {
        await using var context = fixture.CreateContext();
        var seeded = await Seed(context);
        var service = new CertificateService(context, NullLogger<CertificateService>.Instance, new FixtureAuditSink(false));
        var issued = await service.IssueCertificateAsync(seeded.Template, seeded.Enrollment, seeded.Owner, seeded.Course, seeded.Tenant);
        Assert.True(issued.IsSuccess);
        Assert.False(await context.Set<OutboxMessage>().AnyAsync(m => m.AggregateId == issued.Value.Id.ToString("D"), Ct));
    }

    [Fact]
    public async Task RepeatedRevocationKeepsOneDurableIntentAndTheOriginalReason()
    {
        var issued = await Issue();
        await using (var first = fixture.CreateContext())
        {
            Assert.True((await Service(first).RevokeCertificateAsync(issued.Id, "original private explanation")).IsSuccess);
        }
        await using (var retry = fixture.CreateContext())
        {
            Assert.True((await Service(retry).RevokeCertificateAsync(issued.Id, "different private explanation")).IsSuccess);
        }
        await using var fresh = fixture.CreateContext();
        Assert.Equal(1, await fresh.Set<OutboxMessage>().CountAsync(m => m.AggregateId == issued.Id.ToString("D") &&
            m.EventName == CertificateEvmRevocationRequestedV1.Name, Ct));
        Assert.Equal("original private explanation", (await fresh.Set<Certificate>().SingleAsync(c => c.Id == issued.Id, Ct)).RevocationReason);
    }

    [Fact]
    public async Task MissingOriginalIdentityNeverPretendsRevocationWasPublished()
    {
        Certificate issued;
        await using (var disabled = fixture.CreateContext())
        {
            var seeded = await Seed(disabled);
            var service = new CertificateService(disabled, NullLogger<CertificateService>.Instance, new FixtureAuditSink(false));
            var result = await service.IssueCertificateAsync(seeded.Template, seeded.Enrollment, seeded.Owner, seeded.Course, seeded.Tenant);
            Assert.True(result.IsSuccess); issued = result.Value;
        }
        await using (var enabled = fixture.CreateContext())
        {
            Assert.True((await Service(enabled).RevokeCertificateAsync(issued.Id, "private explanation")).IsSuccess);
        }
        await using var fresh = fixture.CreateContext();
        var message = await fresh.Set<OutboxMessage>().SingleAsync(m => m.AggregateId == issued.Id.ToString("D"), Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Consumer(fresh).HandleAsync(
            Assert.IsType<CertificateEvmRevocationRequestedV1>(DurableEventSerializer.Deserialize(message)), Ct));
        Assert.Null(await Journal.FindByDocumentAsync(CertificateEvmEvidenceProducer.ProducerName,
            issued.Id, issued.UserId, issued.TenantId, EvmCertificateOperationKind.Revoke, Ct));
    }

    [Fact]
    public async Task JournalLookupDoesNotBroadenTheCapturedOwnerOrTenant()
    {
        var issued = await Issue();
        await using var context = fixture.CreateContext();
        var message = await context.Set<OutboxMessage>().SingleAsync(m => m.AggregateId == issued.Id.ToString("D"), Ct);
        await Consumer(context).HandleAsync(Assert.IsType<CertificateEvmAnchorRequestedV1>(DurableEventSerializer.Deserialize(message)), Ct);
        Assert.Null(await Journal.FindByDocumentAsync(CertificateEvmEvidenceProducer.ProducerName, issued.Id,
            Guid.NewGuid(), issued.TenantId, EvmCertificateOperationKind.Anchor, Ct));
        Assert.Null(await Journal.FindByDocumentAsync(CertificateEvmEvidenceProducer.ProducerName, issued.Id,
            issued.UserId, Guid.NewGuid(), EvmCertificateOperationKind.Anchor, Ct));
    }

    [Fact]
    public async Task AmbiguousHistoricalNetworksRequireAttentionInsteadOfPickingAnArbitraryOriginal()
    {
        var issued = await Issue();
        await using var context = fixture.CreateContext();
        var message = await context.Set<OutboxMessage>().SingleAsync(m => m.AggregateId == issued.Id.ToString("D"), Ct);
        var request = Assert.IsType<CertificateEvmAnchorRequestedV1>(DurableEventSerializer.Deserialize(message));
        await Consumer(context).HandleAsync(request, Ct);
        await Journal.EnqueueAsync(new(CertificateEvmEvidenceProducerTests.Network("another-profile", '4'),
            issued.Id, issued.UserId, issued.TenantId, CertificateEvmEvidenceProducer.ProducerName, request.CertificateHash, EvmCertificateOperationKind.Anchor), Ct);
        var error = await Assert.ThrowsAsync<EvmCertificateJournalException>(() => Original(issued));
        Assert.Equal(EvmCertificateJournalFailure.Conflict, error.Failure);
    }

    private async Task<Certificate> Issue()
    {
        await using var context = fixture.CreateContext();
        var seeded = await Seed(context);
        var result = await Service(context).IssueCertificateAsync(seeded.Template, seeded.Enrollment, seeded.Owner, seeded.Course, seeded.Tenant);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private CertificateService Service(ApplicationDbContext context, bool failAudit = false, bool changedProfile = false, ICertificateAnchoring? auxiliary = null)
    {
        var actors = new Mock<IActorContextAccessor>(); actors.SetupGet(a => a.ActorContext).Returns(ActorContext.Anonymous);
        var network = CertificateEvmEvidenceProducerTests.Network(changedProfile ? "changed-profile" : "original-profile", changedProfile ? '4' : '1');
        return new CertificateService(context, NullLogger<CertificateService>.Instance, new FixtureAuditSink(failAudit), auxiliary,
            requestContextAccessor: null,
            evidenceProducer: new CertificateEvmEvidenceProducer(network, changedProfile ? "changed-issuer" : "original-issuer", actors.Object, TimeProvider.System));
    }

    private CertificateEvmPublicationConsumer Consumer(ApplicationDbContext context) => new(context, Journal, CertificateEvmEvidenceProducerTests.Network());
    private Task<EvmCertificateJournalSnapshot?> Original(Certificate certificate) => Journal.FindByDocumentAsync(
        CertificateEvmEvidenceProducer.ProducerName, certificate.Id, certificate.UserId, certificate.TenantId, EvmCertificateOperationKind.Anchor, Ct);

    private static async Task<(Guid Template, Guid Enrollment, Guid Owner, Guid Course, Guid Tenant)> Seed(ApplicationDbContext context)
    {
        var marker = Guid.NewGuid().ToString("N");
        var user = User.Create($"certificate-{marker}@example.test", "Confidential recipient"); user.Username = "certificate-" + marker;
        var course = new Course { Id = Guid.NewGuid(), Title = "Certificate fixture", Slug = "certificate-" + marker, CreatorId = user.Id };
        var tenant = Guid.NewGuid();
        var template = CertificateTemplate.Create(course.Id, "Certificate template", "<div>Certificate fixture</div>", tenant);
        context.AddRange(user, course, template); await context.SaveChangesAsync(Ct);
        return (template.Id, Guid.NewGuid(), user.Id, course.Id, tenant);
    }

    private sealed class FixtureAuditSink(bool fail) : ILearningAuditEventSink
    {
        public Task RecordAsync(LearningAuditEvent auditEvent, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(auditEvent); cancellationToken.ThrowIfCancellationRequested();
            if (fail) { throw new InvalidOperationException("Fixture audit is unavailable."); }
            return Task.CompletedTask;
        }
    }

    private sealed class OfflineAuxiliaryEvidence : ICertificateAnchoring
    {
        public Task AnchorIssuedCertificateAsync(Certificate certificate, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(certificate); cancellationToken.ThrowIfCancellationRequested();
            throw new EvmCertificateRpcException(EvmCertificateRpcFailure.Timeout);
        }
        public Task RecordRevocationAsync(Certificate certificate, string reason, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(certificate); ArgumentException.ThrowIfNullOrWhiteSpace(reason); cancellationToken.ThrowIfCancellationRequested();
            throw new EvmCertificateRpcException(EvmCertificateRpcFailure.Timeout);
        }
        public Task<CertificateAnchorVerification> VerifyAnchoringAsync(Certificate certificate, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(certificate); cancellationToken.ThrowIfCancellationRequested();
            throw new EvmCertificateRpcException(EvmCertificateRpcFailure.Timeout);
        }
    }
}

public sealed class CertificateEvmOutboxPostgreSqlFixture : IDbContextFactory<EvmCertificateJournalDbContext>, IAsyncLifetime
{
    private EconomyPostgreSqlTestDatabase? database;
    public async Task InitializeAsync()
    {
        database = await EconomyPostgreSqlTestDatabase.CreateAsync("certificate_evm_outbox");
        await using var app = CreateContext(); await app.Database.EnsureCreatedAsync();
        await using var journal = CreateDbContext(); await journal.Database.MigrateAsync();
    }
    public ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database!.ConnectionString).Options,
        enableEntityChangeAuditCapture: false);
    public EvmCertificateJournalDbContext CreateDbContext() => new(new DbContextOptionsBuilder<EvmCertificateJournalDbContext>()
        .UseNpgsql(database!.ConnectionString, p => p.MigrationsHistoryTable(EvmCertificateJournalDbContext.MigrationsHistoryTable, EvmCertificateJournalDbContext.SchemaName)).Options);
    public async Task DisposeAsync()
    {
        if (database is not null) { await database.DisposeAsync(); }
    }
}
