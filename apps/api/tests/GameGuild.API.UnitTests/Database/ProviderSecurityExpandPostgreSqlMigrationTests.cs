using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.Database.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;

namespace GameGuild.API.UnitTests.Database;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class ProviderSecurityExpandPostgreSqlMigrationTests
{
    private const string PreviousMigration = "20260716160000_AddAssessmentIntegrityGuards";
    private const string ExpandMigration = "20260717112000_ExpandProviderSecuritySchema";
    private const string CurrentMigration = "20260717112100_CreateProviderSecurityIndexesConcurrently";

    [Fact]
    public void Up_Is_Additive_Nullable_And_Preserves_Legacy_Unique_Index()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedMigration().BuildUp(builder);

        var columns = builder.Operations.OfType<AddColumnOperation>().ToList();
        columns.Select(column => new
            {
                column.Table,
                column.Name,
                column.ColumnType,
                column.MaxLength,
                column.IsNullable
            })
            .Should().BeEquivalentTo(
                new[]
                {
                    new { Table = "billing_webhook_events", Name = "EventSchemaVersion", ColumnType = "character varying(50)", MaxLength = (int?)50, IsNullable = true },
                    new { Table = "billing_webhook_events", Name = "IsLiveMode", ColumnType = "boolean", MaxLength = (int?)null, IsNullable = true },
                    new { Table = "billing_webhook_events", Name = "ProviderAccountId", ColumnType = "character varying(255)", MaxLength = (int?)255, IsNullable = true },
                    new { Table = "billing_webhook_events", Name = "ProviderEnvironment", ColumnType = "character varying(32)", MaxLength = (int?)32, IsNullable = true },
                    new { Table = "billing_webhook_events", Name = "ProviderMonetaryLeg", ColumnType = "character varying(100)", MaxLength = (int?)100, IsNullable = true },
                    new { Table = "billing_webhook_events", Name = "ProviderObjectId", ColumnType = "character varying(255)", MaxLength = (int?)255, IsNullable = true },
                    new { Table = "billing_webhook_events", Name = "ProviderObjectType", ColumnType = "character varying(100)", MaxLength = (int?)100, IsNullable = true },
                    new { Table = "billing_webhook_events", Name = "WebhookEndpointId", ColumnType = "character varying(255)", MaxLength = (int?)255, IsNullable = true },
                    new { Table = "payments", Name = "ProviderAccountId", ColumnType = "character varying(255)", MaxLength = (int?)255, IsNullable = true },
                    new { Table = "payments", Name = "ProviderEnvironment", ColumnType = "character varying(32)", MaxLength = (int?)32, IsNullable = true },
                    new { Table = "payments", Name = "ProviderMonetaryLeg", ColumnType = "character varying(100)", MaxLength = (int?)100, IsNullable = true },
                    new { Table = "payments", Name = "ProviderObjectId", ColumnType = "character varying(255)", MaxLength = (int?)255, IsNullable = true },
                    new { Table = "payments", Name = "ProviderObjectType", ColumnType = "character varying(100)", MaxLength = (int?)100, IsNullable = true }
                });
        builder.Operations.OfType<DropIndexOperation>()
            .Should().NotContain(operation => operation.Name == "ix_billing_webhook_events_external_id_provider");
        builder.Operations.OfType<CreateIndexOperation>().Should().BeEmpty();
    }

    [Fact]
    public void Index_Migration_Is_Concurrent_Retry_Safe_Filtered_And_NonUnique()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedIndexMigration().BuildUp(builder);

        var operations = builder.Operations.OfType<SqlOperation>().ToList();
        operations.Should().HaveCount(6);
        operations.Should().OnlyContain(operation => operation.SuppressTransaction);
        var sql = string.Join('\n', operations.Select(operation => operation.Sql));
        operations[0].Sql.Should().StartWith("DROP INDEX CONCURRENTLY IF EXISTS ix_billing_webhook_events_provider_object_leg");
        operations[1].Sql.Should().Contain("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_billing_webhook_events_provider_object_leg");
        operations[2].Sql.Should().StartWith("DROP INDEX CONCURRENTLY IF EXISTS ix_billing_webhook_events_provider_scope_event");
        operations[3].Sql.Should().Contain("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_billing_webhook_events_provider_scope_event");
        operations[4].Sql.Should().StartWith("DROP INDEX CONCURRENTLY IF EXISTS ix_payments_provider_object_leg");
        operations[5].Sql.Should().Contain("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payments_provider_object_leg");
        sql.Should().Contain("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_billing_webhook_events_provider_scope_event");
        sql.Should().Contain("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_billing_webhook_events_provider_object_leg");
        sql.Should().Contain("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_payments_provider_object_leg");
        sql.Should().NotContain("CREATE UNIQUE INDEX");
        sql.Should().Contain("(\"Provider\", \"ProviderEnvironment\", \"ProviderAccountId\", \"WebhookEndpointId\", \"ExternalEventId\")");
        sql.Should().Contain("(\"Provider\", \"ProviderEnvironment\", \"ProviderAccountId\", \"ProviderObjectId\", \"ProviderMonetaryLeg\")");
        sql.Should().Contain("WHERE \"ProviderEnvironment\" IS NOT NULL");
    }

    [DockerFact]
    public async Task Ef_Migrator_Up_NoOp_Down_And_Current_Reapply_Preserve_Legacy_Rows_On_PostgreSql()
    {
        await using var container = await EconomyPostgreSqlTestDatabase.CreateAsync("provider_security_expand");
        var connectionString = container.ConnectionString;
        await using var context = CreateContext(connectionString);
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(PreviousMigration);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, """
            INSERT INTO billing_webhook_events
                ("Id", "Provider", "ExternalEventId", "EventType", "Payload", "IsProcessed", "IsFailed",
                 "ProcessingAttempts", "Version", "CreatedAt", "UpdatedAt")
            VALUES
                ('00000000-0000-0000-0000-000000000001', 'stripe', 'evt_legacy', 'payment.succeeded', '{}',
                 false, false, 0, 1, now(), now());
            INSERT INTO payments
                ("Id", "TenantId", "Amount", "Currency", "Status", "Provider", "ExternalPaymentId",
                 "IdempotencyKey", "RetryCount", "MaxRetries", "RefundedAmount", "Version", "CreatedAt", "UpdatedAt")
            VALUES
                ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000003', 10, 'USD', 0,
                 'stripe', 'pi_legacy', 'provider-expand-legacy', 0, 3, 0, 1, now(), now()),
                ('00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000003', 20, 'USD', 0,
                 'stripe', 'pi_legacy_2', 'provider-expand-legacy-2', 0, 3, 0, 1, now(), now());
            """);

        await migrator.MigrateAsync(CurrentMigration);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM billing_webhook_events WHERE \"ExternalEventId\" = 'evt_legacy';"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, """
            SELECT count(*)
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND ((table_name = 'billing_webhook_events' AND column_name IN
                    ('EventSchemaVersion', 'IsLiveMode', 'ProviderAccountId', 'ProviderEnvironment',
                     'ProviderMonetaryLeg', 'ProviderObjectId', 'ProviderObjectType', 'WebhookEndpointId'))
                OR (table_name = 'payments' AND column_name IN
                    ('ProviderAccountId', 'ProviderEnvironment', 'ProviderMonetaryLeg',
                     'ProviderObjectId', 'ProviderObjectType')));
            """)).Should().Be(13);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" IN ('{ExpandMigration}', '{CurrentMigration}');"))
            .Should().Be(2);

        await ExecuteAsync(connection, $"DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{CurrentMigration}';");
        await ExecuteAsync(connection, "DROP INDEX CONCURRENTLY ix_payments_provider_object_leg;");
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            connection,
            "CREATE UNIQUE INDEX CONCURRENTLY ix_payments_provider_object_leg ON payments (\"Provider\");"));
        (await ScalarAsync<long>(connection, """
            SELECT count(*)
            FROM pg_index AS index
            JOIN pg_class AS index_class ON index_class.oid = index.indexrelid
            WHERE index_class.relname = 'ix_payments_provider_object_leg' AND NOT index.indisvalid;
            """)).Should().Be(1);

        await migrator.MigrateAsync(CurrentMigration);
        (await ScalarAsync<long>(connection, """
            SELECT count(*)
            FROM pg_index AS index
            JOIN pg_class AS index_class ON index_class.oid = index.indexrelid
            WHERE index_class.relname IN (
                'ix_billing_webhook_events_provider_scope_event',
                'ix_billing_webhook_events_provider_object_leg',
                'ix_payments_provider_object_leg')
              AND index.indisvalid;
            """)).Should().Be(3);
        (await ScalarAsync<string>(connection, """
            SELECT pg_get_indexdef(index.indexrelid)
            FROM pg_index AS index
            JOIN pg_class AS index_class ON index_class.oid = index.indexrelid
            WHERE index_class.relname = 'ix_payments_provider_object_leg';
            """)).Should().Contain("\"Provider\", \"ProviderEnvironment\", \"ProviderAccountId\", \"ProviderObjectId\", \"ProviderMonetaryLeg\"");

        await migrator.MigrateAsync(CurrentMigration);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" IN ('{ExpandMigration}', '{CurrentMigration}');"))
            .Should().Be(2);

        await migrator.MigrateAsync(PreviousMigration);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM billing_webhook_events WHERE \"ExternalEventId\" = 'evt_legacy';"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM information_schema.columns WHERE table_name IN ('billing_webhook_events', 'payments') AND column_name = 'ProviderEnvironment';"))
            .Should().Be(0);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" IN ('{ExpandMigration}', '{CurrentMigration}');"))
            .Should().Be(0);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM pg_indexes WHERE indexname = 'ix_billing_webhook_events_external_id_provider';"))
            .Should().Be(1);

        await migrator.MigrateAsync(CurrentMigration);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM pg_indexes WHERE indexname IN ('ix_billing_webhook_events_provider_scope_event', 'ix_billing_webhook_events_provider_object_leg', 'ix_payments_provider_object_leg');"))
            .Should().Be(3);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" IN ('{ExpandMigration}', '{CurrentMigration}');"))
            .Should().Be(2);
    }

    private static ApplicationDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private sealed class ExposedMigration : ExpandProviderSecuritySchema
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
        public void BuildDown(MigrationBuilder builder) => Down(builder);
    }

    private sealed class ExposedIndexMigration : CreateProviderSecurityIndexesConcurrently
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
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
