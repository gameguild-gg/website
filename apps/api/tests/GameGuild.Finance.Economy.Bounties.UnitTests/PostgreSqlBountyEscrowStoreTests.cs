using FluentAssertions;
using GameGuild;
using GameGuild.Finance.Economy.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Bounties.UnitTests;

public sealed class PostgreSqlBountyEscrowStoreTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    [Fact]
    public void StoreRejectsInvalidConstructionAndUnboundCommands()
    {
        FluentActions.Invoking(() => new PostgreSqlBountyEscrowStore(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlBountyEscrowStore(new NonRelationalContext()))
            .Should().Throw<InvalidOperationException>();

        using var context = new BountiesModelContext(
            new DbContextOptionsBuilder<BountiesModelContext>()
                .UseNpgsql("Host=localhost;Database=bounty_store_validation;Username=test;Password=test")
                .Options);
        var store = new PostgreSqlBountyEscrowStore(context);

        FluentActions.Invoking(() => store.FindPostReplay(TenantId, new IdempotencyKey("bounty-post"), ""))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => store.Create(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => store.Create(new CreateBountyEscrowPersistenceCommand(
                null!,
                TenantId,
                new IdempotencyKey("bounty-post"),
                "request-hash",
                PostingId.New())))
            .Should().Throw<ArgumentNullException>();
    }

    private sealed class BountiesModelContext(DbContextOptions<BountiesModelContext> options) : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new Persistence.BountiesModelConfiguration().Configure(modelBuilder);

        Task<IDbContextTransaction> IApplicationDbContext.BeginTransactionAsync(CancellationToken cancellationToken) =>
            Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class NonRelationalContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
