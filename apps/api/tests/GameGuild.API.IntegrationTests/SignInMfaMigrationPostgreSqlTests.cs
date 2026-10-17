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

        // Seed and verify with raw SQL against the historical schema: the current model maps
        // columns that this rollback point predates (for example push_device_token and
        // sms_phone_number from 20261014101541), so materializing the entity here would
        // reference columns that do not exist yet.
        var enrollmentId = Guid.NewGuid();
        var preferredMethod = MfaMethod.Totp.ToString();
        var now = DateTime.UtcNow;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO "gameguild.authentication".user_mfa_configuration
                (id, user_id, is_enabled, is_setup_complete, preferred_method, failed_attempts, created_at, updated_at)
            VALUES ({enrollmentId}, {Guid.NewGuid()}, {true}, {true}, {preferredMethod}, {0}, {now}, {now})
            """);
        await migrator.MigrateAsync(migration);
        Assert.Empty(await db.Set<SignInMfaChallenge>().AsNoTracking().Select(row => row.Id).ToListAsync());
        await AssertReplayStateEmptyAsync(db);
        Assert.True(await db.Database.SqlQuery<bool>($"""
            SELECT is_setup_complete AS "Value" FROM "gameguild.authentication".user_mfa_configuration WHERE id = {enrollmentId}
            """).SingleAsync());

        await migrator.MigrateAsync(predecessor);
        var tableCount = await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*)::int AS "Value" FROM information_schema.tables
            WHERE table_schema = 'gameguild.authentication'
                AND table_name IN ('sign_in_mfa_challenges', 'totp_replay_state')
            """).SingleAsync();
        Assert.Equal(0, tableCount);
        Assert.True(await db.Database.SqlQuery<bool>($"""
            SELECT is_enabled AS "Value" FROM "gameguild.authentication".user_mfa_configuration WHERE id = {enrollmentId}
            """).SingleAsync());

        await migrator.MigrateAsync(migration);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Set<SignInMfaChallenge>().AsNoTracking().Select(row => row.Id).ToListAsync());
        await AssertReplayStateEmptyAsync(db);
    }

    // Counted with raw SQL for the same reason as the seeded enrollment: entity queries at the
    // historical schema must not reference columns that only later migrations introduce.
    private static async Task AssertReplayStateEmptyAsync(ApplicationDbContext db)
    {
        var replayStates = await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*)::int AS "Value" FROM "gameguild.authentication".totp_replay_state
            """).SingleAsync();
        Assert.Equal(0, replayStates);
    }
}
