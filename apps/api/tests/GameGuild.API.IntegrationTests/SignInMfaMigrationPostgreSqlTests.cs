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
public sealed class SignInMfaMigrationPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task UpgradeAndRollbackPreserveExistingEnrollmentAndMatchTheNativeModel()
    {
        // Load the same host module composition before checking its design model.
        using var hostScope = fixture.Factory.Services.CreateScope();
        _ = hostScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Model;
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("signin_mfa_migration");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        Assert.False(db.Database.HasPendingModelChanges());
        var migrations = db.Database.GetMigrations().ToList();
        var migration = Assert.Single(migrations, value => value.EndsWith("_AddSignInMfaChallengesAndTotpReplayState", StringComparison.Ordinal));
        var predecessor = migrations[migrations.IndexOf(migration) - 1];
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(predecessor);

        var enrollment = new UserMfaConfiguration { UserId = Guid.NewGuid(), IsEnabled = true, IsSetupComplete = true };
        db.Set<UserMfaConfiguration>().Add(enrollment);
        await db.SaveChangesAsync();
        await migrator.MigrateAsync(migration);
        Assert.Empty(await db.Set<SignInMfaChallenge>().AsNoTracking().Select(row => row.Id).ToListAsync());
        Assert.Empty(await db.Set<TotpReplayState>().AsNoTracking().ToListAsync());
        Assert.True((await db.Set<UserMfaConfiguration>().AsNoTracking().SingleAsync(value => value.Id == enrollment.Id)).IsSetupComplete);

        await migrator.MigrateAsync(predecessor);
        var tableCount = await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*)::int AS "Value" FROM information_schema.tables
            WHERE table_schema = 'gameguild.authentication'
                AND table_name IN ('sign_in_mfa_challenges', 'totp_replay_state')
            """).SingleAsync();
        Assert.Equal(0, tableCount);
        Assert.True((await db.Set<UserMfaConfiguration>().AsNoTracking().SingleAsync(value => value.Id == enrollment.Id)).IsEnabled);

        await migrator.MigrateAsync(migration);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Set<SignInMfaChallenge>().AsNoTracking().Select(row => row.Id).ToListAsync());
        Assert.Empty(await db.Set<TotpReplayState>().AsNoTracking().ToListAsync());
    }
}
