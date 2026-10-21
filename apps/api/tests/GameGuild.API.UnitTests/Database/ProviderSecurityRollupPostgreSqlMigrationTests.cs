using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.Database.Migrations;
using GameGuild.Commerce.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace GameGuild.API.UnitTests.Database;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class ProviderSecurityRollupPostgreSqlMigrationTests
{
    private const string PreviousMigration = "20260717112100_CreateProviderSecurityIndexesConcurrently";
    private const string RollupMigration = "20260718171325_RollupProviderSecurityConstraints";

    [Fact]
    public void Migration_Is_Surgical_Retry_Safe_And_Leaves_Destructive_Contract_Cleanup_For_Later()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedMigration().BuildUp(builder);

        builder.Operations.Should().OnlyContain(operation => operation is SqlOperation);
        var operations = builder.Operations.Cast<SqlOperation>().ToList();
        operations.Should().HaveCount(14);
        operations.Count(operation => operation.SuppressTransaction).Should().Be(9);

        var sql = string.Join('\n', operations.Select(operation => operation.Sql));
        sql.Should().Contain("num_nonnulls");
        sql.Should().Contain("ck_payments_provider_mapping_complete");
        sql.Should().Contain("ck_payments_stripe_value_mapping_required");
        sql.Should().Contain("ck_billing_webhook_events_provider_scope_complete");
        sql.Should().Contain("VALIDATE CONSTRAINT");
        sql.Should().Contain("SELECT 1 FROM pg_constraint");
        sql.Should().Contain("ix_billing_webhook_events_external_id_provider__rollup");
        sql.Should().Contain("ix_billing_webhook_events_provider_scope_event__rollup");
        sql.Should().Contain("ix_payments_provider_object_leg__rollup");
        sql.Should().Contain("value-relevant Stripe payments require verified provider mapping reconciliation");
        sql.Should().NotContain("DROP TABLE");
        sql.Should().NotContain("DROP COLUMN");
        sql.Should().NotContain("ALTER COLUMN");
    }

    [DockerFact]
    public async Task Up_Down_Current_Duplicates_And_Partial_Mappings_Are_Enforced_On_PostgreSql()
    {
        await using var container = await EconomyPostgreSqlTestDatabase.CreateAsync("provider_security_rollup");

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
                ('10000000-0000-0000-0000-000000000001', 'paypal', 'evt_legacy', 'payment.succeeded', '{}',
                 false, false, 0, 1, now(), now());

            INSERT INTO billing_webhook_events
                ("Id", "Provider", "ExternalEventId", "ProviderEnvironment", "ProviderAccountId",
                 "WebhookEndpointId", "ProviderObjectId", "ProviderObjectType", "ProviderMonetaryLeg",
                 "IsLiveMode", "EventType", "Payload", "IsProcessed", "IsFailed", "ProcessingAttempts",
                 "Version", "CreatedAt", "UpdatedAt")
            VALUES
                ('10000000-0000-0000-0000-000000000002', 'stripe', 'evt_scoped', 'LIVE', 'acct_one',
                 'we_one', 'pi_one', 'payment_intent', 'capture', true, 'payment.succeeded', '{}',
                 false, false, 0, 1, now(), now());

            INSERT INTO payments
                ("Id", "TenantId", "Amount", "Currency", "Status", "Provider", "ExternalPaymentId",
                 "IdempotencyKey", "RetryCount", "MaxRetries", "RefundedAmount", "Version", "CreatedAt", "UpdatedAt")
            VALUES
                ('20000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001', 10, 'USD', 0,
                 'paypal', 'legacy_payment', 'legacy-payment', 0, 3, 0, 1, now(), now());

            INSERT INTO payments
                ("Id", "TenantId", "Amount", "Currency", "Status", "Provider", "ExternalPaymentId",
                 "ProviderEnvironment", "ProviderAccountId", "ProviderObjectId", "ProviderObjectType",
                 "ProviderMonetaryLeg", "IdempotencyKey", "RetryCount", "MaxRetries", "RefundedAmount",
                 "Version", "CreatedAt", "UpdatedAt")
            VALUES
                ('20000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000001', 20, 'USD', 0,
                 'stripe', 'pi_one', 'TEST', 'acct_one', 'pi_one', 'payment_intent',
                 'capture', 'mapped-payment', 0, 3, 0, 1, now(), now());
            """);

        await migrator.MigrateAsync(RollupMigration);

        (await ScalarAsync<long>(connection, """
            SELECT count(*)
            FROM pg_constraint
            WHERE conname IN (
                'ck_payments_provider_mapping_complete',
                'ck_payments_provider_environment',
                'ck_payments_stripe_value_mapping_required',
                'ck_billing_webhook_events_provider_scope_complete',
                'ck_billing_webhook_events_provider_environment',
                'ck_billing_webhook_events_provider_object_complete')
              AND convalidated;
            """)).Should().Be(6);
        (await ScalarAsync<long>(connection, """
            SELECT count(*)
            FROM pg_index AS i
            JOIN pg_class AS c ON c.oid = i.indexrelid
            WHERE c.relname IN (
                'ix_billing_webhook_events_external_id_provider',
                'ix_billing_webhook_events_provider_scope_event',
                'ix_payments_provider_object_leg')
              AND i.indisunique
              AND i.indisvalid;
            """)).Should().Be(3);
        (await ScalarAsync<string>(connection, """
            SELECT "ProviderEnvironment"
            FROM billing_webhook_events
            WHERE "ExternalEventId" = 'evt_scoped';
            """)).Should().Be("live");
        (await ScalarAsync<string>(connection, """
            SELECT "ProviderEnvironment"
            FROM payments
            WHERE "IdempotencyKey" = 'mapped-payment';
            """)).Should().Be("test");

        await ExecuteAsync(connection, """
            INSERT INTO billing_webhook_events
                ("Id", "Provider", "ExternalEventId", "ProviderEnvironment", "ProviderAccountId",
                 "WebhookEndpointId", "IsLiveMode", "EventType", "Payload", "IsProcessed", "IsFailed",
                 "ProcessingAttempts", "Version", "CreatedAt", "UpdatedAt")
            VALUES
                ('10000000-0000-0000-0000-000000000006', 'stripe', 'evt_scoped', 'live', 'acct_two',
                 'we_two', true, 'payment.succeeded', '{}', false, false, 0, 1, now(), now());
            """);

        var duplicateEvent = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            INSERT INTO billing_webhook_events
                ("Id", "Provider", "ExternalEventId", "ProviderEnvironment", "ProviderAccountId",
                 "WebhookEndpointId", "IsLiveMode", "EventType", "Payload", "IsProcessed", "IsFailed",
                 "ProcessingAttempts", "Version", "CreatedAt", "UpdatedAt")
            VALUES
                ('10000000-0000-0000-0000-000000000003', 'stripe', 'evt_scoped', 'live', 'acct_one',
                 'we_one', true, 'payment.succeeded', '{}', false, false, 0, 1, now(), now());
            """));
        duplicateEvent.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        duplicateEvent.ConstraintName.Should().Be("ix_billing_webhook_events_provider_scope_event");

        var duplicatePayment = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            INSERT INTO payments
                ("Id", "TenantId", "Amount", "Currency", "Status", "Provider", "ExternalPaymentId",
                 "ProviderEnvironment", "ProviderAccountId", "ProviderObjectId", "ProviderObjectType",
                 "ProviderMonetaryLeg", "IdempotencyKey", "RetryCount", "MaxRetries", "RefundedAmount",
                 "Version", "CreatedAt", "UpdatedAt")
            VALUES
                ('20000000-0000-0000-0000-000000000003', '30000000-0000-0000-0000-000000000001', 20, 'USD', 0,
                 'stripe', 'pi_duplicate', 'test', 'acct_one', 'pi_one', 'payment_intent',
                 'capture', 'duplicate-mapping', 0, 3, 0, 1, now(), now());
            """));
        duplicatePayment.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        duplicatePayment.ConstraintName.Should().Be("ix_payments_provider_object_leg");

        var partialMapping = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            INSERT INTO payments
                ("Id", "TenantId", "Amount", "Currency", "Status", "Provider", "ExternalPaymentId",
                 "ProviderEnvironment", "IdempotencyKey", "RetryCount", "MaxRetries", "RefundedAmount",
                 "Version", "CreatedAt", "UpdatedAt")
            VALUES
                ('20000000-0000-0000-0000-000000000004', '30000000-0000-0000-0000-000000000001', 5, 'USD', 0,
                 'stripe', 'pi_partial', 'test', 'partial-mapping', 0, 3, 0, 1, now(), now());
            """));
        partialMapping.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        partialMapping.ConstraintName.Should().Be("ck_payments_provider_mapping_complete");

        await ExecuteAsync(connection, """
            INSERT INTO payments
                ("Id", "TenantId", "Amount", "Currency", "Status", "Provider", "ExternalPaymentId",
                 "IdempotencyKey", "RetryCount", "MaxRetries", "RefundedAmount", "Version", "CreatedAt", "UpdatedAt")
            VALUES
                ('20000000-0000-0000-0000-000000000005', '30000000-0000-0000-0000-000000000001', 7, 'USD', 0,
                 'paypal', 'paypal_unmapped', 'paypal-unmapped', 0, 3, 0, 1, now(), now());
            """);

        (await ScalarAsync<long>(connection, "SELECT count(*) FROM billing_webhook_events;")).Should().Be(3);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM payments;")).Should().Be(3);

        await ExecuteAsync(connection, """
            DELETE FROM billing_webhook_events
            WHERE "Id" = '10000000-0000-0000-0000-000000000006';
            """);

        await migrator.MigrateAsync(PreviousMigration);
        (await ScalarAsync<long>(connection, """
            SELECT count(*)
            FROM pg_constraint
            WHERE conname LIKE 'ck_payments_provider_%'
               OR conname LIKE 'ck_billing_webhook_events_provider_%';
            """)).Should().Be(0);
        (await ScalarAsync<long>(connection, """
            SELECT count(*)
            FROM pg_index AS i
            JOIN pg_class AS c ON c.oid = i.indexrelid
            WHERE c.relname IN (
                'ix_billing_webhook_events_provider_scope_event',
                'ix_payments_provider_object_leg')
              AND NOT i.indisunique
              AND i.indisvalid;
            """)).Should().Be(2);

        await ExecuteAsync(connection, """
            INSERT INTO payments
                ("Id", "TenantId", "Amount", "Currency", "Status", "Provider", "ExternalPaymentId",
                 "IdempotencyKey", "RetryCount", "MaxRetries", "RefundedAmount", "Version", "CreatedAt", "UpdatedAt")
            VALUES
                ('20000000-0000-0000-0000-000000000006', '30000000-0000-0000-0000-000000000001', 12, 'USD', 2,
                 'stripe', 'pi_unverified', 'unverified-value-payment', 0, 3, 0, 1, now(), now());
            """);
        var unmappedValuePayment = await Assert.ThrowsAsync<PostgresException>(
            () => migrator.MigrateAsync(RollupMigration));
        unmappedValuePayment.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        unmappedValuePayment.MessageText.Should().Contain("verified provider mapping reconciliation");
        await ExecuteAsync(connection, """
            DELETE FROM payments WHERE "IdempotencyKey" = 'unverified-value-payment';
            """);

        await ExecuteAsync(connection, """
            ALTER TABLE payments
                ADD CONSTRAINT ck_payments_provider_environment
                CHECK ("ProviderEnvironment" IS NULL OR "ProviderEnvironment" IN ('test', 'live')) NOT VALID;
            """);
        await ExecuteAsync(connection, """
            CREATE UNIQUE INDEX CONCURRENTLY ix_payments_provider_object_leg__rollup
                ON payments
                ("Provider", "ProviderEnvironment", "ProviderAccountId", "ProviderObjectId", "ProviderMonetaryLeg")
                WHERE "ProviderEnvironment" IS NOT NULL
                  AND "ProviderAccountId" IS NOT NULL
                  AND "ProviderObjectId" IS NOT NULL
                  AND "ProviderMonetaryLeg" IS NOT NULL;
            """);

        await migrator.MigrateAsync(RollupMigration);
        await migrator.MigrateAsync(RollupMigration);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{RollupMigration}';"))
            .Should().Be(1);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM payments;")).Should().Be(3);
    }

    [DockerFact]
    public async Task Atomic_Webhook_Claim_Uses_Current_Schema_On_PostgreSql()
    {
        // The repository uses the current model, including fields added after the rollup.
        // Keep its concurrency check on the current schema and the historical migration
        // up/down/partial-state assertions above on their original migration boundary.
        await using var container = await EconomyPostgreSqlTestDatabase.CreateAsync("provider_current_claim");
        var connectionString = container.ConnectionString;
        await using (var seedContext = CreateContext(connectionString))
        {
            await seedContext.Database.MigrateAsync();
            seedContext.Set<BillingWebhookEvent>().Add(new BillingWebhookEvent
            {
                Provider = "stripe",
                ProviderEnvironment = "live",
                ProviderAccountId = "acct_one",
                WebhookEndpointId = "we_one",
                ExternalEventId = "evt_scoped",
                IsLiveMode = true,
                EventType = "payment.succeeded",
                Payload = "{}"
            });
            await seedContext.SaveChangesAsync();
        }

        await using var firstContext = CreateContext(connectionString);
        await using var secondContext = CreateContext(connectionString);
        var firstRepository = new BillingWebhookRepository(
            firstContext,
            NullLogger<BillingWebhookRepository>.Instance);
        var secondRepository = new BillingWebhookRepository(
            secondContext,
            NullLogger<BillingWebhookRepository>.Instance);
        var firstCandidate = await firstRepository.GetByProviderScopeAsync(
            "stripe", "live", "acct_one", "we_one", "evt_scoped");
        var secondCandidate = await secondRepository.GetByProviderScopeAsync(
            "stripe", "live", "acct_one", "we_one", "evt_scoped");

        firstCandidate.Should().NotBeNull();
        secondCandidate.Should().NotBeNull();
        (await firstRepository.TryClaimProcessingAsync(
            firstCandidate!,
            DateTime.UtcNow.AddMinutes(-1))).Should().BeTrue();
        (await secondRepository.TryClaimProcessingAsync(
            secondCandidate!,
            DateTime.UtcNow.AddMinutes(-1))).Should().BeFalse();

        await using var verificationContext = CreateContext(connectionString);
        var persisted = await verificationContext.Set<BillingWebhookEvent>().AsNoTracking().SingleAsync();
        persisted.ProcessingAttempts.Should().Be(1);
        persisted.IsProcessed.Should().BeFalse();
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

    private sealed class ExposedMigration : RollupProviderSecurityConstraints
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
    }

    private sealed class DockerFactAttribute : FactAttribute
    {
        public DockerFactAttribute()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("SKIP_DOCKER_TESTS"), "1", StringComparison.Ordinal))
                Skip = "Docker tests disabled by SKIP_DOCKER_TESTS=1.";
        }
    }
}
