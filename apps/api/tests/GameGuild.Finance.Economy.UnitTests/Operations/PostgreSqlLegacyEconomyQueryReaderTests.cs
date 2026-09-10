using FluentAssertions;
using GameGuild.Finance.Economy.Operations;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.UnitTests.Operations;

public sealed class PostgreSqlLegacyEconomyQueryReaderTests
{
    private static readonly Guid TenantId = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListsOnlyActorTenantBatchesWithStableCursor()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("legacy_queries");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var rows = Enumerable.Range(1, 3).Select(index => Batch(TenantId, index)).ToArray();
        context.Set<EconomyLegacyShadowBatchRow>().AddRange(rows.Append(Batch(Guid.NewGuid(), 4)));
        await context.SaveChangesAsync();
        var reader = new PostgreSqlLegacyEconomyQueryReader(context);

        var first = await reader.ListAsync(TenantId, null, 2, null, default);
        var second = await reader.ListAsync(TenantId, null, 2, first.NextCursor, default);

        first.Items.Select(item => item.Id).Should().Equal(rows[0].Id, rows[1].Id);
        second.Items.Select(item => item.Id).Should().Equal(rows[2].Id);
        first.Items.Should().OnlyContain(item => item.TenantId == TenantId);
    }

    [Fact]
    public async Task FiltersByStateAndRejectsInvalidCursor()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("legacy_query_filter");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var row = Batch(TenantId, 1);
        context.Set<EconomyLegacyShadowBatchRow>().Add(row);
        await context.SaveChangesAsync();
        var reader = new PostgreSqlLegacyEconomyQueryReader(context);

        var page = await reader.ListAsync(TenantId, LegacyEconomyShadowState.Captured, 20, null, default);

        page.Items.Should().ContainSingle().Which.State.Should().Be(LegacyEconomyShadowState.Captured);
        await FluentActions.Awaiting(() => reader.ListAsync(
                TenantId, null, 20, "invalid", default).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task MapsEveryStateAndRejectsUnknownStates()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("legacy_query_states");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.EnsureCreatedAsync();
        var publicStates = Enum.GetValues<LegacyEconomyShadowState>();
        var storedStates = Enum.GetValues<EconomyLegacyShadowBatchState>();
        var rows = storedStates.Select((state, index) =>
        {
            var row = Batch(TenantId, index + 1);
            row.State = state;
            return row;
        }).ToArray();
        context.Set<EconomyLegacyShadowBatchRow>().AddRange(rows);
        await context.SaveChangesAsync();
        var reader = new PostgreSqlLegacyEconomyQueryReader(context);

        var all = await reader.ListAsync(TenantId, null, 100, null, default);
        all.Items.Select(item => item.State).Should().BeEquivalentTo(publicStates);
        foreach (var state in publicStates)
        {
            var filtered = await reader.ListAsync(TenantId, state, 100, null, default);
            filtered.Items.Should().ContainSingle().Which.State.Should().Be(state);
        }

        await FluentActions.Awaiting(() => reader.ListAsync(
                TenantId, (LegacyEconomyShadowState)int.MaxValue, 20, null, default).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => PostgreSqlLegacyEconomyQueryReader.ToPublicState(
                (EconomyLegacyShadowBatchState)int.MaxValue))
            .Should().Throw<InvalidOperationException>();
    }

    private static EconomyLegacyShadowBatchRow Batch(Guid tenantId, int index) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, RequestedBy = Guid.NewGuid(), JurisdictionCode = "BR",
        PolicyVersion = 1, State = EconomyLegacyShadowBatchState.Captured, WalletCount = index,
        TransactionCount = index, FinancialLedgerEntryCount = index, ExpectedHardUnits = index,
        WalletSnapshotHash = $"wallet-{index}", TransactionSnapshotHash = $"transaction-{index}",
        FinancialLedgerSnapshotHash = $"ledger-{index}", RequestHash = $"request-{index}",
        CapturedAt = Now.AddMinutes(-index), UpdatedAt = Now.AddMinutes(-index), Version = 1
    };

    private static QueryDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<QueryDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class QueryDbContext(DbContextOptions<QueryDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new EconomyModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);
    }
}
