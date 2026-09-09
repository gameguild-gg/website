using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Projections;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using System.Reflection;

namespace GameGuild.Finance.Economy.UnitTests.Projections;

public sealed class PostgreSqlProjectionGenerationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RebuildFencesAuthorizableBalanceAndRequiresTwoIndependentApprovalsForCutover()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("projection_generation");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var walletId = await SeedJournalAndInflatedProjectionAsync(context);
        var service = new PostgreSqlProjectionGenerationService(context);
        var proposer = Guid.NewGuid();

        var generation = await service.RebuildAsync(proposer, Now, CancellationToken.None);

        generation.MismatchCount.Should().Be(1);
        generation.IsActive.Should().BeFalse();
        var fenced = await context.Set<EconomyWalletBalanceProjectionRow>().SingleAsync();
        fenced.AvailableHardToSpend.Should().Be(50);
        fenced.ReviewState.Should().Be(WalletReviewState.ReviewRequired);
        (await context.Set<EconomyProjectionReconciliationEventRow>().CountAsync()).Should().Be(1);

        var selfApproval = () => service.ApproveAndTryActivateAsync(
            generation.Generation, proposer, "reauth-proposer", Now.AddMinutes(1), CancellationToken.None).AsTask();
        await selfApproval.Should().ThrowAsync<ProjectionGenerationException>().WithMessage("*cannot approve*");

        var first = await service.ApproveAndTryActivateAsync(
            generation.Generation, Guid.NewGuid(), "reauth-one", Now.AddMinutes(1), CancellationToken.None);
        first.IsActive.Should().BeFalse();
        first.State.Should().Be("AwaitingSecondApproval");
        var second = await service.ApproveAndTryActivateAsync(
            generation.Generation, Guid.NewGuid(), "reauth-two", Now.AddMinutes(2), CancellationToken.None);
        second.IsActive.Should().BeTrue();
        second.ApprovedBy.Should().HaveCount(2);
        var active = await context.Set<EconomyWalletBalanceProjectionRow>().SingleAsync(row => row.WalletId == walletId);
        active.PurchasedHard.Should().Be(50);
        active.AvailableHardToSpend.Should().Be(50);
        active.ReviewState.Should().Be(WalletReviewState.Healthy);
        active.SourceJournalSequence.Should().Be(1);
    }

    [Fact]
    public void ProjectionComparisonChecksEveryPersistedBalanceAndEvidenceField()
    {
        var matches = typeof(PostgreSqlProjectionGenerationService)
            .GetMethod("Matches", BindingFlags.NonPublic | BindingFlags.Static)!;
        var row = new EconomyWalletBalanceProjectionRow
        {
            PendingHard = 1,
            PendingSoft = 0,
            PurchasedHard = 2,
            EarnedHard = 3,
            RestrictedHard = 4,
            Soft = 5,
            ImmatureEarnedHard = 6,
            HeldHard = 7,
            HeldSoft = 8,
            AvailableHardToSpend = 9,
            AvailableSoftToSpend = 10,
            WithdrawableHard = 11,
            SourceJournalSequence = 12,
            ProjectionHash = "hash"
        };
        object?[] expected = [row, 1L, 2L, 3L, 4L, 5L, 6L, 7L, 8L, 9L, 10L, 11L, 12L, "hash"];
        bool Invoke() => (bool)matches.Invoke(null, expected)!;

        Invoke().Should().BeTrue();
        string[] properties =
        [
            nameof(row.PendingHard), nameof(row.PendingSoft), nameof(row.PurchasedHard), nameof(row.EarnedHard),
            nameof(row.RestrictedHard), nameof(row.Soft), nameof(row.ImmatureEarnedHard), nameof(row.HeldHard),
            nameof(row.HeldSoft), nameof(row.AvailableHardToSpend), nameof(row.AvailableSoftToSpend),
            nameof(row.WithdrawableHard), nameof(row.SourceJournalSequence)
        ];
        foreach (var propertyName in properties)
        {
            var property = typeof(EconomyWalletBalanceProjectionRow).GetProperty(propertyName)!;
            var original = (long)property.GetValue(row)!;
            property.SetValue(row, original + 1);
            Invoke().Should().BeFalse(propertyName);
            property.SetValue(row, original);
        }
        row.ProjectionHash = "different";
        Invoke().Should().BeFalse();
    }

    private static async Task<Guid> SeedJournalAndInflatedProjectionAsync(ProjectionDbContext context)
    {
        var walletId = Guid.NewGuid();
        var walletAccount = Guid.NewGuid();
        var clearing = Guid.NewGuid();
        var postingId = Guid.NewGuid();
        var riskDecisionId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        const string requestHash = "projection-request";
        var hash = JournalIntegrityVerifier.ComputeSqlWriterVerificationHash(
            1, JournalChain.GenesisHash, postingId, requestHash);
        context.Set<EconomyWalletRow>().Add(new EconomyWalletRow
        {
            Id = walletId, OwnerId = Guid.NewGuid(), TenantId = Guid.NewGuid(),
            State = WalletLifecycleState.Active, CreatedAt = Now
        });
        context.Set<EconomyRiskDecisionRow>().Add(new EconomyRiskDecisionRow
        {
            Id = riskDecisionId,
            Outcome = GameGuild.Finance.Economy.Risk.RiskOutcome.Allow,
            OperationFingerprint = $"projection-{postingId:N}",
            ActorHash = "projection-actor",
            TemplateKind = PostingTemplateKind.Spend,
            SourceWalletId = walletId,
            DestinationWalletId = walletId,
            Currency = CurrencyCode.HardCoin,
            AmountUnits = 50,
            CurrencyLegs = "[]",
            SourceRoots = "[]",
            ProviderReferenceHash = "projection-provider",
            PolicyVersion = 1,
            ReserveVersion = 1,
            ReserveAuthorizationEpoch = 1,
            FeatureVersion = 1,
            KillSwitchEpoch = 0,
            CounterVersion = 1,
            EntityGraphVersion = 0,
            EntityGraphEvidenceHash = "projection-graph",
            ReasonCodes = "[]",
            IssuedAt = Now.AddMinutes(-1),
            ExpiresAt = Now.AddMinutes(5)
        });
        await context.SaveChangesAsync();
        context.Set<EconomyAccountRow>().AddRange(
            new EconomyAccountRow
            {
                Id = walletAccount, WalletId = walletId, Code = EconomyAccountCode.PurchasedHardLiability,
                Currency = CurrencyCode.HardCoin, Provenance = ProvenanceKind.PurchasedHard, CreatedAt = Now
            },
            new EconomyAccountRow
            {
                Id = clearing, Code = EconomyAccountCode.ExternalClearingHard,
                Currency = CurrencyCode.HardCoin, CreatedAt = Now
            });
        context.Set<EconomyPostingGroupRow>().Add(new EconomyPostingGroupRow
        {
            Id = postingId, IdempotencyKey = "projection", TemplateKind = PostingTemplateKind.Spend,
            TemplateVersion = 1, Authority = PostingAuthority.WalletOwner, Status = PostingStatus.Accepted,
            CapabilityId = Guid.NewGuid(), ActorId = Guid.NewGuid(), TenantId = Guid.NewGuid(),
            RiskDecisionId = riskDecisionId, PolicyVersion = 1, ReserveVersion = 1,
            ReserveAuthorizationEpoch = 1, RecordedAt = Now
        });
        context.Set<EconomyJournalEntryRow>().Add(new EconomyJournalEntryRow
        {
            Id = entryId, PostingGroupId = postingId, Sequence = 1, PreviousHash = JournalChain.GenesisHash,
            CanonicalPayloadHash = requestHash, HashAlgorithmVersion = 2, Hash = hash, RecordedAt = Now
        });
        context.Set<EconomyJournalLineRow>().AddRange(
            new EconomyJournalLineRow
            {
                Id = Guid.NewGuid(), JournalEntryId = entryId, AccountId = walletAccount, WalletId = walletId,
                Sequence = 1, Side = EntrySide.Credit, Currency = CurrencyCode.HardCoin,
                AmountUnits = 50, Provenance = ProvenanceKind.PurchasedHard
            },
            new EconomyJournalLineRow
            {
                Id = Guid.NewGuid(), JournalEntryId = entryId, AccountId = clearing,
                Sequence = 2, Side = EntrySide.Debit, Currency = CurrencyCode.HardCoin, AmountUnits = 50
            });
        context.Set<EconomyIdempotencyRecordRow>().Add(new EconomyIdempotencyRecordRow
        {
            Id = Guid.NewGuid(), Key = "projection", RequestHash = requestHash,
            PostingGroupId = postingId, CreatedAt = Now
        });
        context.Set<EconomyChainHeadRow>().Update(new EconomyChainHeadRow
        {
            Id = 1, Sequence = 1, Hash = hash, UpdatedAt = Now
        });
        context.Set<EconomyJournalVerificationCheckpointRow>().Add(new EconomyJournalVerificationCheckpointRow
        {
            Id = Guid.NewGuid(), FromSequence = 1, ToSequence = 1,
            PreviousHash = JournalChain.GenesisHash, CurrentHash = hash, IsValid = true,
            FencingToken = 1, StartedAt = Now, CompletedAt = Now
        });
        context.Set<EconomyWalletBalanceProjectionRow>().Add(new EconomyWalletBalanceProjectionRow
        {
            WalletId = walletId, PurchasedHard = 100, AvailableHardToSpend = 100,
            ReviewState = WalletReviewState.Healthy, SourceJournalSequence = 1,
            ProjectionHash = "inflated-live", RebuiltAt = Now
        });
        await context.SaveChangesAsync();
        return walletId;
    }

    private static ProjectionDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ProjectionDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class ProjectionDbContext(DbContextOptions<ProjectionDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new EconomyModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }
}
