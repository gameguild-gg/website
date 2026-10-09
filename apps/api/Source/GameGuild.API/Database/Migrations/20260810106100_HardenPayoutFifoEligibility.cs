using GameGuild.API.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260810106100_HardenPayoutFifoEligibility")]
public partial class HardenPayoutFifoEligibility : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        InstallHardenedPayoutFifoEligibility(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep the hardened eligibility checks installed during rollback;
        // restoring the previous function would weaken payout reservation rules.
    }
}
