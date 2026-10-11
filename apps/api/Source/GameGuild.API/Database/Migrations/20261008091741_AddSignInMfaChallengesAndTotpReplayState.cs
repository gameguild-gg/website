using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSignInMfaChallengesAndTotpReplayState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sign_in_mfa_challenges",
                schema: "gameguild.authentication",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_token_version = table.Column<int>(type: "integer", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    policy_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    purpose = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    first_factor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verification_method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sign_in_mfa_challenges", x => x.id);
                    table.CheckConstraint("ck_sign_in_mfa_lifetime", "expires_at > created_at AND expires_at <= created_at + INTERVAL '5 minutes'");
                    table.CheckConstraint("ck_sign_in_mfa_policy_hash", "policy_fingerprint ~ '^[a-f0-9]{64}$'");
                    table.CheckConstraint("ck_sign_in_mfa_subject_version", "subject_token_version > 0");
                    table.CheckConstraint("ck_sign_in_mfa_token_hash", "token_hash ~ '^[a-f0-9]{64}$'");
                });

            migrationBuilder.CreateTable(
                name: "totp_replay_state",
                schema: "gameguild.authentication",
                columns: table => new
                {
                    configuration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    secret_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_accepted_step = table.Column<long>(type: "bigint", nullable: false),
                    last_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_totp_replay_state", x => new { x.configuration_id, x.secret_fingerprint });
                    table.CheckConstraint("ck_totp_replay_state_nonnegative_step", "last_accepted_step >= 0");
                    table.ForeignKey(
                        name: "FK_totp_replay_state_user_mfa_configuration_configuration_id",
                        column: x => x.configuration_id,
                        principalSchema: "gameguild.authentication",
                        principalTable: "user_mfa_configuration",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sign_in_mfa_challenges_subject_expiry",
                schema: "gameguild.authentication",
                table: "sign_in_mfa_challenges",
                columns: new[] { "subject_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_sign_in_mfa_challenges_token_hash",
                schema: "gameguild.authentication",
                table: "sign_in_mfa_challenges",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sign_in_mfa_challenges",
                schema: "gameguild.authentication");

            migrationBuilder.DropTable(
                name: "totp_replay_state",
                schema: "gameguild.authentication");
        }
    }
}
