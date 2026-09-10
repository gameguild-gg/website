using FluentAssertions;
using GameGuild;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Treasury.UnitTests;

public sealed class PostgreSqlAdminWithdrawalStoreValidationTests
{
    [Fact]
    public void PersistenceBoundaries_RejectInvalidConstructionAndArguments()
    {
        FluentActions.Invoking(() => new PostgreSqlAdminWithdrawalStore(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlAdminWithdrawalStore(new NonRelationalContext()))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => new PostgreSqlAdminWithdrawalAuditTrail(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlAdminWithdrawalAuditTrail(new NonRelationalContext()))
            .Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => new PostgreSqlAdminWithdrawalFencingTokenAllocator(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlAdminWithdrawalFencingTokenAllocator(
                new NonRelationalContext()))
            .Should().Throw<InvalidOperationException>();

        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"treasury-validation-{Guid.NewGuid():N}")
                .Options);
        var store = new PostgreSqlAdminWithdrawalStore(context);
        var audit = new PostgreSqlAdminWithdrawalAuditTrail(context);
        var run = CreateRun();

        FluentActions.Invoking(() => store.Get(Guid.Empty)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.FindReplay("", "hash")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.FindReplay("key", "")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.Add(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => store.Update(null!, 1)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => store.FindProviderEvent("", "hash")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.FindProviderEvent("event", "")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.RecordProviderEvent("", "hash", run, 1))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.RecordProviderEvent("event", "", run, 1))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.RecordProviderEvent("event", "hash", null!, 1))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => audit.Append(Guid.Empty, "kind", null, "evidence", DateTimeOffset.UtcNow))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => audit.Append(Guid.NewGuid(), "", null, "evidence", DateTimeOffset.UtcNow))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => audit.Append(Guid.NewGuid(), "kind", null, "", DateTimeOffset.UtcNow))
            .Should().Throw<ArgumentException>();
    }

    private static AdminWithdrawalRun CreateRun() => new(
        Guid.NewGuid(), Guid.NewGuid(), new IdempotencyKey("treasury-validation"), "request-hash",
        new DateOnly(2026, 8, 1), Guid.NewGuid(), null, WalletId.New(),
        new CoinAmount(CurrencyCode.HardCoin, 1), "asset", "destination",
        AdminWithdrawalRunState.PendingApproval, 1, 1, 1, new ReserveVersion(1),
        1, new PolicyVersion(1), null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private sealed class NonRelationalContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
