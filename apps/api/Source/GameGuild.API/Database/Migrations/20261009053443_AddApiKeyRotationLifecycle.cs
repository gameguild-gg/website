using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyRotationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "replaces_key_id",
                schema: "gameguild.authentication",
                table: "api_keys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "rotation_grace_ends_at",
                schema: "gameguild.authentication",
                table: "api_keys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_api_keys_replaces_key_id",
                schema: "gameguild.authentication",
                table: "api_keys",
                column: "replaces_key_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_api_keys_replaces_key_id",
                schema: "gameguild.authentication",
                table: "api_keys");

            migrationBuilder.DropColumn(
                name: "replaces_key_id",
                schema: "gameguild.authentication",
                table: "api_keys");

            migrationBuilder.DropColumn(
                name: "rotation_grace_ends_at",
                schema: "gameguild.authentication",
                table: "api_keys");
        }
    }
}
