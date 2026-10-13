using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class SessionMfaEvidenceMigrationPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task UpgradeAndRollbackPreserveExistingSessionAndMatchTheNativeModel()
    {
        using var hostScope = fixture.Factory.Services.CreateScope();
        _ = hostScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Model;
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("session_mfa_migration");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        Assert.False(db.Database.HasPendingModelChanges());
        var migrations = db.Database.GetMigrations().ToList();
        var migration = Assert.Single(migrations, value => value.EndsWith("_AddSessionMfaEvidence", StringComparison.Ordinal));
        var predecessor = migrations[migrations.IndexOf(migration) - 1];
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(predecessor);
        var session = new UserSession
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), RefreshToken = Guid.NewGuid().ToString("N"),
            IpAddress = "127.0.0.1", UserAgent = "Synthetic migration client", IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(1), LastUsedAt = DateTime.UtcNow
        };
        db.Set<UserSession>().Add(session);
        await db.SaveChangesAsync();
        await migrator.MigrateAsync(migration);
        Assert.Empty(await db.Set<SessionMfaEvidence>().AsNoTracking().ToListAsync());
        Assert.True((await db.Set<UserSession>().AsNoTracking().SingleAsync(row => row.Id == session.Id)).IsActive);
        await migrator.MigrateAsync(predecessor);
        var tableCount = await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*)::int AS "Value" FROM information_schema.tables
            WHERE table_schema = 'gameguild.authentication' AND table_name = 'session_mfa_evidence'
            """).SingleAsync();
        Assert.Equal(0, tableCount);
        Assert.Equal(session.RefreshToken, (await db.Set<UserSession>().AsNoTracking().SingleAsync(row => row.Id == session.Id)).RefreshToken);
        await migrator.MigrateAsync(migration);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Set<SessionMfaEvidence>().AsNoTracking().ToListAsync());
    }
}
