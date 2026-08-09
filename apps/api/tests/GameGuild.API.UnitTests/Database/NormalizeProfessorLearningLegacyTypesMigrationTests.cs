using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.Database.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace GameGuild.API.UnitTests.Database;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class NormalizeProfessorLearningLegacyTypesMigrationTests
{
    [Fact]
    public async Task Up_NormalizesPersistedLegacyContentTypes()
    {
        var container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("legacy_program_content_types")
            .WithUsername("test")
            .WithPassword("test")
            .WithCleanUp(true)
            .Build();
        await container.StartAsync();
        try
        {
            await using var connection = new NpgsqlConnection(container.GetConnectionString());
            await connection.OpenAsync();
            var pageId = Guid.NewGuid();
            var challengeId = Guid.NewGuid();
            var lessonId = Guid.NewGuid();
            var assignmentId = Guid.NewGuid();
            await ExecuteAsync(connection, """
                CREATE TABLE "Assessments" ("Id" uuid PRIMARY KEY, "Type" integer NOT NULL);
                CREATE TABLE program_contents ("Id" uuid PRIMARY KEY, "Type" integer NOT NULL);
                """);
            await ExecuteAsync(connection, $"""
                INSERT INTO program_contents ("Id", "Type") VALUES
                    ('{pageId}', 1),
                    ('{challengeId}', 6),
                    ('{lessonId}', 0),
                    ('{assignmentId}', 2);
                """);

            await ApplyUpAsync(connection);

            await using var command = new NpgsqlCommand(
                "SELECT \"Id\", \"Type\" FROM program_contents ORDER BY \"Id\";",
                connection);
            await using var reader = await command.ExecuteReaderAsync();
            var persistedTypes = new Dictionary<Guid, int>();
            while (await reader.ReadAsync())
            {
                persistedTypes.Add(reader.GetGuid(0), reader.GetInt32(1));
            }

            persistedTypes.Should().Contain(new KeyValuePair<Guid, int>(pageId, 0));
            persistedTypes.Should().Contain(new KeyValuePair<Guid, int>(challengeId, 2));
            persistedTypes.Should().Contain(new KeyValuePair<Guid, int>(lessonId, 0));
            persistedTypes.Should().Contain(new KeyValuePair<Guid, int>(assignmentId, 2));
        }
        finally
        {
            await container.DisposeAsync();
        }
    }

    private static async Task ApplyUpAsync(NpgsqlConnection connection)
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedMigration().BuildUp(builder);
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connection.ConnectionString)
                .Options);
        var generator = context.GetService<IMigrationsSqlGenerator>();
        foreach (var command in generator.Generate(builder.Operations, null))
        {
            await ExecuteAsync(connection, command.CommandText);
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class ExposedMigration : NormalizeProfessorLearningLegacyTypes
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
    }
}
