using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceUserTenantWithTenantPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentTypePermissions_UserTenants_UserTenantId",
                table: "ContentTypePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputationHistory_UserTenants_UserTenantId",
                table: "UserReputationHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenantReputations_UserTenants_UserTenantId",
                table: "UserTenantReputations");

            migrationBuilder.DropTable(
                name: "UserTenants");

            migrationBuilder.RenameColumn(
                name: "UserTenantId",
                table: "UserTenantReputations",
                newName: "TenantPermissionId");

            migrationBuilder.RenameIndex(
                name: "IX_UserTenantReputations_UserTenantId",
                table: "UserTenantReputations",
                newName: "IX_UserTenantReputations_TenantPermissionId");

            migrationBuilder.RenameColumn(
                name: "UserTenantId",
                table: "UserReputationHistory",
                newName: "TenantPermissionId");

            migrationBuilder.RenameIndex(
                name: "IX_UserReputationHistory_UserTenantId_OccurredAt",
                table: "UserReputationHistory",
                newName: "IX_UserReputationHistory_TenantPermissionId_OccurredAt");

            migrationBuilder.RenameColumn(
                name: "UserTenantId",
                table: "ContentTypePermissions",
                newName: "TenantPermissionId");

            migrationBuilder.RenameIndex(
                name: "IX_ContentTypePermissions_UserTenantId",
                table: "ContentTypePermissions",
                newName: "IX_ContentTypePermissions_TenantPermissionId");

            migrationBuilder.CreateTable(
                name: "TenantPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    PermissionFlags1 = table.Column<ulong>(type: "bigint", nullable: false),
                    PermissionFlags2 = table.Column<ulong>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantPermissions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenantPermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantPermissions_CreatedAt",
                table: "TenantPermissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPermissions_DeletedAt",
                table: "TenantPermissions",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPermissions_ExpiresAt",
                table: "TenantPermissions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPermissions_IsActive",
                table: "TenantPermissions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPermissions_Status",
                table: "TenantPermissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPermissions_TenantId",
                table: "TenantPermissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPermissions_UserId",
                table: "TenantPermissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPermissions_UserId_TenantId",
                table: "TenantPermissions",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ContentTypePermissions_TenantPermissions_TenantPermissionId",
                table: "ContentTypePermissions",
                column: "TenantPermissionId",
                principalTable: "TenantPermissions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputationHistory_TenantPermissions_TenantPermissionId",
                table: "UserReputationHistory",
                column: "TenantPermissionId",
                principalTable: "TenantPermissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenantReputations_TenantPermissions_TenantPermissionId",
                table: "UserTenantReputations",
                column: "TenantPermissionId",
                principalTable: "TenantPermissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentTypePermissions_TenantPermissions_TenantPermissionId",
                table: "ContentTypePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputationHistory_TenantPermissions_TenantPermissionId",
                table: "UserReputationHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenantReputations_TenantPermissions_TenantPermissionId",
                table: "UserTenantReputations");

            migrationBuilder.DropTable(
                name: "TenantPermissions");

            migrationBuilder.RenameColumn(
                name: "TenantPermissionId",
                table: "UserTenantReputations",
                newName: "UserTenantId");

            migrationBuilder.RenameIndex(
                name: "IX_UserTenantReputations_TenantPermissionId",
                table: "UserTenantReputations",
                newName: "IX_UserTenantReputations_UserTenantId");

            migrationBuilder.RenameColumn(
                name: "TenantPermissionId",
                table: "UserReputationHistory",
                newName: "UserTenantId");

            migrationBuilder.RenameIndex(
                name: "IX_UserReputationHistory_TenantPermissionId_OccurredAt",
                table: "UserReputationHistory",
                newName: "IX_UserReputationHistory_UserTenantId_OccurredAt");

            migrationBuilder.RenameColumn(
                name: "TenantPermissionId",
                table: "ContentTypePermissions",
                newName: "UserTenantId");

            migrationBuilder.RenameIndex(
                name: "IX_ContentTypePermissions_TenantPermissionId",
                table: "ContentTypePermissions",
                newName: "IX_ContentTypePermissions_UserTenantId");

            migrationBuilder.CreateTable(
                name: "UserTenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTenants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTenants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_CreatedAt",
                table: "UserTenants",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_DeletedAt",
                table: "UserTenants",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_TenantId",
                table: "UserTenants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_UserId_TenantId",
                table: "UserTenants",
                columns: new[] { "UserId", "TenantId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentTypePermissions_UserTenants_UserTenantId",
                table: "ContentTypePermissions",
                column: "UserTenantId",
                principalTable: "UserTenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputationHistory_UserTenants_UserTenantId",
                table: "UserReputationHistory",
                column: "UserTenantId",
                principalTable: "UserTenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenantReputations_UserTenants_UserTenantId",
                table: "UserTenantReputations",
                column: "UserTenantId",
                principalTable: "UserTenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
