using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.Migrations
{
    /// <inheritdoc />
    public partial class PendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comment_Users_OwnerId",
                table: "Comment");

            migrationBuilder.DropForeignKey(
                name: "FK_ContentLicenses_Users_OwnerId",
                table: "ContentLicenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Users_OwnerId",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_programs_Users_OwnerId",
                table: "programs");

            migrationBuilder.DropForeignKey(
                name: "FK_ReputationActions_Users_OwnerId",
                table: "ReputationActions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReputationLevels_Users_OwnerId",
                table: "ReputationLevels");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputationHistory_Users_OwnerId",
                table: "UserReputationHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputations_Users_OwnerId",
                table: "UserReputations");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenantReputations_Users_OwnerId",
                table: "UserTenantReputations");

            migrationBuilder.DropIndex(
                name: "IX_UserTenantReputations_OwnerId",
                table: "UserTenantReputations");

            migrationBuilder.DropIndex(
                name: "IX_UserReputations_OwnerId",
                table: "UserReputations");

            migrationBuilder.DropIndex(
                name: "IX_UserReputationHistory_OwnerId",
                table: "UserReputationHistory");

            migrationBuilder.DropIndex(
                name: "IX_ReputationLevels_OwnerId",
                table: "ReputationLevels");

            migrationBuilder.DropIndex(
                name: "IX_ReputationActions_OwnerId",
                table: "ReputationActions");

            migrationBuilder.DropIndex(
                name: "IX_programs_OwnerId",
                table: "programs");

            migrationBuilder.DropIndex(
                name: "IX_Products_OwnerId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ContentLicenses_OwnerId",
                table: "ContentLicenses");

            migrationBuilder.DropIndex(
                name: "IX_Comment_OwnerId",
                table: "Comment");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "UserTenantReputations");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "UserReputations");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "UserReputationHistory");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "ReputationLevels");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "ReputationActions");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "programs");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "ContentLicenses");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Comment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "UserTenantReputations",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "UserReputations",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "UserReputationHistory",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "ReputationLevels",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "ReputationActions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "programs",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Products",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "ContentLicenses",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Comment",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_OwnerId",
                table: "UserTenantReputations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_OwnerId",
                table: "UserReputations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_OwnerId",
                table: "UserReputationHistory",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_OwnerId",
                table: "ReputationLevels",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_OwnerId",
                table: "ReputationActions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_programs_OwnerId",
                table: "programs",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_OwnerId",
                table: "Products",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentLicenses_OwnerId",
                table: "ContentLicenses",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Comment_OwnerId",
                table: "Comment",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Comment_Users_OwnerId",
                table: "Comment",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentLicenses_Users_OwnerId",
                table: "ContentLicenses",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Users_OwnerId",
                table: "Products",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_programs_Users_OwnerId",
                table: "programs",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReputationActions_Users_OwnerId",
                table: "ReputationActions",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReputationLevels_Users_OwnerId",
                table: "ReputationLevels",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputationHistory_Users_OwnerId",
                table: "UserReputationHistory",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputations_Users_OwnerId",
                table: "UserReputations",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenantReputations_Users_OwnerId",
                table: "UserTenantReputations",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
