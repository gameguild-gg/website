using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class BindLimitedMfaEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve every existing factor: ambiguous accounts require explicit reconciliation.
            migrationBuilder.Sql("""
                DO $enrollment_preflight$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "gameguild.authentication"."user_mfa_configuration"
                               GROUP BY user_id HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Please reconcile duplicate MFA configurations before applying limited enrollment.'
                            USING ERRCODE = '23505';
                    END IF;
                END $enrollment_preflight$;
                """);

            migrationBuilder.DropIndex(
                name: "ix_user_mfa_configuration_user_id",
                schema: "gameguild.authentication",
                table: "user_mfa_configuration");

            migrationBuilder.AddColumn<Guid>(
                name: "enrollment_configuration_id",
                schema: "gameguild.authentication",
                table: "sign_in_mfa_challenges",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "enrollment_initialized_at",
                schema: "gameguild.authentication",
                table: "sign_in_mfa_challenges",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "enrollment_secret_fingerprint",
                schema: "gameguild.authentication",
                table: "sign_in_mfa_challenges",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_mfa_configuration_user_id",
                schema: "gameguild.authentication",
                table: "user_mfa_configuration",
                column: "user_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_sign_in_mfa_enrollment_binding",
                schema: "gameguild.authentication",
                table: "sign_in_mfa_challenges",
                sql: "(enrollment_configuration_id IS NULL AND enrollment_secret_fingerprint IS NULL AND enrollment_initialized_at IS NULL)\nOR (purpose = 'EnrollFactor' AND enrollment_configuration_id IS NOT NULL\n    AND enrollment_configuration_id <> '00000000-0000-0000-0000-000000000000'::uuid\n    AND enrollment_secret_fingerprint IS NOT NULL AND enrollment_secret_fingerprint ~ '^[a-f0-9]{64}$'\n    AND enrollment_initialized_at IS NOT NULL AND enrollment_initialized_at >= created_at AND enrollment_initialized_at < expires_at)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_mfa_configuration_user_id",
                schema: "gameguild.authentication",
                table: "user_mfa_configuration");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sign_in_mfa_enrollment_binding",
                schema: "gameguild.authentication",
                table: "sign_in_mfa_challenges");

            migrationBuilder.DropColumn(
                name: "enrollment_configuration_id",
                schema: "gameguild.authentication",
                table: "sign_in_mfa_challenges");

            migrationBuilder.DropColumn(
                name: "enrollment_initialized_at",
                schema: "gameguild.authentication",
                table: "sign_in_mfa_challenges");

            migrationBuilder.DropColumn(
                name: "enrollment_secret_fingerprint",
                schema: "gameguild.authentication",
                table: "sign_in_mfa_challenges");

            migrationBuilder.CreateIndex(
                name: "ix_user_mfa_configuration_user_id",
                schema: "gameguild.authentication",
                table: "user_mfa_configuration",
                column: "user_id");
        }
    }
}
