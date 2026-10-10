using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PermissionAuditLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationType = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceType = table.Column<string>(type: "text", nullable: true),
                    PermissionType = table.Column<string>(type: "text", nullable: true),
                    PermissionDetails = table.Column<string>(type: "text", nullable: true),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    PerformedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionAuditLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionAuditLog_TenantId",
                table: "PermissionAuditLog",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionAuditLog_Timestamp",
                table: "PermissionAuditLog",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionAuditLog_UserId",
                table: "PermissionAuditLog",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionAuditLogs_User_Permission_Timestamp",
                table: "PermissionAuditLog",
                columns: new[] { "UserId", "PermissionType", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermissionAuditLog");
        }
    }
}
