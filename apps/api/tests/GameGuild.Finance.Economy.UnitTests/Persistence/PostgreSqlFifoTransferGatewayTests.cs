using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.Database.Migrations;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;

namespace GameGuild.Finance.Economy.UnitTests.Persistence;

public sealed class PostgreSqlFifoTransferGatewayTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MigrationInstallsAnAtomicFifoTransferWriter()
    {
        var up = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedMigration().BuildUp(up);
        var sql = string.Join('\n', up.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));

        up.Operations.OfType<CreateTableOperation>()
            .Should().Contain(operation => operation.Name == "economy_fifo_transfer_operations");
        sql.Should().Contain("post_fifo_transfer_v1");
        sql.Should().Contain("pg_advisory_xact_lock");
        sql.Should().Contain("reserve_fifo_fragments_v1");
        sql.Should().Contain("transition_fifo_fragment_reservations_v1");
        sql.Should().Contain("economy_lot_lineage_edges");
        sql.Should().Contain("rebuild_wallet_projection_v1");
        sql.Should().Contain("economy_outbox_messages");
        sql.Should().Contain("SECURITY DEFINER");
    }

    [Fact]
    public async Task WriterConsumesFifoFragmentsAndPersistsTheWholeTransferAtomically()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("fifo_transfer");

        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(database.ConnectionString)
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options);
        await context.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var sourceWallet = Guid.NewGuid();
        var destinationWallet = Guid.NewGuid();
        var firstRoot = Guid.NewGuid();
        var secondRoot = Guid.NewGuid();
        var firstLot = Guid.NewGuid();
        var secondLot = Guid.NewGuid();
        var capability = Guid.NewGuid();
        var riskDecision = Guid.NewGuid();
        var counter = Guid.NewGuid();
        var posting = Guid.NewGuid();
        var actor = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var tenant = Guid.Parse("00000000-0000-0000-0000-000000000002");

        await SeedAsync(connection, sourceWallet, destinationWallet, firstRoot, secondRoot, firstLot, secondLot, capability, riskDecision, counter);
        await ExecuteAsync(connection, "SET ROLE gameguild_economy_writer;");
        await ScalarAsync<bool>(connection, $"""
            SELECT economy_private.reserve_risk_counter_v1(
                '{Guid.NewGuid()}', '{riskDecision}', '{counter}', 1, 15, '{Now:O}');
            """);

        await ExecuteAsync(connection, "RESET ROLE;");
        var gateway = new PostgreSqlFifoTransferGateway(context);
        var authority = new RegisteredPostingAuthority(
            capability,
            actor,
            tenant,
            riskDecision,
            "fifo-transfer-operation",
            1);
        var command = new TransferFragmentsCommand(
            new PostingId(posting),
            new IdempotencyKey("fifo-transfer-key"),
            new WalletId(sourceWallet),
            new WalletId(destinationWallet),
            new CoinAmount(CurrencyCode.HardCoin, 15),
            ProvenanceKind.PurchasedHard,
            new ReserveVersion(1),
            new PolicyVersion(1),
            Now);
        var receipt = gateway.Transfer(new PersistedFifoTransferRequest(command, authority));
        receipt.IsDuplicate.Should().BeFalse();
        receipt.PostingId.Value.Should().Be(posting);
        receipt.JournalSequence.Should().BePositive();

        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM public.economy_credit_lots WHERE \"WalletId\" = '{destinationWallet}';"))
            .Should().Be(2);
        (await ScalarAsync<long>(connection, $"SELECT COALESCE(sum(\"AmountUnits\")::bigint, 0) FROM public.economy_credit_lots WHERE \"WalletId\" = '{destinationWallet}';"))
            .Should().Be(15);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM public.economy_lot_lineage_edges;"))
            .Should().Be(2);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM public.economy_entry_allocations;"))
            .Should().Be(2);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM public.economy_fragment_reservations WHERE \"OperationId\" = '{posting}' AND \"Status\" = 3;"))
            .Should().Be(2);
        (await ScalarAsync<long>(connection, $"SELECT \"PurchasedHard\" FROM public.economy_wallet_balance_projections WHERE \"WalletId\" = '{sourceWallet}';"))
            .Should().Be(5);
        (await ScalarAsync<long>(connection, $"SELECT \"PurchasedHard\" FROM public.economy_wallet_balance_projections WHERE \"WalletId\" = '{destinationWallet}';"))
            .Should().Be(15);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM public.economy_outbox_messages;"))
            .Should().Be(1);
        var replay = gateway.Transfer(new PersistedFifoTransferRequest(command, authority));
        replay.IsDuplicate.Should().BeTrue();
        replay.JournalSequence.Should().Be(receipt.JournalSequence);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM public.economy_outbox_messages;"))
            .Should().Be(1);
        await ExecuteAsync(connection, "SET ROLE gameguild_economy_writer;");

        var directWrite = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, $"""
            INSERT INTO public.economy_fifo_transfer_operations (
                "Id", "IdempotencyKey", "RequestHash", "SourceWalletId", "DestinationWalletId", "Currency", "Provenance", "AmountUnits", "CreatedAt")
            VALUES ('{Guid.NewGuid()}', 'direct-write', 'forbidden', '{sourceWallet}', '{destinationWallet}', 1, 1, 1, '{Now:O}');
            """));
        directWrite.SqlState.Should().Be("42501");
    }

    private static async Task SeedAsync(
        NpgsqlConnection connection,
        Guid sourceWallet,
        Guid destinationWallet,
        Guid firstRoot,
        Guid secondRoot,
        Guid firstLot,
        Guid secondLot,
        Guid capability,
        Guid riskDecision,
        Guid counter)
    {
        await ExecuteAsync(connection, $"""
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt") VALUES
                ('{sourceWallet}', '{Guid.NewGuid()}', '{Guid.NewGuid()}', 1, '{Now.AddMinutes(-3):O}'),
                ('{destinationWallet}', '{Guid.NewGuid()}', '{Guid.NewGuid()}', 1, '{Now.AddMinutes(-3):O}');
            INSERT INTO public.economy_accounts ("Id", "WalletId", "Code", "Currency", "Provenance", "CreatedAt") VALUES
                ('{Guid.NewGuid()}', '{sourceWallet}', 2, 1, 1, '{Now.AddMinutes(-3):O}'),
                ('{Guid.NewGuid()}', '{destinationWallet}', 2, 1, 1, '{Now.AddMinutes(-3):O}');
            INSERT INTO public.economy_registered_capabilities ("Id", "Name", "AllowedTemplateKinds", "IsEnabled", "CreatedAt", "RevokedAt")
            VALUES ('{capability}', 'fifo-transfer-test-{capability:N}', '[4]'::jsonb, true, '{Now.AddMinutes(-3):O}', NULL);
            INSERT INTO public.economy_risk_counters (
                "Id", "Dimension", "SubjectHash", "Operation", "Currency", "WindowStartedAt", "WindowEndsAt",
                "CounterVersion", "MaxUnits", "UsedUnits", "UpdatedAt")
            VALUES ('{counter}', 1, 'fifo-source', 1, 1, '{Now.AddHours(-1):O}', '{Now.AddHours(1):O}', 1, 100, 0, '{Now.AddHours(-1):O}');
            INSERT INTO public.economy_risk_decisions (
                "Id", "Outcome", "OperationFingerprint", "ActorHash", "TemplateKind", "SourceWalletId", "DestinationWalletId",
                "Currency", "AmountUnits", "CurrencyLegs", "SourceRoots", "ProviderReferenceHash", "PolicyVersion",
                "ReserveVersion", "ReserveAuthorizationEpoch", "FeatureVersion", "KillSwitchEpoch", "CounterVersion", "EntityGraphVersion",
                "EntityGraphEvidenceHash", "ReasonCodes", "IssuedAt", "ExpiresAt")
            VALUES ('{riskDecision}', 1, 'fifo-transfer-operation', 'actor-hash', 4, '{sourceWallet}', '{destinationWallet}',
                1, 15, '[]', '[]', 'provider-hash', 1, 1, 1, 1, 0, 1, 0, 'graph-hash', '[]', '{Now.AddMinutes(-1):O}', '{Now.AddMinutes(5):O}');
            """);

        await SeedLotAsync(connection, sourceWallet, firstRoot, firstLot, 10, Now.AddMinutes(-2), 1);
        await SeedLotAsync(connection, sourceWallet, secondRoot, secondLot, 10, Now.AddMinutes(-1), 2);
    }

    private static Task SeedLotAsync(NpgsqlConnection connection, Guid walletId, Guid rootId, Guid lotId, long amount, DateTimeOffset confirmedAt, long sequence) =>
        ExecuteAsync(connection, $"""
            INSERT INTO public.economy_source_stamps (
                "Id", "SourceKind", "InternalSourceId", "SourceLegId", "Provider", "ProviderReference", "EvidenceHash",
                "Provenance", "State", "ActorId", "TenantId", "PostingReferenceId", "PolicyVersion", "AuthoritativeUnits", "ObservedAt", "ConfirmedAt")
            VALUES ('{rootId}', 'test', '{rootId:N}', 'leg', NULL, NULL, 'evidence-{rootId:N}', 1, 2,
                '{Guid.NewGuid()}', '{Guid.NewGuid()}', NULL, 1, {amount}, '{confirmedAt:O}', '{confirmedAt:O}');
            INSERT INTO public.economy_credit_lots (
                "Id", "WalletId", "RootSourceStampId", "Currency", "AmountUnits", "Provenance", "CreditedAt", "ConfirmedAt",
                "OriginalMaturesAt", "CashOutEligible", "JournalSequence", "State", "ReversalEpoch")
            VALUES ('{lotId}', '{walletId}', '{rootId}', 1, {amount}, 1, '{confirmedAt:O}', '{confirmedAt:O}',
                '{confirmedAt:O}', false, {sequence}, 1, 0);
            INSERT INTO public.economy_root_reversal_states (
                "RootSourceStampId", "Epoch", "CumulativeProviderUnits", "ReversedUnits", "State", "TargetedRanges", "UpdatedAt")
            VALUES ('{rootId}', 0, 0, 0, 'active', '[]'::jsonb, '{confirmedAt:O}');
            INSERT INTO public.economy_fragment_root_ranges (
                "Id", "RootSourceStampId", "CreditLotId", "EntryAllocationId", "StartInclusive", "EndExclusive", "ReversalEpoch")
            VALUES ('{Guid.NewGuid()}', '{rootId}', '{lotId}', NULL, 0, {amount * 1000}, 0);
            """);

    private static async Task<PostingReceipt> PostAsync(
        NpgsqlConnection connection,
        Guid capability,
        Guid riskDecision,
        Guid posting,
        Guid sourceWallet,
        Guid destinationWallet)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT * FROM economy_private.post_fifo_transfer_v1(
                '{capability}', '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000002', '{posting}', 'fifo-transfer-key', 1, 1,
                '{riskDecision}', 'fifo-transfer-operation', 1, '{sourceWallet}', '{destinationWallet}', 1, 1, 15,
                '{Now:O}', NULL);
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        return new PostingReceipt(reader.GetGuid(0), reader.GetInt64(1), reader.GetBoolean(3));
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
        return value is null or DBNull ? default! : (T)value;
    }

    private sealed record PostingReceipt(Guid PostingId, long Sequence, bool Duplicate);

    private sealed class ExposedMigration : AddEconomyFifoTransferWriter
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
    }
}
