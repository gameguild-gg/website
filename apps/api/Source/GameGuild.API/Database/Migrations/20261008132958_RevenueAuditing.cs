using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class RevenueAuditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "revenue_anomaly_alerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    DetectedForDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ObservedNetRevenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ExpectedNetRevenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ZScore = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    BaselineDays = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcknowledgementNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revenue_anomaly_alerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "revenue_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Metadata = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ProcessingNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revenue_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "revenue_reconciliation_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalStatementId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StatementLineCount = table.Column<int>(type: "integer", nullable: false),
                    MatchedCount = table.Column<int>(type: "integer", nullable: false),
                    DiscrepancyCount = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SummaryJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InitiatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revenue_reconciliation_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "revenue_reconciliation_discrepancies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ExternalCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    ExternalOccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevenueEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    InternalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    InternalCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revenue_reconciliation_discrepancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_revenue_reconciliation_discrepancies_revenue_reconciliation~",
                        column: x => x.RunId,
                        principalTable: "revenue_reconciliation_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_revenue_anomaly_alerts_DetectedAtUtc",
                table: "revenue_anomaly_alerts",
                column: "DetectedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_anomaly_alerts_DetectedForDateUtc",
                table: "revenue_anomaly_alerts",
                column: "DetectedForDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_anomaly_alerts_Kind",
                table: "revenue_anomaly_alerts",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_anomaly_alerts_Status",
                table: "revenue_anomaly_alerts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_events_EventType",
                table: "revenue_events",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_events_ReferenceId",
                table: "revenue_events",
                column: "ReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_events_Source",
                table: "revenue_events",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_events_Status",
                table: "revenue_events",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_events_Timestamp",
                table: "revenue_events",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_events_UserId",
                table: "revenue_events",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_reconciliation_discrepancies_ExternalReference",
                table: "revenue_reconciliation_discrepancies",
                column: "ExternalReference");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_reconciliation_discrepancies_Kind",
                table: "revenue_reconciliation_discrepancies",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_reconciliation_discrepancies_RunId",
                table: "revenue_reconciliation_discrepancies",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_reconciliation_runs_CompletedAtUtc",
                table: "revenue_reconciliation_runs",
                column: "CompletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_reconciliation_runs_PeriodEndUtc",
                table: "revenue_reconciliation_runs",
                column: "PeriodEndUtc");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_reconciliation_runs_PeriodStartUtc",
                table: "revenue_reconciliation_runs",
                column: "PeriodStartUtc");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_reconciliation_runs_Status",
                table: "revenue_reconciliation_runs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "revenue_anomaly_alerts");

            migrationBuilder.DropTable(
                name: "revenue_events");

            migrationBuilder.DropTable(
                name: "revenue_reconciliation_discrepancies");

            migrationBuilder.DropTable(
                name: "revenue_reconciliation_runs");
        }
    }
}
