using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaSmsPushAndForensicsGeoColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                schema: "gameguild.authentication",
                table: "authenticationattempt",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                schema: "gameguild.authentication",
                table: "authenticationattempt",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                schema: "gameguild.authentication",
                table: "authenticationattempt");

            migrationBuilder.DropColumn(
                name: "Country",
                schema: "gameguild.authentication",
                table: "authenticationattempt");
        }
    }
}
