using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Reserves;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class PostgreSqlEconomyCapabilityControlPlaneStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RiskDecisionId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void ReceiptMappingTreatsJsonNullCollectionsAsFailClosedEmptyEvidence()
    {
        var map = typeof(PostgreSqlEconomyCapabilityControlPlaneStore)
            .GetMethod("MapReceipt", System.Reflection.BindingFlags.NonPublic |
                                     System.Reflection.BindingFlags.Static)!;
        var row = new EconomyCapabilityReceiptRow
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), ActorId = Guid.NewGuid(),
            SubjectReference = "subject", JurisdictionCode = "BR",
            Capability = EconomyValueMovementCapability.PayoutExecution,
            OperationFingerprint = "operation", PolicyVersion = 1, ReserveVersion = 1,
            RiskDecisionId = Guid.NewGuid(), ProviderHash = "provider", DestinationHash = "destination",
            SourceRootHashes = "null", EvidenceHashes = "null", IssuedAt = Now,
            ExpiresAt = Now.AddMinutes(1), ReceiptHash = "receipt", KeyId = "key", Signature = "signature"
        };

        var nullCollections = (CapabilityAuthorizationReceipt)map.Invoke(null, [row])!;
        nullCollections.SourceRootHashes.Should().BeEmpty();
        nullCollections.EvidenceHashes.Should().BeEmpty();
        row.SourceRootHashes = "[\"source\"]";
        row.EvidenceHashes = "[\"evidence\"]";
        var populated = (CapabilityAuthorizationReceipt)map.Invoke(null, [row])!;
        populated.SourceRootHashes.Should().Equal("source");
        populated.EvidenceHashes.Should().Equal("evidence");
    }

    [Fact]
    public void ConstructorRequiresRelationalContextAndVerifier()
    {
        FluentActions.Invoking(() => new PostgreSqlEconomyCapabilityControlPlaneStore(null!, new StubPolicyVerifier()))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlEconomyCapabilityControlPlaneStore(
                new StubApplicationDbContext(), new StubPolicyVerifier()))
            .Should().Throw<InvalidOperationException>();

        using var context = CreateContext("Host=localhost;Database=unused;Username=unused;Password=unused");
        FluentActions.Invoking(() => new PostgreSqlEconomyCapabilityControlPlaneStore(context, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task EmptyDatabaseProducesFailClosedSnapshot()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("capability_empty");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var verifier = new StubPolicyVerifier();
        var store = new PostgreSqlEconomyCapabilityControlPlaneStore(context, verifier);

        var snapshot = await store.ReadSnapshotAsync(EvaluationContext(), CancellationToken.None);

        snapshot.HasActivePolicy.Should().BeFalse();
        snapshot.PolicySignatureValid.Should().BeFalse();
        snapshot.PolicyVersion.Should().Be(0);
        snapshot.PolicyExpiresAt.Should().Be(Now);
        snapshot.JurisdictionAllowed.Should().BeFalse();
        snapshot.ComplianceAvailable.Should().BeFalse();
        snapshot.ComplianceExpiresAt.Should().Be(Now);
        snapshot.ManualReviewRequired.Should().BeTrue();
        snapshot.RiskDecisionId.Should().BeEmpty();
        snapshot.LedgerHealthy.Should().BeFalse();
        snapshot.ProjectionMatches.Should().BeFalse();
        snapshot.ReserveSufficient.Should().BeFalse();
        snapshot.ReserveVersion.Should().Be(0);
        snapshot.ReserveExpiresAt.Should().Be(Now);
        snapshot.CustodyReconciled.Should().BeFalse();
        snapshot.AnchorValid.Should().BeFalse();
        snapshot.AnchorExpiresAt.Should().Be(Now);
        snapshot.ProviderReady.Should().BeFalse();
        snapshot.KillSwitchActive.Should().BeFalse();
        snapshot.KillSwitchEpoch.Should().Be(0);
        snapshot.EvidenceHashes.Should().BeEmpty();
        verifier.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ReadyStateIssuesPersistsAndConsumesExactReceiptOnce()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("capability_ready");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        await SeedReadyControlPlaneAsync(context);
        var verifier = new StubPolicyVerifier();
        var store = new PostgreSqlEconomyCapabilityControlPlaneStore(context, verifier);
        var snapshot = await store.ReadSnapshotAsync(EvaluationContext(), CancellationToken.None);

        snapshot.HasActivePolicy.Should().BeTrue();
        snapshot.PolicySignatureValid.Should().BeTrue();
        snapshot.PolicyVersion.Should().Be(7);
        snapshot.JurisdictionAllowed.Should().BeTrue();
        snapshot.ComplianceAvailable.Should().BeTrue();
        snapshot.ManualReviewRequired.Should().BeFalse();
        snapshot.RiskDecisionId.Should().Be(RiskDecisionId);
        snapshot.LedgerHealthy.Should().BeTrue();
        snapshot.ProjectionMatches.Should().BeTrue();
        snapshot.ReserveSufficient.Should().BeTrue();
        snapshot.ReserveVersion.Should().Be(11);
        snapshot.CustodyReconciled.Should().BeTrue();
        snapshot.AnchorValid.Should().BeTrue();
        snapshot.ProviderReady.Should().BeTrue();
        snapshot.KillSwitchActive.Should().BeFalse();
        snapshot.KillSwitchEpoch.Should().Be(0);
        snapshot.EvidenceHashes.Should().HaveCount(6);
        verifier.Calls.Should().Be(1);

        var evaluator = new EconomyCapabilityEvaluator(store, new StubReceiptSigner());
        var result = await evaluator.EvaluateAsync(EvaluationContext(), CancellationToken.None);
        var receipt = result.Receipt!;
        (await context.Set<EconomyCapabilityReceiptRow>().AsNoTracking().SingleAsync()).ReceiptHash
            .Should().Be(receipt.ReceiptHash);

        await store.ConsumeAsync(
            receipt.Id,
            receipt.OperationFingerprint,
            receipt.TenantId,
            receipt.ActorId,
            receipt.KillSwitchEpoch,
            Now.AddSeconds(1),
            CancellationToken.None);
        (await context.Set<EconomyCapabilityReceiptConsumptionRow>().CountAsync()).Should().Be(1);

        await FluentActions.Awaiting(() => store.ConsumeAsync(
                receipt.Id,
                receipt.OperationFingerprint,
                receipt.TenantId,
                receipt.ActorId,
                receipt.KillSwitchEpoch,
                Now.AddSeconds(2),
                CancellationToken.None).AsTask())
            .Should().ThrowAsync<CapabilityReceiptConsumptionException>();
    }

    [Fact]
    public async Task RelevantKillSwitchInvalidatesReceiptEpochImmediately()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("capability_kill_switch");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        await SeedReadyControlPlaneAsync(context);
        context.Set<EconomyKillSwitchRow>().Add(new EconomyKillSwitchRow
        {
            Id = Guid.NewGuid(),
            ScopeKey = "tenant-capability",
            TenantId = TenantId,
            Capability = EconomyValueMovementCapability.PayoutExecution,
            Epoch = 9,
            IsActive = true,
            Reason = "journal-mismatch",
            ActivatedBy = ActorId,
            ActivatedAt = Now
        });
        await context.SaveChangesAsync();
        var store = new PostgreSqlEconomyCapabilityControlPlaneStore(context, new StubPolicyVerifier());

        var snapshot = await store.ReadSnapshotAsync(EvaluationContext(), CancellationToken.None);

        snapshot.KillSwitchActive.Should().BeTrue();
        snapshot.KillSwitchEpoch.Should().Be(9);
        var result = await new EconomyCapabilityEvaluator(store, new StubReceiptSigner())
            .EvaluateAsync(EvaluationContext(), CancellationToken.None);
        result.State.Should().Be(EconomyCapabilityReadinessStatus.KillSwitchActive);
        result.Receipt.Should().BeNull();
    }

    [Fact]
    public async Task KillSwitchActivatedAfterIssuancePreventsReceiptConsumption()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("capability_epoch_race");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        await SeedReadyControlPlaneAsync(context);
        var store = new PostgreSqlEconomyCapabilityControlPlaneStore(context, new StubPolicyVerifier());
        var receipt = (await new EconomyCapabilityEvaluator(store, new StubReceiptSigner())
            .EvaluateAsync(EvaluationContext(), CancellationToken.None)).Receipt!;

        context.Set<EconomyKillSwitchRow>().Add(new EconomyKillSwitchRow
        {
            Id = Guid.NewGuid(), ScopeKey = "global", Epoch = 1, IsActive = true,
            Reason = "containment", ActivatedBy = ActorId, ActivatedAt = Now.AddMilliseconds(1)
        });
        await context.SaveChangesAsync();

        await FluentActions.Awaiting(() => store.ConsumeAsync(
                receipt.Id, receipt.OperationFingerprint, receipt.TenantId, receipt.ActorId,
                receipt.KillSwitchEpoch, Now.AddSeconds(1), CancellationToken.None).AsTask())
            .Should().ThrowAsync<CapabilityReceiptConsumptionException>();
    }

    [Fact]
    public async Task ComplianceHoldRequiresReviewAndInvalidatesPreviouslyIssuedReceipt()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("capability_compliance_hold");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        await SeedReadyControlPlaneAsync(context);
        var store = new PostgreSqlEconomyCapabilityControlPlaneStore(context, new StubPolicyVerifier());
        var receipt = (await new EconomyCapabilityEvaluator(store, new StubReceiptSigner())
            .EvaluateAsync(EvaluationContext(), CancellationToken.None)).Receipt!;
        context.Set<EconomyComplianceHoldRow>().Add(new EconomyComplianceHoldRow
        {
            Id = Guid.NewGuid(), ScopeKey = $"{TenantId:N}:subject-hash:all", TenantId = TenantId,
            SubjectHash = "subject-hash", CaseReferenceHash = "case-hash", ReasonCode = "sanctions-review",
            EvidenceHash = "hold-evidence", IdempotencyKeyHash = "hold-key", RequestHash = "hold-request",
            ActivatedBy = ActorId, ActivatedAt = Now.AddMilliseconds(1), ExpiresAt = Now.AddHours(1)
        });
        await context.SaveChangesAsync();

        var snapshot = await store.ReadSnapshotAsync(EvaluationContext() with
        {
            EvaluatedAt = Now.AddSeconds(1)
        }, CancellationToken.None);
        snapshot.ManualReviewRequired.Should().BeTrue();
        snapshot.EvidenceHashes.Should().Contain("hold-evidence");

        await FluentActions.Awaiting(() => store.ConsumeAsync(
                receipt.Id, receipt.OperationFingerprint, receipt.TenantId, receipt.ActorId,
                receipt.KillSwitchEpoch, Now.AddSeconds(1), CancellationToken.None).AsTask())
            .Should().ThrowAsync<CapabilityReceiptConsumptionException>();
    }

    private static CapabilityAuthorizationReceipt Receipt() => new(
        Guid.NewGuid(),
        TenantId,
        ActorId,
        "subject-hash",
        "BR",
        EconomyValueMovementCapability.PayoutExecution,
        "operation-fingerprint",
        7,
        11,
        RiskDecisionId,
        0,
        "provider-hash",
        "destination-hash",
        ["root-a"],
        ["policy-hash"],
        Now,
        Now.AddMinutes(5),
        "receipt-hash",
        "kms-key",
        "signature");

    private static EconomyCapabilityEvaluationContext EvaluationContext() => new(
        TenantId,
        ActorId,
        "subject-hash",
        "BR",
        EconomyValueMovementCapability.PayoutExecution,
        RiskDecisionId,
        "operation-fingerprint",
        "provider-hash",
        "destination-hash",
        ["root-a"],
        Now);

    private static async Task SeedReadyControlPlaneAsync(ControlPlaneDbContext context)
    {
        var sourceWallet = Guid.NewGuid();
        var destinationWallet = Guid.NewGuid();
        context.Set<EconomyWalletRow>().AddRange(
            new EconomyWalletRow { Id = sourceWallet, OwnerId = Guid.NewGuid(), TenantId = TenantId, State = WalletLifecycleState.Active, CreatedAt = Now },
            new EconomyWalletRow { Id = destinationWallet, OwnerId = Guid.NewGuid(), TenantId = TenantId, State = WalletLifecycleState.Active, CreatedAt = Now });
        context.Set<EconomyRiskDecisionRow>().Add(new EconomyRiskDecisionRow
        {
            Id = RiskDecisionId,
            Outcome = RiskOutcome.Allow,
            OperationFingerprint = "operation-fingerprint",
            ActorHash = "actor-hash",
            TemplateKind = PostingTemplateKind.PayoutReservation,
            SourceWalletId = sourceWallet,
            DestinationWalletId = destinationWallet,
            Currency = CurrencyCode.HardCoin,
            AmountUnits = 10,
            CurrencyLegs = "[]",
            SourceRoots = "[]",
            ProviderReferenceHash = "provider-hash",
            PolicyVersion = 7,
            ReserveVersion = 11,
            ReserveAuthorizationEpoch = 1,
            FeatureVersion = 1,
            KillSwitchEpoch = 0,
            CounterVersion = 1,
            EntityGraphVersion = 1,
            EntityGraphEvidenceHash = "graph-hash",
            ReasonCodes = "[]",
            IssuedAt = Now.AddMinutes(-1),
            ExpiresAt = Now.AddMinutes(10)
        });
        context.Set<EconomyCapabilityPolicyRow>().Add(new EconomyCapabilityPolicyRow
        {
            Id = Guid.NewGuid(), ScopeKey = "tenant-policy", TenantId = TenantId,
            Capability = EconomyValueMovementCapability.PayoutExecution, JurisdictionCode = "BR", Version = 7,
            CanonicalPayload = "{}", PayloadHash = "policy-hash", KeyId = "kms-policy", Signature = "signature",
            ProposedBy = Guid.NewGuid(), ApprovedBy = Guid.NewGuid(), ProposedAt = Now.AddHours(-2),
            ApprovedAt = Now.AddHours(-1), EffectiveAt = Now.AddMinutes(-30), ExpiresAt = Now.AddMinutes(10),
            ProviderReady = true, IsActive = true
        });
        context.Set<EconomyComplianceEvidenceRow>().AddRange(
            Compliance("sumsub", "evt-kyc", ComplianceEvidenceKinds.KycAml, "kyc-hash"),
            Compliance("internal-financial-crime", "evt-fc", ComplianceEvidenceKinds.FinancialCrime, "financial-hash"),
            Compliance("internal-trust-safety", "evt-ts", ComplianceEvidenceKinds.TrustSafety, "trust-hash"));
        context.Set<EconomyChainHeadRow>().Update(new EconomyChainHeadRow { Id = 1, Sequence = 100, Hash = "chain-hash", UpdatedAt = Now });
        context.Set<EconomyJournalVerificationCheckpointRow>().Add(new EconomyJournalVerificationCheckpointRow
        {
            Id = Guid.NewGuid(), FromSequence = 1, ToSequence = 100, PreviousHash = "previous-hash",
            CurrentHash = "chain-hash", IsValid = true, FencingToken = 1, StartedAt = Now.AddMinutes(-1), CompletedAt = Now
        });
        context.Set<EconomyProjectionGenerationRow>().Add(new EconomyProjectionGenerationRow
        {
            Id = Guid.NewGuid(), Generation = 2, FromSequence = 1, ToSequence = 100,
            ProjectionHash = "projection-hash", JournalHash = "chain-hash", State = "Active", IsActive = true, ProposedBy = Guid.NewGuid(),
            ApprovedBy = Guid.NewGuid(), StartedAt = Now.AddMinutes(-2), CompletedAt = Now.AddMinutes(-1), ActivatedAt = Now
        });
        context.Set<EconomyReserveHeadRow>().Add(new EconomyReserveHeadRow
        {
            Version = 11, IsActive = true, PolicyVersion = 7, AuthorizationEpoch = 1,
            ObservedAt = Now.AddMinutes(-1), ExpiresAt = Now.AddMinutes(10), HardFaceValueUsdMinor = 1,
            RequiredHardReserveUsdMinor = 1, SoftFaceValueUsdNanos = 1, StressedExpectedRedemptionCostUsdNanos = 1,
            RequiredSoftReserveUsdNanos = 1, HardBackingUsdNanos = 1_000_000, SoftBackingUsdNanos = 1,
            Coverage = ReserveCoverageState.Covered, EvidenceHash = "reserve-hash", ActivatedAt = Now
        });
        var custodyObservationId = Guid.NewGuid();
        context.Set<EconomyCustodyObservationRow>().Add(new EconomyCustodyObservationRow
        {
            Id = custodyObservationId, Provider = "custody", AssetKey = "usd", Purpose = ReserveBackingPurpose.HardCoin,
            Version = 1, EligibleUsdNanos = 1, ObservedAt = Now.AddMinutes(-1), ExpiresAt = Now.AddMinutes(10),
            PayloadHash = "custody-payload", KeyId = "custody-key", Signature = "custody-signature"
        });
        context.Set<EconomyCustodyReconciliationRow>().Add(new EconomyCustodyReconciliationRow
        {
            Id = Guid.NewGuid(), ReserveVersion = 11, ObservationIds = $"[\"{custodyObservationId}\"]", LiabilityUsdNanos = 1,
            EligibleAssetUsdNanos = 1, VarianceUsdNanos = 0, IsReconciled = true,
            EvidenceHash = "custody-hash", ReconciledBy = ActorId, ReconciledAt = Now
        });
        var anchorId = Guid.NewGuid();
        context.Set<EconomyExternalAnchorRow>().Add(new EconomyExternalAnchorRow
        {
            Id = anchorId, JournalSequence = 100, JournalHash = "chain-hash", Signature = "anchor-signature",
            WormReference = "s3://worm", Provider = "s3-object-lock", ProviderReference = "object-version",
            AnchoredAt = Now
        });
        context.Set<EconomyAnchorVerificationRow>().Add(new EconomyAnchorVerificationRow
        {
            Id = Guid.NewGuid(), ExternalAnchorId = anchorId, KeyId = "kms-anchor", ObjectVersion = "v1",
            ETag = "etag", RetainUntil = Now.AddYears(1), ObjectHash = "anchor-object-hash",
            SignatureValid = true, ObjectMatches = true, VerifiedAt = Now
        });
        await context.SaveChangesAsync();
    }

    private static EconomyComplianceEvidenceRow Compliance(
        string provider,
        string eventId,
        string kind,
        string evidenceHash) => new()
    {
        Id = Guid.NewGuid(), Provider = provider, Environment = "sandbox", ProviderEventId = eventId,
        TenantId = TenantId, SubjectHash = "subject-hash", EvidenceKind = kind, Version = 1,
        Result = "Approved", PolicyVersion = 7, PayloadHash = $"{eventId}-payload", SignatureVerified = true,
        RawObjectReference = "s3://opaque", EvidenceHash = evidenceHash, IssuedAt = Now.AddMinutes(-2),
        ExpiresAt = Now.AddMinutes(10), ReceivedAt = Now.AddMinutes(-1)
    };

    private static ControlPlaneDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options)
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

    private sealed class StubReceiptSigner : ICapabilityReceiptSigner
    {
        public ValueTask<CapabilityReceiptSignature> SignAsync(
            string canonicalPayload,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new CapabilityReceiptSignature("kms-receipt", "signature"));
    }

    private sealed class StubApplicationDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
