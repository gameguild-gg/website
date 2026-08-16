using FluentAssertions;
using GameGuild.API.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace GameGuild.API.UnitTests.Database;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class EconomyMigrationPrerequisiteTests
{
    [Fact]
    public async Task PrepareAsync_WithNonPostgreSqlProvider_ShouldSkipRolePreparation()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var prerequisite = new EconomyMigrationPrerequisite();

        Func<Task> action = () => prerequisite.PrepareAsync(context, CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    [DockerFact]
    public async Task PrepareAsync_WithRestrictedMigrationRole_ShouldAllowEconomyOwnershipMigration()
    {
        await using var container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("economy_prerequisite")
            .WithUsername("test")
            .WithPassword("test")
            .WithCleanUp(true)
            .Build();
        await container.StartAsync();

        await using (var admin = new NpgsqlConnection(container.GetConnectionString()))
        {
            await admin.OpenAsync();
            await using var command = admin.CreateCommand();
            command.CommandText = """
                CREATE ROLE economy_migrator LOGIN PASSWORD 'migration-secret' CREATEROLE;
                ALTER DATABASE economy_prerequisite OWNER TO economy_migrator;
                """;
            await command.ExecuteNonQueryAsync();
        }

        var migrationConnectionString = new NpgsqlConnectionStringBuilder(container.GetConnectionString())
        {
            Username = "economy_migrator",
            Password = "migration-secret"
        }.ConnectionString;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(migrationConnectionString, npgsqlOptions =>
                npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var prerequisite = new EconomyMigrationPrerequisite();

        await prerequisite.PrepareAsync(context, CancellationToken.None);
        await using (var preparedConnection = new NpgsqlConnection(migrationConnectionString))
        {
            await preparedConnection.OpenAsync();
            await using var preparedCommand = preparedConnection.CreateCommand();
            preparedCommand.CommandText = """
                SELECT pg_has_role(current_user, 'gameguild_economy_procedure_owner', 'MEMBER'),
                       pg_has_role(current_user, 'gameguild_economy_procedure_owner', 'USAGE');
                """;
            await using var reader = await preparedCommand.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();
            reader.GetBoolean(0).Should().BeTrue();
            reader.GetBoolean(1).Should().BeTrue();
        }

        await context.Database.GetService<IMigrator>()
            .MigrateAsync("20260719012558_AddEconomyFoundationSchemaRollup");

        await using var connection = new NpgsqlConnection(migrationConnectionString);
        await connection.OpenAsync();
        await using var membershipCommand = connection.CreateCommand();
        membershipCommand.CommandText =
            "SELECT pg_has_role(current_user, 'gameguild_economy_procedure_owner', 'MEMBER');";
        (await membershipCommand.ExecuteScalarAsync()).Should().Be(true);

        await using var ownerCommand = connection.CreateCommand();
        ownerCommand.CommandText = """
            SELECT pg_get_userbyid(proowner)
            FROM pg_proc p
            JOIN pg_namespace n ON n.oid = p.pronamespace
            WHERE n.nspname = 'economy_private'
              AND p.proname = 'deny_immutable_mutation_v1';
            """;
        (await ownerCommand.ExecuteScalarAsync()).Should().Be("gameguild_economy_procedure_owner");
    }

    private sealed class DockerFactAttribute : FactAttribute
    {
        public DockerFactAttribute()
        {
            if (string.Equals(
                    Environment.GetEnvironmentVariable("SKIP_DOCKER_TESTS"),
                    "1",
                    StringComparison.Ordinal))
            {
                Skip = "Docker tests disabled by SKIP_DOCKER_TESTS=1.";
            }
        }
    }
}
