using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentTokenId",
                schema: "gameguild.authentication",
                table: "refreshtoken",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                schema: "gameguild.authentication",
                table: "refreshtoken",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_refreshtoken_parent_token_id",
                schema: "gameguild.authentication",
                table: "refreshtoken",
                column: "ParentTokenId");

            migrationBuilder.CreateIndex(
                name: "ix_refreshtoken_session_id",
                schema: "gameguild.authentication",
                table: "refreshtoken",
                column: "SessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_refreshtoken_refreshtoken_ParentTokenId",
                schema: "gameguild.authentication",
                table: "refreshtoken",
                column: "ParentTokenId",
                principalSchema: "gameguild.authentication",
                principalTable: "refreshtoken",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_refreshtoken_usersession_SessionId",
                schema: "gameguild.authentication",
                table: "refreshtoken",
                column: "SessionId",
                principalSchema: "gameguild.authentication",
                principalTable: "usersession",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_refreshtoken_refreshtoken_ParentTokenId",
                schema: "gameguild.authentication",
                table: "refreshtoken");

            migrationBuilder.DropForeignKey(
                name: "FK_refreshtoken_usersession_SessionId",
                schema: "gameguild.authentication",
                table: "refreshtoken");

            migrationBuilder.DropIndex(
                name: "ix_refreshtoken_parent_token_id",
                schema: "gameguild.authentication",
                table: "refreshtoken");

            migrationBuilder.DropIndex(
                name: "ix_refreshtoken_session_id",
                schema: "gameguild.authentication",
                table: "refreshtoken");

            migrationBuilder.DropColumn(
                name: "ParentTokenId",
                schema: "gameguild.authentication",
                table: "refreshtoken");

            migrationBuilder.DropColumn(
                name: "SessionId",
                schema: "gameguild.authentication",
                table: "refreshtoken");
        }
    }
}
