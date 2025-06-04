using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cms.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPermissionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserTenantPermissions");

            migrationBuilder.AddColumn<Guid>(
                name: "UserTenantId",
                table: "ContentTypePermissions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissions_UserTenantId",
                table: "ContentTypePermissions",
                column: "UserTenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentTypePermissions_UserTenants_UserTenantId",
                table: "ContentTypePermissions",
                column: "UserTenantId",
                principalTable: "UserTenants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentTypePermissions_UserTenants_UserTenantId",
                table: "ContentTypePermissions");

            migrationBuilder.DropIndex(
                name: "IX_ContentTypePermissions_UserTenantId",
                table: "ContentTypePermissions");

            migrationBuilder.DropColumn(
                name: "UserTenantId",
                table: "ContentTypePermissions");

            migrationBuilder.CreateTable(
                name: "UserTenantPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    AssignedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserTenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Permissions = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "INTEGER", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTenantPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTenantPermissions_UserTenants_UserTenantId",
                        column: x => x.UserTenantId,
                        principalTable: "UserTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTenantPermissions_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantPermissions_AssignedByUserId",
                table: "UserTenantPermissions",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantPermissions_CreatedAt",
                table: "UserTenantPermissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantPermissions_DeletedAt",
                table: "UserTenantPermissions",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantPermissions_Permissions",
                table: "UserTenantPermissions",
                column: "Permissions");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantPermissions_UserTenantId",
                table: "UserTenantPermissions",
                column: "UserTenantId");
        }
    }
}
