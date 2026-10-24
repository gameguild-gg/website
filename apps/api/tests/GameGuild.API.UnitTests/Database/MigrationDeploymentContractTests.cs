using FluentAssertions;
using GameGuild.API.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Reflection;

namespace GameGuild.API.UnitTests.Database;

public sealed class MigrationDeploymentContractTests
{
    [Fact]
    public void MigrationCatalog_ContainsEveryConcreteHostMigration()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new ApplicationDbContext(options);
        var discovered = context.GetService<IMigrationsAssembly>().Migrations;
        var migrations = typeof(ApplicationDbContext).Assembly.GetTypes()
            .Where(type => typeof(Migration).IsAssignableFrom(type) && !type.IsAbstract)
            .ToArray();

        migrations.Should().NotBeEmpty();
        foreach (var migration in migrations)
        {
            var contextAttribute = migration.GetCustomAttribute<DbContextAttribute>();
            var identity = migration.GetCustomAttribute<MigrationAttribute>();
            contextAttribute.Should().NotBeNull($"{migration.Name} must declare its DbContext");
            contextAttribute!.ContextType.Should().Be(typeof(ApplicationDbContext));
            identity.Should().NotBeNull($"{migration.Name} must declare its deployed migration ID");
            discovered.Should().ContainKey(identity!.Id, $"{migration.Name} must be applied by Database.Migrate");
            discovered[identity.Id].AsType().Should().Be(migration);
        }

        discovered.Should().HaveCount(migrations.Length);
    }

    [Fact]
    public void MigrationCatalog_PreservesTheDeployedReconciliationIdentity()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;

        using var context = new ApplicationDbContext(options);
        var migrations = context.Database.GetMigrations().ToArray();

        migrations.Should().Contain("20260814193823_ReconcileApplicationModel");
        migrations.Should().NotContain("20260815011232_ReconcileApplicationModel");
        migrations.Should().OnlyHaveUniqueItems();
    }
}
