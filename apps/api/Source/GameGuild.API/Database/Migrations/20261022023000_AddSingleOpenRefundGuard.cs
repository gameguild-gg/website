using GameGuild.API.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261022023000_AddSingleOpenRefundGuard")]
public partial class AddSingleOpenRefundGuard : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ix_refunds_single_open_per_payment
                ON refunds ("PaymentId")
                WHERE "Status" IN (0, 1, 3, 5);
            """,
            suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX CONCURRENTLY IF EXISTS ix_refunds_single_open_per_payment;
            """,
            suppressTransaction: true);
    }
}
