using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledAuditExports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduledAuditExports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CronExpression = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Timezone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NextRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DestinationType = table.Column<int>(type: "integer", nullable: false),
                    DestinationUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    DestinationPath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CredentialKeyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExportFormat = table.Column<int>(type: "integer", nullable: false),
                    ComplianceFramework = table.Column<int>(type: "integer", nullable: true),
                    ExportTemplate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IncludeEventTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    ExcludeEventTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    CsvColumns = table.Column<string[]>(type: "text[]", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RiskLevelFilter = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserIdFilter = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    EncryptExport = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptionKeyId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SignExport = table.Column<bool>(type: "boolean", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    LastSuccessAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastFailureAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NotifyOnSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnFailure = table.Column<bool>(type: "boolean", nullable: false),
                    NotificationEmails = table.Column<string[]>(type: "text[]", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledAuditExports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditExportHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledExportId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RecordCount = table.Column<int>(type: "integer", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ExportPath = table.Column<string>(type: "text", nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FileChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExecutionDuration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditExportHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditExportHistories_ScheduledAuditExports_ScheduledExportId",
                        column: x => x.ScheduledExportId,
                        principalTable: "ScheduledAuditExports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditExportHistories_ScheduledExportId_ExecutedAt",
                table: "AuditExportHistories",
                columns: new[] { "ScheduledExportId", "ExecutedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditExportHistories_TenantId_ExecutedAt",
                table: "AuditExportHistories",
                columns: new[] { "TenantId", "ExecutedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledAuditExports_IsEnabled_NextRunAt",
                table: "ScheduledAuditExports",
                columns: new[] { "IsEnabled", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledAuditExports_TenantId_JobName",
                table: "ScheduledAuditExports",
                columns: new[] { "TenantId", "JobName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditExportHistories");

            migrationBuilder.DropTable(
                name: "ScheduledAuditExports");
        }
    }
}
