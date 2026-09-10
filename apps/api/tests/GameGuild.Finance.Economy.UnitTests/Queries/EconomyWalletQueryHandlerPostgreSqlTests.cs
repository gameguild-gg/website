using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Projections;
using GameGuild.Finance.Economy.Queries;
using GameGuild.Identity.Context.Actors;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.UnitTests.Queries;

public sealed class EconomyWalletQueryHandlerPostgreSqlTests : IAsyncLifetime
{
    private static readonly DateTimeOffset RecordedAt = DateTimeOffset.Parse("2026-08-09T12:00:00Z");
    private readonly Dictionary<Guid, Guid> _accountIds = [];
    private EconomyPostgreSqlTestDatabase? _database;

    public async Task InitializeAsync()
    {
        _database = await EconomyPostgreSqlTestDatabase.CreateAsync("wallet_queries");
        await ResetSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        if (_database is not null)
            await _database.DisposeAsync();
    }

    [Fact]
    public async Task GetMyWallet_ReturnsOnlyTheAuthenticatedTenantWallet()
    {
        await ResetSchemaAsync();
        var ownerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var walletId = Guid.NewGuid();

        await using (var seed = CreateContext())
        {
            seed.Add(Wallet(walletId, ownerId, tenantId));
            seed.Add(new EconomyWalletBalanceProjectionRow
            {
                WalletId = walletId,
                PendingHard = 10,
                PendingSoft = 20,
                PurchasedHard = 30,
                EarnedHard = 40,
                RestrictedHard = 50,
                Soft = 60,
                ImmatureEarnedHard = 70,
                HeldHard = 80,
                HeldSoft = 90,
                AvailableHardToSpend = 100,
                AvailableSoftToSpend = 110,
                WithdrawableHard = 120,
                ReviewState = WalletReviewState.Healthy,
                SourceJournalSequence = 130,
                ProjectionHash = new string('a', 64),
                RebuiltAt = RecordedAt
            });
            seed.Add(new EconomyWalletDebtRow
            {
                WalletId = walletId,
                OutstandingHardUnits = 140,
                Version = 1
            });
            seed.Add(Wallet(Guid.NewGuid(), ownerId, Guid.NewGuid()));
            seed.Add(Wallet(Guid.NewGuid(), Guid.NewGuid(), tenantId));
            await seed.SaveChangesAsync();
        }

        var actor = CreateActor(ownerId, tenantId);
        await using var context = CreateContext();
        var handler = new GetMyEconomyWalletQueryHandler(context, actor);

        var result = await handler.Handle(new GetMyEconomyWalletQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(new EconomyWalletSummaryDto(
            walletId,
            WalletLifecycleState.Active,
            RecordedAt,
            10, 20, 30, 40, 50, 60, 80, 90, 100, 110, 120, 140, RecordedAt, 130));
    }

    [Fact]
    public async Task GetMyWallet_ReturnsNullWhenActorOwnsNoWallet()
    {
        await ResetSchemaAsync();
        var handler = new GetMyEconomyWalletQueryHandler(CreateContext(), CreateActor(Guid.NewGuid(), Guid.NewGuid()));

        var result = await handler.Handle(new GetMyEconomyWalletQuery(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMyWallet_RejectsAnonymousActor()
    {
        await using var context = CreateContext();
        var accessor = new ActorContextAccessor();
        accessor.ClearActorContext();
        var handler = new GetMyEconomyWalletQueryHandler(context, accessor);

        var act = () => handler.Handle(new GetMyEconomyWalletQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetMyWalletRejectsAuthenticatedActorWithoutGuidSubjectOrTenant()
    {
        await using var context = CreateContext();
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = "not-a-guid",
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        var invalidSubject = new GetMyEconomyWalletQueryHandler(context, accessor);
        await FluentActions.Invoking(() => invalidSubject.Handle(
                new GetMyEconomyWalletQuery(), CancellationToken.None))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        accessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = null,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        var missingTenant = new GetMyEconomyWalletQueryHandler(context, accessor);
        await FluentActions.Invoking(() => missingTenant.Handle(
                new GetMyEconomyWalletQuery(), CancellationToken.None))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        var valid = new GetMyEconomyWalletQueryHandler(context, CreateActor(Guid.NewGuid(), Guid.NewGuid()));
        await valid.Handle(new GetMyEconomyWalletQuery(), CancellationToken.None);
    }

    [Fact]
    public async Task ListMyWalletTransactions_ReturnsEmptyWhenActorOwnsNoWallet()
    {
        await ResetSchemaAsync();
        var handler = new ListMyEconomyWalletTransactionsQueryHandler(CreateContext(), CreateActor(Guid.NewGuid(), Guid.NewGuid()));

        var result = await handler.Handle(new ListMyEconomyWalletTransactionsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListMyWalletTransactions_ReturnsNewestLinesFromOnlyTheAuthenticatedWallet()
    {
        await ResetSchemaAsync();
        var ownerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var walletId = Guid.NewGuid();
        var otherWalletId = Guid.NewGuid();

        await using (var seed = CreateContext())
        {
            seed.Add(Wallet(walletId, ownerId, tenantId));
            seed.Add(Wallet(otherWalletId, Guid.NewGuid(), tenantId));
            await seed.SaveChangesAsync();

            await AddJournalLineAsync(seed, walletId, 10, 1, 100);
            await AddJournalLineAsync(seed, walletId, 20, 2, 200);
            await AddJournalLineAsync(seed, otherWalletId, 30, 3, 300);
        }

        var actor = CreateActor(ownerId, tenantId);
        await using var context = CreateContext();
        var handler = new ListMyEconomyWalletTransactionsQueryHandler(context, actor);

        var result = await handler.Handle(new ListMyEconomyWalletTransactionsQuery(500), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(item => item.JournalSequence).Should().Equal(20, 10);
        result.Select(item => item.AmountUnits).Should().Equal(200, 100);
        result.Should().OnlyContain(item => item.Currency == CurrencyCode.HardCoin);
        result[0].Should().BeEquivalentTo(new EconomyWalletTransactionDto(
            result[0].PostingGroupId,
            result[0].JournalEntryId,
            20,
            PostingTemplateKind.HardToSoftConversion,
            PostingStatus.Accepted,
            RecordedAt.AddMinutes(20),
            EntrySide.Credit,
            CurrencyCode.HardCoin,
            200,
            ProvenanceKind.PurchasedHard));
    }

    [Fact]
    public async Task GetMyWallet_UsesSafeDefaultsWithoutProjectionOrDebt()
    {
        await ResetSchemaAsync();
        var ownerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var walletId = Guid.NewGuid();

        await using (var seed = CreateContext())
        {
            seed.Add(Wallet(walletId, ownerId, tenantId));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var result = await new GetMyEconomyWalletQueryHandler(context, CreateActor(ownerId, tenantId))
            .Handle(new GetMyEconomyWalletQuery(), CancellationToken.None);

        result.Should().BeEquivalentTo(new EconomyWalletSummaryDto(
            walletId,
            WalletLifecycleState.Active,
            RecordedAt,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, RecordedAt, 0));
    }

    [Fact]
    public async Task ListMyWalletTransactions_ClampsNonPositiveTakeToOne()
    {
        await ResetSchemaAsync();
        var ownerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var walletId = Guid.NewGuid();

        await using (var seed = CreateContext())
        {
            seed.Add(Wallet(walletId, ownerId, tenantId));
            await seed.SaveChangesAsync();
            await AddJournalLineAsync(seed, walletId, 10, 1, 100);
            await AddJournalLineAsync(seed, walletId, 20, 2, 200);
        }

        await using var context = CreateContext();
        var result = await new ListMyEconomyWalletTransactionsQueryHandler(context, CreateActor(ownerId, tenantId))
            .Handle(new ListMyEconomyWalletTransactionsQuery(0), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].JournalSequence.Should().Be(20);
    }
    private async Task AddJournalLineAsync(
        EconomyWalletQueryDbContext context,
        Guid walletId,
        long journalSequence,
        int lineSequence,
        long amountUnits)
    {
        var accountId = _accountIds.GetValueOrDefault(walletId);
        if (accountId == Guid.Empty)
        {
            accountId = Guid.NewGuid();
            _accountIds.Add(walletId, accountId);
            context.Add(new EconomyAccountRow
            {
                Id = accountId,
                WalletId = walletId,
                Code = EconomyAccountCode.PurchasedHardLiability,
                Currency = CurrencyCode.HardCoin,
                Provenance = ProvenanceKind.PurchasedHard,
                CreatedAt = RecordedAt
            });
        }

        var postingGroupId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        context.Add(new EconomyPostingGroupRow
        {
            Id = postingGroupId,
            IdempotencyKey = $"wallet-query-{journalSequence}",
            TemplateKind = PostingTemplateKind.HardToSoftConversion,
            TemplateVersion = 1,
            Authority = PostingAuthority.WalletOwner,
            Status = PostingStatus.Accepted,
            CapabilityId = Guid.NewGuid(),
            ActorId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            RiskDecisionId = Guid.NewGuid(),
            PolicyVersion = 1,
            ReserveVersion = 1,
            ReserveAuthorizationEpoch = 1,
            RecordedAt = RecordedAt.AddMinutes(journalSequence)
        });
        context.Add(new EconomyJournalEntryRow
        {
            Id = entryId,
            PostingGroupId = postingGroupId,
            Sequence = journalSequence,
            PreviousHash = new string('b', 64),
            Hash = new string('c', 64),
            RecordedAt = RecordedAt.AddMinutes(journalSequence)
        });
        context.Add(new EconomyJournalLineRow
        {
            Id = Guid.NewGuid(),
            JournalEntryId = entryId,
            AccountId = accountId,
            WalletId = walletId,
            Sequence = lineSequence,
            Side = EntrySide.Credit,
            Currency = CurrencyCode.HardCoin,
            AmountUnits = amountUnits,
            Provenance = ProvenanceKind.PurchasedHard
        });
        await context.SaveChangesAsync();
    }

    private async Task ResetSchemaAsync()
    {
        await (_database ?? throw new InvalidOperationException("The PostgreSQL fixture was not initialized.")).ResetAsync();
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    private EconomyWalletQueryDbContext CreateContext() => new(
        new DbContextOptionsBuilder<EconomyWalletQueryDbContext>()
            .UseNpgsql((_database ?? throw new InvalidOperationException("The PostgreSQL fixture was not initialized.")).ConnectionString)
            .Options);

    private static EconomyWalletRow Wallet(Guid id, Guid ownerId, Guid tenantId) => new()
    {
        Id = id,
        OwnerId = ownerId,
        TenantId = tenantId,
        State = WalletLifecycleState.Active,
        CreatedAt = RecordedAt
    };

    private static ActorContextAccessor CreateActor(Guid userId, Guid tenantId)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        return accessor;
    }

    private sealed class EconomyWalletQueryDbContext(DbContextOptions<EconomyWalletQueryDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new EconomyModelConfiguration().Configure(modelBuilder);
    }

}
