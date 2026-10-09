using FluentAssertions;
using GameGuild.API.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace GameGuild.API.UnitTests.Database;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class EconomyProjectionRecoveryPostgreSqlMigrationTests
{
    private const string WalletId = "92000000-0000-0000-0000-000000000001";
    private static readonly DateTimeOffset AsOf = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    [DockerFact]
    public async Task RebuildDetectsRepairsAndAuditsCorruptProjectionWithoutGrantingDirectMutation()
    {
        await using var container = await EconomyPostgreSqlTestDatabase.CreateAsync("economy_projection_recovery");

        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(container.ConnectionString)
                .Options);
        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(container.ConnectionString);
        await connection.OpenAsync();
        await SeedImmutableFactsAsync(connection);

        var initial = await RebuildAsWriterAsync(connection);
        initial.WasCorrupt.Should().BeFalse();
        initial.ReviewState.Should().Be(1);
        await AssertProjectionAsync(connection, reviewState: 1);
        (await ScalarAsync<long>(connection,
            "SELECT count(*) FROM public.economy_projection_reconciliation_events;")).Should().Be(0);

        await ExecuteAsync(connection, $"""
            UPDATE public.economy_wallet_balance_projections
            SET "AvailableHardToSpend" = 999, "ProjectionHash" = 'corrupt'
            WHERE "WalletId" = '{WalletId}';
            """);

        var repaired = await RebuildAsWriterAsync(connection);
        repaired.WasCorrupt.Should().BeTrue();
        repaired.ReviewState.Should().Be(2);
        await AssertProjectionAsync(connection, reviewState: 2);
        (await ScalarAsync<long>(connection,
            "SELECT count(*) FROM public.economy_projection_reconciliation_events;")).Should().Be(1);
        (await ScalarAsync<string>(connection,
            "SELECT \"PreviousHash\" FROM public.economy_projection_reconciliation_events LIMIT 1;"))
            .Should().Be("corrupt");

        var immutableEvent = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            connection,
            "UPDATE public.economy_projection_reconciliation_events SET \"PreviousHash\" = 'rewritten';"));
        immutableEvent.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);

        await ExecuteAsRoleAsync(connection, "gameguild_economy_runtime",
            "SELECT count(*) FROM public.economy_projection_reconciliation_events;");

        var runtimeUpdate = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsRoleAsync(
            connection,
            "gameguild_economy_runtime",
            $"UPDATE public.economy_wallet_balance_projections SET \"AvailableHardToSpend\" = 1 WHERE \"WalletId\" = '{WalletId}';"));
        runtimeUpdate.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);

        var writerUpdate = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsRoleAsync(
            connection,
            "gameguild_economy_writer",
            $"UPDATE public.economy_wallet_balance_projections SET \"AvailableHardToSpend\" = 1 WHERE \"WalletId\" = '{WalletId}';"));
        writerUpdate.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);

        var runtimeRebuild = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsRoleAsync(
            connection,
            "gameguild_economy_runtime",
            $"SELECT * FROM economy_private.rebuild_wallet_projection_v1('{WalletId}', '{AsOf:O}');"));
        runtimeRebuild.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);

        var verified = await RebuildAsWriterAsync(connection);
        verified.WasCorrupt.Should().BeFalse();
        verified.ReviewState.Should().Be(1);
        await AssertProjectionAsync(connection, reviewState: 1);
        (await ScalarAsync<long>(connection,
            "SELECT count(*) FROM public.economy_projection_reconciliation_events;")).Should().Be(1);
    }

    [DockerFact]
    public async Task MigrationCanRollBackAndReapplyProjectionRecoveryWithoutResidualObjects()
    {
        await using var container = await EconomyPostgreSqlTestDatabase.CreateAsync("economy_projection_recovery_rollback");

        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(container.ConnectionString)
                .Options);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync();

        await using var connection = new NpgsqlConnection(container.ConnectionString);
        await connection.OpenAsync();
        (await ScalarAsync<string?>(connection,
            "SELECT to_regclass('public.economy_wallet_balance_projections')::text;"))
            .Should().Be("economy_wallet_balance_projections");
        (await ScalarAsync<string?>(connection,
            "SELECT to_regprocedure('economy_private.rebuild_wallet_projection_v1(uuid,timestamptz)')::text;"))
            .Should().NotBeNull();

        await migrator.MigrateAsync("20260803050213_HardenEconomyPostingWriter");

        (await ScalarAsync<string?>(connection,
            "SELECT to_regclass('public.economy_wallet_balance_projections')::text;"))
            .Should().BeNull();
        (await ScalarAsync<string?>(connection,
            "SELECT to_regprocedure('economy_private.rebuild_wallet_projection_v1(uuid,timestamptz)')::text;"))
            .Should().BeNull();

        await migrator.MigrateAsync();
        (await ScalarAsync<string?>(connection,
            "SELECT to_regclass('public.economy_wallet_balance_projections')::text;"))
            .Should().Be("economy_wallet_balance_projections");
        (await ScalarAsync<string?>(connection,
            "SELECT to_regprocedure('economy_private.rebuild_wallet_projection_v1(uuid,timestamptz)')::text;"))
            .Should().NotBeNull();
    }

    private static async Task SeedImmutableFactsAsync(NpgsqlConnection connection)
    {
        await ExecuteAsync(connection, $"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
            VALUES ('{WalletId}', '92000000-0000-0000-0000-000000000002',
                    '92000000-0000-0000-0000-000000000003', 1, '2026-01-01T00:00:00Z');

            INSERT INTO public.economy_source_stamps (
                "Id", "SourceKind", "InternalSourceId", "SourceLegId", "Provider", "ProviderReference",
                "EvidenceHash", "Provenance", "State", "ActorId", "TenantId", "PostingReferenceId",
                "PolicyVersion", "AuthoritativeUnits", "ObservedAt", "ConfirmedAt")
            VALUES
                ('92100000-0000-0000-0000-000000000001', 'provider', 'pending', 'principal', 'test', 'pending',
                 'pending-hash', 1, 1, '92000000-0000-0000-0000-000000000002',
                 '92000000-0000-0000-0000-000000000003', NULL, 1, 25, '2026-07-31T00:00:00Z', NULL),
                ('92100000-0000-0000-0000-000000000002', 'ledger', 'purchased', 'principal', NULL, NULL,
                 'purchased-hash', 1, 2, '92000000-0000-0000-0000-000000000002',
                 '92000000-0000-0000-0000-000000000003', NULL, 1, 100, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z'),
                ('92100000-0000-0000-0000-000000000003', 'ledger', 'mature-earned', 'principal', NULL, NULL,
                 'mature-earned-hash', 2, 2, '92000000-0000-0000-0000-000000000002',
                 '92000000-0000-0000-0000-000000000003', NULL, 1, 50, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z'),
                ('92100000-0000-0000-0000-000000000004', 'ledger', 'immature-earned', 'principal', NULL, NULL,
                 'immature-earned-hash', 2, 2, '92000000-0000-0000-0000-000000000002',
                 '92000000-0000-0000-0000-000000000003', NULL, 1, 30, '2026-07-15T00:00:00Z', '2026-07-15T00:00:00Z'),
                ('92100000-0000-0000-0000-000000000005', 'ledger', 'held-earned', 'principal', NULL, NULL,
                 'held-earned-hash', 2, 2, '92000000-0000-0000-0000-000000000002',
                 '92000000-0000-0000-0000-000000000003', NULL, 1, 20, '2026-01-02T00:00:00Z', '2026-01-02T00:00:00Z'),
                ('92100000-0000-0000-0000-000000000006', 'ledger', 'soft', 'principal', NULL, NULL,
                 'soft-hash', 4, 2, '92000000-0000-0000-0000-000000000002',
                 '92000000-0000-0000-0000-000000000003', NULL, 1, 100, '2026-07-01T00:00:00Z', '2026-07-01T00:00:00Z');

            INSERT INTO public.economy_funding_claims (
                "SourceStampId", "WalletId", "Provider", "Environment", "ConnectedAccount", "ProviderObject",
                "ProviderMonetaryLeg", "AuthoritativeUsdMinorUnits", "State", "ObservedAt", "ConfirmedAt",
                "StateChangedAt", "PostingGroupId", "RootCreditLotId", "CumulativeProviderReversalUnits", "Version")
            VALUES ('92100000-0000-0000-0000-000000000001', '{WalletId}', 'test', 'test', 'platform',
                    'pending-object', 'principal', 25, 1, '2026-07-31T00:00:00Z', NULL,
                    '2026-07-31T00:00:00Z', NULL, NULL, 0, 1);

            INSERT INTO public.economy_credit_lots (
                "Id", "WalletId", "RootSourceStampId", "Currency", "AmountUnits", "Provenance", "CreditedAt",
                "ConfirmedAt", "OriginalMaturesAt", "CashOutEligible", "JournalSequence", "State", "ReversalEpoch")
            VALUES
                ('92200000-0000-0000-0000-000000000001', '{WalletId}', '92100000-0000-0000-0000-000000000002',
                 1, 100, 1, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', false, 1, 1, 0),
                ('92200000-0000-0000-0000-000000000002', '{WalletId}', '92100000-0000-0000-0000-000000000003',
                 1, 50, 2, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', '2026-05-01T00:00:00Z', true, 2, 1, 0),
                ('92200000-0000-0000-0000-000000000003', '{WalletId}', '92100000-0000-0000-0000-000000000004',
                 1, 30, 2, '2026-07-15T00:00:00Z', '2026-07-15T00:00:00Z', '2026-11-12T00:00:00Z', true, 3, 1, 0),
                ('92200000-0000-0000-0000-000000000004', '{WalletId}', '92100000-0000-0000-0000-000000000005',
                 1, 20, 2, '2026-01-02T00:00:00Z', '2026-01-02T00:00:00Z', '2026-05-02T00:00:00Z', true, 4, 2, 0),
                ('92200000-0000-0000-0000-000000000005', '{WalletId}', '92100000-0000-0000-0000-000000000006',
                 2, 100, 4, '2026-07-01T00:00:00Z', '2026-07-01T00:00:00Z', '2026-07-01T00:00:00Z', false, 5, 1, 0);

            INSERT INTO public.economy_holds (
                "Id", "WalletId", "Currency", "AmountUnits", "Reason", "Status", "EffectiveAt", "ReleasedAt")
            VALUES ('92300000-0000-0000-0000-000000000001', '{WalletId}', 1, 5, 1, 1,
                    '2026-07-31T00:00:00Z', NULL);
            """);
    }

    private static async Task<(bool WasCorrupt, int ReviewState)> RebuildAsWriterAsync(NpgsqlConnection connection)
    {
        await ExecuteAsync(connection, "SET ROLE gameguild_economy_writer;");
        try
        {
            await using var command = new NpgsqlCommand(
                "SELECT was_corrupt, review_state FROM economy_private.rebuild_wallet_projection_v1(@wallet, @as_of);",
                connection);
            command.Parameters.AddWithValue("wallet", Guid.Parse(WalletId));
            command.Parameters.AddWithValue("as_of", AsOf);
            await using var reader = await command.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();
            return (reader.GetBoolean(0), reader.GetInt32(1));
        }
        finally
        {
            await ExecuteAsync(connection, "RESET ROLE;");
        }
    }

    private static async Task AssertProjectionAsync(NpgsqlConnection connection, int reviewState)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT "PendingHard", "PendingSoft", "PurchasedHard", "EarnedHard", "RestrictedHard", "Soft",
                   "ImmatureEarnedHard", "HeldHard", "HeldSoft", "AvailableHardToSpend",
                   "AvailableSoftToSpend", "WithdrawableHard", "ReviewState"
            FROM public.economy_wallet_balance_projections
            WHERE "WalletId" = '{WalletId}';
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetInt64(0).Should().Be(25);
        reader.GetInt64(1).Should().Be(0);
        reader.GetInt64(2).Should().Be(100);
        reader.GetInt64(3).Should().Be(100);
        reader.GetInt64(4).Should().Be(0);
        reader.GetInt64(5).Should().Be(100);
        reader.GetInt64(6).Should().Be(30);
        reader.GetInt64(7).Should().Be(25);
        reader.GetInt64(8).Should().Be(0);
        reader.GetInt64(9).Should().Be(175);
        reader.GetInt64(10).Should().Be(100);
        reader.GetInt64(11).Should().Be(45);
        reader.GetInt32(12).Should().Be(reviewState);
    }

    private static async Task ExecuteAsRoleAsync(NpgsqlConnection connection, string role, string sql)
    {
        await ExecuteAsync(connection, $"SET ROLE {role};");
        try
        {
            await ExecuteAsync(connection, sql);
        }
        finally
        {
            await ExecuteAsync(connection, "RESET ROLE;");
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull
            ? default!
            : (T)value;
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
