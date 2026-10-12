using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AuditRetentionSimulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditDataAccessObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordDateUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    ReadCount = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditDataAccessObservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditRetentionConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRetentionConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditRetentionSimulationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationRevision = table.Column<int>(type: "integer", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "text", nullable: false),
                    RequestJson = table.Column<string>(type: "text", nullable: false),
                    ReportJson = table.Column<string>(type: "text", nullable: false),
                    ModelVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ForecastMonths = table.Column<int>(type: "integer", nullable: false),
                    BaselineTotalCost = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: false),
                    RecommendedTotalCost = table.Column<decimal>(type: "numeric(38,8)", precision: 38, scale: 8, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRetentionSimulationRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditDataAccessObservations_TenantId_CreatedAt",
                table: "AuditDataAccessObservations",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRetentionConfigurations_TenantId",
                table: "AuditRetentionConfigurations",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditRetentionSimulationRuns_TenantId_CreatedAt",
                table: "AuditRetentionSimulationRuns",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditDataAccessObservations");

            migrationBuilder.DropTable(
                name: "AuditRetentionConfigurations");

            migrationBuilder.DropTable(
                name: "AuditRetentionSimulationRuns");
        }
    }
}
