using System.Text.Json;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Writer;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace GameGuild.API.UnitTests.Database;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class EconomyWriterParityPostgreSqlMigrationTests
{
    private const string SourceWallet = "91000000-0000-0000-0000-000000000001";
    private const string DestinationWallet = "91000000-0000-0000-0000-000000000002";

    [DockerFact]
    public async Task CurrentSchemaMatchesEveryRegisteredPostingTemplateAndRejectsUnauthorizedConversion()
    {
        await using var container = await EconomyPostgreSqlTestDatabase.CreateAsync("economy_writer_parity");

        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(container.ConnectionString)
                .Options);
        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(container.ConnectionString);
        await connection.OpenAsync();

        foreach (var registration in PostingTemplateCatalog.All)
        {
            var accepted = await ValidateAsync(connection, registration.Kind, CanonicalLines(registration.Kind));
            accepted.Should().BeTrue($"template {registration.Kind} is registered by the domain catalog");
        }

        var unauthorizedConversion = CanonicalLines(PostingTemplateKind.HardToSoftConversion);
        unauthorizedConversion[0]["account_code"] = (int)EconomyAccountCode.PlatformHardTreasury;
        (await ValidateAsync(connection, PostingTemplateKind.HardToSoftConversion, unauthorizedConversion))
            .Should().BeFalse();

        (await ScalarAsync<long>(connection, """
            SELECT count(*)
            FROM pg_tables
            WHERE schemaname = 'public'
              AND tablename LIKE 'economy_%'
              AND (has_table_privilege('gameguild_economy_runtime', format('%I.%I', schemaname, tablename), 'INSERT')
                OR has_table_privilege('gameguild_economy_runtime', format('%I.%I', schemaname, tablename), 'UPDATE')
                OR has_table_privilege('gameguild_economy_runtime', format('%I.%I', schemaname, tablename), 'DELETE')
                OR has_table_privilege('gameguild_economy_writer', format('%I.%I', schemaname, tablename), 'INSERT')
                OR has_table_privilege('gameguild_economy_writer', format('%I.%I', schemaname, tablename), 'UPDATE')
                OR has_table_privilege('gameguild_economy_writer', format('%I.%I', schemaname, tablename), 'DELETE'));
            """)).Should().Be(0);

        (await QueryStringsAsync(connection, """
            SELECT procedure.proname
            FROM pg_proc procedure
            JOIN pg_namespace schema ON schema.oid = procedure.pronamespace
            WHERE schema.nspname = 'economy_private'
              AND has_function_privilege('gameguild_economy_runtime', procedure.oid, 'EXECUTE')
            ORDER BY procedure.proname;
            """)).Should().Equal(
            "apply_economy_top_up_provider_event_v1",
            "bind_economy_top_up_provider_v1",
            "prepare_economy_top_up_intent_v1",
            "prepare_self_service_transfer_intent_v1",
            "read_economy_top_up_payment_fact_v1",
            "reserve_self_service_transfer_roots_v1");
    }

    [DockerFact]
    public async Task MigrationCanRollBackAndForwardWithoutLeavingWriterDependenciesBehind()
    {
        await using var container = await EconomyPostgreSqlTestDatabase.CreateAsync("economy_writer_rollback");

        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(container.ConnectionString)
                .Options);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync();

        await using var connection = new NpgsqlConnection(container.ConnectionString);
        await connection.OpenAsync();
        (await ValidateAsync(connection, PostingTemplateKind.ProviderConvertedSoftReversal,
            CanonicalLines(PostingTemplateKind.ProviderConvertedSoftReversal))).Should().BeTrue();

        await migrator.MigrateAsync("20260803005725_AddEconomyCapabilitySchemaRollup");

        (await ValidateAsync(connection, PostingTemplateKind.ProviderConvertedSoftReversal,
            CanonicalLines(PostingTemplateKind.ProviderConvertedSoftReversal))).Should().BeFalse();
        (await ValidateAsync(connection, PostingTemplateKind.HardToSoftConversion,
            CanonicalLines(PostingTemplateKind.HardToSoftConversion))).Should().BeTrue();

        var invalidLegacyConversion = CanonicalLines(PostingTemplateKind.HardToSoftConversion);
        invalidLegacyConversion[0]["account_code"] = (int)EconomyAccountCode.PlatformHardTreasury;
        (await ValidateAsync(connection, PostingTemplateKind.HardToSoftConversion, invalidLegacyConversion))
            .Should().BeFalse("the rollback must restore the exact legacy template matrix");

        (await ScalarAsync<long>(connection, """
            SELECT count(*)
            FROM pg_proc procedure
            JOIN pg_namespace schema ON schema.oid = procedure.pronamespace
            WHERE schema.nspname = 'economy_private'
              AND procedure.proname = 'line_matches_v2';
            """)).Should().Be(0);

        await migrator.MigrateAsync();
        (await ValidateAsync(connection, PostingTemplateKind.ProviderConvertedSoftReversal,
            CanonicalLines(PostingTemplateKind.ProviderConvertedSoftReversal))).Should().BeTrue();
    }

    [DockerFact]
    public async Task BountyReclaimValidatorAcceptsFeePairsAfterReturnPairs()
    {
        await using var container = await EconomyPostgreSqlTestDatabase.CreateAsync("economy_writer_bounty_fee_validation");

        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(container.ConnectionString)
                .Options);
        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(container.ConnectionString);
        await connection.OpenAsync();

        var lines = Enumerable.Range(0, 10)
            .SelectMany(index => new[]
            {
                Line(1, 9, 1, 1),
                index < 8
                    ? Line(2, 2, 1, 1, DestinationWallet, 1)
                    : Line(2, 14, 1, 1)
            })
            .ToList();

        (await ValidateAsync(connection, PostingTemplateKind.BountyReclaim, lines)).Should().BeTrue();
    }
    private static async Task<bool> ValidateAsync(
        NpgsqlConnection connection,
        PostingTemplateKind kind,
        IReadOnlyList<Dictionary<string, object?>> lines)
    {
        await using var command = new NpgsqlCommand(
            "SELECT economy_private.validate_posting_lines_v1(@kind, @lines);",
            connection);
        command.Parameters.AddWithValue("kind", (int)kind);
        command.Parameters.AddWithValue("lines", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(lines));
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static List<Dictionary<string, object?>> CanonicalLines(PostingTemplateKind kind) => kind switch
    {
        PostingTemplateKind.ConfirmedTopUpMint =>
        [
            Line(1, 1, 1, 2),
            Line(2, 2, 1, 2, DestinationWallet, 1)
        ],
        PostingTemplateKind.ProviderReversalFull or PostingTemplateKind.ProviderReversalPartial =>
        [
            Line(1, 2, 1, 2, SourceWallet, 1),
            Line(2, 1, 1, 2)
        ],
        PostingTemplateKind.Spend =>
        [
            Line(1, 2, 1, 2, SourceWallet, 1),
            Line(2, 2, 1, 2, DestinationWallet, 1)
        ],
        PostingTemplateKind.HardToSoftConversion =>
        [
            Line(1, 2, 1, 2, SourceWallet, 1),
            Line(2, 5, 1, 2),
            Line(1, 6, 2, 2_000),
            Line(2, 4, 2, 2_000, DestinationWallet, 3)
        ],
        PostingTemplateKind.SystemBackedGrant =>
        [
            Line(1, 7, 1, 2),
            Line(2, 5, 1, 2),
            Line(1, 6, 2, 2_000),
            Line(2, 4, 2, 2_000, DestinationWallet, 5)
        ],
        PostingTemplateKind.Burn =>
        [
            Line(1, 2, 1, 2, SourceWallet, 1),
            Line(2, 5, 1, 2)
        ],
        PostingTemplateKind.Escrow =>
        [
            Line(1, 2, 1, 2, SourceWallet, 1),
            Line(2, 9, 1, 2)
        ],
        PostingTemplateKind.BountyEscrow =>
        [
            Line(1, 2, 1, 2, SourceWallet, 1),
            Line(2, 9, 1, 2)
        ],
        PostingTemplateKind.BountyClaim =>
        [
            Line(1, 9, 1, 2),
            Line(2, 3, 1, 2, DestinationWallet, 2)
        ],
        PostingTemplateKind.BountyReclaim =>
        [
            Line(1, 9, 1, 2),
            Line(2, 2, 1, 2, DestinationWallet, 1)
        ],
        PostingTemplateKind.Reclaim =>
        [
            Line(1, 9, 1, 2),
            Line(2, 2, 1, 2, DestinationWallet, 7)
        ],
        PostingTemplateKind.Refund =>
        [
            Line(1, 2, 1, 2, SourceWallet, 1),
            Line(2, 2, 1, 2, DestinationWallet, 6)
        ],
        PostingTemplateKind.PayoutReservation =>
        [
            Line(1, 3, 1, 2, SourceWallet, 2),
            Line(2, 11, 1, 2)
        ],
        PostingTemplateKind.PayoutSuccess => [Line(1, 11, 1, 2), Line(2, 1, 1, 2)],
        PostingTemplateKind.PayoutFailure =>
        [
            Line(1, 11, 1, 2),
            Line(2, 3, 1, 2, DestinationWallet, 2)
        ],
        PostingTemplateKind.AdminWithdrawalReservation => [Line(1, 7, 1, 2), Line(2, 12, 1, 2)],
        PostingTemplateKind.AdminWithdrawalSuccess => [Line(1, 12, 1, 2), Line(2, 1, 1, 2)],
        PostingTemplateKind.AdminWithdrawalFailure => [Line(1, 12, 1, 2), Line(2, 7, 1, 2)],
        PostingTemplateKind.HardToSoftConversionFee =>
        [
            Line(1, 2, 1, 2, SourceWallet, 1),
            Line(2, 14, 1, 2)
        ],
        PostingTemplateKind.ProviderConvertedSoftReversal =>
        [
            Line(1, 4, 2, 2_000, SourceWallet, 3),
            Line(2, 6, 2, 2_000),
            Line(1, 5, 1, 2),
            Line(2, 1, 1, 2)
        ],
        PostingTemplateKind.ProviderReversalDebt => [Line(1, 13, 1, 2), Line(2, 1, 1, 2)],
        PostingTemplateKind.ProviderReversalLoss => [Line(1, 15, 1, 2), Line(2, 1, 1, 2)],
        PostingTemplateKind.AdRewardIssuance =>
        [
            Line(1, 6, 2, 2_000),
            Line(2, 4, 2, 2_000, DestinationWallet, 4)
        ],
        PostingTemplateKind.MarketplaceSettlement =>
        [
            Line(1, 2, 1, 2, SourceWallet, 1),
            Line(2, 3, 1, 1, DestinationWallet, 2),
            Line(2, 3, 1, 1, DestinationWallet, 2)
        ],
        PostingTemplateKind.MarketplaceRefund =>
        [
            Line(1, 13, 1, 2),
            Line(2, 2, 1, 2, DestinationWallet, 1)
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static Dictionary<string, object?> Line(
        int side,
        int account,
        int currency,
        long amount,
        string? wallet = null,
        int? provenance = null) => new()
    {
        ["id"] = Guid.NewGuid(),
        ["account_id"] = Guid.NewGuid(),
        ["account_code"] = account,
        ["wallet_id"] = wallet,
        ["side"] = side,
        ["currency"] = currency,
        ["amount_units"] = amount,
        ["provenance"] = provenance
    };

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<IReadOnlyList<string>> QueryStringsAsync(
        NpgsqlConnection connection,
        string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
            values.Add(reader.GetString(0));
        return values;
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
