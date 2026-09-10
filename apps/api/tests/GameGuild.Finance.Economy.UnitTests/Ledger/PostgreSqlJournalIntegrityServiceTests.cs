using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Finance.Economy.Writer;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using System.Reflection;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class PostgreSqlJournalIntegrityServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task VerifiesPersistedJournalIncrementAndStoresFencedCheckpoint()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("journal_worker_valid");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        await SeedEntryAsync(context, validHash: true);
        var killSwitches = new StubKillSwitchStore();
        var service = new PostgreSqlJournalIntegrityService(context, new JournalIntegrityVerifier(), killSwitches);

        var result = await service.RunIncrementAsync("worker-one", Now, 100, CancellationToken.None);

        result.Status.Should().Be(JournalIntegrityRunStatus.Verified);
        result.Verification!.IsValid.Should().BeTrue();
        result.Verification.ToSequence.Should().Be(1);
        var checkpoint = await context.Set<EconomyJournalVerificationCheckpointRow>().SingleAsync();
        checkpoint.IsValid.Should().BeTrue();
        checkpoint.FencingToken.Should().Be(1);
        killSwitches.Activations.Should().BeEmpty();
    }

    [Fact]
    public async Task CorruptionPersistsFailureAndActivatesGlobalKillSwitchImmediately()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("journal_worker_invalid");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        await SeedEntryAsync(context, validHash: false);
        var killSwitches = new StubKillSwitchStore();
        var service = new PostgreSqlJournalIntegrityService(context, new JournalIntegrityVerifier(), killSwitches);

        var result = await service.RunIncrementAsync("worker-one", Now, 100, CancellationToken.None);

        result.Status.Should().Be(JournalIntegrityRunStatus.Failed);
        result.Verification!.FailureCode.Should().Be(JournalIntegrityFailureCode.EntryHashMismatch);
        (await context.Set<EconomyJournalVerificationCheckpointRow>().SingleAsync()).IsValid.Should().BeFalse();
        killSwitches.Activations.Should().ContainSingle()
            .Which.Scope.Should().Be(EconomyKillSwitchScope.Global);
    }

    [Fact]
    public async Task ActiveLeaseFencesConcurrentWorker()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("journal_worker_lease");
        await using var firstContext = CreateContext(database.ConnectionString);
        await firstContext.Database.EnsureCreatedAsync();
        await SeedEntryAsync(firstContext, validHash: true);
        await using var secondContext = CreateContext(database.ConnectionString);
        var killSwitches = new StubKillSwitchStore();
        var first = new PostgreSqlJournalIntegrityService(firstContext, new JournalIntegrityVerifier(), killSwitches);
        var second = new PostgreSqlJournalIntegrityService(secondContext, new JournalIntegrityVerifier(), killSwitches);

        (await first.RunIncrementAsync("worker-one", Now, 100, CancellationToken.None)).Status
            .Should().Be(JournalIntegrityRunStatus.Verified);
        (await second.RunIncrementAsync("worker-two", Now.AddSeconds(1), 100, CancellationToken.None)).Status
            .Should().Be(JournalIntegrityRunStatus.LeaseUnavailable);
    }

    [Fact]
    public async Task EmptyJournalCreatesGenesisCheckpointForSafeReads()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("journal_worker_empty");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        context.Set<EconomyChainHeadRow>().Update(new EconomyChainHeadRow
        {
            Id = 1, Sequence = 0, Hash = JournalChain.GenesisHash, UpdatedAt = Now
        });
        await context.SaveChangesAsync();
        var service = new PostgreSqlJournalIntegrityService(
            context, new JournalIntegrityVerifier(), new StubKillSwitchStore());

        var result = await service.RunIncrementAsync("worker-one", Now, 100, CancellationToken.None);

        result.Status.Should().Be(JournalIntegrityRunStatus.Verified);
        var checkpoint = await context.Set<EconomyJournalVerificationCheckpointRow>().SingleAsync();
        checkpoint.FromSequence.Should().Be(0);
        checkpoint.ToSequence.Should().Be(0);
        checkpoint.CurrentHash.Should().Be(JournalChain.GenesisHash);
    }

    [Fact]
    public void SourceAndAllocationValidationFailClosedForEveryMissingOrMismatchedBinding()
    {
        var sourceMethod = typeof(PostgreSqlJournalIntegrityService)
            .GetMethod("ValidateSource", BindingFlags.NonPublic | BindingFlags.Static)!;
        var allocationMethod = typeof(PostgreSqlJournalIntegrityService)
            .GetMethod("ValidateAllocations", BindingFlags.NonPublic | BindingFlags.Static)!;
        var tenantId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var group = new EconomyPostingGroupRow { TenantId = tenantId };
        var optional = new PostingTemplateRegistration(
            PostingTemplateKind.Spend, 1, PostingAuthority.WalletOwner, 2, null, true);
        var required = optional with { RequiredSourceState = SourceConfirmationState.Confirmed };
        var source = new EconomySourceStampRow
        {
            Id = sourceId,
            TenantId = tenantId,
            State = SourceConfirmationState.Confirmed,
            EvidenceHash = "evidence",
            ObservedAt = Now
        };
        var sources = new Dictionary<Guid, EconomySourceStampRow> { [sourceId] = source };
        bool ValidateSource(PostingTemplateRegistration? registration) =>
            (bool)sourceMethod.Invoke(null, [group, registration, sources])!;

        ValidateSource(null).Should().BeFalse();
        ValidateSource(optional).Should().BeTrue();
        ValidateSource(required).Should().BeFalse();
        group.SourceStampId = sourceId;
        ValidateSource(optional).Should().BeTrue();
        ValidateSource(required).Should().BeTrue();
        source.TenantId = Guid.NewGuid();
        ValidateSource(required).Should().BeFalse();
        source.TenantId = tenantId;
        source.State = SourceConfirmationState.Observed;
        ValidateSource(required).Should().BeFalse();
        sources.Clear();
        ValidateSource(required).Should().BeFalse();

        var line = new EconomyJournalLineRow
        {
            Id = Guid.NewGuid(), Currency = CurrencyCode.HardCoin, AmountUnits = 10
        };
        var lotId = Guid.NewGuid();
        var lot = new EconomyCreditLotRow
        {
            Id = lotId, Currency = CurrencyCode.HardCoin, AmountUnits = 10
        };
        var lots = new Dictionary<Guid, EconomyCreditLotRow> { [lotId] = lot };
        bool ValidateAllocations(params EconomyEntryAllocationRow[] allocations) =>
            (bool)allocationMethod.Invoke(null,
                [new[] { line }, allocations, lots])!;
        EconomyEntryAllocationRow Allocation(long amount, Guid? parentLotId = null) => new()
        {
            Id = Guid.NewGuid(), JournalLineId = line.Id,
            ParentLotId = parentLotId ?? lotId, AmountUnits = amount
        };

        ValidateAllocations().Should().BeTrue();
        ValidateAllocations(Allocation(0)).Should().BeFalse();
        ValidateAllocations(Allocation(11)).Should().BeFalse();
        ValidateAllocations(Allocation(1, Guid.NewGuid())).Should().BeFalse();
        lot.Currency = CurrencyCode.SoftCoin;
        ValidateAllocations(Allocation(1)).Should().BeFalse();
        lot.Currency = CurrencyCode.HardCoin;
        ValidateAllocations(Allocation(1)).Should().BeTrue();
    }

    private static async Task SeedEntryAsync(JournalWorkerDbContext context, bool validHash)
    {
        var postingId = Guid.NewGuid();
        var riskDecisionId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var sourceWalletId = Guid.NewGuid();
        var destinationWalletId = Guid.NewGuid();
        var accountDebit = Guid.NewGuid();
        var accountCredit = Guid.NewGuid();
        const string requestHash = "request-hash";
        var hash = JournalIntegrityVerifier.ComputeSqlWriterVerificationHash(
            1, JournalChain.GenesisHash, postingId, requestHash);
        context.Set<EconomyWalletRow>().AddRange(
            new EconomyWalletRow
            {
                Id = sourceWalletId, OwnerId = actorId, TenantId = tenantId,
                State = WalletLifecycleState.Active, CreatedAt = Now
            },
            new EconomyWalletRow
            {
                Id = destinationWalletId, OwnerId = Guid.NewGuid(), TenantId = tenantId,
                State = WalletLifecycleState.Active, CreatedAt = Now
            });
        context.Set<EconomyRiskDecisionRow>().Add(new EconomyRiskDecisionRow
        {
            Id = riskDecisionId,
            Outcome = RiskOutcome.Allow,
            OperationFingerprint = $"journal-worker-{postingId:N}",
            ActorHash = "journal-worker-actor",
            TemplateKind = PostingTemplateKind.PayoutReservation,
            SourceWalletId = sourceWalletId,
            DestinationWalletId = destinationWalletId,
            Currency = CurrencyCode.HardCoin,
            AmountUnits = 10,
            CurrencyLegs = "[]",
            SourceRoots = "[]",
            ProviderReferenceHash = "journal-worker-provider",
            PolicyVersion = 1,
            ReserveVersion = 1,
            ReserveAuthorizationEpoch = 1,
            FeatureVersion = 1,
            KillSwitchEpoch = 0,
            CounterVersion = 1,
            EntityGraphVersion = 0,
            EntityGraphEvidenceHash = "journal-worker-graph",
            ReasonCodes = "[]",
            IssuedAt = Now.AddMinutes(-1),
            ExpiresAt = Now.AddMinutes(5)
        });
        await context.SaveChangesAsync();
        context.Set<EconomyAccountRow>().AddRange(
            new EconomyAccountRow { Id = accountDebit, Code = EconomyAccountCode.ExternalClearingHard, Currency = CurrencyCode.HardCoin, CreatedAt = Now },
            new EconomyAccountRow { Id = accountCredit, Code = EconomyAccountCode.HardCoinReserve, Currency = CurrencyCode.HardCoin, CreatedAt = Now });
        context.Set<EconomyPostingGroupRow>().Add(new EconomyPostingGroupRow
        {
            Id = postingId, IdempotencyKey = "journal-worker", TemplateKind = PostingTemplateKind.PayoutReservation,
            TemplateVersion = 1, Authority = PostingAuthority.PayoutCoordinator, Status = PostingStatus.Accepted,
            CapabilityId = Guid.NewGuid(), ActorId = actorId, TenantId = tenantId,
            RiskDecisionId = riskDecisionId, PolicyVersion = 1, ReserveVersion = 1,
            ReserveAuthorizationEpoch = 1, RecordedAt = Now
        });
        var entryId = Guid.NewGuid();
        context.Set<EconomyJournalEntryRow>().Add(new EconomyJournalEntryRow
        {
            Id = entryId, PostingGroupId = postingId, Sequence = 1, PreviousHash = JournalChain.GenesisHash,
            CanonicalPayloadHash = requestHash, HashAlgorithmVersion = 2,
            Hash = validHash ? hash : "invalid-hash", RecordedAt = Now
        });
        context.Set<EconomyJournalLineRow>().AddRange(
            new EconomyJournalLineRow
            {
                Id = Guid.NewGuid(), JournalEntryId = entryId, AccountId = accountDebit, Sequence = 1,
                Side = EntrySide.Debit, Currency = CurrencyCode.HardCoin, AmountUnits = 10
            },
            new EconomyJournalLineRow
            {
                Id = Guid.NewGuid(), JournalEntryId = entryId, AccountId = accountCredit, Sequence = 2,
                Side = EntrySide.Credit, Currency = CurrencyCode.HardCoin, AmountUnits = 10
            });
        context.Set<EconomyIdempotencyRecordRow>().Add(new EconomyIdempotencyRecordRow
        {
            Id = Guid.NewGuid(), Key = "journal-worker", RequestHash = requestHash,
            PostingGroupId = postingId, CreatedAt = Now
        });
        context.Set<EconomyChainHeadRow>().Update(new EconomyChainHeadRow
        {
            Id = 1, Sequence = 1, Hash = validHash ? hash : "invalid-hash", UpdatedAt = Now
        });
        await context.SaveChangesAsync();
    }

    private static JournalWorkerDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<JournalWorkerDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class JournalWorkerDbContext(DbContextOptions<JournalWorkerDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new EconomyModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class StubKillSwitchStore : IEconomyKillSwitchStore
    {
        public List<(EconomyKillSwitchScope Scope, string Reason)> Activations { get; } = [];

        public ValueTask<EconomyKillSwitchState> ActivateAsync(
            Guid activationId, EconomyKillSwitchScope scope, string reason, Guid actorId,
            DateTimeOffset activatedAt, CancellationToken cancellationToken)
        {
            Activations.Add((scope, reason));
            return ValueTask.FromResult(new EconomyKillSwitchState(
                activationId, scope, 1, true, reason, actorId, activatedAt, null, null, [], null));
        }

        public ValueTask<EconomyKillSwitchState> ProposeReleaseAsync(Guid killSwitchId, Guid actorId, string reauthenticationHash, DateTimeOffset proposedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<EconomyKillSwitchState> ApproveReleaseAsync(Guid killSwitchId, Guid actorId, string reauthenticationHash, DateTimeOffset approvedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<EconomyKillSwitchState> TryReleaseAsync(Guid killSwitchId, DateTimeOffset releasedAt, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
