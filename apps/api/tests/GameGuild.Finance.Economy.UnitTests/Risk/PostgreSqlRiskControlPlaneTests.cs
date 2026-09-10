using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class PostgreSqlRiskControlPlaneTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 15, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task EntityGraphIsVersionedTenantScopedAndTraversable()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("risk_graph");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var store = new PostgreSqlEntityRiskGraphStore(context);
        var account = new RiskEntityNode(RiskEntityType.Account, "account-hash");
        var device = new RiskEntityNode(RiskEntityType.DeviceRiskToken, "device-hash");
        var ip = new RiskEntityNode(RiskEntityType.IpAddress, "ip-hash");

        var first = await store.LinkAsync(
            new EntityRiskLinkRequest(TenantId, account, device, "uses", "evidence-1", Now),
            CancellationToken.None);
        var second = await store.LinkAsync(
            new EntityRiskLinkRequest(TenantId, device, ip, "observed-from", "evidence-2", Now.AddSeconds(1)),
            CancellationToken.None);
        var replay = await store.LinkAsync(
            new EntityRiskLinkRequest(TenantId, device, account, "uses", "evidence-1", Now),
            CancellationToken.None);

        first.Version.Should().Be(1);
        second.Version.Should().Be(2);
        replay.Version.Should().Be(2);
        (await context.Set<EconomyEntityGraphEdgeRow>().CountAsync()).Should().Be(2);
        var cluster = await store.ClusterForAsync(TenantId, account, CancellationToken.None);
        cluster.Version.Should().Be(2);
        cluster.Nodes.Should().BeEquivalentTo([account, device, ip]);
        (await store.ClusterForAsync(Guid.NewGuid(), account, CancellationToken.None)).Nodes.Should().ContainSingle()
            .Which.Should().Be(account);
    }

    [Fact]
    public async Task AggregateCounterReservationsAreAtomicConsumableAndReleasable()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("risk_counter");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var decisionOne = await SeedDecisionAsync(context, 60, "decision-one");
        var decisionTwo = await SeedDecisionAsync(context, 50, "decision-two");
        var store = new PostgreSqlAggregateRiskCounterStore(context);
        var limit = new AggregateRiskLimit(
            new RiskLimitKey(RiskLimitDimension.Tenant, "tenant-hash"),
            1,
            100,
            TimeSpan.FromHours(1));

        var reserved = await store.ReserveAsync(
            Guid.NewGuid(), TenantId, decisionOne, PostingTemplateKind.PayoutReservation,
            new CoinAmount(CurrencyCode.HardCoin, 60), [limit], Now, Now.AddMinutes(5), CancellationToken.None);

        reserved.Status.Should().Be(RiskCounterReservationStatus.Reserved);
        reserved.Allocations.Should().ContainSingle().Which.Units.Should().Be(60);
        await FluentActions.Awaiting(() => store.ReserveAsync(
                Guid.NewGuid(), TenantId, decisionTwo, PostingTemplateKind.PayoutReservation,
                new CoinAmount(CurrencyCode.HardCoin, 50), [limit], Now.AddSeconds(1), Now.AddMinutes(5),
                CancellationToken.None).AsTask())
            .Should().ThrowAsync<AggregateRiskLimitExceededException>();

        var consumed = await store.ConsumeAsync(reserved.Id, Now.AddMinutes(1), CancellationToken.None);
        consumed.Status.Should().Be(RiskCounterReservationStatus.Consumed);
        await FluentActions.Awaiting(() => store.ReleaseAsync(reserved.Id, Now.AddMinutes(2), CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>();

        var decisionThree = await SeedDecisionAsync(context, 30, "decision-three");
        var releasable = await store.ReserveAsync(
            Guid.NewGuid(), TenantId, decisionThree, PostingTemplateKind.PayoutReservation,
            new CoinAmount(CurrencyCode.HardCoin, 30), [limit], Now.AddMinutes(2), Now.AddMinutes(3),
            CancellationToken.None);
        var released = await store.ReleaseAsync(releasable.Id, Now.AddMinutes(2).AddSeconds(1), CancellationToken.None);
        released.Status.Should().Be(RiskCounterReservationStatus.Released);
    }

    [Fact]
    public async Task ProtectedChangesPreserveAppendOnlyHistoryAndLatestEvaluation()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("risk_cooldown");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var store = new PostgreSqlProtectedChangeCooldownStore(context);
        var subject = Guid.NewGuid();

        var first = await store.RecordAsync(
            TenantId, subject, ProtectedChangeKind.PayoutDestination, "destination-1", Now,
            TimeSpan.FromHours(24), CancellationToken.None);
        var second = await store.RecordAsync(
            TenantId, subject, ProtectedChangeKind.PayoutDestination, "destination-2", Now.AddHours(1),
            TimeSpan.FromHours(24), CancellationToken.None);

        first.Version.Should().Be(1);
        second.Version.Should().Be(2);
        (await store.EvaluateAsync(TenantId, subject, ProtectedChangeKind.PayoutDestination, Now.AddHours(2), CancellationToken.None))
            .IsElapsed.Should().BeFalse();
        (await store.ForSubjectAsync(TenantId, subject, CancellationToken.None)).Should().HaveCount(2);
        (await store.ForSubjectAsync(Guid.NewGuid(), subject, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task RiskReviewUsesTenantScopeAppendOnlyEventsAndDualControl()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("risk_review");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var decisionId = await SeedDecisionAsync(context, 10, "review-decision", RiskOutcome.Review);
        var submitter = Guid.NewGuid();
        var reviewerOne = Guid.NewGuid();
        var reviewerTwo = Guid.NewGuid();
        var store = new PostgreSqlRiskReviewStore(context);

        var submitted = await store.SubmitAsync(
            TenantId, Guid.NewGuid(), decisionId, submitter, ["evidence-hash"], Now, 2,
            CancellationToken.None);
        var laterDecision = await SeedDecisionAsync(context, 5, "later-review", RiskOutcome.Review);
        var later = await store.SubmitAsync(
            TenantId, Guid.NewGuid(), laterDecision, submitter, ["later-evidence"], Now.AddMinutes(1), 1,
            CancellationToken.None);
        var foreignTenant = Guid.NewGuid();
        var foreignDecision = await SeedDecisionAsync(
            context, 5, "foreign-review", RiskOutcome.Review, foreignTenant);
        var foreign = await store.SubmitAsync(
            foreignTenant, Guid.NewGuid(), foreignDecision, submitter, ["foreign-evidence"], Now, 1,
            CancellationToken.None);

        var firstPage = await store.ListAsync(
            TenantId, RiskReviewStatus.Pending, 1, null, CancellationToken.None);
        firstPage.Items.Should().ContainSingle().Which.Id.Should().Be(later.Id);
        firstPage.NextCursor.Should().NotBeNullOrWhiteSpace();
        var secondPage = await store.ListAsync(
            TenantId, RiskReviewStatus.Pending, 1, firstPage.NextCursor, CancellationToken.None);
        secondPage.Items.Should().ContainSingle().Which.Id.Should().Be(submitted.Id);
        secondPage.NextCursor.Should().BeNull();
        (await store.ListAsync(foreignTenant, null, 10, null, CancellationToken.None))
            .Items.Should().ContainSingle().Which.Id.Should().Be(foreign.Id);
        await FluentActions.Awaiting(() => store.ApproveAsync(
                TenantId, submitted.Id, submitter, RiskManualDecisionCode.EvidenceVerified,
                "self approval", Now.AddMinutes(1), CancellationToken.None).AsTask())
            .Should().ThrowAsync<InvalidOperationException>();

        var pending = await store.ApproveAsync(
            TenantId, submitted.Id, reviewerOne, RiskManualDecisionCode.EvidenceVerified,
            "first review", Now.AddMinutes(1), CancellationToken.None);
        pending.Status.Should().Be(RiskReviewStatus.Pending);
        var approved = await store.ApproveAsync(
            TenantId, submitted.Id, reviewerTwo, RiskManualDecisionCode.RiskAccepted,
            "second review", Now.AddMinutes(2), CancellationToken.None);

        approved.Status.Should().Be(RiskReviewStatus.Approved);
        approved.Approvers.Should().BeEquivalentTo([reviewerOne, reviewerTwo]);
        (await store.EventsAsync(TenantId, submitted.Id, CancellationToken.None))
            .Select(item => item.Kind)
            .Should().Equal(RiskReviewEventKind.Submitted, RiskReviewEventKind.ApprovalRecorded, RiskReviewEventKind.Approved);
        await FluentActions.Awaiting(() => store.CurrentAsync(Guid.NewGuid(), submitted.Id, CancellationToken.None).AsTask())
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => store.ListAsync(
                TenantId, null, 10, "invalid", CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void ConstructorsRequireRelationalApplicationContext()
    {
        var context = new StubApplicationDbContext();
        FluentActions.Invoking(() => new PostgreSqlEntityRiskGraphStore(context)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => new PostgreSqlAggregateRiskCounterStore(context)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => new PostgreSqlProtectedChangeCooldownStore(context)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => new PostgreSqlRiskReviewStore(context)).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task CounterCooldownAndReviewCommandsRejectEveryInvalidIdentityBeforePersistence()
    {
        await using var context = CreateContext("Host=localhost;Database=unused;Username=unused;Password=unused");
        var counters = new PostgreSqlAggregateRiskCounterStore(context);
        var cooldowns = new PostgreSqlProtectedChangeCooldownStore(context);
        var reviews = new PostgreSqlRiskReviewStore(context);
        var reservationId = Guid.NewGuid();
        var decisionId = Guid.NewGuid();
        var limit = new AggregateRiskLimit(
            new RiskLimitKey(RiskLimitDimension.Tenant, "tenant"), 1, 10, TimeSpan.FromMinutes(1));
        typeof(PostgreSqlAggregateRiskCounterStore)
            .GetMethod("ValidateReservationInputs", System.Reflection.BindingFlags.NonPublic |
                                                    System.Reflection.BindingFlags.Static)!
            .Invoke(null,
            [
                reservationId, TenantId, decisionId, PostingTemplateKind.PayoutReservation,
                new CoinAmount(CurrencyCode.HardCoin, 1), new[] { limit }, Now, Now.AddMinutes(1)
            ]);
        typeof(PostgreSqlProtectedChangeCooldownStore)
            .GetMethod("ValidateKey", System.Reflection.BindingFlags.NonPublic |
                                      System.Reflection.BindingFlags.Static)!
            .Invoke(null, [TenantId, Guid.NewGuid(), ProtectedChangeKind.PayoutDestination]);
        typeof(PostgreSqlRiskReviewStore)
            .GetMethod("ValidateTenantReviewActor", System.Reflection.BindingFlags.NonPublic |
                                                   System.Reflection.BindingFlags.Static)!
            .Invoke(null, [TenantId, Guid.NewGuid(), Guid.NewGuid()]);
        ValueTask<DurableAggregateRiskCounterReservation> Reserve(
            Guid reservation,
            Guid tenant,
            Guid decision,
            PostingTemplateKind operation,
            CoinAmount amount,
            IReadOnlyCollection<AggregateRiskLimit>? limits,
            DateTimeOffset expiresAt) => counters.ReserveAsync(
            reservation, tenant, decision, operation, amount, limits!, Now, expiresAt, CancellationToken.None);

        await FluentActions.Invoking(() => Reserve(Guid.Empty, TenantId, decisionId,
                PostingTemplateKind.PayoutReservation, new CoinAmount(CurrencyCode.HardCoin, 1), [limit], Now.AddMinutes(1)).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Reserve(reservationId, Guid.Empty, decisionId,
                PostingTemplateKind.PayoutReservation, new CoinAmount(CurrencyCode.HardCoin, 1), [limit], Now.AddMinutes(1)).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Reserve(reservationId, TenantId, Guid.Empty,
                PostingTemplateKind.PayoutReservation, new CoinAmount(CurrencyCode.HardCoin, 1), [limit], Now.AddMinutes(1)).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Reserve(reservationId, TenantId, decisionId,
                (PostingTemplateKind)999, new CoinAmount(CurrencyCode.HardCoin, 1), [limit], Now.AddMinutes(1)).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Invoking(() => Reserve(reservationId, TenantId, decisionId,
                PostingTemplateKind.PayoutReservation, new CoinAmount(CurrencyCode.HardCoin, 0), [limit], Now.AddMinutes(1)).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Invoking(() => Reserve(reservationId, TenantId, decisionId,
                PostingTemplateKind.PayoutReservation, new CoinAmount(CurrencyCode.HardCoin, 1), null, Now.AddMinutes(1)).AsTask())
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Invoking(() => Reserve(reservationId, TenantId, decisionId,
                PostingTemplateKind.PayoutReservation, new CoinAmount(CurrencyCode.HardCoin, 1), [], Now.AddMinutes(1)).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Reserve(reservationId, TenantId, decisionId,
                PostingTemplateKind.PayoutReservation, new CoinAmount(CurrencyCode.HardCoin, 1), [limit, limit], Now.AddMinutes(1)).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => Reserve(reservationId, TenantId, decisionId,
                PostingTemplateKind.PayoutReservation, new CoinAmount(CurrencyCode.HardCoin, 1), [limit], Now).AsTask())
            .Should().ThrowAsync<ArgumentException>();

        await FluentActions.Invoking(() => cooldowns.EvaluateAsync(
                Guid.Empty, Guid.NewGuid(), ProtectedChangeKind.PayoutDestination, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => cooldowns.EvaluateAsync(
                TenantId, Guid.Empty, ProtectedChangeKind.PayoutDestination, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => cooldowns.EvaluateAsync(
                TenantId, Guid.NewGuid(), (ProtectedChangeKind)999, Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();

        await FluentActions.Invoking(() => reviews.RejectAsync(
                Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), RiskManualDecisionCode.RiskAccepted,
                "resolution", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => reviews.RejectAsync(
                TenantId, Guid.Empty, Guid.NewGuid(), RiskManualDecisionCode.RiskAccepted,
                "resolution", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => reviews.RejectAsync(
                TenantId, Guid.NewGuid(), Guid.Empty, RiskManualDecisionCode.RiskAccepted,
                "resolution", Now, CancellationToken.None).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    private static async Task<Guid> SeedDecisionAsync(
        RiskDbContext context,
        long amount,
        string fingerprint,
        RiskOutcome outcome = RiskOutcome.Allow,
        Guid? tenantId = null)
    {
        var decisionTenant = tenantId ?? TenantId;
        var sourceWallet = Guid.NewGuid();
        var destinationWallet = Guid.NewGuid();
        context.Set<EconomyWalletRow>().AddRange(
            new EconomyWalletRow { Id = sourceWallet, OwnerId = Guid.NewGuid(), TenantId = decisionTenant, State = WalletLifecycleState.Active, CreatedAt = Now },
            new EconomyWalletRow { Id = destinationWallet, OwnerId = Guid.NewGuid(), TenantId = decisionTenant, State = WalletLifecycleState.Active, CreatedAt = Now });
        var id = Guid.NewGuid();
        context.Set<EconomyRiskDecisionRow>().Add(new EconomyRiskDecisionRow
        {
            Id = id, Outcome = outcome, OperationFingerprint = fingerprint, ActorHash = "actor-hash",
            TemplateKind = PostingTemplateKind.PayoutReservation, SourceWalletId = sourceWallet,
            DestinationWalletId = destinationWallet, Currency = CurrencyCode.HardCoin, AmountUnits = amount,
            CurrencyLegs = "[]", SourceRoots = "[]", ProviderReferenceHash = "provider-hash",
            PolicyVersion = 1, ReserveVersion = 1, ReserveAuthorizationEpoch = 1, FeatureVersion = 1,
            KillSwitchEpoch = 0, CounterVersion = 1, EntityGraphVersion = 1,
            EntityGraphEvidenceHash = "graph-hash", ReasonCodes = "[]", IssuedAt = Now,
            ExpiresAt = Now.AddHours(1)
        });
        await context.SaveChangesAsync();
        return id;
    }

    private static RiskDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<RiskDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class RiskDbContext(DbContextOptions<RiskDbContext> options)
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
