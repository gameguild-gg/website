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
        var configuration = new UserMfaConfiguration { UserId = Guid.NewGuid(), IsEnabled = true, IsSetupComplete = true,
            TotpSecretKey = "synthetic-preserved-encrypted-value", BackupCodes = "[]" };
        db.Set<UserMfaConfiguration>().Add(configuration);
        await db.SaveChangesAsync();
        await migrator.MigrateAsync(migration);
        var saved = await db.Set<UserMfaConfiguration>().AsNoTracking().SingleAsync(row => row.Id == configuration.Id);
        Assert.True(saved.IsEnabled);
        Assert.Equal(configuration.TotpSecretKey, saved.TotpSecretKey);
        Assert.Equal(configuration.BackupCodes, saved.BackupCodes);
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
        Assert.Equal(configuration.TotpSecretKey, (await db.Set<UserMfaConfiguration>().AsNoTracking().SingleAsync(row => row.Id == configuration.Id)).TotpSecretKey);
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
        var userId = Guid.NewGuid();
        var first = new UserMfaConfiguration { UserId = userId, IsEnabled = true, IsSetupComplete = true, TotpSecretKey = "synthetic-first-factor" };
        var second = new UserMfaConfiguration { UserId = userId, IsEnabled = true, IsSetupComplete = true, TotpSecretKey = "synthetic-second-factor" };
        db.Set<UserMfaConfiguration>().AddRange(first, second);
        await db.SaveChangesAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(migration));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
        Assert.Contains("reconcile duplicate MFA configurations", exception.MessageText, StringComparison.Ordinal);
        var saved = await db.Set<UserMfaConfiguration>().AsNoTracking().Where(row => row.UserId == userId).ToListAsync();
        Assert.Equal(2, saved.Count);
        Assert.Contains(saved, row => row.Id == first.Id && row.TotpSecretKey == first.TotpSecretKey && row.IsEnabled);
        Assert.Contains(saved, row => row.Id == second.Id && row.TotpSecretKey == second.TotpSecretKey && row.IsEnabled);
        Assert.DoesNotContain(migration, await db.Database.GetAppliedMigrationsAsync());
    }
}
