using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaRecoveryRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mfa_recovery_requests",
                schema: "gameguild.authentication",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    tenant_id = table.Column<string>(type: "text", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cooldown_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    request_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    confirmation_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    confirmation_token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mfa_recovery_requests", x => x.id);
                    table.CheckConstraint("ck_mfa_recovery_cooldown", "cooldown_ends_at >= requested_at");
                    table.CheckConstraint("ck_mfa_recovery_status", "status IN ('Pending', 'AwaitingConfirmation', 'Completed', 'Cancelled', 'Expired')");
                    table.CheckConstraint("ck_mfa_recovery_tokens", "(request_token_hash IS NULL AND confirmation_token_hash IS NULL AND confirmation_token_expires_at IS NULL)\nOR (request_token_hash ~ '^[a-f0-9]{64}$')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_mfa_recovery_requests_tenant_status",
                schema: "gameguild.authentication",
                table: "mfa_recovery_requests",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_mfa_recovery_requests_active_user",
                schema: "gameguild.authentication",
                table: "mfa_recovery_requests",
                column: "user_id",
                unique: true,
                filter: "status IN ('Pending', 'AwaitingConfirmation')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mfa_recovery_requests",
                schema: "gameguild.authentication");
        }
    }
}
