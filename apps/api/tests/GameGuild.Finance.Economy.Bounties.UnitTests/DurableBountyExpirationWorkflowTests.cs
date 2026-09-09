using FluentAssertions;
using GameGuild.Finance.Economy.Bounties.Persistence;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Bounties.UnitTests;

public sealed class DurableBountyExpirationWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExpireDueAsync_OrdersLocksAndPersistsExactlyOneExpirationEventPerDueBounty()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("bounty_expiration");
        await using var context = new PostgreSqlBountiesContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var oldest = Bounty(BountyStatus.Open, Now.AddMinutes(-10), "oldest");
        var next = Bounty(BountyStatus.Open, Now.AddMinutes(-5), "next");
        var future = Bounty(BountyStatus.Open, Now.AddMinutes(5), "future");
        var claimed = Bounty(BountyStatus.Claimed, Now.AddMinutes(-20), "claimed");
        context.AddRange(oldest, next, future, claimed);
        await context.SaveChangesAsync();
        var workflow = new PostgreSqlBountyExpirationWorkflow(context);

        var first = await workflow.ExpireDueAsync(Now, 1);
        var second = await workflow.ExpireDueAsync(Now, 10);
        var empty = await workflow.ExpireDueAsync(Now, 10);

        first.EvaluatedAt.Should().Be(Now);
        first.ExpiredBounties.Should().Equal(new BountyId(oldest.Id));
        second.ExpiredBounties.Should().Equal(new BountyId(next.Id));
        empty.ExpiredBounties.Should().BeEmpty();
        var rows = await context.Set<BountyRow>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync();
        rows.Single(row => row.Id == oldest.Id).Should().Match<BountyRow>(row =>
            row.Status == BountyStatus.Expired && row.Version == 2);
        rows.Single(row => row.Id == next.Id).Should().Match<BountyRow>(row =>
            row.Status == BountyStatus.Expired && row.Version == 2);
        rows.Single(row => row.Id == future.Id).Status.Should().Be(BountyStatus.Open);
        rows.Single(row => row.Id == claimed.Id).Status.Should().Be(BountyStatus.Claimed);
        var events = await context.Set<BountyExpirationEventRow>().AsNoTracking()
            .OrderBy(row => row.ExpiresAt).ToArrayAsync();
        events.Should().HaveCount(2);
        events.Select(row => row.BountyId).Should().Equal(oldest.Id, next.Id);
        events.Should().OnlyContain(row => row.RecordedAt == Now && row.BountyVersion == 2);
    }

    [Fact]
    public async Task PrepareForReclaimAsync_ReopensOnlyExpiredAndDueBounty()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("bounty_reclaim_prepare");
        await using var context = new PostgreSqlBountiesContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var due = Bounty(BountyStatus.Expired, Now.AddMinutes(-1), "due");
        var future = Bounty(BountyStatus.Expired, Now.AddMinutes(1), "future");
        context.AddRange(due, future);
        await context.SaveChangesAsync();
        var workflow = new PostgreSqlBountyExpirationWorkflow(context);

        var prepared = await workflow.PrepareForReclaimAsync(new BountyId(due.Id), Now);
        var replay = await workflow.PrepareForReclaimAsync(new BountyId(due.Id), Now);
        var notDue = await workflow.PrepareForReclaimAsync(new BountyId(future.Id), Now);

        prepared.Should().BeTrue();
        replay.Should().BeFalse();
        notDue.Should().BeFalse();
        context.ChangeTracker.Clear();
        (await context.Set<BountyRow>().FindAsync(due.Id))!.Status.Should().Be(BountyStatus.Open);
        (await context.Set<BountyRow>().FindAsync(future.Id))!.Status.Should().Be(BountyStatus.Expired);
    }

    [Fact]
    public async Task ExpireDueAsync_RejectsUnsafeBatchSizesBeforeDatabaseAccess()
    {
        using var context = new ScriptedBountiesContext(new ScriptedRelationalInterceptor());
        var workflow = new PostgreSqlBountyExpirationWorkflow(context);

        await FluentActions.Awaiting(() => workflow.ExpireDueAsync(Now, 0))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => workflow.ExpireDueAsync(Now, 1_001))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ConstructorsAndLegacyTransition_RemainFailClosed()
    {
        FluentActions.Invoking(() => new PostgreSqlBountyExpirationWorkflow(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlBountyExpirationWorkflow(new NonRelationalApplicationContext()))
            .Should().Throw<InvalidOperationException>();

        var result = await LegacyOpenBountyExpirationTransition.Instance.PrepareForReclaimAsync(
            BountyId.New(), Now);

        result.Should().BeFalse();
    }

    private static BountyRow Bounty(BountyStatus status, DateTimeOffset expiresAt, string key) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        PosterId = Guid.NewGuid(),
        PosterWalletId = Guid.NewGuid(),
        EscrowWalletId = Guid.NewGuid(),
        Currency = CurrencyCode.HardCoin,
        AmountUnits = 100,
        ReclaimFeePpm = 10_000,
        RequiresPrerequisite = false,
        MinimumReputation = 0,
        RequiresInstructorVerification = false,
        Status = status,
        IdempotencyKey = $"expiration-{key}-{Guid.NewGuid():N}",
        RequestHash = $"hash-{key}",
        PostedAt = expiresAt.AddDays(-1),
        ExpiresAt = expiresAt,
        Version = 1
    };

    private sealed class PostgreSqlBountiesContext : DbContext, IApplicationDbContext
    {
        public PostgreSqlBountiesContext(string connectionString)
            : base(new DbContextOptionsBuilder<PostgreSqlBountiesContext>()
                .UseNpgsql(connectionString)
                .Options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new BountiesModelConfiguration().Configure(modelBuilder);

        Task<IDbContextTransaction> IApplicationDbContext.BeginTransactionAsync(
            CancellationToken cancellationToken) => Database.BeginTransactionAsync(cancellationToken);
    }
}
