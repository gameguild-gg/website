using FluentAssertions;
using GameGuild.API.Database.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GameGuild.Finance.Economy.UnitTests.Persistence;

public sealed class EconomyTopUpMigrationTests
{
    [Fact]
    public void UpInstallsPaymentForeignKeyAndLeastPrivilegeWriters()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        new ExposedMigration().BuildUp(builder);

        builder.Operations.OfType<CreateTableOperation>().Should().ContainSingle(operation =>
            operation.Name == "economy_top_up_intents");
        builder.Operations.OfType<AddForeignKeyOperation>().Should().ContainSingle(operation =>
            operation.Table == "economy_top_up_intents" && operation.PrincipalTable == "payments" &&
            operation.Columns.Length == 1 && operation.Columns[0] == "PaymentId");
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
        sql.Should().Contain("prepare_economy_top_up_intent_v1");
        sql.Should().Contain("bind_economy_top_up_provider_v1");
        sql.Should().Contain("guard_economy_top_up_intent_mutation_v1");
        sql.Should().Contain("pg_advisory_xact_lock");
        sql.Should().Contain("SECURITY DEFINER");
        sql.Should().Contain("Economy top-up authority is immutable");
        sql.Should().Contain("REVOKE ALL ON TABLE public.economy_top_up_intents FROM gameguild_economy_runtime");
        sql.Should().Contain("GRANT SELECT ON TABLE public.economy_top_up_intents TO gameguild_economy_runtime");
        sql.Should().Contain("TO gameguild_economy_procedure_owner");
    }

    [Fact]
    public void DownRemovesWritersForeignKeyAndTable()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        new ExposedMigration().BuildDown(builder);

        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
        sql.Should().Contain("DROP FUNCTION IF EXISTS economy_private.bind_economy_top_up_provider_v1");
        sql.Should().Contain("DROP FUNCTION IF EXISTS economy_private.prepare_economy_top_up_intent_v1");
        sql.Should().Contain("DROP TRIGGER IF EXISTS guard_economy_top_up_intent_mutation");
        builder.Operations.OfType<DropForeignKeyOperation>().Should().ContainSingle(operation =>
            operation.Table == "economy_top_up_intents");
        builder.Operations.OfType<DropTableOperation>().Should().ContainSingle(operation =>
            operation.Name == "economy_top_up_intents");
    }

    private sealed class ExposedMigration : AddEconomySelfServiceTopUpIntents
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
        public void BuildDown(MigrationBuilder builder) => Down(builder);
    }
}

public sealed class EconomyTopUpSettlementMigrationTests
{
    [Fact]
    public void UpInstallsDurableProviderSettlementAndLeastPrivilegePaymentReader()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        new ExposedMigration().BuildUp(builder);

        builder.Operations.OfType<AddForeignKeyOperation>().Should().ContainSingle(operation =>
            operation.Table == "economy_top_up_intents" &&
            operation.PrincipalTable == "economy_posting_groups" &&
            operation.Columns.Length == 1 && operation.Columns[0] == "PostingGroupId");
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
        sql.Should().Contain("economy.confirm-hard-coin-funding.v1");
        sql.Should().Contain("initialize_economy_top_up_intent_timestamps_v1");
        sql.Should().Contain("guard_economy_top_up_intent_mutation_v1");
        sql.Should().Contain("read_economy_top_up_payment_fact_v1");
        sql.Should().Contain("apply_economy_top_up_provider_event_v1");
        sql.Should().Contain("SECURITY DEFINER");
        sql.Should().Contain("Economy top-up provider fact does not match authoritative intent");
        sql.Should().Contain("REVOKE ALL ON FUNCTION economy_private.read_economy_top_up_payment_fact_v1");
        sql.Should().Contain("REVOKE ALL ON FUNCTION economy_private.initialize_economy_top_up_intent_timestamps_v1");
        sql.Should().Contain("GRANT EXECUTE ON FUNCTION economy_private.apply_economy_top_up_provider_event_v1");
        sql.Should().NotContain("GRANT SELECT ON TABLE public.payments TO gameguild_economy_runtime");
    }

    [Fact]
    public void DownRemovesSettlementProceduresCapabilityAndPostingForeignKey()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        new ExposedMigration().BuildDown(builder);

        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
        sql.Should().Contain("DROP FUNCTION IF EXISTS economy_private.apply_economy_top_up_provider_event_v1");
        sql.Should().Contain("DROP FUNCTION IF EXISTS economy_private.read_economy_top_up_payment_fact_v1");
        sql.Should().Contain("DROP FUNCTION IF EXISTS economy_private.initialize_economy_top_up_intent_timestamps_v1");
        sql.Should().Contain("DELETE FROM public.economy_registered_capabilities");
        builder.Operations.OfType<DropForeignKeyOperation>().Should().ContainSingle(operation =>
            operation.Table == "economy_top_up_intents");
    }

    private sealed class ExposedMigration : AddEconomyTopUpProviderSettlement
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
        public void BuildDown(MigrationBuilder builder) => Down(builder);
    }
}
