using FluentAssertions;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class PostgreSqlComplianceEvidenceStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task PublishesValidContiguousEvidenceAtomicallyAndReplaysIdempotently()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("compliance_evidence");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var store = new PostgreSqlComplianceEvidenceStore(context);
        var envelope = Envelope(version: 1);

        var first = await store.IngestAsync(envelope, CancellationToken.None);
        var replay = await store.IngestAsync(envelope, CancellationToken.None);

        first.Status.Should().Be(ComplianceEvidenceIngestionStatus.Published);
        first.EvidenceId.Should().NotBeEmpty();
        replay.Status.Should().Be(ComplianceEvidenceIngestionStatus.Duplicate);
        replay.EvidenceId.Should().Be(first.EvidenceId);
        (await context.Set<EconomyComplianceInboxRow>().CountAsync()).Should().Be(1);
        (await context.Set<EconomyComplianceEvidenceRow>().CountAsync()).Should().Be(1);
        (await context.Set<EconomyComplianceOutboxRow>().CountAsync()).Should().Be(1);
        var persisted = await context.Set<EconomyComplianceEvidenceRow>().AsNoTracking().SingleAsync();
        persisted.RawObjectReference.Should().Be("s3://encrypted/raw-event");
        persisted.EvidenceHash.Should().Be(envelope.EvidenceHash);
        var latest = await store.ReadLatestAsync(
            TenantId, envelope.SubjectHash, envelope.EvidenceKind, CancellationToken.None);
        latest.Should().Be(new DurableComplianceEvidence(
            envelope.Provider, envelope.Environment, envelope.ProviderEventId, envelope.TenantId,
            envelope.SubjectHash, envelope.EvidenceKind, envelope.Version, envelope.Result,
            envelope.PolicyVersion, envelope.EvidenceHash, envelope.SignatureVerified,
            envelope.IssuedAt, envelope.ExpiresAt)
        {
            JurisdictionCode = "BRA"
        });

        var tracked = await context.Set<EconomyComplianceEvidenceRow>().SingleAsync();
        tracked.Result = "not-an-enum";
        await context.SaveChangesAsync();
        (await store.ReadLatestAsync(TenantId, envelope.SubjectHash, envelope.EvidenceKind,
            CancellationToken.None))!.Result.Should().Be(ComplianceEvidenceResult.Unavailable);
    }

    [Fact]
    public async Task PersistsButDoesNotPublishUnsignedStaleOrOutOfOrderEvents()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("compliance_deferred");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var store = new PostgreSqlComplianceEvidenceStore(context);

        var unsigned = await store.IngestAsync(
            Envelope(1) with { ProviderEventId = "unsigned", SignatureVerified = false },
            CancellationToken.None);
        var invalidLifetime = await store.IngestAsync(
            Envelope(1) with { ProviderEventId = "stale", IssuedAt = Now, ExpiresAt = Now },
            CancellationToken.None);
        var outOfOrder = await store.IngestAsync(
            Envelope(3) with { ProviderEventId = "future" },
            CancellationToken.None);
        var unsignedReplay = await store.IngestAsync(
            Envelope(1) with { ProviderEventId = "unsigned", SignatureVerified = false },
            CancellationToken.None);

        unsigned.Status.Should().Be(ComplianceEvidenceIngestionStatus.Rejected);
        invalidLifetime.Status.Should().Be(ComplianceEvidenceIngestionStatus.Rejected);
        outOfOrder.Status.Should().Be(ComplianceEvidenceIngestionStatus.Deferred);
        unsignedReplay.Should().Be(new ComplianceEvidenceIngestionResult(
            ComplianceEvidenceIngestionStatus.Duplicate, null));
        (await context.Set<EconomyComplianceInboxRow>().CountAsync()).Should().Be(3);
        (await context.Set<EconomyComplianceEvidenceRow>().CountAsync()).Should().Be(0);
        (await context.Set<EconomyComplianceOutboxRow>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RejectsProviderEventReplayWithDifferentPayloadHash()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("compliance_conflict");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var store = new PostgreSqlComplianceEvidenceStore(context);
        var envelope = Envelope(1);
        await store.IngestAsync(envelope, CancellationToken.None);

        await FluentActions.Awaiting(() => store.IngestAsync(
                envelope with { PayloadHash = "different-payload-hash" },
                CancellationToken.None).AsTask())
            .Should().ThrowAsync<ComplianceEvidenceConflictException>();
    }

    [Fact]
    public void ConstructorRequiresRelationalApplicationContext()
    {
        FluentActions.Invoking(() => new PostgreSqlComplianceEvidenceStore(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlComplianceEvidenceStore(new StubApplicationDbContext()))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task ReaderAndEnvelopeBoundariesFailClosed()
    {
        var unavailable = new UnavailableComplianceEvidenceReader();
        (await unavailable.ReadLatestAsync(TenantId, "subject", ComplianceEvidenceKinds.KycAml,
            CancellationToken.None)).Should().BeNull();
        await FluentActions.Awaiting(() => unavailable.ReadLatestAsync(
                Guid.Empty, "subject", ComplianceEvidenceKinds.KycAml, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>().WithParameterName("tenantId");
        await FluentActions.Awaiting(() => unavailable.ReadLatestAsync(
                TenantId, " ", ComplianceEvidenceKinds.KycAml, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => unavailable.ReadLatestAsync(
                TenantId, "subject", " ", CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();

        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("compliance_evidence_boundaries");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var store = new PostgreSqlComplianceEvidenceStore(context);
        (await store.ReadLatestAsync(TenantId, "missing", ComplianceEvidenceKinds.KycAml,
            CancellationToken.None)).Should().BeNull();
        await FluentActions.Awaiting(() => store.ReadLatestAsync(
                Guid.Empty, "subject", ComplianceEvidenceKinds.KycAml, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => store.ReadLatestAsync(
                TenantId, " ", ComplianceEvidenceKinds.KycAml, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => store.ReadLatestAsync(
                TenantId, "subject", " ", CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();

        var valid = Envelope(1);
        var invalid = new[]
        {
            valid with { Provider = " " },
            valid with { Environment = " " },
            valid with { ProviderEventId = " " },
            valid with { TenantId = Guid.Empty },
            valid with { SubjectHash = " " },
            valid with { EvidenceKind = " " },
            valid with { Version = 0 },
            valid with { PolicyVersion = 0 },
            valid with { JurisdictionCode = null },
            valid with { JurisdictionCode = "BR" },
            valid with { PayloadHash = " " },
            valid with { RawObjectReference = " " },
            valid with { EvidenceHash = " " },
            valid with { ReceivedAt = valid.IssuedAt.AddTicks(-1) }
        };
        await FluentActions.Awaiting(() => store.IngestAsync(null!, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();
        foreach (var envelope in invalid)
        {
            await FluentActions.Awaiting(() => store.IngestAsync(envelope, CancellationToken.None).AsTask())
                .Should().ThrowAsync<ArgumentException>();
        }

        var rejectedKyc = ComplianceEvidenceEnvelope.Create(
            "sumsub", "sandbox", "rejected-kyc", TenantId, "subject-hash", 1,
            ComplianceEvidenceResult.Rejected, Now.AddMinutes(-1), Now.AddDays(30), 7,
            "payload-hash", true, "s3://encrypted/rejected-kyc", Now);
        var approvedTrustSafety = rejectedKyc with
        {
            ProviderEventId = "approved-trust-safety",
            Result = ComplianceEvidenceResult.Approved,
            EvidenceKind = ComplianceEvidenceKinds.TrustSafety
        };
        FluentActions.Invoking(() => PostgreSqlComplianceEvidenceStore.ValidateEnvelope(rejectedKyc))
            .Should().NotThrow();
        FluentActions.Invoking(() => PostgreSqlComplianceEvidenceStore.ValidateEnvelope(approvedTrustSafety))
            .Should().NotThrow();
    }

    [Fact]
    public async Task DefersAContiguousVersionWhoseIssuedAtDoesNotAdvance()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("compliance_evidence_ordering");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var store = new PostgreSqlComplianceEvidenceStore(context);
        await store.IngestAsync(Envelope(1), CancellationToken.None);

        var deferred = await store.IngestAsync(
            Envelope(2) with { IssuedAt = Envelope(1).IssuedAt }, CancellationToken.None);

        deferred.Status.Should().Be(ComplianceEvidenceIngestionStatus.Deferred);
        (await context.Set<EconomyComplianceInboxRow>().SingleAsync(row => row.ProviderEventId == "event-2"))
            .ProcessingError.Should().Be("out-of-order-issued-at");
    }

    private static ComplianceEvidenceEnvelope Envelope(long version) => ComplianceEvidenceEnvelope.Create(
        provider: "sumsub",
        environment: "sandbox",
        providerEventId: "event-" + version,
        tenantId: TenantId,
        subjectHash: "subject-hash",
        version: version,
        result: ComplianceEvidenceResult.Approved,
        issuedAt: Now.AddMinutes(-1),
        expiresAt: Now.AddDays(30),
        policyVersion: 7,
        payloadHash: "payload-hash-" + version,
        signatureVerified: true,
        rawObjectReference: "s3://encrypted/raw-event",
        receivedAt: Now,
        jurisdictionCode: "BRA");

    private static ComplianceDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class ComplianceDbContext(DbContextOptions<ComplianceDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new EconomyModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class StubApplicationDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
