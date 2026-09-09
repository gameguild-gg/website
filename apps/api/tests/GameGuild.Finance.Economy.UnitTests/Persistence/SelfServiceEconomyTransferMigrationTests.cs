using FluentAssertions;
using GameGuild.API.Database.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GameGuild.Finance.Economy.UnitTests.Persistence;

public sealed class SelfServiceEconomyTransferMigrationTests
{
    [Fact]
    public void MigrationInstallsAnAppendOnlyRuntimeScopedIntentProcedure()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        new ExposedMigration().BuildUp(builder);

        builder.Operations.OfType<CreateTableOperation>().Should().ContainSingle(operation =>
            operation.Name == "economy_self_service_transfer_intents");
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
        sql.Should().Contain("prepare_self_service_transfer_intent_v1");
        sql.Should().Contain("reserve_self_service_transfer_roots_v1");
        sql.Should().Contain("reserve_fifo_fragments_v1");
        sql.Should().Contain("validate_self_service_transfer_root_binding_v1");
        sql.Should().Contain("tr_economy_self_service_transfer_root_binding");
        sql.Should().Contain("source roots do not match the risk decision");
        sql.Should().Contain("#variable_conflict use_column");
        sql.Should().Contain("pg_advisory_xact_lock");
        sql.Should().Contain("SECURITY DEFINER");
        sql.Should().Contain("GRANT USAGE ON SCHEMA economy_private TO gameguild_economy_runtime");
        sql.Should().Contain("deny_immutable_mutation");
        sql.Should().Contain("'e1000000-0000-0000-0000-000000000011', 'fifo-transfer', '[4]'::jsonb");
    }

    [Fact]
    public void DownRemovesTheProcedureCapabilityAndIntentTable()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        new ExposedMigration().BuildDown(builder);

        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
        sql.Should().Contain("DROP FUNCTION IF EXISTS economy_private.prepare_self_service_transfer_intent_v1");
        sql.Should().Contain("DROP FUNCTION IF EXISTS economy_private.reserve_self_service_transfer_roots_v1");
        sql.Should().Contain("DROP FUNCTION IF EXISTS economy_private.validate_self_service_transfer_root_binding_v1");
        sql.Should().Contain("WHERE \"Name\" = 'fifo-transfer'");
        builder.Operations.OfType<DropTableOperation>().Should().ContainSingle(operation =>
            operation.Name == "economy_self_service_transfer_intents");
    }

    private sealed class ExposedMigration : AddSelfServiceEconomyTransferIntents
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
        public void BuildDown(MigrationBuilder builder) => Down(builder);
    }
}
