using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditArchiveTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArchiveAfterDays",
                table: "SecurityLogRetentionPolicies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchivedCount",
                table: "SecurityLogRetentionExecutions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ArchivedAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceAuditLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OriginalCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_entity_change_audit_tenant_time",
                schema: "gameguild.audit",
                table: "entity_change_audit_records",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedAuditLogs_TenantId_OriginalCreatedAt",
                table: "ArchivedAuditLogs",
                columns: new[] { "TenantId", "OriginalCreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedAuditLogs_TenantId_SourceAuditLogId",
                table: "ArchivedAuditLogs",
                columns: new[] { "TenantId", "SourceAuditLogId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivedAuditLogs");

            migrationBuilder.DropIndex(
                name: "ix_entity_change_audit_tenant_time",
                schema: "gameguild.audit",
                table: "entity_change_audit_records");

            migrationBuilder.DropColumn(
                name: "ArchiveAfterDays",
                table: "SecurityLogRetentionPolicies");

            migrationBuilder.DropColumn(
                name: "ArchivedCount",
                table: "SecurityLogRetentionExecutions");
        }
    }
}
