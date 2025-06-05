using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cms.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentTypePermissions_Tenants_TenantId",
                table: "ContentTypePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputations_ReputationLevels_ReputationLevelId",
                table: "UserReputations");

            migrationBuilder.RenameColumn(
                name: "ReputationLevelId",
                table: "UserReputations",
                newName: "ReputationTierId");

            migrationBuilder.RenameIndex(
                name: "IX_UserReputations_ReputationLevelId",
                table: "UserReputations",
                newName: "IX_UserReputations_ReputationTierId");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UserTenantRoles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Tenants",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ResourcePermissions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ResourceMetadata",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ResourceLocalizations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Languages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Token = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsRevoked = table.Column<bool>(type: "INTEGER", nullable: false),
                    RevokedByIp = table.Column<string>(type: "TEXT", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReplacedByToken = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedByIp = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantRoles_TenantId",
                table: "UserTenantRoles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantId",
                table: "Tenants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_TenantId",
                table: "ResourcePermissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMetadata_TenantId",
                table: "ResourceMetadata",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocalizations_TenantId",
                table: "ResourceLocalizations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Languages_TenantId",
                table: "Languages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_CreatedAt",
                table: "RefreshTokens",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_DeletedAt",
                table: "RefreshTokens",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TenantId",
                table: "RefreshTokens",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentTypePermissions_Tenants_TenantId",
                table: "ContentTypePermissions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Languages_Tenants_TenantId",
                table: "Languages",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceLocalizations_Tenants_TenantId",
                table: "ResourceLocalizations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceMetadata_Tenants_TenantId",
                table: "ResourceMetadata",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourcePermissions_Tenants_TenantId",
                table: "ResourcePermissions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Tenants_TenantId",
                table: "Tenants",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputations_ReputationLevels_ReputationTierId",
                table: "UserReputations",
                column: "ReputationTierId",
                principalTable: "ReputationLevels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenantRoles_Tenants_TenantId",
                table: "UserTenantRoles",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentTypePermissions_Tenants_TenantId",
                table: "ContentTypePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Languages_Tenants_TenantId",
                table: "Languages");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceLocalizations_Tenants_TenantId",
                table: "ResourceLocalizations");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceMetadata_Tenants_TenantId",
                table: "ResourceMetadata");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourcePermissions_Tenants_TenantId",
                table: "ResourcePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Tenants_TenantId",
                table: "Tenants");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputations_ReputationLevels_ReputationTierId",
                table: "UserReputations");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenantRoles_Tenants_TenantId",
                table: "UserTenantRoles");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_UserTenantRoles_TenantId",
                table: "UserTenantRoles");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_TenantId",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_ResourcePermissions_TenantId",
                table: "ResourcePermissions");

            migrationBuilder.DropIndex(
                name: "IX_ResourceMetadata_TenantId",
                table: "ResourceMetadata");

            migrationBuilder.DropIndex(
                name: "IX_ResourceLocalizations_TenantId",
                table: "ResourceLocalizations");

            migrationBuilder.DropIndex(
                name: "IX_Languages_TenantId",
                table: "Languages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserTenantRoles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ResourcePermissions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ResourceMetadata");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ResourceLocalizations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Languages");

            migrationBuilder.RenameColumn(
                name: "ReputationTierId",
                table: "UserReputations",
                newName: "ReputationLevelId");

            migrationBuilder.RenameIndex(
                name: "IX_UserReputations_ReputationTierId",
                table: "UserReputations",
                newName: "IX_UserReputations_ReputationLevelId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentTypePermissions_Tenants_TenantId",
                table: "ContentTypePermissions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputations_ReputationLevels_ReputationLevelId",
                table: "UserReputations",
                column: "ReputationLevelId",
                principalTable: "ReputationLevels",
                principalColumn: "Id");
        }
    }
}
