using FluentAssertions;
using GameGuild.API.Database.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GameGuild.API.UnitTests.Database;

public sealed class SingleOpenRefundGuardMigrationTests
{
    private sealed class ExposedMigration : AddSingleOpenRefundGuard
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);

        public void BuildDown(MigrationBuilder builder) => Down(builder);
    }

    [Fact]
    public void Up_CreatesAPartialUniqueGuardConcurrentlyOutsideATransaction()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedMigration().BuildUp(builder);

        var operation = builder.Operations.Cast<SqlOperation>().Single();
        operation.SuppressTransaction.Should().BeTrue();
        operation.Sql.Should().Contain("CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ix_refunds_single_open_per_payment");
        operation.Sql.Should().Contain("ON refunds (\"PaymentId\")");
        operation.Sql.Should().Contain("\"Status\" IN (0, 1, 3, 5)");
    }

    [Fact]
    public void Down_DropsTheGuardConcurrentlyOutsideATransaction()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedMigration().BuildDown(builder);

        var operation = builder.Operations.Cast<SqlOperation>().Single();
        operation.SuppressTransaction.Should().BeTrue();
        operation.Sql.Should().Contain("DROP INDEX CONCURRENTLY IF EXISTS ix_refunds_single_open_per_payment");
    }
}
