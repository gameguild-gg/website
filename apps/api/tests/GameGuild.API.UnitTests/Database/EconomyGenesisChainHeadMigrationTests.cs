using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.Database.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;

namespace GameGuild.API.UnitTests.Database;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class EconomyGenesisChainHeadMigrationTests
{
    private const string PreviousMigration = "20260825053623_AddEconomyLegacyShadowMigration";

    [Fact]
    public void MigrationSeedsOnlyAnEmptyJournalAndRemovesOnlyAnUnusedGenesisHead()
    {
        var migration = new ExposedMigration();
        var up = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var down = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        migration.BuildUp(up);
        migration.BuildDown(down);

        var upSql = up.Operations.Should().ContainSingle().Which.Should().BeOfType<SqlOperation>().Which.Sql;
        upSql.Should().Contain("NOT EXISTS (SELECT 1 FROM public.economy_chain_head)");
        upSql.Should().Contain("NOT EXISTS (SELECT 1 FROM public.economy_journal_entries)");

        var downSql = down.Operations.Should().ContainSingle().Which.Should().BeOfType<SqlOperation>().Which.Sql;
        downSql.Should().Contain("head.\"Sequence\" = 0");
        downSql.Should().Contain("NOT EXISTS (SELECT 1 FROM public.economy_journal_entries)");
        downSql.Should().Contain("NOT EXISTS (SELECT 1 FROM public.economy_journal_verification_checkpoints)");
    }

    [Fact]
    public async Task MigrationCanRollBackAndReapplyTheGenesisHeadOnAnEmptyDatabase()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("economy_genesis_chain_head");
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(database.ConnectionString)
                .Options);
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(PreviousMigration);
        (await ReadGenesisHeadAsync(database.ConnectionString)).Should().BeNull();

        await migrator.MigrateAsync();
        (await ReadGenesisHeadAsync(database.ConnectionString)).Should().Be((1, 0L, 64));
    }

    private static async Task<(int Id, long Sequence, int HashLength)?> ReadGenesisHeadAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT \"Id\", \"Sequence\", length(\"Hash\") FROM public.economy_chain_head;",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return (reader.GetInt32(0), reader.GetInt64(1), reader.GetInt32(2));
    }

    private sealed class ExposedMigration : InitializeEconomyGenesisChainHead
    {
        public void BuildUp(MigrationBuilder migrationBuilder) => Up(migrationBuilder);

        public void BuildDown(MigrationBuilder migrationBuilder) => Down(migrationBuilder);
    }
}
