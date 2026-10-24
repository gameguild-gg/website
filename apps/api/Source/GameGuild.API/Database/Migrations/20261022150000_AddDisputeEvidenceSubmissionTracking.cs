using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261022150000_AddDisputeEvidenceSubmissionTracking")]
    public partial class AddDisputeEvidenceSubmissionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Evidence-bundle submission tracking (issue #419): when the merchant evidence bundle
            // was last submitted to the gateway. Null means nothing was submitted yet.
            migrationBuilder.AddColumn<DateTime>(
                name: "EvidenceSubmittedAt",
                table: "payment_disputes",
                type: "timestamp with time zone",
                nullable: true);

            // Per-evidence submission status (issue #419): when each evidence row was included in
            // a bundle successfully delivered to the gateway; the first submission wins.
            migrationBuilder.AddColumn<DateTime>(
                name: "GatewaySubmittedAt",
                table: "dispute_evidence",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvidenceSubmittedAt",
                table: "payment_disputes");

            migrationBuilder.DropColumn(
                name: "GatewaySubmittedAt",
                table: "dispute_evidence");
        }
    }
}
