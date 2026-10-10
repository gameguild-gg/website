using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalLoginScopeConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsentVersion",
                schema: "gameguild.authentication",
                table: "externallogin",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentedAt",
                schema: "gameguild.authentication",
                table: "externallogin",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrantedScopes",
                schema: "gameguild.authentication",
                table: "externallogin",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsentVersion",
                schema: "gameguild.authentication",
                table: "externallogin");

            migrationBuilder.DropColumn(
                name: "ConsentedAt",
                schema: "gameguild.authentication",
                table: "externallogin");

            migrationBuilder.DropColumn(
                name: "GrantedScopes",
                schema: "gameguild.authentication",
                table: "externallogin");
        }
    }
}
