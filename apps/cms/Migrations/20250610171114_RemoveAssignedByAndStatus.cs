using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cms.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAssignedByAndStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentTypePermissions_Tenants_TenantId",
                table: "ContentTypePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_ContentTypePermissions_Users_AssignedByUserId",
                table: "ContentTypePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantPermissions_Users_UserId",
                table: "TenantPermissions");

            migrationBuilder.DropIndex(
                name: "IX_TenantPermissions_IsActive",
                table: "TenantPermissions");

            migrationBuilder.DropIndex(
                name: "IX_TenantPermissions_Status",
                table: "TenantPermissions");

            migrationBuilder.DropIndex(
                name: "IX_TenantPermissions_UserId_TenantId",
                table: "TenantPermissions");

            migrationBuilder.DropIndex(
                name: "IX_ContentTypePermissions_AssignedByUser",
                table: "ContentTypePermissions");

            migrationBuilder.DropIndex(
                name: "IX_ContentTypePermissions_User_Tenant_ContentType",
                table: "ContentTypePermissions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "TenantPermissions");

            migrationBuilder.DropColumn(
                name: "JoinedAt",
                table: "TenantPermissions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "TenantPermissions");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "ContentTypePermissions");

            migrationBuilder.DropColumn(
                name: "AssignedByUserId",
                table: "ContentTypePermissions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ContentTypePermissions");

            migrationBuilder.RenameColumn(
                name: "ContentTypeName",
                table: "ContentTypePermissions",
                newName: "ContentType");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId1",
                table: "TenantPermissions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId1",
                table: "ContentTypePermissions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantPermissions_User_Tenant",
                table: "TenantPermissions",
                columns: new[] { "UserId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantPermissions_UserId1",
                table: "TenantPermissions",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissions_ContentType_Tenant",
                table: "ContentTypePermissions",
                columns: new[] { "ContentType", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissions_ContentType_User_Tenant",
                table: "ContentTypePermissions",
                columns: new[] { "ContentType", "UserId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissions_ExpiresAt",
                table: "ContentTypePermissions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissions_UserId",
                table: "ContentTypePermissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissions_UserId1",
                table: "ContentTypePermissions",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentTypePermissions_Tenants_TenantId",
                table: "ContentTypePermissions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentTypePermissions_Users_UserId1",
                table: "ContentTypePermissions",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TenantPermissions_Users_UserId",
                table: "TenantPermissions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenantPermissions_Users_UserId1",
                table: "TenantPermissions",
                column: "UserId1",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentTypePermissions_Tenants_TenantId",
                table: "ContentTypePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_ContentTypePermissions_Users_UserId1",
                table: "ContentTypePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantPermissions_Users_UserId",
                table: "TenantPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantPermissions_Users_UserId1",
                table: "TenantPermissions");

            migrationBuilder.DropIndex(
                name: "IX_TenantPermissions_User_Tenant",
                table: "TenantPermissions");

            migrationBuilder.DropIndex(
                name: "IX_TenantPermissions_UserId1",
                table: "TenantPermissions");

            migrationBuilder.DropIndex(
                name: "IX_ContentTypePermissions_ContentType_Tenant",
                table: "ContentTypePermissions");

            migrationBuilder.DropIndex(
                name: "IX_ContentTypePermissions_ContentType_User_Tenant",
                table: "ContentTypePermissions");

            migrationBuilder.DropIndex(
                name: "IX_ContentTypePermissions_ExpiresAt",
                table: "ContentTypePermissions");

            migrationBuilder.DropIndex(
                name: "IX_ContentTypePermissions_UserId",
                table: "ContentTypePermissions");

            migrationBuilder.DropIndex(
                name: "IX_ContentTypePermissions_UserId1",
                table: "ContentTypePermissions");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "TenantPermissions");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "ContentTypePermissions");

            migrationBuilder.RenameColumn(
                name: "ContentType",
                table: "ContentTypePermissions",
                newName: "ContentTypeName");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TenantPermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "JoinedAt",
                table: "TenantPermissions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "TenantPermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "ContentTypePermissions",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedByUserId",
                table: "ContentTypePermissions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ContentTypePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantPermissions_IsActive",
                table: "TenantPermissions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPermissions_Status",
                table: "TenantPermissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPermissions_UserId_TenantId",
                table: "TenantPermissions",
                columns: new[] { "UserId", "TenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissions_AssignedByUser",
                table: "ContentTypePermissions",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissions_User_Tenant_ContentType",
                table: "ContentTypePermissions",
                columns: new[] { "UserId", "TenantId", "ContentTypeName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentTypePermissions_Tenants_TenantId",
                table: "ContentTypePermissions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentTypePermissions_Users_AssignedByUserId",
                table: "ContentTypePermissions",
                column: "AssignedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TenantPermissions_Users_UserId",
                table: "TenantPermissions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
