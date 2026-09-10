using FluentAssertions;
using GameGuild;
using GameGuild.API.Database;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Payouts.UnitTests;

public sealed class PostgreSqlPayoutFencingTokenAllocatorTests
{
    [Fact]
    public async Task AllocateAsync_ReturnsDatabaseMonotonicTokensAndRejectsCorruptedSequenceOutput()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("payout_fencing");
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(database.ConnectionString).Options);
        await context.Database.MigrateAsync();
        var allocator = new PostgreSqlPayoutFencingTokenAllocator(context);

        var first = await allocator.AllocateAsync();
        var second = await allocator.AllocateAsync();

        first.Should().BePositive();
        second.Should().Be(first + 1);

        await context.Database.ExecuteSqlRawAsync(
            "ALTER SEQUENCE economy_private.payout_fencing_sequence MINVALUE -1 RESTART WITH -1;");
        await FluentActions.Invoking(async () => await allocator.AllocateAsync())
            .Should().ThrowAsync<PayoutStaleCommandException>();
    }

    [Fact]
    public void Constructor_RequiresTheApplicationRelationalDbContext()
    {
        FluentActions.Invoking(() => new PostgreSqlPayoutFencingTokenAllocator(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlPayoutFencingTokenAllocator(new NonRelationalContext()))
            .Should().Throw<InvalidOperationException>();
    }

    private sealed class NonRelationalContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
