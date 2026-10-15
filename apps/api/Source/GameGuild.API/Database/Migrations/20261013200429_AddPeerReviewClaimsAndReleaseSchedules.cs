using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPeerReviewClaimsAndReleaseSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EvidenceDeadlineAt",
                table: "ReviewStages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GradeResultReleaseSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GradeRoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ScheduledByActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScheduledByService = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CancelledByActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeResultReleaseSchedules", x => x.Id);
                    table.CheckConstraint("CK_GradeResultReleaseSchedules_Cancellation", "(\"Status\" = 'cancelled') = (\"CancelledByActorId\" IS NOT NULL AND \"CancellationReason\" IS NOT NULL AND \"CancelledAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_GradeResultReleaseSchedules_Producer", "num_nonnulls(\"ScheduledByActorId\", \"ScheduledByService\") = 1");
                    table.CheckConstraint("CK_GradeResultReleaseSchedules_Release", "(\"Status\" = 'released') = (\"ReleasedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_GradeResultReleaseSchedules_Status", "\"Status\" IN ('scheduled', 'cancelled', 'released')");
                    table.ForeignKey(
                        name: "FK_GradeResultReleaseSchedules_GradeRounds_GradeRoundId",
                        column: x => x.GradeRoundId,
                        principalTable: "GradeRounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeerReviewClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewStageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewerTestRunSubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewEvidenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeaseExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeerReviewClaims", x => x.Id);
                    table.CheckConstraint("CK_PeerReviewClaims_Lease", "\"LeaseExpiresAt\" > \"ClaimedAt\"");
                    table.CheckConstraint("CK_PeerReviewClaims_Reviewer", "num_nonnulls(\"ReviewerUserId\", \"ReviewerTestRunSubjectId\") = 1");
                    table.CheckConstraint("CK_PeerReviewClaims_Status", "\"Status\" IN ('assigned', 'submitted', 'expired', 'cancelled')");
                    table.CheckConstraint("CK_PeerReviewClaims_Submission", "(\"Status\" = 'submitted') = (\"ReviewEvidenceId\" IS NOT NULL AND \"SubmittedAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PeerReviewClaims_AssessmentTestRunSubjects_ReviewerTestRunS~",
                        column: x => x.ReviewerTestRunSubjectId,
                        principalTable: "AssessmentTestRunSubjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeerReviewClaims_ReviewEvidence_ReviewEvidenceId",
                        column: x => x.ReviewEvidenceId,
                        principalTable: "ReviewEvidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeerReviewClaims_ReviewStages_ReviewStageId",
                        column: x => x.ReviewStageId,
                        principalTable: "ReviewStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewStages_Status_EvidenceDeadlineAt",
                table: "ReviewStages",
                columns: new[] { "Status", "EvidenceDeadlineAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GradeResultReleaseSchedules_GradeRoundId",
                table: "GradeResultReleaseSchedules",
                column: "GradeRoundId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradeResultReleaseSchedules_Status_ScheduledFor",
                table: "GradeResultReleaseSchedules",
                columns: new[] { "Status", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_PeerReviewClaims_ReviewerTestRunSubjectId",
                table: "PeerReviewClaims",
                column: "ReviewerTestRunSubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PeerReviewClaims_ReviewEvidenceId",
                table: "PeerReviewClaims",
                column: "ReviewEvidenceId",
                unique: true,
                filter: "\"ReviewEvidenceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PeerReviewClaims_ReviewStageId_ReviewerTestRunSubjectId",
                table: "PeerReviewClaims",
                columns: new[] { "ReviewStageId", "ReviewerTestRunSubjectId" },
                unique: true,
                filter: "\"ReviewerTestRunSubjectId\" IS NOT NULL AND \"Status\" IN ('assigned', 'submitted')");

            migrationBuilder.CreateIndex(
                name: "IX_PeerReviewClaims_ReviewStageId_ReviewerUserId",
                table: "PeerReviewClaims",
                columns: new[] { "ReviewStageId", "ReviewerUserId" },
                unique: true,
                filter: "\"ReviewerUserId\" IS NOT NULL AND \"Status\" IN ('assigned', 'submitted')");

            migrationBuilder.CreateIndex(
                name: "IX_PeerReviewClaims_Status_LeaseExpiresAt",
                table: "PeerReviewClaims",
                columns: new[] { "Status", "LeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GradeResultReleaseSchedules");

            migrationBuilder.DropTable(
                name: "PeerReviewClaims");

            migrationBuilder.DropIndex(
                name: "IX_ReviewStages_Status_EvidenceDeadlineAt",
                table: "ReviewStages");

            migrationBuilder.DropColumn(
                name: "EvidenceDeadlineAt",
                table: "ReviewStages");
        }
    }
}
