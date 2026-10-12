using GameGuild.TestSupport.Finance.Economy;
using Npgsql;

namespace GameGuild.Resources.IntegrationTests.Infrastructure;

public sealed class PostgreSqlDatabaseResetTests
{
    [Fact]
    public async Task ResetRemovesManyRelationsWithoutAccumulatingSchemaLocksOrChangingTheGateTemplate()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("reset_lock_capacity");
        var originalName = new NpgsqlConnectionStringBuilder(database.ConnectionString).Database;
        var templateBefore = await ReadTemplateTablesAsync();
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            // Each creation batch commits independently. Reset must handle the accumulated
            // relations without retaining thousands of catalog locks in one transaction.
            for (var batch = 0; batch < 200; batch++)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"""
                    DO $create$ BEGIN
                        FOR relation_index IN {batch * 25}..{batch * 25 + 24} LOOP
                            EXECUTE format('CREATE TABLE reset_capacity_%s (id integer)', relation_index);
                        END LOOP;
                    END $create$;
                    """;
                await command.ExecuteNonQueryAsync();
            }
        }

        await database.ResetAsync();

        Assert.Equal(originalName, new NpgsqlConnectionStringBuilder(database.ConnectionString).Database);
        await using var resetConnection = new NpgsqlConnection(database.ConnectionString);
        await resetConnection.OpenAsync();
        await using var tables = resetConnection.CreateCommand();
        tables.CommandText = "SELECT count(*) FROM pg_tables WHERE schemaname NOT LIKE 'pg_%' AND schemaname <> 'information_schema'";
        Assert.Equal(0L, await tables.ExecuteScalarAsync());
        await using var usable = resetConnection.CreateCommand();
        usable.CommandText = "CREATE TABLE reset_is_usable (id integer); INSERT INTO reset_is_usable VALUES (292); SELECT id FROM reset_is_usable";
        Assert.Equal(292, await usable.ExecuteScalarAsync());
        Assert.Equal(templateBefore, await ReadTemplateTablesAsync());
    }

    private static async Task<string[]> ReadTemplateTablesAsync()
    {
        var gate = Environment.GetEnvironmentVariable("ECONOMY_POSTGRES_CONNECTION");
        var template = Environment.GetEnvironmentVariable("ECONOMY_POSTGRES_TEMPLATE_DATABASE");
        if (string.IsNullOrWhiteSpace(gate) || string.IsNullOrWhiteSpace(template))
        {
            return [];
        }
        var builder = new NpgsqlConnectionStringBuilder(gate) { Database = template, Pooling = false };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT schemaname || '.' || tablename FROM pg_tables WHERE schemaname NOT LIKE 'pg_%' AND schemaname <> 'information_schema' ORDER BY schemaname, tablename";
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<string>();
        while (await reader.ReadAsync()) { result.Add(reader.GetString(0)); }
        return result.ToArray();
    }
}
