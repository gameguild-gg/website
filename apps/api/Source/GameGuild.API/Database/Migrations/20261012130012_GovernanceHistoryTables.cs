using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class GovernanceHistoryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PermissionApprovalWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentStageIndex = table.Column<int>(type: "integer", nullable: false),
                    TotalStages = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionApprovalWorkflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PolicyVersionHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ChangeType = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    PolicyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ChangedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyVersionHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermissionApprovalStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageIndex = table.Column<int>(type: "integer", nullable: false),
                    StageName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequiredApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionApprovalStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PermissionApprovalStages_PermissionApprovalWorkflows_Workfl~",
                        column: x => x.WorkflowId,
                        principalTable: "PermissionApprovalWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionApprovalStages_Workflow_Index",
                table: "PermissionApprovalStages",
                columns: new[] { "WorkflowId", "StageIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionApprovalWorkflows_Subject",
                table: "PermissionApprovalWorkflows",
                column: "SubjectUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionApprovalWorkflows_Tenant_Status",
                table: "PermissionApprovalWorkflows",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PolicyVersionHistory_Policy_Version",
                table: "PolicyVersionHistory",
                columns: new[] { "PolicyId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PolicyVersionHistory_PolicyId",
                table: "PolicyVersionHistory",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyVersionHistory_RecordedAt",
                table: "PolicyVersionHistory",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyVersionHistory_Tenant_Time",
                table: "PolicyVersionHistory",
                columns: new[] { "TenantId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermissionApprovalStages");

            migrationBuilder.DropTable(
                name: "PolicyVersionHistory");

            migrationBuilder.DropTable(
                name: "PermissionApprovalWorkflows");
        }
    }
}
