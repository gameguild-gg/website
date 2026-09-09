using System.Security.Cryptography;
using System.Text;
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

namespace GameGuild.Finance.Economy.UnitTests.Reserves;

public sealed class PostgreSqlReserveCustodyControlPlaneTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SignedCustodyProposalRequiresIndependentApprovalAndProducesAuthoritativeHead()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("reserve_control_plane");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        await SeedVerifiedGenesisAsync(context);
        var verifier = new DeterministicSignatureVerifier();
        var control = new PostgreSqlReserveCustodyControlPlane(context, verifier);
        var hard = SignedObservation("custody", "usd-cash", ReserveBackingPurpose.HardCoin, 1, 10_000_000_000);
        var soft = SignedObservation("custody", "provider-credit", ReserveBackingPurpose.SoftCoin, 1, 10_000_000_000);
        await control.IngestObservationAsync(hard, CancellationToken.None);
        await control.IngestObservationAsync(soft, CancellationToken.None);
        var proposer = Guid.NewGuid();
        var proposal = await control.ProposeAsync(new DurableReserveProposalCommand(
            Guid.NewGuid(), 1, null, 1, 1, Now, Now.AddHours(1),
            new ReserveBufferPosition(0, 0, 0, 0, 0, 0, 0), [], [hard.Id, soft.Id], 0,
            proposer, Now), CancellationToken.None);

        proposal.Coverage.Should().Be(ReserveCoverageState.Covered);
        var selfApproval = () => control.ApproveAndActivateAsync(
            proposal.Id, proposer, "reauth", Now.AddMinutes(1), CancellationToken.None).AsTask();
        await selfApproval.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot approve*");

        var head = await control.ApproveAndActivateAsync(
            proposal.Id, Guid.NewGuid(), "reauth", Now.AddMinutes(1), CancellationToken.None);
        head.Coverage.Should().Be(ReserveCoverageState.Covered);
        head.AssetAllocations.Should().HaveCount(2);
        var current = await control.CurrentHeadAsync(Now.AddMinutes(2), CancellationToken.None);
        current.Should().BeEquivalentTo(head);
        var authorization = await control.AuthorizeAsync(
            new ReserveVersion(1), 1, Now.AddMinutes(2), CancellationToken.None);
        authorization.Version.Value.Should().Be(1);
        (await context.Set<EconomyCustodyReconciliationRow>().SingleAsync()).IsReconciled.Should().BeTrue();
    }

    [Fact]
    public async Task LiabilitySnapshotComesFromJournalLiabilityAccountsAndRejectsStaleCustody()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("reserve_liabilities");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        await SeedLiabilityJournalAsync(context);
        var verifier = new DeterministicSignatureVerifier();
        var control = new PostgreSqlReserveCustodyControlPlane(context, verifier);

        var liabilities = await control.CalculateLiabilitiesAsync(CancellationToken.None);

        liabilities.OutstandingHardUnits.Should().Be(25);
        liabilities.OutstandingSoftUnits.Should().Be(1_000);
        liabilities.JournalSequence.Should().Be(1);
        var stale = SignedObservation("custody", "stale", ReserveBackingPurpose.HardCoin, 1, 100)
            with { ObservedAt = Now.AddHours(-2), ExpiresAt = Now.AddHours(-1) };
        var canonical = PostgreSqlReserveCustodyControlPlane.CanonicalObservationPayload(stale);
        stale = stale with
        {
            PayloadHash = PostgreSqlReserveCustodyControlPlane.Hash(canonical),
            Signature = DeterministicSignatureVerifier.Sign(canonical)
        };
        await control.IngestObservationAsync(stale, CancellationToken.None);
        var propose = () => control.ProposeAsync(new DurableReserveProposalCommand(
            Guid.NewGuid(), 1, null, 1, 1, Now.AddHours(-2), Now.AddHours(1),
            new ReserveBufferPosition(0, 0, 0, 0, 0, 0, 0), [], [stale.Id], 0,
            Guid.NewGuid(), Now), CancellationToken.None).AsTask();
        await propose.Should().ThrowAsync<ReserveInputUnknownException>().WithMessage("*stale*");
    }

    [Fact]
    public async Task ObservationAndProposalBoundaryContractsFailClosed()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("reserve_boundaries");
        await using var context = CreateContext(database.ConnectionString);
        var control = new PostgreSqlReserveCustodyControlPlane(context, new DeterministicSignatureVerifier());
        var observation = SignedObservation("custody", "asset", ReserveBackingPurpose.HardCoin, 1, 100);
        Func<CustodyObservationCommand, Task> ingest = command =>
            control.IngestObservationAsync(command, CancellationToken.None).AsTask();

        await FluentActions.Awaiting(() => ingest(null!)).Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => ingest(observation with { Id = Guid.Empty })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => ingest(observation with { Provider = " " })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => ingest(observation with { AssetKey = " " })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => ingest(observation with { Purpose = (ReserveBackingPurpose)int.MaxValue })).Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => ingest(observation with { Version = 0 })).Should().ThrowAsync<CustodyObservationException>();
        await FluentActions.Awaiting(() => ingest(observation with { EligibleUsdNanos = -1 })).Should().ThrowAsync<CustodyObservationException>();
        await FluentActions.Awaiting(() => ingest(observation with { ExpiresAt = observation.ObservedAt })).Should().ThrowAsync<CustodyObservationException>();
        await FluentActions.Awaiting(() => ingest(observation with { PayloadHash = " " })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => ingest(observation with { KeyId = " " })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => ingest(observation with { Signature = " " })).Should().ThrowAsync<ArgumentException>();

        var proposal = new DurableReserveProposalCommand(
            Guid.NewGuid(), 1, null, 1, 1, Now, Now.AddHours(1),
            new ReserveBufferPosition(0, 0, 0, 0, 0, 0, 0), [], [], 0, Guid.NewGuid(), Now);
        Func<DurableReserveProposalCommand, Task> propose = command =>
            control.ProposeAsync(command, CancellationToken.None).AsTask();
        await FluentActions.Awaiting(() => propose(null!)).Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => propose(proposal with { Id = Guid.Empty })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => propose(proposal with { ProposedBy = Guid.Empty })).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => propose(proposal with { Version = 0 })).Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => propose(proposal with { PolicyVersion = 0 })).Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => propose(proposal with { AuthorizationEpoch = 0 })).Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => propose(proposal with { ObservedAt = Now.AddTicks(1) })).Should().ThrowAsync<ReserveInputUnknownException>();
        await FluentActions.Awaiting(() => propose(proposal with { ExpiresAt = Now })).Should().ThrowAsync<ReserveInputUnknownException>();
        await FluentActions.Awaiting(() => propose(proposal with { Buffers = null! })).Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => propose(proposal with { Services = null! })).Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => propose(proposal with { CustodyObservationIds = null! })).Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => propose(proposal with { IrreversibleInFlightProviderCostUsdNanos = -1 })).Should().ThrowAsync<ArgumentOutOfRangeException>();

        new CustodyObservationException("invalid").Message.Should().Be("invalid");
    }

    private static CustodyObservationCommand SignedObservation(
        string provider,
        string asset,
        ReserveBackingPurpose purpose,
        long version,
        long amount)
    {
        var command = new CustodyObservationCommand(
            Guid.NewGuid(), provider, asset, purpose, version, amount,
            Now, Now.AddHours(1), "pending", "custody-key", "pending");
        var canonical = PostgreSqlReserveCustodyControlPlane.CanonicalObservationPayload(command);
        return command with
        {
            PayloadHash = PostgreSqlReserveCustodyControlPlane.Hash(canonical),
            Signature = DeterministicSignatureVerifier.Sign(canonical)
        };
    }

    private static async Task SeedVerifiedGenesisAsync(ReserveDbContext context)
    {
        context.Set<EconomyChainHeadRow>().Update(new EconomyChainHeadRow
        {
            Id = 1, Sequence = 0, Hash = JournalChain.GenesisHash, UpdatedAt = Now
        });
        context.Set<EconomyJournalVerificationCheckpointRow>().Add(new EconomyJournalVerificationCheckpointRow
        {
            Id = Guid.NewGuid(), FromSequence = 0, ToSequence = 0,
            PreviousHash = JournalChain.GenesisHash, CurrentHash = JournalChain.GenesisHash,
            IsValid = true, FencingToken = 1, StartedAt = Now, CompletedAt = Now
        });
        await context.SaveChangesAsync();
    }

    private static async Task SeedLiabilityJournalAsync(ReserveDbContext context)
    {
        var postingId = Guid.NewGuid();
        var hard = Guid.NewGuid();
        var soft = Guid.NewGuid();
        var clearingHard = Guid.NewGuid();
        var clearingSoft = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var riskDecisionId = Guid.NewGuid();
        const string requestHash = "reserve-liability-request";
        var hash = JournalIntegrityVerifier.ComputeSqlWriterVerificationHash(
            1, JournalChain.GenesisHash, postingId, requestHash);
        context.Set<EconomyWalletRow>().Add(new EconomyWalletRow
        {
            Id = walletId, OwnerId = Guid.NewGuid(), TenantId = Guid.NewGuid(),
            State = WalletLifecycleState.Active, CreatedAt = Now
        });
        context.Set<EconomyAccountRow>().AddRange(
            new EconomyAccountRow { Id = hard, WalletId = walletId, Code = EconomyAccountCode.PurchasedHardLiability, Currency = CurrencyCode.HardCoin, Provenance = ProvenanceKind.PurchasedHard, CreatedAt = Now },
            new EconomyAccountRow { Id = soft, WalletId = walletId, Code = EconomyAccountCode.SoftCoinLiability, Currency = CurrencyCode.SoftCoin, CreatedAt = Now },
            new EconomyAccountRow { Id = clearingHard, Code = EconomyAccountCode.ExternalClearingHard, Currency = CurrencyCode.HardCoin, CreatedAt = Now },
            new EconomyAccountRow { Id = clearingSoft, Code = EconomyAccountCode.SoftCoinReserve, Currency = CurrencyCode.SoftCoin, CreatedAt = Now });
        context.Set<EconomyRiskDecisionRow>().Add(new EconomyRiskDecisionRow
        {
            Id = riskDecisionId,
            Outcome = RiskOutcome.Allow,
            OperationFingerprint = "reserve-liability-fixture",
            IdempotencyKey = "reserve-liability-fixture",
            ActorHash = "fixture-actor",
            TemplateKind = PostingTemplateKind.SystemBackedGrant,
            SourceWalletId = walletId,
            DestinationWalletId = walletId,
            Currency = CurrencyCode.HardCoin,
            AmountUnits = 25,
            CurrencyLegs = "[]",
            SourceRoots = "[]",
            ProviderReferenceHash = "fixture-provider",
            PolicyVersion = 1,
            ReserveVersion = 1,
            ReserveAuthorizationEpoch = 1,
            FeatureVersion = 1,
            KillSwitchEpoch = 0,
            CounterVersion = 1,
            EntityGraphVersion = 0,
            EntityGraphEvidenceHash = "fixture-evidence",
            ReasonCodes = "[]",
            IssuedAt = Now,
            ExpiresAt = Now.AddHours(1)
        });
        await context.SaveChangesAsync();
        context.Set<EconomyPostingGroupRow>().Add(new EconomyPostingGroupRow
        {
            Id = postingId, IdempotencyKey = "reserve-liability", TemplateKind = PostingTemplateKind.SystemBackedGrant,
            TemplateVersion = 1, Authority = PostingAuthority.PlatformSystem, Status = PostingStatus.Accepted,
            CapabilityId = Guid.NewGuid(), ActorId = Guid.NewGuid(), TenantId = Guid.NewGuid(), RiskDecisionId = riskDecisionId,
            PolicyVersion = 1, ReserveVersion = 1, ReserveAuthorizationEpoch = 1, RecordedAt = Now
        });
        context.Set<EconomyJournalEntryRow>().Add(new EconomyJournalEntryRow
        {
            Id = entryId, PostingGroupId = postingId, Sequence = 1, PreviousHash = JournalChain.GenesisHash,
            CanonicalPayloadHash = requestHash, HashAlgorithmVersion = 2, Hash = hash, RecordedAt = Now
        });
        context.Set<EconomyJournalLineRow>().AddRange(
            new EconomyJournalLineRow { Id = Guid.NewGuid(), JournalEntryId = entryId, AccountId = hard, Sequence = 1, Side = EntrySide.Credit, Currency = CurrencyCode.HardCoin, AmountUnits = 25 },
            new EconomyJournalLineRow { Id = Guid.NewGuid(), JournalEntryId = entryId, AccountId = clearingHard, Sequence = 2, Side = EntrySide.Debit, Currency = CurrencyCode.HardCoin, AmountUnits = 25 },
            new EconomyJournalLineRow { Id = Guid.NewGuid(), JournalEntryId = entryId, AccountId = soft, Sequence = 3, Side = EntrySide.Credit, Currency = CurrencyCode.SoftCoin, AmountUnits = 1_000 },
            new EconomyJournalLineRow { Id = Guid.NewGuid(), JournalEntryId = entryId, AccountId = clearingSoft, Sequence = 4, Side = EntrySide.Debit, Currency = CurrencyCode.SoftCoin, AmountUnits = 1_000 });
        context.Set<EconomyIdempotencyRecordRow>().Add(new EconomyIdempotencyRecordRow
        {
            Id = Guid.NewGuid(), Key = "reserve-liability", RequestHash = requestHash,
            PostingGroupId = postingId, CreatedAt = Now
        });
        context.Set<EconomyChainHeadRow>().Update(new EconomyChainHeadRow { Id = 1, Sequence = 1, Hash = hash, UpdatedAt = Now });
        context.Set<EconomyJournalVerificationCheckpointRow>().Add(new EconomyJournalVerificationCheckpointRow
        {
            Id = Guid.NewGuid(), FromSequence = 1, ToSequence = 1,
            PreviousHash = JournalChain.GenesisHash, CurrentHash = hash, IsValid = true,
            FencingToken = 1, StartedAt = Now, CompletedAt = Now
        });
        await context.SaveChangesAsync();
    }

    private static ReserveDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ReserveDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class ReserveDbContext(DbContextOptions<ReserveDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new EconomyModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class DeterministicSignatureVerifier : ICapabilityPolicySignatureVerifier
    {
        public ValueTask<bool> VerifyAsync(
            string canonicalPayload,
            string keyId,
            string signature,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(keyId == "custody-key" && signature == Sign(canonicalPayload));

        public static string Sign(string value) => Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
