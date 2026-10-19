using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using GameGuild;
using GameGuild.Identity.Authentication;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.UnitTests.Database;

public sealed class MagicLinkTokenStoreTests
{
    [Fact]
    public async Task ConsumeAsync_IsSharedAndOneTime_AndPersistsOnlyTokenHash()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MagicLinkTokenStoreTestContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new MagicLinkTokenStoreTestContext(options);
        await context.Database.EnsureCreatedAsync();

        var store = new DatabaseMagicLinkTokenStore(context);
        var token = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var now = SystemClock.UtcNow;
        await store.AddAsync(token, userId, "Magic@Test.COM", now.AddMinutes(15));

        await using var replicaContext = new MagicLinkTokenStoreTestContext(options);
        var replicaStore = new DatabaseMagicLinkTokenStore(replicaContext);
        (await replicaStore.IsValidAsync(token, now)).Should().BeTrue();

        var firstResult = await replicaStore.ConsumeAsync(token, now);
        var secondResult = await store.ConsumeAsync(token, now);

        firstResult.Success.Should().BeTrue();
        firstResult.UserId.Should().Be(userId);
        firstResult.Email.Should().Be("magic@test.com");
        secondResult.Success.Should().BeFalse();
        var persistedToken = await context.Set<MagicLinkToken>().SingleAsync();
        persistedToken.TokenHash.Should().Be(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant());
    }

    [Fact]
    public async Task ExpiredAndMissingTokens_CannotBeConsumed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MagicLinkTokenStoreTestContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new MagicLinkTokenStoreTestContext(options);
        await context.Database.EnsureCreatedAsync();
        var store = new DatabaseMagicLinkTokenStore(context);
        var now = SystemClock.UtcNow;
        const string token = "expired-magic-link-fixture";
        await store.AddAsync(token, Guid.NewGuid(), "Expired@Test.COM", now.AddMinutes(-1));

        (await store.IsValidAsync(token, now)).Should().BeFalse();
        (await store.ConsumeAsync(token, now)).Success.Should().BeFalse();
        (await store.IsValidAsync("missing-magic-link-fixture", now)).Should().BeFalse();
        (await store.ConsumeAsync("missing-magic-link-fixture", now)).Success.Should().BeFalse();
    }

    private sealed class MagicLinkTokenStoreTestContext(DbContextOptions<MagicLinkTokenStoreTestContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new MagicLinkTokenModelConfiguration().Configure(modelBuilder);
            modelBuilder.Entity<MagicLinkToken>()
                .Property(token => token.ExpiresAt)
                .HasConversion<long>();
            modelBuilder.Entity<MagicLinkToken>()
                .Property(token => token.ConsumedAt)
                .HasConversion<long>();
        }
    }
}
