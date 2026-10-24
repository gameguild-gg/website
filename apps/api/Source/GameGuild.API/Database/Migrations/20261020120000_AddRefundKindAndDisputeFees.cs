using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261020120000_AddRefundKindAndDisputeFees")]
    public partial class AddRefundKindAndDisputeFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refund kind (issue #419): how the refund amount was determined — full remaining
            // balance (0, the pre-existing behavior), explicit partial (1) or computed
            // proration (2). Defaults to 0 so existing rows stay "full".
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "refunds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Dispute fee (issue #419): the gateway's chargeback fee recorded from the dispute
            // webhook and expensed at resolution time. Defaults to 0 (no fee reported).
            migrationBuilder.AddColumn<decimal>(
                name: "DisputeFee",
                table: "payment_disputes",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "DisputeFee",
                table: "payment_disputes");
        }
    }
}
