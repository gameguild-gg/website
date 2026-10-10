using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRevenueAnomalyAlertCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing alerts predate the per-currency series split; backfill them with
            // the platform default currency (RevenueEvent.Currency also defaults to USD).
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "revenue_anomaly_alerts",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "revenue_anomaly_alerts");
        }
    }
}
