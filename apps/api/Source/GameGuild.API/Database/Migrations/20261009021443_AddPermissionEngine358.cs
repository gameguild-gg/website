using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionEngine358 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid[]>(
                name: "AdditionalParentRoleIds",
                table: "DynamicRole",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "BlockedInheritedPermissions",
                table: "DynamicRole",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.CreateTable(
                name: "PermissionEvaluationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequiredPermissions = table.Column<string[]>(type: "text[]", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Operation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionEvaluationLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionEvaluationLogs_Outcome",
                table: "PermissionEvaluationLogs",
                column: "Outcome");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionEvaluationLogs_Tenant_Time",
                table: "PermissionEvaluationLogs",
                columns: new[] { "TenantId", "EvaluatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionEvaluationLogs_Time",
                table: "PermissionEvaluationLogs",
                column: "EvaluatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermissionEvaluationLogs");

            migrationBuilder.DropColumn(
                name: "AdditionalParentRoleIds",
                table: "DynamicRole");

            migrationBuilder.DropColumn(
                name: "BlockedInheritedPermissions",
                table: "DynamicRole");
        }
    }
}
