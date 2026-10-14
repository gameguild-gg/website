using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class MfaEnrollmentBindingMigrationPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task UpgradeRollbackAndUpgradePreserveExistingMfaConfigurationAndMatchTheNativeModel()
    {
        using var hostScope = fixture.Factory.Services.CreateScope();
        _ = hostScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Model;
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("mfa_enrollment_migration");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var migrations = db.Database.GetMigrations().ToList();
        var migration = Assert.Single(migrations, item => item.EndsWith("_BindLimitedMfaEnrollment", StringComparison.Ordinal));
        var predecessor = migrations[migrations.IndexOf(migration) - 1];
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(predecessor);

        // Seed and verify with raw SQL against the historical schema: the current model maps
        // columns that this rollback point predates (for example push_device_token and
        // sms_phone_number from 20261014101541), so materializing the entity here would
        // reference columns that do not exist yet.
        var configurationId = Guid.NewGuid();
        var preferredMethod = MfaMethod.Totp.ToString();
        var now = DateTime.UtcNow;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO gameguild.authentication.user_mfa_configuration
                (id, user_id, is_enabled, is_setup_complete, preferred_method, failed_attempts, created_at, updated_at, totp_secret_key, backup_codes)
            VALUES ({configurationId}, {Guid.NewGuid()}, {true}, {true}, {preferredMethod}, {0}, {now}, {now}, {"synthetic-preserved-encrypted-value"}, {"[]"})
            """);
        await migrator.MigrateAsync(migration);
        Assert.True(await db.Database.SqlQuery<bool>($"""
            SELECT is_enabled AS "Value" FROM gameguild.authentication.user_mfa_configuration WHERE id = {configurationId}
            """).SingleAsync());
        Assert.Equal("synthetic-preserved-encrypted-value", await db.Database.SqlQuery<string>($"""
            SELECT totp_secret_key AS "Value" FROM gameguild.authentication.user_mfa_configuration WHERE id = {configurationId}
            """).SingleAsync());
        Assert.Equal("[]", await db.Database.SqlQuery<string>($"""
            SELECT backup_codes AS "Value" FROM gameguild.authentication.user_mfa_configuration WHERE id = {configurationId}
            """).SingleAsync());
        var index = await db.Database.SqlQueryRaw<string>("""
            SELECT indexdef AS "Value" FROM pg_indexes WHERE schemaname = 'gameguild.authentication'
            AND indexname = 'ix_user_mfa_configuration_user_id'
            """).SingleAsync();
        Assert.Contains("CREATE UNIQUE INDEX", index, StringComparison.Ordinal);
        await migrator.MigrateAsync(predecessor);
        var columns = await db.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM information_schema.columns WHERE table_schema = 'gameguild.authentication'
            AND table_name = 'sign_in_mfa_challenges' AND column_name LIKE 'enrollment_%'
            """).SingleAsync();
        Assert.Equal(0, columns);
        Assert.Equal("synthetic-preserved-encrypted-value", await db.Database.SqlQuery<string>($"""
            SELECT totp_secret_key AS "Value" FROM gameguild.authentication.user_mfa_configuration WHERE id = {configurationId}
            """).SingleAsync());
        await migrator.MigrateAsync(migration);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task DuplicateConfigurationsAbortTheUpgradeWithoutDeletingOrChoosingAnExistingFactor()
    {
        using var hostScope = fixture.Factory.Services.CreateScope();
        _ = hostScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Model;
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("mfa_enrollment_duplicates");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);
        var migrations = db.Database.GetMigrations().ToList();
        var migration = Assert.Single(migrations, item => item.EndsWith("_BindLimitedMfaEnrollment", StringComparison.Ordinal));
        var predecessor = migrations[migrations.IndexOf(migration) - 1];
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(predecessor);

        // Seed with raw SQL for the same reason as above: entity materialization at this
        // historical schema would reference columns that only later migrations introduce.
        var userId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var preferredMethod = MfaMethod.Totp.ToString();
        var now = DateTime.UtcNow;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO gameguild.authentication.user_mfa_configuration
                (id, user_id, is_enabled, is_setup_complete, preferred_method, failed_attempts, created_at, updated_at, totp_secret_key)
            VALUES ({firstId}, {userId}, {true}, {true}, {preferredMethod}, {0}, {now}, {now}, {"synthetic-first-factor"}),
                   ({secondId}, {userId}, {true}, {true}, {preferredMethod}, {0}, {now}, {now}, {"synthetic-second-factor"})
            """);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(migration));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Contains("reconcile duplicate MFA configurations", exception.MessageText, StringComparison.Ordinal);
        Assert.Equal(2, await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::int AS "Value" FROM gameguild.authentication.user_mfa_configuration WHERE user_id = {userId}
            """).SingleAsync());
        Assert.Equal("synthetic-first-factor", await db.Database.SqlQuery<string>($"""
            SELECT totp_secret_key AS "Value" FROM gameguild.authentication.user_mfa_configuration WHERE id = {firstId}
            """).SingleAsync());
        Assert.True(await db.Database.SqlQuery<bool>($"""
            SELECT is_enabled AS "Value" FROM gameguild.authentication.user_mfa_configuration WHERE id = {firstId}
            """).SingleAsync());
        Assert.Equal("synthetic-second-factor", await db.Database.SqlQuery<string>($"""
            SELECT totp_secret_key AS "Value" FROM gameguild.authentication.user_mfa_configuration WHERE id = {secondId}
            """).SingleAsync());
        Assert.True(await db.Database.SqlQuery<bool>($"""
            SELECT is_enabled AS "Value" FROM gameguild.authentication.user_mfa_configuration WHERE id = {secondId}
            """).SingleAsync());
        Assert.DoesNotContain(migration, await db.Database.GetAppliedMigrationsAsync());
    }
}
