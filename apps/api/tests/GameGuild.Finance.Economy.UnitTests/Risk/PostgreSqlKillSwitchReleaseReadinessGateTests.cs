using System.Text.Json;
using FluentAssertions;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class PostgreSqlKillSwitchReleaseReadinessGateTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("81000000-0000-0000-0000-000000000001");

    [Fact]
    public void ConstructorRequiresRelationalContextVerifierAndClock()
    {
        var clock = new FixedTimeProvider(Now);
        var verifier = new StubPolicyVerifier();

        FluentActions.Invoking(() => new PostgreSqlKillSwitchReleaseReadinessGate(null!, verifier, clock))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlKillSwitchReleaseReadinessGate(
                new StubApplicationDbContext(), verifier, clock))
            .Should().Throw<InvalidOperationException>();

        using var context = CreateContext("Host=localhost;Database=unused;Username=unused;Password=unused");
        FluentActions.Invoking(() => new PostgreSqlKillSwitchReleaseReadinessGate(context, null!, clock))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlKillSwitchReleaseReadinessGate(context, verifier, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ScopeValidationRejectsEmptyTenantAndUnscopedOrUnknownCapability()
    {
        using var context = CreateContext("Host=localhost;Database=unused;Username=unused;Password=unused");
        var gate = CreateGate(context);
        typeof(PostgreSqlKillSwitchReleaseReadinessGate)
            .GetMethod("ValidateScope", System.Reflection.BindingFlags.NonPublic |
                                        System.Reflection.BindingFlags.Static)!
            .Invoke(null, [EconomyKillSwitchScope.ForTenant(TenantId)]);

        await FluentActions.Invoking(() => gate.IsReadyAsync(null!, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Invoking(() => gate.IsReadyAsync(
                new EconomyKillSwitchScope(" ", null, null), CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => gate.IsReadyAsync(
                new EconomyKillSwitchScope("tenant:empty", Guid.Empty, null), CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => gate.IsReadyAsync(
                new EconomyKillSwitchScope("capability:unscoped", null,
                    EconomyValueMovementCapability.PayoutExecution), CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => gate.IsReadyAsync(
                new EconomyKillSwitchScope("capability:unknown", TenantId,
                    (EconomyValueMovementCapability)999), CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task EmptyControlPlaneIsNotReady()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("kill_release_empty");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var gate = CreateGate(context);

        (await gate.IsReadyAsync(EconomyKillSwitchScope.Global, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task HealthyCapabilityScopeWithVerifiedPolicyIsReady()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("kill_release_ready");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        await SeedHealthyControlPlaneAsync(context, includePolicy: true);
        var verifier = new StubPolicyVerifier();
        var gate = CreateGate(context, verifier);

        var ready = await gate.IsReadyAsync(
            EconomyKillSwitchScope.ForCapability(TenantId, EconomyValueMovementCapability.PayoutExecution),
            CancellationToken.None);

        ready.Should().BeTrue();
        verifier.Calls.Should().Be(1);
    }

    [Fact]
    public async Task CapabilityScopeWithoutActivePolicyIsNotReadyButBroaderDisabledScopeCanRecover()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("kill_release_disabled");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        await SeedHealthyControlPlaneAsync(context, includePolicy: false);
        var gate = CreateGate(context);

        (await gate.IsReadyAsync(
                EconomyKillSwitchScope.ForCapability(TenantId, EconomyValueMovementCapability.PayoutExecution),
                CancellationToken.None)).Should().BeFalse();
        (await gate.IsReadyAsync(EconomyKillSwitchScope.ForTenant(TenantId), CancellationToken.None))
            .Should().BeTrue("the absent policy keeps value disabled after the tenant switch is released");
        (await gate.IsReadyAsync(EconomyKillSwitchScope.Global, CancellationToken.None))
            .Should().BeTrue("the absent allowlist keeps value disabled after the global switch is released");
    }

    [Fact]
    public async Task EveryInvalidReadinessPredicateFailsClosed()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("kill_release_predicates");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        await SeedHealthyControlPlaneAsync(context, includePolicy: true);
        var gate = CreateGate(context);
        var scope = EconomyKillSwitchScope.ForCapability(
            TenantId, EconomyValueMovementCapability.PayoutExecution);

        var checkpoint = await context.Set<EconomyJournalVerificationCheckpointRow>().SingleAsync();
        checkpoint.IsValid = false;
        await AssertFailsClosedAsync(context, gate, scope);
        checkpoint.IsValid = true;

        var projection = await context.Set<EconomyProjectionGenerationRow>().SingleAsync();
        projection.MismatchCount = 1;
        await AssertFailsClosedAsync(context, gate, scope);
        projection.MismatchCount = 0;

        var reserve = await context.Set<EconomyReserveHeadRow>().SingleAsync();
        reserve.Coverage = ReserveCoverageState.Shortfall;
        await AssertFailsClosedAsync(context, gate, scope);
        reserve.Coverage = ReserveCoverageState.Covered;

        var custody = await context.Set<EconomyCustodyReconciliationRow>().SingleAsync();
        custody.VarianceUsdNanos = 1;
        await AssertFailsClosedAsync(context, gate, scope);
        custody.VarianceUsdNanos = 0;

        var anchor = await context.Set<EconomyAnchorVerificationRow>().SingleAsync();
        anchor.ObjectMatches = false;
        await AssertFailsClosedAsync(context, gate, scope);
        anchor.ObjectMatches = true;

        var policy = await context.Set<EconomyCapabilityPolicyRow>().SingleAsync();
        policy.Signature = "invalid";
        await AssertFailsClosedAsync(context, gate, scope);
        policy.Signature = "signature";
        await context.SaveChangesAsync();

        (await gate.IsReadyAsync(scope, CancellationToken.None)).Should().BeTrue();
    }

    private static async Task AssertFailsClosedAsync(
        ReadinessDbContext context,
        PostgreSqlKillSwitchReleaseReadinessGate gate,
        EconomyKillSwitchScope scope)
    {
        await context.SaveChangesAsync();
        (await gate.IsReadyAsync(scope, CancellationToken.None)).Should().BeFalse();
    }

    private static PostgreSqlKillSwitchReleaseReadinessGate CreateGate(
        ReadinessDbContext context,
        StubPolicyVerifier? verifier = null) =>
        new(context, verifier ?? new StubPolicyVerifier(), new FixedTimeProvider(Now));

    private static async Task SeedHealthyControlPlaneAsync(ReadinessDbContext context, bool includePolicy)
    {
        context.Set<EconomyChainHeadRow>().Update(new EconomyChainHeadRow
        {
            Id = 1, Sequence = 100, Hash = "chain-hash", UpdatedAt = Now
        });
        context.Set<EconomyJournalVerificationCheckpointRow>().Add(new EconomyJournalVerificationCheckpointRow
        {
            Id = Guid.NewGuid(), FromSequence = 1, ToSequence = 100, PreviousHash = "previous-hash",
            CurrentHash = "chain-hash", IsValid = true, FencingToken = 1,
            StartedAt = Now.AddMinutes(-1), CompletedAt = Now
        });
        context.Set<EconomyProjectionGenerationRow>().Add(new EconomyProjectionGenerationRow
        {
            Id = Guid.NewGuid(), Generation = 1, FromSequence = 1, ToSequence = 100,
            ProjectionHash = "projection-hash", JournalHash = "chain-hash", MismatchCount = 0,
            State = "Active", IsActive = true, ProposedBy = Guid.NewGuid(), ApprovedBy = Guid.NewGuid(),
            SecondApprovedBy = Guid.NewGuid(), StartedAt = Now.AddMinutes(-2),
            CompletedAt = Now.AddMinutes(-1), ActivatedAt = Now
        });
        context.Set<EconomyReserveHeadRow>().Add(new EconomyReserveHeadRow
        {
            Version = 3, IsActive = true, PolicyVersion = 7, AuthorizationEpoch = 2,
            ObservedAt = Now.AddMinutes(-1), ExpiresAt = Now.AddMinutes(10),
            HardFaceValueUsdMinor = 1, RequiredHardReserveUsdMinor = 1,
            SoftFaceValueUsdNanos = 1, StressedExpectedRedemptionCostUsdNanos = 1,
            RequiredSoftReserveUsdNanos = 1, HardBackingUsdNanos = 1_000_000,
            SoftBackingUsdNanos = 1, Coverage = ReserveCoverageState.Covered,
            EvidenceHash = "reserve-hash", ActivatedAt = Now
        });
        var observationId = Guid.NewGuid();
        context.Set<EconomyCustodyObservationRow>().Add(new EconomyCustodyObservationRow
        {
            Id = observationId, Provider = "custody", AssetKey = "usd",
            Purpose = ReserveBackingPurpose.HardCoin, Version = 1, EligibleUsdNanos = 1,
            ObservedAt = Now.AddMinutes(-1), ExpiresAt = Now.AddMinutes(10),
            PayloadHash = "custody-payload", KeyId = "custody-key", Signature = "custody-signature"
        });
        context.Set<EconomyCustodyReconciliationRow>().Add(new EconomyCustodyReconciliationRow
        {
            Id = Guid.NewGuid(), ReserveVersion = 3,
            ObservationIds = JsonSerializer.Serialize(new[] { observationId }),
            LiabilityUsdNanos = 1, EligibleAssetUsdNanos = 1, VarianceUsdNanos = 0,
            IsReconciled = true, EvidenceHash = "custody-hash", ReconciledBy = Guid.NewGuid(),
            ReconciledAt = Now
        });
        var anchorId = Guid.NewGuid();
        context.Set<EconomyExternalAnchorRow>().Add(new EconomyExternalAnchorRow
        {
            Id = anchorId, JournalSequence = 100, JournalHash = "chain-hash",
            Signature = "anchor-signature", WormReference = "s3://worm/anchor",
            Provider = "s3-object-lock", ProviderReference = "v1", AnchoredAt = Now
        });
        context.Set<EconomyAnchorVerificationRow>().Add(new EconomyAnchorVerificationRow
        {
            Id = Guid.NewGuid(), ExternalAnchorId = anchorId, KeyId = "kms-anchor",
            ObjectVersion = "v1", ETag = "etag", RetainUntil = Now.AddYears(1),
            ObjectHash = "object-hash", SignatureValid = true, ObjectMatches = true, VerifiedAt = Now
        });
        if (includePolicy)
        {
            context.Set<EconomyCapabilityPolicyRow>().Add(new EconomyCapabilityPolicyRow
            {
                Id = Guid.NewGuid(), ScopeKey = "tenant-policy", TenantId = TenantId,
                Capability = EconomyValueMovementCapability.PayoutExecution, JurisdictionCode = "BR",
                Version = 7, CanonicalPayload = "{}", PayloadHash = "policy-hash",
                KeyId = "kms-policy", Signature = "signature", RequestHash = "request-hash",
                ProposedBy = Guid.NewGuid(), ApprovedBy = Guid.NewGuid(), ProposedAt = Now.AddHours(-1),
                ApprovedAt = Now.AddMinutes(-30), EffectiveAt = Now.AddMinutes(-20),
                ExpiresAt = Now.AddMinutes(20), ProviderReady = true, IsActive = true
            });
        }
        await context.SaveChangesAsync();
    }

    private static ReadinessDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ReadinessDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class ReadinessDbContext(DbContextOptions<ReadinessDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new EconomyModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class StubPolicyVerifier : ICapabilityPolicySignatureVerifier
    {
        public int Calls { get; private set; }

        public ValueTask<bool> VerifyAsync(
            string canonicalPayload,
            string keyId,
            string signature,
            CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(signature == "signature");
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubApplicationDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
