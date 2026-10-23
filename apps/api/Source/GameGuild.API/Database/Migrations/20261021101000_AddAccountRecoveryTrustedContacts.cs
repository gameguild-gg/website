using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountRecoveryTrustedContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trusted_contacts",
                schema: "gameguild.authentication",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    relation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    invite_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    invite_token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trusted_contacts", x => x.id);
                    table.CheckConstraint("ck_trusted_contacts_status", "status IN ('Invited', 'Confirmed', 'Revoked')");
                    table.CheckConstraint("ck_trusted_contacts_invite_token", "(invite_token_hash IS NULL OR invite_token_hash ~ '^[a-f0-9]{64}$')");
                });

            migrationBuilder.CreateTable(
                name: "account_recovery_requests",
                schema: "gameguild.authentication",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cooldown_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approval_window_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    required_approvals = table.Column<int>(type: "integer", nullable: false),
                    reset_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    reset_token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reset_unlocked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_recovery_requests", x => x.id);
                    table.CheckConstraint("ck_account_recovery_status", "status IN ('PendingApprovals', 'ResetUnlocked', 'Completed', 'Cancelled', 'Expired')");
                    table.CheckConstraint("ck_account_recovery_cooldown", "cooldown_ends_at >= requested_at");
                    table.CheckConstraint("ck_account_recovery_window", "approval_window_ends_at >= cooldown_ends_at");
                    table.CheckConstraint("ck_account_recovery_required_approvals", "required_approvals >= 1");
                    table.CheckConstraint("ck_account_recovery_reset_token", "(reset_token_hash IS NULL OR reset_token_hash ~ '^[a-f0-9]{64}$')");
                });

            migrationBuilder.CreateTable(
                name: "recovery_approvals",
                schema: "gameguild.authentication",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recovery_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    factor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    trusted_contact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approval_code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    webauthn_credential_id = table.Column<Guid>(type: "uuid", nullable: true),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approver_ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    approver_user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recovery_approvals", x => x.id);
                    table.CheckConstraint("ck_recovery_approvals_factor", "factor IN ('TrustedContact', 'Passkey')");
                    table.CheckConstraint("ck_recovery_approvals_factor_target", "(factor = 'Passkey' AND webauthn_credential_id IS NOT NULL AND approval_code_hash IS NULL AND trusted_contact_id IS NULL)\nOR (factor = 'TrustedContact' AND trusted_contact_id IS NOT NULL AND approval_code_hash IS NOT NULL AND webauthn_credential_id IS NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_trusted_contacts_owner_status",
                schema: "gameguild.authentication",
                table: "trusted_contacts",
                columns: new[] { "owner_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_trusted_contacts_tenant",
                schema: "gameguild.authentication",
                table: "trusted_contacts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_trusted_contacts_active_owner_email",
                schema: "gameguild.authentication",
                table: "trusted_contacts",
                columns: new[] { "owner_user_id", "email" },
                unique: true,
                filter: "status IN ('Invited', 'Confirmed')");

            migrationBuilder.CreateIndex(
                name: "ix_account_recovery_requests_tenant_status",
                schema: "gameguild.authentication",
                table: "account_recovery_requests",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_account_recovery_requests_active_owner",
                schema: "gameguild.authentication",
                table: "account_recovery_requests",
                column: "owner_user_id",
                unique: true,
                filter: "status IN ('PendingApprovals', 'ResetUnlocked')");

            migrationBuilder.CreateIndex(
                name: "ix_recovery_approvals_request",
                schema: "gameguild.authentication",
                table: "recovery_approvals",
                column: "recovery_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_recovery_approvals_tenant",
                schema: "gameguild.authentication",
                table: "recovery_approvals",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_recovery_approvals_request_contact",
                schema: "gameguild.authentication",
                table: "recovery_approvals",
                columns: new[] { "recovery_request_id", "trusted_contact_id" },
                unique: true,
                filter: "trusted_contact_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_recovery_approvals_request_credential",
                schema: "gameguild.authentication",
                table: "recovery_approvals",
                columns: new[] { "recovery_request_id", "webauthn_credential_id" },
                unique: true,
                filter: "webauthn_credential_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recovery_approvals",
                schema: "gameguild.authentication");

            migrationBuilder.DropTable(
                name: "account_recovery_requests",
                schema: "gameguild.authentication");

            migrationBuilder.DropTable(
                name: "trusted_contacts",
                schema: "gameguild.authentication");
        }
    }
}
