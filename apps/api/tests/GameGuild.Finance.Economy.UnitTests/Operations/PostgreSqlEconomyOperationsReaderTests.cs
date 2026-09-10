using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.UnitTests.Operations;

public sealed class PostgreSqlEconomyOperationsReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 20, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("a1000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task ReadsLedgerHealthAndOnlyGlobalOrActorTenantControlPlaneState()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("operations_reader");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        SeedHealthyLedger(context);
        SeedControlPlane(context);
        await context.SaveChangesAsync();
        var reader = new PostgreSqlEconomyOperationsReader(context);

        var health = await reader.ReadLedgerHealthAsync(Now, CancellationToken.None);
        var controlPlane = await reader.ReadCapabilityConfigurationAsync(
            TenantId, includeInactiveKillSwitches: false, limit: 50, Now, CancellationToken.None);

        health.IsJournalHealthy.Should().BeTrue();
        health.IsProjectionHealthy.Should().BeTrue();
        health.IsAnchorHealthy.Should().BeTrue();
        health.IsReserveHealthy.Should().BeTrue();
        health.Diagnostics.Should().BeEmpty();
        health.Head!.Sequence.Should().Be(10);
        health.ActiveProjection!.Generation.Should().Be(3);
        health.LatestAnchor!.JournalSequence.Should().Be(10);
        health.ActiveReserve!.Version.Should().Be(5);
        controlPlane.Policies.Should().HaveCount(2);
        controlPlane.Policies.Should().OnlyContain(policy =>
            policy.TenantId == null || policy.TenantId == TenantId);
        controlPlane.KillSwitches.Should().ContainSingle()
            .Which.Scope.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public async Task MissingStateIsFailClosedAndInputsAreValidated()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("operations_reader_empty");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var reader = new PostgreSqlEconomyOperationsReader(context);

        var health = await reader.ReadLedgerHealthAsync(Now, CancellationToken.None);

        health.IsJournalHealthy.Should().BeFalse();
        health.IsProjectionHealthy.Should().BeFalse();
        health.IsAnchorHealthy.Should().BeFalse();
        health.IsReserveHealthy.Should().BeFalse();
        health.Diagnostics.Should().HaveCount(4);
        await FluentActions.Awaiting(() => reader.ReadCapabilityConfigurationAsync(
                Guid.Empty, false, 10, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => reader.ReadCapabilityConfigurationAsync(
                TenantId, false, 0, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new PostgreSqlEconomyOperationsReader(new StubApplicationDbContext()))
            .Should().Throw<InvalidOperationException>();
    }

    private static void SeedHealthyLedger(OperationsDbContext context)
    {
        var anchorId = Guid.NewGuid();
        context.Set<EconomyChainHeadRow>().Update(new EconomyChainHeadRow
        {
            Id = 1, Sequence = 10, Hash = "head-hash", UpdatedAt = Now
        });
        context.Set<EconomyJournalVerificationCheckpointRow>().Add(new EconomyJournalVerificationCheckpointRow
        {
            Id = Guid.NewGuid(), FromSequence = 1, ToSequence = 10,
            PreviousHash = JournalChain.GenesisHash, CurrentHash = "head-hash", IsValid = true,
            FencingToken = 2, StartedAt = Now.AddMinutes(-2), CompletedAt = Now.AddMinutes(-1)
        });
        context.Set<EconomyProjectionGenerationRow>().Add(new EconomyProjectionGenerationRow
        {
            Id = Guid.NewGuid(), Generation = 3, FromSequence = 0, ToSequence = 10,
            ProjectionHash = "projection-hash", JournalHash = "head-hash", MismatchCount = 0,
            State = "Active", IsActive = true, ProposedBy = Guid.NewGuid(), ApprovedBy = Guid.NewGuid(),
            SecondApprovedBy = Guid.NewGuid(), StartedAt = Now.AddMinutes(-4),
            CompletedAt = Now.AddMinutes(-3), ActivatedAt = Now.AddMinutes(-2)
        });
        context.Set<EconomyExternalAnchorRow>().Add(new EconomyExternalAnchorRow
        {
            Id = anchorId, JournalSequence = 10, JournalHash = "head-hash", Signature = "signature",
            WormReference = "worm/ref", Provider = "s3-object-lock", ProviderReference = "version-1",
            AnchoredAt = Now.AddMinutes(-2)
        });
        context.Set<EconomyAnchorVerificationRow>().Add(new EconomyAnchorVerificationRow
        {
            Id = Guid.NewGuid(), ExternalAnchorId = anchorId, KeyId = "kms-key",
            ObjectVersion = "version-1", ETag = "etag", RetainUntil = Now.AddYears(1),
            ObjectHash = "object-hash", SignatureValid = true, ObjectMatches = true,
            VerifiedAt = Now.AddMinutes(-1)
        });
        context.Set<EconomyReserveHeadRow>().Add(new EconomyReserveHeadRow
        {
            Version = 5, IsActive = true, PolicyVersion = 7, AuthorizationEpoch = 2,
            ObservedAt = Now.AddMinutes(-5), ExpiresAt = Now.AddHours(1),
            Coverage = ReserveCoverageState.Covered, EvidenceHash = "reserve-evidence", ActivatedAt = Now.AddMinutes(-4)
        });
        context.Set<EconomyCustodyReconciliationRow>().Add(new EconomyCustodyReconciliationRow
        {
            Id = Guid.NewGuid(), ReserveVersion = 5, ObservationIds = $"[\"{Guid.NewGuid():N}\"]",
            LiabilityUsdNanos = 10, EligibleAssetUsdNanos = 10, VarianceUsdNanos = 0,
            IsReconciled = true, EvidenceHash = "custody-evidence", ReconciledBy = Guid.NewGuid(),
            ReconciledAt = Now.AddMinutes(-4)
        });
    }

    private static void SeedControlPlane(OperationsDbContext context)
    {
        context.Set<EconomyCapabilityPolicyRow>().AddRange(
            Policy(null, "global:5:BR", 1),
            Policy(TenantId, $"{TenantId:N}:5:BR", 2),
            Policy(Guid.NewGuid(), "foreign:5:BR", 3));
        context.Set<EconomyKillSwitchRow>().AddRange(
            KillSwitch(TenantId, true, 1),
            KillSwitch(null, false, 2),
            KillSwitch(Guid.NewGuid(), true, 3));
    }

    private static EconomyCapabilityPolicyRow Policy(Guid? tenantId, string scope, long version) => new()
    {
        Id = Guid.NewGuid(), ScopeKey = scope, TenantId = tenantId,
        Capability = EconomyValueMovementCapability.PayoutExecution, JurisdictionCode = "BR",
        Version = version, CanonicalPayload = "{}", PayloadHash = $"policy-{version}", KeyId = "kms",
        Signature = "signature", RequestHash = $"request-{version}", ProposedBy = Guid.NewGuid(),
        ApprovedBy = Guid.NewGuid(), ProposedAt = Now.AddHours(-2), ApprovedAt = Now.AddHours(-1),
        EffectiveAt = Now.AddMinutes(-30), ExpiresAt = Now.AddHours(1), ProviderReady = true, IsActive = true
    };

    private static EconomyKillSwitchRow KillSwitch(Guid? tenantId, bool active, long epoch) => new()
    {
        Id = Guid.NewGuid(), ScopeKey = tenantId is null ? "global" : $"tenant:{tenantId:N}",
        TenantId = tenantId, Epoch = epoch, IsActive = active, Reason = "test",
        RequestHash = $"kill-{epoch}", ActivatedBy = Guid.NewGuid(), ActivatedAt = Now.AddMinutes(-10)
    };

    private static OperationsDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<OperationsDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class OperationsDbContext(DbContextOptions<OperationsDbContext> options)
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
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
