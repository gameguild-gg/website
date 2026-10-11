using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionMfaEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "session_mfa_evidence",
                schema: "gameguild.authentication",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    challenge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_version = table.Column<int>(type: "integer", nullable: false),
                    policy_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    first_factor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    first_factor_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_mfa_evidence", x => x.session_id);
                    table.CheckConstraint("ck_session_mfa_method", "method IN ('Totp', 'BackupCode', 'WebAuthn')");
                    table.CheckConstraint("ck_session_mfa_policy", "policy_fingerprint ~ '^[a-f0-9]{64}$'");
                    table.CheckConstraint("ck_session_mfa_time", "verified_at >= first_factor_verified_at AND verified_at <= first_factor_verified_at + INTERVAL '5 minutes'");
                    table.CheckConstraint("ck_session_mfa_version", "token_version > 0");
                    table.ForeignKey(
                        name: "FK_session_mfa_evidence_usersession_session_id",
                        column: x => x.session_id,
                        principalSchema: "gameguild.authentication",
                        principalTable: "usersession",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_session_mfa_evidence_challenge",
                schema: "gameguild.authentication",
                table: "session_mfa_evidence",
                column: "challenge_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_mfa_evidence",
                schema: "gameguild.authentication");
        }
    }
}
