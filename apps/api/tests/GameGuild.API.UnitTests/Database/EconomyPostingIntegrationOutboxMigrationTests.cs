using FluentAssertions;
using GameGuild.API.Database.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GameGuild.API.UnitTests.Database;

public sealed class EconomyPostingIntegrationOutboxMigrationTests
{
    [Fact]
    public void Up_BridgesExistingAndFutureEconomyPostingsIntoDurableOutbox()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new TestableMigration().BuildUp(builder);

        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
        sql.Should().Contain("AFTER INSERT ON public.economy_outbox_messages");
        sql.Should().Contain("INSERT INTO \"gameguild.integration\".\"outbox_messages\"");
        sql.Should().Contain("economy.posting.accepted.v1");
        sql.Should().Contain("GameGuild.Finance.Contracts.EconomyPostingAcceptedEventV1, GameGuild.Finance.Contracts");
        sql.Should().Contain("jsonb_agg");
        sql.Should().Contain("ON CONFLICT (\"EventId\") DO NOTHING");
        sql.Should().Contain("SECURITY DEFINER");
        sql.Should().Contain("REVOKE ALL ON FUNCTION");
    }

    [Fact]
    public void Down_RemovesTriggerAndBridgeFunctions()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new TestableMigration().BuildDown(builder);

        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
        sql.Should().Contain("DROP TRIGGER IF EXISTS trg_bridge_economy_posting_outbox_v1");
        sql.Should().Contain("DROP FUNCTION IF EXISTS public.bridge_economy_posting_outbox_v1()");
        sql.Should().Contain("DROP FUNCTION IF EXISTS");
        sql.Should().Contain("\"gameguild.integration\".enqueue_economy_posting_accepted_v1");
    }

    private sealed class TestableMigration : BridgeEconomyPostingOutbox
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);

        public void BuildDown(MigrationBuilder builder) => Down(builder);
    }
}
