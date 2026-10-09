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
public sealed class EconomyReserveAuthorityPostgreSqlMigrationTests
{
    [Fact]
    public void MigrationAddsOnlyReserveAuthorityStateAndFailClosedAuthorizationColumns()
    {
        var up = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var down = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var migration = new ExposedReserveMigration();

        migration.BuildUp(up);
        migration.BuildDown(down);

        up.Operations.OfType<CreateTableOperation>().Select(operation => operation.Name)
            .Should().BeEquivalentTo("economy_reserve_heads", "economy_reserve_asset_allocations");
        up.Operations.OfType<AddColumnOperation>().Select(operation => (operation.Table, operation.Name))
            .Should().BeEquivalentTo(new (string Table, string Name)[]
            {
                ("economy_risk_decisions", "ReserveAuthorizationEpoch"),
                ("economy_posting_groups", "ReserveAuthorizationEpoch"),
                ("economy_dispatch_snapshots", "ReserveAuthorizationEpoch")
            });

        var sql = string.Join('\n', up.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
        sql.Should().Contain("SET \"ReserveAuthorizationEpoch\" = 0");
        sql.Should().Contain("ck_economy_posting_groups_reserve_authorization");
        sql.Should().Contain("NOT VALID");
        sql.Should().Contain("activate_reserve_head_v1");
        sql.Should().Contain("SECURITY DEFINER");
        sql.Should().Contain("SET search_path = pg_catalog, economy_private");
        sql.Should().Contain("REVOKE ALL ON TABLE public.economy_reserve_heads FROM gameguild_economy_writer");
        down.Operations.OfType<DropTableOperation>().Select(operation => operation.Name)
            .Should().BeEquivalentTo("economy_reserve_heads", "economy_reserve_asset_allocations");
    }

    [DockerFact]
    public async Task MigrationEnforcesOneActiveHeadExclusiveAssetsAndNewEpochBindings()
    {
        await using var container = await EconomyPostgreSqlTestDatabase.CreateAsync("economy_reserve");

        await using var connection = new NpgsqlConnection(container.ConnectionString);
        await connection.OpenAsync();
        await ApplyAsync(connection, BuildFoundationUp());
        await ApplyAsync(connection, BuildReserveUp());

        (await ScalarAsync<long>(connection,
                "SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tablename LIKE 'economy_reserve_%';"))
            .Should().Be(2);
        (await ScalarAsync<long>(connection,
                "SELECT count(*) FROM pg_constraint WHERE conname IN ('ck_economy_risk_decisions_versions_positive', 'ck_economy_posting_groups_reserve_authorization', 'ck_economy_dispatch_snapshots_reserve_authorization') AND NOT convalidated;"))
            .Should().Be(3);

        (await ScalarAsync<bool>(connection,
                "SELECT has_function_privilege('gameguild_economy_writer', 'economy_private.activate_reserve_head_v1(bigint,bigint,bigint,bigint,timestamptz,timestamptz,bigint,bigint,bigint,bigint,bigint,bigint,bigint,integer,text,timestamptz,jsonb)', 'EXECUTE');"))
            .Should().BeTrue();
        (await ScalarAsync<bool>(connection,
                "SELECT has_table_privilege('gameguild_economy_writer', 'public.economy_reserve_heads', 'INSERT');"))
            .Should().BeFalse();

        await ExecuteAsync(connection, "SET ROLE gameguild_economy_writer;");
        (await ScalarAsync<long>(connection, ActivateSql(1, "NULL", 1, "settled-cash-1"))).Should().Be(1);
        var staleVersion = await Assert.ThrowsAsync<PostgresException>(() =>
            ScalarAsync<long>(connection, ActivateSql(2, "NULL", 2, "settled-cash-2")));
        staleVersion.SqlState.Should().Be(PostgresErrorCodes.SerializationFailure);
        await ExecuteAsync(connection, "RESET ROLE;");

        var duplicateActive = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync(connection, HeadInsert(2, true, 2)));
        duplicateActive.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);

        await ExecuteAsync(connection, HeadInsert(2, false, 2));
        var duplicateAsset = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            INSERT INTO economy_reserve_asset_allocations
                ("Id", "ReserveVersion", "AssetKey", "Purpose", "EligibleUsdNanos")
            VALUES
                ('10000000-0000-0000-0000-000000000002', 1, 'settled-cash-1', 2, 10000000);
            """));
        duplicateAsset.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);

        var invalidPurpose = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            INSERT INTO economy_reserve_asset_allocations
                ("Id", "ReserveVersion", "AssetKey", "Purpose", "EligibleUsdNanos")
            VALUES
                ('10000000-0000-0000-0000-000000000003', 1, 'invalid-purpose', 3, 10000000);
            """));
        invalidPurpose.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);

        await ApplyAsync(connection, BuildReserveDown());
        (await ScalarAsync<long>(connection,
                "SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tablename LIKE 'economy_reserve_%';"))
            .Should().Be(0);
    }

    private static string HeadInsert(long version, bool active, long epoch) => $$"""
        INSERT INTO economy_reserve_heads
            ("Version", "IsActive", "PolicyVersion", "AuthorizationEpoch", "ObservedAt", "ExpiresAt",
             "HardFaceValueUsdMinor", "RequiredHardReserveUsdMinor", "SoftFaceValueUsdNanos",
             "StressedExpectedRedemptionCostUsdNanos", "RequiredSoftReserveUsdNanos",
             "HardBackingUsdNanos", "SoftBackingUsdNanos", "Coverage", "EvidenceHash", "ActivatedAt")
        VALUES
            ({{version}}, {{active.ToString().ToLowerInvariant()}}, 1, {{epoch}}, '2026-07-18T11:00:00Z', '2026-07-18T13:00:00Z',
             0, 0, 0, 0, 0, 0, 0, 1, 'evidence-{{version}}', '2026-07-18T12:00:00Z');
        """;

    private static string ActivateSql(long version, string expectedVersion, long epoch, string assetKey) => $$"""
        SELECT economy_private.activate_reserve_head_v1(
            {{version}}, {{expectedVersion}}, 1, {{epoch}},
            '2026-07-18T11:00:00Z', '2026-07-18T13:00:00Z',
            0, 0, 0, 0, 0, 10000000, 10000000, 1,
            'evidence-{{version}}', '2026-07-18T12:00:00Z',
            '[{"assetKey":"{{assetKey}}","purpose":1,"eligibleUsdNanos":10000000},
              {"assetKey":"soft-{{assetKey}}","purpose":2,"eligibleUsdNanos":10000000}]'::jsonb);
        """;

    private static MigrationBuilder BuildFoundationUp()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedFoundationMigration().BuildUp(builder);
        return builder;
    }

    private static MigrationBuilder BuildReserveUp()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedReserveMigration().BuildUp(builder);
        return builder;
    }

    private static MigrationBuilder BuildReserveDown()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedReserveMigration().BuildDown(builder);
        return builder;
    }

    private static async Task ApplyAsync(NpgsqlConnection connection, MigrationBuilder builder)
    {
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).Options);
        var generator = context.GetService<IMigrationsSqlGenerator>();
        await using var transaction = await connection.BeginTransactionAsync();
        foreach (var command in generator.Generate(builder.Operations, null))
        {
            await ExecuteAsync(connection, command.CommandText, transaction);
        }

        await transaction.CommitAsync();
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private sealed class ExposedFoundationMigration : AddEconomyFoundationSchemaRollup
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
    }

    private sealed class ExposedReserveMigration : AddEconomyCoreReserveAuthority
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
        public void BuildDown(MigrationBuilder builder) => Down(builder);
    }

    private sealed class DockerFactAttribute : FactAttribute
    {
        public DockerFactAttribute()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("SKIP_DOCKER_TESTS"), "1", StringComparison.Ordinal))
            {
                Skip = "Docker tests disabled by SKIP_DOCKER_TESTS=1.";
            }
        }
    }
}
