using FluentAssertions;
using GameGuild;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class PostgreSqlPayoutOperationStoreTests
{
    private static readonly DateTimeOffset Time = new(2026, 8, 10, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Store_PersistsReadsTransitionsAndRecordsProviderEvidence()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("payout_operation_store");

        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(database.ConnectionString)
                .Options);
        await context.Database.MigrateAsync();

        var store = new PostgreSqlPayoutOperationStore(context);
        var operation = Operation();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
            VALUES ({operation.WalletId.Value}, {operation.PayeeId}, {operation.TenantId}, 1, {Time});
            """);

        store.Add(operation);
        store.Get(operation.Id).Should().BeEquivalentTo(operation);
        store.GetForTenant(operation.TenantId, operation.Id).Should().BeEquivalentTo(operation);
        FluentActions.Invoking(() => store.GetForTenant(Guid.NewGuid(), operation.Id))
            .Should().Throw<KeyNotFoundException>();
        store.ListForTenant(operation.TenantId, 10).Should().ContainSingle().Which.Should().BeEquivalentTo(operation);
        store.ListForPayee(operation.TenantId, operation.PayeeId, 10).Should().ContainSingle().Which.Should().BeEquivalentTo(operation);
        FluentActions.Invoking(() => store.Get(Guid.NewGuid()))
            .Should().Throw<KeyNotFoundException>();
        store.FindReplay(operation.TenantId, operation.IdempotencyKey.Value, operation.RequestHash)
            .Should().BeEquivalentTo(operation);
        store.FindReplay(operation.TenantId, "missing", operation.RequestHash).Should().BeNull();

        FluentActions.Invoking(() => store.FindReplay(operation.TenantId, operation.IdempotencyKey.Value, "mutated"))
            .Should().Throw<PayoutReplayConflictException>();
        FluentActions.Invoking(() => store.Add(operation))
            .Should().Throw<PayoutReplayConflictException>();

        var dispatching = operation.Transition(
            PayoutOperationState.Dispatching,
            Time.AddMinutes(1),
            dispatchSnapshotHash: "dispatch-hash");
        store.Update(dispatching, operation.Version).Should().BeSameAs(dispatching);
        store.Get(operation.Id).Should().BeEquivalentTo(dispatching);

        var succeeded = dispatching.Transition(
            PayoutOperationState.Succeeded,
            Time.AddMinutes(2),
            providerPayoutId: "provider-payout");
        var eventRecord = store.RecordProviderEvent(
            " evt_1 ",
            " event-hash ",
            succeeded,
            dispatching.Version,
            Time.AddMinutes(2));
        eventRecord.EventId.Should().Be("evt_1");
        store.Get(operation.Id).Should().BeEquivalentTo(succeeded);
        store.FindProviderEvent("evt_1", "event-hash").Should().BeEquivalentTo(eventRecord);
        store.FindProviderEvent("missing", "event-hash").Should().BeNull();

        FluentActions.Invoking(() => store.FindProviderEvent("evt_1", "mutated"))
            .Should().Throw<PayoutReplayConflictException>();
        FluentActions.Invoking(() => store.RecordProviderEvent(
                "evt_1", "mutated", succeeded, dispatching.Version, Time.AddMinutes(3)))
            .Should().Throw<PayoutReplayConflictException>();
        FluentActions.Invoking(() => store.Update(dispatching, operation.Version))
            .Should().Throw<PayoutStaleCommandException>();
    }

    [Fact]
    public void Store_RejectsInvalidConstructionAndArguments()
    {
        FluentActions.Invoking(() => new PostgreSqlPayoutOperationStore(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlPayoutOperationStore(new NonRelationalContext()))
            .Should().Throw<InvalidOperationException>();

        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"payout-store-validation-{Guid.NewGuid():N}")
                .Options);
        var store = new PostgreSqlPayoutOperationStore(context);
        var operation = Operation();

        FluentActions.Invoking(() => store.Get(Guid.Empty)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.GetForTenant(Guid.Empty, Guid.NewGuid())).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.GetForTenant(Guid.NewGuid(), Guid.Empty)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.ListForTenant(Guid.Empty, 10)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.ListForTenant(Guid.NewGuid(), 0)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => store.ListForPayee(Guid.Empty, Guid.NewGuid(), 10)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.ListForPayee(Guid.NewGuid(), Guid.Empty, 10)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.ListForPayee(Guid.NewGuid(), Guid.NewGuid(), 0)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => store.ListForPayee(Guid.NewGuid(), Guid.NewGuid(), 101)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => store.FindReplay(Guid.Empty, "key", "hash")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.FindReplay(Guid.NewGuid(), "", "hash")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.FindReplay(Guid.NewGuid(), "key", "")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.Add(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => store.Add(operation with { TenantId = Guid.Empty }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.Update(null!, 1)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => store.FindProviderEvent("", "hash")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.FindProviderEvent("event", "")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.RecordProviderEvent("", "hash", operation, 1, Time))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.RecordProviderEvent("event", "", operation, 1, Time))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.RecordProviderEvent("event", "hash", null!, 1, Time))
            .Should().Throw<ArgumentNullException>();
    }

    private static PayoutOperation Operation() => new(
        Guid.NewGuid(),
        new IdempotencyKey($"payout-{Guid.NewGuid():N}"),
        "request-hash",
        Guid.NewGuid(),
        Guid.NewGuid(),
        WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 750),
        "acct_1",
        "destination-hash",
        "binding-hash",
        "eligibility-hash",
        null,
        null,
        PayoutOperationState.Reserved,
        1,
        1,
        1,
        new ReserveVersion(1),
        1,
        new PolicyVersion(1),
        Guid.NewGuid(),
        Time,
        Time,
        Guid.NewGuid());

    private sealed class NonRelationalContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

}
