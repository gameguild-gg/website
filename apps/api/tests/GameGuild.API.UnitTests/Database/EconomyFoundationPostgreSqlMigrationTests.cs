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
public sealed class EconomyFoundationPostgreSqlMigrationTests
{
    private static readonly string[] EconomyRoles =
    [
        "gameguild_economy_migration",
        "gameguild_economy_runtime",
        "gameguild_economy_writer",
        "gameguild_economy_procedure_owner"
    ];

    [Fact]
    public void Migration_Is_Limited_To_The_Economy_Foundation_And_Private_Writer_Contracts()
    {
        var up = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var down = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var migration = new ExposedMigration();
        migration.BuildUp(up);
        migration.BuildDown(down);

        var createdTables = up.Operations
            .OfType<CreateTableOperation>()
            .Select(operation => operation.Name)
            .ToArray();
        createdTables.Should().HaveCount(29);
        createdTables.Should().OnlyContain(name => name.StartsWith("economy_", StringComparison.Ordinal));

        down.Operations.OfType<DropTableOperation>()
            .Select(operation => operation.Name)
            .Should().BeEquivalentTo(createdTables);

        var sql = string.Join('\n', up.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
        var downSql = string.Join('\n', down.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
        sql.Should().Contain("SECURITY DEFINER");
        sql.Should().Contain("SET search_path = pg_catalog, economy_private");
        sql.Should().Contain("reserve_risk_counter_v1");
        sql.Should().Contain("post_registered_posting_v1");
        sql.Should().Contain("deny_immutable_mutation_v1");
        sql.Should().NotContain("GRANT SELECT ON ALL TABLES IN SCHEMA public");
        sql.Should().NotContain("GRANT ALL ON ALL TABLES IN SCHEMA public");
        downSql.Should().Contain("DROP OWNED BY gameguild_economy_runtime");
        downSql.Should().NotContain("DROP ROLE");
    }

    [DockerFact]
    public async Task Up_Current_Down_Enforces_Roles_Immutability_And_Atomic_Risk_Counter_Reservations()
    {
        await using var container = await EconomyPostgreSqlTestDatabase.CreateAsync("economy_foundation");

        await using var connection = new NpgsqlConnection(container.ConnectionString);
        await connection.OpenAsync();
        await ApplyUpAsync(connection);

        (await ScalarAsync<long>(connection, "SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tablename LIKE 'economy_%';"))
            .Should().Be(29);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM pg_roles WHERE rolname = ANY (ARRAY[{string.Join(',', EconomyRoles.Select(role => $"'{role}'"))}]);"))
            .Should().Be(EconomyRoles.Length);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'economy_private';"))
            .Should().Be(13);
        (await ScalarAsync<bool>(connection, "SELECT has_table_privilege('gameguild_economy_runtime', 'public.economy_wallets', 'SELECT');"))
            .Should().BeTrue();
        (await ScalarAsync<bool>(connection, "SELECT has_table_privilege('gameguild_economy_writer', 'public.economy_wallets', 'INSERT');"))
            .Should().BeFalse();
        (await ScalarAsync<bool>(connection, "SELECT has_function_privilege('gameguild_economy_writer', 'economy_private.reserve_risk_counter_v1(uuid,uuid,uuid,bigint,bigint,timestamptz)', 'EXECUTE');"))
            .Should().BeTrue();

        var runtimeInsert = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            SET ROLE gameguild_economy_runtime;
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
            VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), 1, now());
            """));
        runtimeInsert.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);
        await ExecuteAsync(connection, "RESET ROLE;");

        await SeedRiskCounterScenarioAsync(connection);
        var reservationResults = await Task.WhenAll(
            ReserveCounterAsync(container.ConnectionString,
                "81000000-0000-0000-0000-000000000001", "71000000-0000-0000-0000-000000000001"),
            ReserveCounterAsync(container.ConnectionString,
                "81000000-0000-0000-0000-000000000002", "71000000-0000-0000-0000-000000000002"));

        reservationResults.Count(result => result.Reserved).Should().Be(1);
        reservationResults.Count(result => result.SqlState == PostgresErrorCodes.NumericValueOutOfRange).Should().Be(1);
        (await ScalarAsync<long>(connection, "SELECT \"UsedUnits\" FROM public.economy_risk_counters WHERE \"Id\" = '61000000-0000-0000-0000-000000000001';"))
            .Should().Be(60);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM public.economy_risk_counter_reservations;"))
            .Should().Be(1);

        var mutation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            UPDATE public.economy_risk_counter_reservations SET "AmountUnits" = 1;
            """));
        mutation.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);

        await ApplyDownAsync(connection);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tablename LIKE 'economy_%';"))
            .Should().Be(0);
        (await ScalarAsync<long>(connection, $"SELECT count(*) FROM pg_roles WHERE rolname = ANY (ARRAY[{string.Join(',', EconomyRoles.Select(role => $"'{role}'"))}]);"))
            .Should().Be(EconomyRoles.Length);

        await ApplyUpAsync(connection);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tablename LIKE 'economy_%';"))
            .Should().Be(29);
    }

    [DockerFact]
    public async Task Registered_Writer_Enforces_Capability_Risk_Idempotency_Shape_And_Concurrent_Chain_Order()
    {
        await using var container = await EconomyPostgreSqlTestDatabase.CreateAsync("economy_writer");

        var connectionString = container.ConnectionString;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ApplyUpAsync(connection);
        await SeedWriterScenarioAsync(connection);

        foreach (var reservation in new[]
                 {
                     ("a1000000-0000-0000-0000-000000000001", "99000000-0000-0000-0000-000000000001", 50L),
                     ("a1000000-0000-0000-0000-000000000003", "99000000-0000-0000-0000-000000000003", 50L),
                     ("a1000000-0000-0000-0000-000000000004", "99000000-0000-0000-0000-000000000004", 50L),
                     ("a1000000-0000-0000-0000-000000000005", "99000000-0000-0000-0000-000000000005", 40L),
                     ("a1000000-0000-0000-0000-000000000006", "99000000-0000-0000-0000-000000000006", 40L)
                 })
        {
            await ReserveAsWriterAsync(connection, reservation.Item1, reservation.Item2, reservation.Item3);
        }

        await ExecuteAsync(connection, """
            INSERT INTO public.economy_risk_counter_reservations
                ("Id", "RiskDecisionId", "RiskCounterId", "AmountUnits", "ReservedAt")
            VALUES
                ('a1000000-0000-0000-0000-000000000002', '99000000-0000-0000-0000-000000000002',
                 '98000000-0000-0000-0000-000000000001', 50, '2026-07-18T00:30:00Z');
            """);

        var validLines = SpendLines(50);
        var accepted = await ExecutePostingAsync(
            connectionString,
            "97000000-0000-0000-0000-000000000001",
            "spend-one",
            "99000000-0000-0000-0000-000000000001",
            "operation-valid",
            validLines);
        accepted.Sequence.Should().Be(1);
        accepted.Duplicate.Should().BeFalse();

        var replay = await ExecutePostingAsync(
            connectionString,
            "97000000-0000-0000-0000-000000000001",
            "spend-one",
            "99000000-0000-0000-0000-000000000001",
            "operation-valid",
            validLines);
        replay.Should().Be(accepted with { Duplicate = true });

        var mutatedReplay = await Assert.ThrowsAsync<PostgresException>(() => ExecutePostingAsync(
            connectionString,
            "97000000-0000-0000-0000-000000000001",
            "spend-one",
            "99000000-0000-0000-0000-000000000001",
            "operation-valid",
            SpendLines(49)));
        mutatedReplay.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);

        var staleDecision = await Assert.ThrowsAsync<PostgresException>(() => ExecutePostingAsync(
            connectionString,
            "97000000-0000-0000-0000-000000000002",
            "spend-stale",
            "99000000-0000-0000-0000-000000000002",
            "operation-stale",
            validLines,
            requestedAt: "2026-07-18T02:00:00Z"));
        staleDecision.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);

        var absentCapability = await Assert.ThrowsAsync<PostgresException>(() => ExecutePostingAsync(
            connectionString,
            "97000000-0000-0000-0000-000000000003",
            "spend-unauthorized",
            "99000000-0000-0000-0000-000000000003",
            "operation-unauthorized",
            validLines,
            capabilityId: "93000000-0000-0000-0000-000000000099"));
        absentCapability.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);

        var forbiddenShape = await Assert.ThrowsAsync<PostgresException>(() => ExecutePostingAsync(
            connectionString,
            "97000000-0000-0000-0000-000000000004",
            "spend-shape",
            "99000000-0000-0000-0000-000000000004",
            "operation-shape",
            UnauthorizedSpendLines(50)));
        forbiddenShape.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);

        var concurrent = await Task.WhenAll(
            ExecutePostingAsync(connectionString,
                "97000000-0000-0000-0000-000000000005", "spend-concurrent-one",
                "99000000-0000-0000-0000-000000000005", "operation-concurrent-one", SpendLines(40, 1)),
            ExecutePostingAsync(connectionString,
                "97000000-0000-0000-0000-000000000006", "spend-concurrent-two",
                "99000000-0000-0000-0000-000000000006", "operation-concurrent-two", SpendLines(40, 2)));
        concurrent.Select(result => result.Sequence).Should().BeEquivalentTo([2L, 3L]);

        (await ScalarAsync<long>(connection, "SELECT \"Sequence\" FROM public.economy_chain_head WHERE \"Id\" = 1;"))
            .Should().Be(3);
        (await ScalarAsync<long>(connection, """
            SELECT count(*)
            FROM public.economy_journal_entries current_entry
            LEFT JOIN public.economy_journal_entries previous_entry
                ON previous_entry."Sequence" = current_entry."Sequence" - 1
            WHERE current_entry."Sequence" > 1
              AND current_entry."PreviousHash" <> previous_entry."Hash";
            """)).Should().Be(0);
        (await ScalarAsync<long>(connection, """
            SELECT abs(
                sum(CASE WHEN "Side" = 1 THEN "AmountUnits" ELSE 0 END) -
                sum(CASE WHEN "Side" = 2 THEN "AmountUnits" ELSE 0 END))::bigint
            FROM public.economy_journal_lines;
            """)).Should().Be(0);
        (await ScalarAsync<long>(connection, "SELECT count(*) FROM public.economy_posting_groups;"))
            .Should().Be(3);
    }

    [DockerFact]
    public async Task TopUp_Writer_Rejects_Absent_Mutated_Unconfirmed_Future_Reused_And_Overcredited_Sources()
    {
        await using var container = await EconomyPostgreSqlTestDatabase.CreateAsync("economy_top_up_writer");

        var connectionString = container.ConnectionString;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ApplyUpAsync(connection);
        await SeedTopUpWriterScenarioAsync(connection);

        var negativeAuthority = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            INSERT INTO public.economy_source_stamps (
                "Id", "SourceKind", "InternalSourceId", "SourceLegId", "EvidenceHash", "Provenance", "State",
                "ActorId", "TenantId", "PolicyVersion", "AuthoritativeUnits", "ObservedAt", "ConfirmedAt")
            VALUES ('c3000000-0000-0000-0000-000000000090', 'top-up', 'negative-source', 'capture',
                    'negative-evidence', 1, 1, 'c1100000-0000-0000-0000-000000000001',
                    'c1200000-0000-0000-0000-000000000001', 1, -1, '2026-07-18T00:00:00Z', NULL);
            """));
        negativeAuthority.ConstraintName.Should().Be("ck_economy_source_stamps_units_nonnegative");

        var forgedState = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            INSERT INTO public.economy_source_stamps (
                "Id", "SourceKind", "InternalSourceId", "SourceLegId", "EvidenceHash", "Provenance", "State",
                "ActorId", "TenantId", "PolicyVersion", "AuthoritativeUnits", "ObservedAt", "ConfirmedAt")
            VALUES ('c3000000-0000-0000-0000-000000000091', 'top-up', 'forged-state-source', 'capture',
                    'forged-state-evidence', 1, 1, 'c1100000-0000-0000-0000-000000000001',
                    'c1200000-0000-0000-0000-000000000001', 1, 100, '2026-07-18T00:00:00Z',
                    '2026-07-18T00:30:00Z');
            """));
        forgedState.ConstraintName.Should().Be("ck_economy_source_stamps_confirmation");

        for (var index = 1; index <= 8; index++)
        {
            await ReserveAsWriterAsync(
            connection,
            $"ca000000-0000-0000-0000-{index:D12}",
            $"c9000000-0000-0000-0000-{index:D12}",
            index == 7 ? 101 : 100,
            "c8000000-0000-0000-0000-000000000001");
        }

        var accepted = await ExecuteTopUpPostingAsync(
            connectionString,
            "c7000000-0000-0000-0000-000000000001",
            "top-up-valid",
            "c9000000-0000-0000-0000-000000000001",
            "top-up-operation-valid",
            "c3000000-0000-0000-0000-000000000001",
            "confirmed-evidence",
            100,
            1);
        accepted.Sequence.Should().Be(1);

        var absentSource = await Assert.ThrowsAsync<PostgresException>(() => ExecuteTopUpPostingAsync(
            connectionString,
            "c7000000-0000-0000-0000-000000000002",
            "top-up-absent",
            "c9000000-0000-0000-0000-000000000002",
            "top-up-operation-absent",
            null,
            null,
            100,
            2));
        absentSource.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);

        var unknownSource = await Assert.ThrowsAsync<PostgresException>(() => ExecuteTopUpPostingAsync(
            connectionString,
            "c7000000-0000-0000-0000-000000000003",
            "top-up-unknown",
            "c9000000-0000-0000-0000-000000000003",
            "top-up-operation-unknown",
            "c3000000-0000-0000-0000-000000000099",
            "unknown-evidence",
            100,
            3));
        unknownSource.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);

        var mutatedEvidence = await Assert.ThrowsAsync<PostgresException>(() => ExecuteTopUpPostingAsync(
            connectionString,
            "c7000000-0000-0000-0000-000000000004",
            "top-up-mutated",
            "c9000000-0000-0000-0000-000000000004",
            "top-up-operation-mutated",
            "c3000000-0000-0000-0000-000000000001",
            "mutated-evidence",
            100,
            4));
        mutatedEvidence.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);

        var unconfirmedSource = await Assert.ThrowsAsync<PostgresException>(() => ExecuteTopUpPostingAsync(
            connectionString,
            "c7000000-0000-0000-0000-000000000005",
            "top-up-unconfirmed",
            "c9000000-0000-0000-0000-000000000005",
            "top-up-operation-unconfirmed",
            "c3000000-0000-0000-0000-000000000002",
            "pending-evidence",
            100,
            5));
        unconfirmedSource.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);

        var forgedConfirmation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteTopUpPostingAsync(
            connectionString,
            "c7000000-0000-0000-0000-000000000006",
            "top-up-future-confirmation",
            "c9000000-0000-0000-0000-000000000006",
            "top-up-operation-future-confirmation",
            "c3000000-0000-0000-0000-000000000003",
            "future-evidence",
            100,
            6));
        forgedConfirmation.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);

        var providerOvercredit = await Assert.ThrowsAsync<PostgresException>(() => ExecuteTopUpPostingAsync(
            connectionString,
            "c7000000-0000-0000-0000-000000000007",
            "top-up-overcredit",
            "c9000000-0000-0000-0000-000000000007",
            "top-up-operation-overcredit",
            "c3000000-0000-0000-0000-000000000004",
            "limited-evidence",
            101,
            7));
        providerOvercredit.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);

        var reusedSource = await Assert.ThrowsAsync<PostgresException>(() => ExecuteTopUpPostingAsync(
            connectionString,
            "c7000000-0000-0000-0000-000000000008",
            "top-up-reused",
            "c9000000-0000-0000-0000-000000000008",
            "top-up-operation-reused",
            "c3000000-0000-0000-0000-000000000001",
            "confirmed-evidence",
            100,
            8));
        reusedSource.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        reusedSource.ConstraintName.Should().Be("ux_economy_posting_groups_source_stamp");

        (await ScalarAsync<long>(connection, "SELECT count(*) FROM public.economy_posting_groups;"))
            .Should().Be(1);
    }

    [DockerFact]
    public async Task Integrity_Constraints_Reject_Early_Maturity_OverAllocation_Overlap_Stale_Epoch_And_Lineage_Loss()
    {
        await using var container = await EconomyPostgreSqlTestDatabase.CreateAsync("economy_integrity");

        await using var connection = new NpgsqlConnection(container.ConnectionString);
        await connection.OpenAsync();
        await ApplyUpAsync(connection);
        await SeedIntegrityScenarioAsync(connection);

        var earlyMaturity = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            INSERT INTO public.economy_credit_lots (
                "Id", "WalletId", "RootSourceStampId", "Currency", "AmountUnits", "Provenance",
                "CreditedAt", "ConfirmedAt", "OriginalMaturesAt", "CashOutEligible", "JournalSequence", "State", "ReversalEpoch")
            VALUES ('b4000000-0000-0000-0000-000000000003', 'b1000000-0000-0000-0000-000000000001',
                'b3000000-0000-0000-0000-000000000005', 1, 100, 2, '2026-01-01T00:00:00Z',
                '2026-01-01T00:00:00Z', '2026-04-30T00:00:00Z', true, 3, 1, 0);
            """));
        earlyMaturity.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        earlyMaturity.ConstraintName.Should().Be("ck_economy_credit_lots_maturity_policy");

        var reusedRoot = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            INSERT INTO public.economy_credit_lots (
                "Id", "WalletId", "RootSourceStampId", "Currency", "AmountUnits", "Provenance",
                "CreditedAt", "ConfirmedAt", "OriginalMaturesAt", "CashOutEligible", "JournalSequence", "State", "ReversalEpoch")
            VALUES ('b4000000-0000-0000-0000-000000000099', 'b1000000-0000-0000-0000-000000000001',
                'b3000000-0000-0000-0000-000000000001', 1, 100, 1, '2026-01-01T00:00:00Z',
                '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', false, 99, 1, 0);
            """));
        reusedRoot.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        reusedRoot.ConstraintName.Should().Be("ux_economy_credit_lots_root_source");

        var providerMismatch = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            INSERT INTO public.economy_provider_fact_allocations (
                "Id", "SourceStampId", "JournalLineId", "Provider", "Environment", "ConnectedAccount",
                "ProviderObject", "ProviderMonetaryLeg", "Currency", "AllocatedUnits",
                "CumulativeCreditedUnits", "AuthoritativeUnits")
            VALUES ('b9000000-0000-0000-0000-000000000001', 'b3000000-0000-0000-0000-000000000001',
                'b8000000-0000-0000-0000-000000000001', 'stripe', 'live', 'acct-one', 'wrong-object',
                'capture', 1, 100, 100, 100);
            """));
        providerMismatch.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);

        var overAllocation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            INSERT INTO public.economy_entry_allocations ("Id", "JournalLineId", "ParentLotId", "AmountUnits")
            VALUES ('ba000000-0000-0000-0000-000000000001', 'b8000000-0000-0000-0000-000000000001',
                'b4000000-0000-0000-0000-000000000001', 101);
            """));
        overAllocation.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);

        await ExecuteAsync(connection, """
            INSERT INTO public.economy_entry_allocations ("Id", "JournalLineId", "ParentLotId", "AmountUnits")
            VALUES ('ba000000-0000-0000-0000-000000000002', 'b8000000-0000-0000-0000-000000000001',
                'b4000000-0000-0000-0000-000000000001', 100);
            INSERT INTO public.economy_root_reversal_states (
                "RootSourceStampId", "Epoch", "CumulativeProviderUnits", "ReversedUnits", "State", "TargetedRanges", "UpdatedAt")
            VALUES ('b3000000-0000-0000-0000-000000000001', 1, 100, 0, 'confirmed', '[]', '2026-01-01T00:00:00Z');
            """);

        var staleEpoch = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            INSERT INTO public.economy_fragment_root_ranges (
                "Id", "RootSourceStampId", "CreditLotId", "EntryAllocationId", "StartInclusive", "EndExclusive", "ReversalEpoch")
            VALUES ('bb000000-0000-0000-0000-000000000001', 'b3000000-0000-0000-0000-000000000001',
                'b4000000-0000-0000-0000-000000000001', NULL, 0, 100, 0);
            """));
        staleEpoch.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);

        var overlappingRange = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            INSERT INTO public.economy_fragment_root_ranges (
                "Id", "RootSourceStampId", "CreditLotId", "EntryAllocationId", "StartInclusive", "EndExclusive", "ReversalEpoch")
            VALUES
                ('bb000000-0000-0000-0000-000000000002', 'b3000000-0000-0000-0000-000000000001',
                 'b4000000-0000-0000-0000-000000000001', NULL, 0, 100, 1),
                ('bb000000-0000-0000-0000-000000000003', 'b3000000-0000-0000-0000-000000000001',
                 NULL, 'ba000000-0000-0000-0000-000000000002', 10, 100, 1);
            """));
        overlappingRange.SqlState.Should().Be(PostgresErrorCodes.ExclusionViolation);
        overlappingRange.ConstraintName.Should().Be("ex_economy_fragment_root_ranges_no_overlap");

        var lineageLoss = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            INSERT INTO public.economy_lot_lineage_edges
                ("Id", "ParentLotId", "ChildLotId", "Currency", "AmountUnits")
            VALUES ('bc000000-0000-0000-0000-000000000001',
                'b4000000-0000-0000-0000-000000000003', 'b4000000-0000-0000-0000-000000000004', 1, 90);
            """));
        lineageLoss.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);

        var incompleteReview = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, """
            INSERT INTO public.economy_risk_review_cases (
                "Id", "RiskDecisionId", "SubmittedBy", "Status", "SubmittedAt", "ResolvedAt",
                "ResolvedBy", "Resolution", "RequiredApprovals", "AppealOf")
            VALUES ('bd000000-0000-0000-0000-000000000001', 'b7000000-0000-0000-0000-000000000001',
                'b2000000-0000-0000-0000-000000000001', 2, '2026-01-01T00:00:00Z', NULL, NULL, NULL, 1, NULL);
            """));
        incompleteReview.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        incompleteReview.ConstraintName.Should().Be("ck_economy_risk_review_cases_state");
    }

    private static async Task SeedIntegrityScenarioAsync(NpgsqlConnection connection) =>
        await ExecuteAsync(connection, """
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt") VALUES
                ('b1000000-0000-0000-0000-000000000001', 'b2000000-0000-0000-0000-000000000001', 'b2100000-0000-0000-0000-000000000001', 1, '2026-01-01T00:00:00Z'),
                ('b1000000-0000-0000-0000-000000000002', 'b2000000-0000-0000-0000-000000000002', 'b2100000-0000-0000-0000-000000000001', 1, '2026-01-01T00:00:00Z');
            INSERT INTO public.economy_accounts ("Id", "WalletId", "Code", "Currency", "Provenance", "CreatedAt")
            VALUES ('b5000000-0000-0000-0000-000000000001', 'b1000000-0000-0000-0000-000000000001',
                2, 1, 1, '2026-01-01T00:00:00Z');

            INSERT INTO public.economy_source_stamps (
                "Id", "SourceKind", "InternalSourceId", "SourceLegId", "Provider", "ProviderReference",
                "EvidenceHash", "Provenance", "State", "ActorId", "TenantId", "PostingReferenceId",
                "PolicyVersion", "AuthoritativeUnits", "ObservedAt", "ConfirmedAt")
            VALUES
                ('b3000000-0000-0000-0000-000000000001', 'top-up', 'source-one', 'capture', 'stripe', 'pi-one',
                 'evidence-one', 1, 2, 'b2000000-0000-0000-0000-000000000001', 'b2100000-0000-0000-0000-000000000001',
                 NULL, 1, 100, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z'),
                ('b3000000-0000-0000-0000-000000000003', 'grant', 'source-three', 'grant', NULL, NULL,
                 'evidence-three', 1, 2, 'b2000000-0000-0000-0000-000000000001', 'b2100000-0000-0000-0000-000000000001',
                 NULL, 1, 100, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z'),
                ('b3000000-0000-0000-0000-000000000004', 'grant', 'source-four', 'grant', NULL, NULL,
                 'evidence-four', 1, 2, 'b2000000-0000-0000-0000-000000000002', 'b2100000-0000-0000-0000-000000000001',
                 NULL, 1, 100, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z'),
                ('b3000000-0000-0000-0000-000000000005', 'bounty', 'source-five', 'earned', NULL, NULL,
                 'evidence-five', 2, 2, 'b2000000-0000-0000-0000-000000000001', 'b2100000-0000-0000-0000-000000000001',
                 NULL, 1, 100, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');

            INSERT INTO public.economy_credit_lots (
                "Id", "WalletId", "RootSourceStampId", "Currency", "AmountUnits", "Provenance",
                "CreditedAt", "ConfirmedAt", "OriginalMaturesAt", "CashOutEligible", "JournalSequence", "State", "ReversalEpoch")
            VALUES
                ('b4000000-0000-0000-0000-000000000001', 'b1000000-0000-0000-0000-000000000001',
                 'b3000000-0000-0000-0000-000000000001', 1, 100, 1, '2026-01-01T00:00:00Z',
                 '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', false, 1, 1, 0),
                ('b4000000-0000-0000-0000-000000000003', 'b1000000-0000-0000-0000-000000000001',
                 'b3000000-0000-0000-0000-000000000003', 1, 100, 1, '2026-01-01T00:00:00Z',
                 '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', false, 3, 1, 0),
                ('b4000000-0000-0000-0000-000000000004', 'b1000000-0000-0000-0000-000000000002',
                 'b3000000-0000-0000-0000-000000000004', 1, 100, 1, '2026-01-01T00:00:00Z',
                 '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', false, 4, 1, 0);

            INSERT INTO public.economy_posting_groups (
                "Id", "IdempotencyKey", "TemplateKind", "TemplateVersion", "Authority", "Status", "CapabilityId",
                "ActorId", "TenantId", "RiskDecisionId", "PolicyVersion", "ReserveVersion", "SourceStampId", "RecordedAt")
            VALUES ('b6000000-0000-0000-0000-000000000001', 'integrity-seed', 4, 1, 3, 1,
                'b6100000-0000-0000-0000-000000000001', 'b2000000-0000-0000-0000-000000000001',
                'b2100000-0000-0000-0000-000000000001', NULL, 1, 1, NULL, '2026-01-01T00:00:00Z');
            INSERT INTO public.economy_journal_entries ("Id", "PostingGroupId", "Sequence", "PreviousHash", "Hash", "RecordedAt")
            VALUES ('b7000000-0000-0000-0000-000000000099', 'b6000000-0000-0000-0000-000000000001',
                1, 'previous', 'current', '2026-01-01T00:00:00Z');
            INSERT INTO public.economy_journal_lines (
                "Id", "JournalEntryId", "AccountId", "WalletId", "CreditLotId", "Sequence", "Side", "Currency", "AmountUnits", "Provenance")
            VALUES ('b8000000-0000-0000-0000-000000000001', 'b7000000-0000-0000-0000-000000000099',
                'b5000000-0000-0000-0000-000000000001', 'b1000000-0000-0000-0000-000000000001',
                NULL, 1, 1, 1, 100, 1);

            INSERT INTO public.economy_risk_decisions (
                "Id", "Outcome", "OperationFingerprint", "ActorHash", "TemplateKind", "SourceWalletId",
                "DestinationWalletId", "Currency", "AmountUnits", "CurrencyLegs", "SourceRoots", "ProviderReferenceHash",
                "PolicyVersion", "ReserveVersion", "FeatureVersion", "KillSwitchEpoch", "CounterVersion", "EntityGraphVersion",
                "EntityGraphEvidenceHash", "ReasonCodes", "IssuedAt", "ExpiresAt")
            VALUES ('b7000000-0000-0000-0000-000000000001', 4, 'review-operation', 'actor', 4,
                'b1000000-0000-0000-0000-000000000001', 'b1000000-0000-0000-0000-000000000002',
                1, 100, '[]', '[]', 'provider', 1, 1, 1, 0, 1, 0, 'graph', '[]',
                '2026-01-01T00:00:00Z', '2026-01-02T00:00:00Z');
            """);

    private static async Task SeedRiskCounterScenarioAsync(NpgsqlConnection connection) =>
        await ExecuteAsync(connection, """
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt") VALUES
                ('51000000-0000-0000-0000-000000000001', '52000000-0000-0000-0000-000000000001', '53000000-0000-0000-0000-000000000001', 1, '2026-07-18T00:00:00Z'),
                ('51000000-0000-0000-0000-000000000002', '52000000-0000-0000-0000-000000000002', '53000000-0000-0000-0000-000000000001', 1, '2026-07-18T00:00:00Z');

            INSERT INTO public.economy_risk_counters (
                "Id", "Dimension", "SubjectHash", "Operation", "Currency", "WindowStartedAt",
                "WindowEndsAt", "CounterVersion", "MaxUnits", "UsedUnits", "UpdatedAt")
            VALUES (
                '61000000-0000-0000-0000-000000000001', 1, 'wallet-cluster', 4, 1,
                '2026-07-18T00:00:00Z', '2026-07-19T00:00:00Z', 1, 100, 0, '2026-07-18T00:00:00Z');

            INSERT INTO public.economy_risk_decisions (
                "Id", "Outcome", "OperationFingerprint", "ActorHash", "TemplateKind", "SourceWalletId",
                "DestinationWalletId", "Currency", "AmountUnits", "CurrencyLegs", "SourceRoots",
                "ProviderReferenceHash", "PolicyVersion", "ReserveVersion", "FeatureVersion", "KillSwitchEpoch",
                "CounterVersion", "EntityGraphVersion", "EntityGraphEvidenceHash", "ReasonCodes", "IssuedAt", "ExpiresAt")
            VALUES
                ('71000000-0000-0000-0000-000000000001', 1, 'operation-one', 'actor-one', 4,
                 '51000000-0000-0000-0000-000000000001', '51000000-0000-0000-0000-000000000002',
                 1, 60, '[]', '[]', 'provider-one', 1, 1, 1, 0, 1, 0, 'graph-one', '[]',
                 '2026-07-18T00:00:00Z', '2026-07-19T00:00:00Z'),
                ('71000000-0000-0000-0000-000000000002', 1, 'operation-two', 'actor-two', 4,
                 '51000000-0000-0000-0000-000000000001', '51000000-0000-0000-0000-000000000002',
                 1, 60, '[]', '[]', 'provider-two', 1, 1, 1, 0, 1, 0, 'graph-two', '[]',
                 '2026-07-18T00:00:00Z', '2026-07-19T00:00:00Z');
            """);

    private static async Task SeedWriterScenarioAsync(NpgsqlConnection connection) =>
        await ExecuteAsync(connection, """
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt") VALUES
                ('91000000-0000-0000-0000-000000000001', '91100000-0000-0000-0000-000000000001', '91200000-0000-0000-0000-000000000001', 1, '2026-07-18T00:00:00Z'),
                ('91000000-0000-0000-0000-000000000002', '91100000-0000-0000-0000-000000000002', '91200000-0000-0000-0000-000000000001', 1, '2026-07-18T00:00:00Z');
            INSERT INTO public.economy_accounts ("Id", "WalletId", "Code", "Currency", "Provenance", "CreatedAt") VALUES
                ('92000000-0000-0000-0000-000000000001', '91000000-0000-0000-0000-000000000001', 2, 1, 1, '2026-07-18T00:00:00Z'),
                ('92000000-0000-0000-0000-000000000002', '91000000-0000-0000-0000-000000000002', 2, 1, 1, '2026-07-18T00:00:00Z');
            INSERT INTO public.economy_registered_capabilities
                ("Id", "Name", "AllowedTemplateKinds", "IsEnabled", "CreatedAt", "RevokedAt")
            VALUES ('93000000-0000-0000-0000-000000000001', 'marketplace-settlement', '[4]', true, '2026-07-18T00:00:00Z', NULL);
            INSERT INTO public.economy_risk_counters (
                "Id", "Dimension", "SubjectHash", "Operation", "Currency", "WindowStartedAt",
                "WindowEndsAt", "CounterVersion", "MaxUnits", "UsedUnits", "UpdatedAt")
            VALUES ('98000000-0000-0000-0000-000000000001', 1, 'writer-wallet', 4, 1,
                '2026-07-18T00:00:00Z', '2026-07-19T00:00:00Z', 1, 500, 0, '2026-07-18T00:00:00Z');
            """ + string.Join('\n', new[]
            {
                RiskDecisionSql("99000000-0000-0000-0000-000000000001", "operation-valid", 50, "2026-07-18T03:00:00Z"),
                RiskDecisionSql("99000000-0000-0000-0000-000000000002", "operation-stale", 50, "2026-07-18T01:00:00Z"),
                RiskDecisionSql("99000000-0000-0000-0000-000000000003", "operation-unauthorized", 50, "2026-07-18T03:00:00Z"),
                RiskDecisionSql("99000000-0000-0000-0000-000000000004", "operation-shape", 50, "2026-07-18T03:00:00Z"),
                RiskDecisionSql("99000000-0000-0000-0000-000000000005", "operation-concurrent-one", 40, "2026-07-18T03:00:00Z"),
                RiskDecisionSql("99000000-0000-0000-0000-000000000006", "operation-concurrent-two", 40, "2026-07-18T03:00:00Z")
            }));

    private static async Task SeedTopUpWriterScenarioAsync(NpgsqlConnection connection) =>
        await ExecuteAsync(connection, """
            INSERT INTO public.economy_wallets ("Id", "OwnerId", "TenantId", "State", "CreatedAt")
            VALUES ('c1000000-0000-0000-0000-000000000001', 'c1100000-0000-0000-0000-000000000001',
                    'c1200000-0000-0000-0000-000000000001', 1, '2026-07-18T00:00:00Z');
            INSERT INTO public.economy_accounts ("Id", "WalletId", "Code", "Currency", "Provenance", "CreatedAt") VALUES
                ('c2000000-0000-0000-0000-000000000001', NULL, 1, 1, 1, '2026-07-18T00:00:00Z'),
                ('c2000000-0000-0000-0000-000000000002', 'c1000000-0000-0000-0000-000000000001', 2, 1, 1, '2026-07-18T00:00:00Z');
            INSERT INTO public.economy_registered_capabilities
                ("Id", "Name", "AllowedTemplateKinds", "IsEnabled", "CreatedAt", "RevokedAt")
            VALUES ('c6000000-0000-0000-0000-000000000001', 'confirmed-top-up', '[1]', true,
                    '2026-07-18T00:00:00Z', NULL);
            INSERT INTO public.economy_risk_counters (
                "Id", "Dimension", "SubjectHash", "Operation", "Currency", "WindowStartedAt",
                "WindowEndsAt", "CounterVersion", "MaxUnits", "UsedUnits", "UpdatedAt")
            VALUES ('c8000000-0000-0000-0000-000000000001', 1, 'top-up-wallet', 1, 1,
                    '2026-07-18T00:00:00Z', '2026-07-19T00:00:00Z', 1, 1000, 0, '2026-07-18T00:00:00Z');
            INSERT INTO public.economy_source_stamps (
                "Id", "SourceKind", "InternalSourceId", "SourceLegId", "Provider", "ProviderReference",
                "EvidenceHash", "Provenance", "State", "ActorId", "TenantId", "PostingReferenceId",
                "PolicyVersion", "AuthoritativeUnits", "ObservedAt", "ConfirmedAt")
            VALUES
                ('c3000000-0000-0000-0000-000000000001', 'top-up', 'confirmed-source', 'capture', 'stripe', 'pi-confirmed',
                 'confirmed-evidence', 1, 2, 'c1100000-0000-0000-0000-000000000001', 'c1200000-0000-0000-0000-000000000001',
                 NULL, 1, 100, '2026-07-18T00:00:00Z', '2026-07-18T00:30:00Z'),
                ('c3000000-0000-0000-0000-000000000002', 'top-up', 'pending-source', 'capture', 'stripe', 'pi-pending',
                 'pending-evidence', 1, 1, 'c1100000-0000-0000-0000-000000000001', 'c1200000-0000-0000-0000-000000000001',
                 NULL, 1, 100, '2026-07-18T00:00:00Z', NULL),
                ('c3000000-0000-0000-0000-000000000003', 'top-up', 'future-source', 'capture', 'stripe', 'pi-future',
                 'future-evidence', 1, 2, 'c1100000-0000-0000-0000-000000000001', 'c1200000-0000-0000-0000-000000000001',
                 NULL, 1, 100, '2026-07-18T00:00:00Z', '2026-07-18T02:00:00Z'),
                ('c3000000-0000-0000-0000-000000000004', 'top-up', 'limited-source', 'capture', 'stripe', 'pi-limited',
                 'limited-evidence', 1, 2, 'c1100000-0000-0000-0000-000000000001', 'c1200000-0000-0000-0000-000000000001',
                 NULL, 1, 100, '2026-07-18T00:00:00Z', '2026-07-18T00:30:00Z');
            """ + string.Join('\n', Enumerable.Range(1, 8).Select(index =>
            TopUpRiskDecisionSql(
                $"c9000000-0000-0000-0000-{index:D12}",
                index switch
                {
                    1 => "top-up-operation-valid",
                    2 => "top-up-operation-absent",
                    3 => "top-up-operation-unknown",
                    4 => "top-up-operation-mutated",
                    5 => "top-up-operation-unconfirmed",
                    6 => "top-up-operation-future-confirmation",
                    7 => "top-up-operation-overcredit",
                    _ => "top-up-operation-reused"
                },
                index == 7 ? 101 : 100))));

    private static string RiskDecisionSql(string id, string fingerprint, long amount, string expiresAt) => $"""
        INSERT INTO public.economy_risk_decisions (
            "Id", "Outcome", "OperationFingerprint", "ActorHash", "TemplateKind", "SourceWalletId",
            "DestinationWalletId", "Currency", "AmountUnits", "CurrencyLegs", "SourceRoots",
            "ProviderReferenceHash", "PolicyVersion", "ReserveVersion", "FeatureVersion", "KillSwitchEpoch",
            "CounterVersion", "EntityGraphVersion", "EntityGraphEvidenceHash", "ReasonCodes", "IssuedAt", "ExpiresAt")
        VALUES ('{id}', 1, '{fingerprint}', 'actor-hash', 4,
            '91000000-0000-0000-0000-000000000001', '91000000-0000-0000-0000-000000000002',
            1, {amount}, '[]', '[]', 'provider-hash', 1, 1, 1, 0, 1, 0, 'graph-hash', '[]',
            '2026-07-18T00:00:00Z', '{expiresAt}');
        """;

    private static string TopUpRiskDecisionSql(string id, string fingerprint, long amount) => $"""
        INSERT INTO public.economy_risk_decisions (
            "Id", "Outcome", "OperationFingerprint", "ActorHash", "TemplateKind", "SourceWalletId",
            "DestinationWalletId", "Currency", "AmountUnits", "CurrencyLegs", "SourceRoots",
            "ProviderReferenceHash", "PolicyVersion", "ReserveVersion", "FeatureVersion", "KillSwitchEpoch",
            "CounterVersion", "EntityGraphVersion", "EntityGraphEvidenceHash", "ReasonCodes", "IssuedAt", "ExpiresAt")
        VALUES ('{id}', 1, '{fingerprint}', 'top-up-actor', 1,
            'c1000000-0000-0000-0000-000000000001',
            'c1000000-0000-0000-0000-000000000001', 1, {amount}, '[]', '[]', 'top-up-provider',
            1, 1, 1, 0, 1, 0, 'top-up-graph', '[]', '2026-07-18T00:00:00Z', '2026-07-18T03:00:00Z');
        """;

    private static async Task ReserveAsWriterAsync(
        NpgsqlConnection connection,
        string reservationId,
        string decisionId,
        long amount,
        string counterId = "98000000-0000-0000-0000-000000000001")
    {
        await ExecuteAsync(connection, "SET ROLE gameguild_economy_writer;");
        await ScalarAsync<bool>(connection, $"""
            SELECT economy_private.reserve_risk_counter_v1(
                '{reservationId}', '{decisionId}', '{counterId}',
                1, {amount}, '2026-07-18T00:30:00Z');
            """);
        await ExecuteAsync(connection, "RESET ROLE;");
    }

    private static async Task<PostingResult> ExecutePostingAsync(
        string connectionString,
        string postingId,
        string idempotencyKey,
        string decisionId,
        string fingerprint,
        string lines,
        string requestedAt = "2026-07-18T01:00:00Z",
        string capabilityId = "93000000-0000-0000-0000-000000000001")
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "SET ROLE gameguild_economy_writer;");
        await using var command = new NpgsqlCommand($"""
            SELECT * FROM economy_private.post_registered_posting_v1(
                '{capabilityId}', '91100000-0000-0000-0000-000000000001',
                '91200000-0000-0000-0000-000000000001', '{postingId}', '{idempotencyKey}',
                4, 1, 2, 1, 1, '{decisionId}', '{fingerprint}', 1,
                NULL, NULL, '{requestedAt}', $lines${lines}$lines$::jsonb, '[]'::jsonb, '[]'::jsonb,
                '[]'::jsonb, NULL);
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        return new PostingResult(reader.GetGuid(0), reader.GetInt64(1), reader.GetString(2), reader.GetBoolean(3));
    }

    private static async Task<PostingResult> ExecuteTopUpPostingAsync(
        string connectionString,
        string postingId,
        string idempotencyKey,
        string decisionId,
        string fingerprint,
        string? sourceStampId,
        string? sourceEvidenceHash,
        long amount,
        int discriminator)
    {
        var sourceStampSql = sourceStampId is null ? "NULL" : $"'{sourceStampId}'";
        var sourceEvidenceSql = sourceEvidenceHash is null ? "NULL" : $"'{sourceEvidenceHash}'";
        var lines = TopUpLines(amount, discriminator);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "SET ROLE gameguild_economy_writer;");
        await using var command = new NpgsqlCommand($"""
            SELECT * FROM economy_private.post_registered_posting_v1(
                'c6000000-0000-0000-0000-000000000001', 'c1100000-0000-0000-0000-000000000001',
                'c1200000-0000-0000-0000-000000000001', '{postingId}', '{idempotencyKey}',
                1, 1, 1, 1, 1, '{decisionId}', '{fingerprint}', 1,
                {sourceStampSql}, {sourceEvidenceSql}, '2026-07-18T01:00:00Z',
                $lines${lines}$lines$::jsonb, '[]'::jsonb, '[]'::jsonb, '[]'::jsonb, NULL);
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        return new PostingResult(reader.GetGuid(0), reader.GetInt64(1), reader.GetString(2), reader.GetBoolean(3));
    }

    private static string SpendLines(long amount, int discriminator = 0) => $$"""
        [
          {"id":"92100000-0000-0000-0000-{{(discriminator * 1000L + amount).ToString("D12")}}","account_id":"92000000-0000-0000-0000-000000000001","account_code":2,"wallet_id":"91000000-0000-0000-0000-000000000001","side":1,"currency":1,"amount_units":{{amount}},"provenance":1},
          {"id":"92200000-0000-0000-0000-{{(discriminator * 1000L + amount).ToString("D12")}}","account_id":"92000000-0000-0000-0000-000000000002","account_code":2,"wallet_id":"91000000-0000-0000-0000-000000000002","side":2,"currency":1,"amount_units":{{amount}},"provenance":1}
        ]
        """;

    private static string UnauthorizedSpendLines(long amount) => $$"""
        [
          {"id":"92300000-0000-0000-0000-000000000001","account_id":"92000000-0000-0000-0000-000000000001","account_code":1,"wallet_id":"91000000-0000-0000-0000-000000000001","side":1,"currency":1,"amount_units":{{amount}},"provenance":1},
          {"id":"92300000-0000-0000-0000-000000000002","account_id":"92000000-0000-0000-0000-000000000002","account_code":1,"wallet_id":"91000000-0000-0000-0000-000000000002","side":2,"currency":1,"amount_units":{{amount}},"provenance":1}
        ]
        """;

    private static string TopUpLines(long amount, int discriminator) => $$"""
        [
          {"id":"c4000000-0000-0000-0000-{{discriminator.ToString("D12")}}","account_id":"c2000000-0000-0000-0000-000000000001","account_code":1,"wallet_id":null,"side":1,"currency":1,"amount_units":{{amount}},"provenance":1},
          {"id":"c5000000-0000-0000-0000-{{discriminator.ToString("D12")}}","account_id":"c2000000-0000-0000-0000-000000000002","account_code":2,"wallet_id":"c1000000-0000-0000-0000-000000000001","side":2,"currency":1,"amount_units":{{amount}},"provenance":1}
        ]
        """;

    private static async Task<ReservationResult> ReserveCounterAsync(
        string connectionString,
        string reservationId,
        string decisionId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "SET ROLE gameguild_economy_writer;");
        try
        {
            var reserved = await ScalarAsync<bool>(connection, $"""
                SELECT economy_private.reserve_risk_counter_v1(
                    '{reservationId}', '{decisionId}', '61000000-0000-0000-0000-000000000001',
                    1, 60, '2026-07-18T01:00:00Z');
                """);
            return new ReservationResult(reserved, null);
        }
        catch (PostgresException exception)
        {
            return new ReservationResult(false, exception.SqlState);
        }
    }

    private static async Task ApplyUpAsync(NpgsqlConnection connection)
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedMigration().BuildUp(builder);
        await ApplyOperationsAsync(connection, builder);
    }

    private static async Task ApplyDownAsync(NpgsqlConnection connection)
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedMigration().BuildDown(builder);
        await ApplyOperationsAsync(connection, builder);
    }

    private static async Task ApplyOperationsAsync(NpgsqlConnection connection, MigrationBuilder builder)
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

    private sealed class ExposedMigration : AddEconomyFoundationSchemaRollup
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

    private sealed record ReservationResult(bool Reserved, string? SqlState);
    private sealed record PostingResult(Guid PostingId, long Sequence, string Hash, bool Duplicate);
}
