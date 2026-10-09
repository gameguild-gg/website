using GameGuild.API.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260810103000_PreserveImmutableFundingProvenance")]
public partial class PreserveImmutableFundingProvenance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        InstallImmutableFundingConfirmation(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep the funding provenance guards installed during rollback;
        // reverting them would allow mutation of authoritative funding evidence.
    }
}
