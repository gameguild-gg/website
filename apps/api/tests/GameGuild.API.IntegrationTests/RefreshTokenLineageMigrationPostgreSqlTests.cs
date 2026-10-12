using System.Security.Cryptography;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class RefreshTokenLineageMigrationPostgreSqlTests
{
    [Fact]
    public async Task UpgradeAndRollbackPreserveLegacyRowsWithoutGuessingHistoricalLineage()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("refresh_lineage_migration");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        var migrations = db.Database.GetMigrations().ToList();
        var migration = Assert.Single(migrations, value => value.EndsWith("_AddRefreshTokenLineage", StringComparison.Ordinal));
        var precedingMigration = migrations[migrations.IndexOf(migration) - 1];
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(precedingMigration);
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var hash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var createdAt = DateTime.UtcNow;
        var expiresAt = createdAt.AddDays(7);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "gameguild.authentication"."refreshtoken"
                ("id", "UserId", "Token", "ExpiresAt", "IsRevoked", "CreatedAt", "UpdatedAt", "CreatedByIp")
            VALUES ({id}, {userId}, {hash}, {expiresAt}, false, {createdAt}, {createdAt}, '192.0.2.1')
            """);

        await migrator.MigrateAsync(migration);
        var upgraded = await db.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == id);
        Assert.Equal(userId, upgraded.UserId);
        Assert.Equal(hash, upgraded.Token);
        Assert.False(upgraded.IsRevoked);
        Assert.Null(upgraded.ParentTokenId);
        Assert.Null(upgraded.SessionId);

        await migrator.MigrateAsync(precedingMigration);
        var retainedRows = await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*)::int AS "Value" FROM "gameguild.authentication"."refreshtoken"
            WHERE "id" = @row_id AND "UserId" = @user_id AND "Token" = @token_hash AND "IsRevoked" = false
            """, new NpgsqlParameter("row_id", id), new NpgsqlParameter("user_id", userId),
            new NpgsqlParameter("token_hash", hash)).SingleAsync();
        Assert.Equal(1, retainedRows);
        var removedColumns = await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*)::int AS "Value" FROM information_schema.columns
            WHERE table_schema = 'gameguild.authentication' AND table_name = 'refreshtoken'
                AND column_name IN ('ParentTokenId', 'SessionId')
            """).SingleAsync();
        Assert.Equal(0, removedColumns);
        await migrator.MigrateAsync(migration);
        var restored = await db.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == id);
        Assert.Null(restored.ParentTokenId);
        Assert.Null(restored.SessionId);
        Assert.Equal(hash, restored.Token);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
