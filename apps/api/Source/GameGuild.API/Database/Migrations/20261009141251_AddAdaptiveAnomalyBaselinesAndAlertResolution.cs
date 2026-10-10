using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAdaptiveAnomalyBaselinesAndAlertResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResolutionNotes",
                table: "SecurityAlerts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAtUtc",
                table: "SecurityAlerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResolvedByUserId",
                table: "SecurityAlerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "adaptivebehaviorbaseline",
                schema: "gameguild.authentication",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubjectKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    HourMeanX = table.Column<double>(type: "double precision", nullable: false),
                    HourMeanY = table.Column<double>(type: "double precision", nullable: false),
                    HourMeanSquaredDeviation = table.Column<double>(type: "double precision", nullable: false),
                    IpSurpriseMean = table.Column<double>(type: "double precision", nullable: false),
                    IpSurpriseMeanSquaredDeviation = table.Column<double>(type: "double precision", nullable: false),
                    IpWeightsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CadenceLogSecondsMean = table.Column<double>(type: "double precision", nullable: false),
                    CadenceLogSecondsMeanSquaredDeviation = table.Column<double>(type: "double precision", nullable: false),
                    CadenceObservationCount = table.Column<int>(type: "integer", nullable: false),
                    ObservationCount = table.Column<int>(type: "integer", nullable: false),
                    LastObservedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adaptivebehaviorbaseline", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_adaptivebehaviorbaseline_last_observed_at",
                schema: "gameguild.authentication",
                table: "adaptivebehaviorbaseline",
                column: "LastObservedAtUtc");

            migrationBuilder.CreateIndex(
                name: "ix_adaptivebehaviorbaseline_subject_key",
                schema: "gameguild.authentication",
                table: "adaptivebehaviorbaseline",
                column: "SubjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_adaptivebehaviorbaseline_tenant_id",
                schema: "gameguild.authentication",
                table: "adaptivebehaviorbaseline",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adaptivebehaviorbaseline",
                schema: "gameguild.authentication");

            migrationBuilder.DropColumn(
                name: "ResolutionNotes",
                table: "SecurityAlerts");

            migrationBuilder.DropColumn(
                name: "ResolvedAtUtc",
                table: "SecurityAlerts");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "SecurityAlerts");
        }
    }
}
