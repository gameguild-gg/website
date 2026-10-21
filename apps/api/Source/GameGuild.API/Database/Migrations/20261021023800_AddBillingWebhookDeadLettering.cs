using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingWebhookDeadLettering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeadLetterReason",
                table: "billing_webhook_events",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadLetteredAt",
                table: "billing_webhook_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSignatureVerified",
                table: "billing_webhook_events",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignatureVerifiedAt",
                table: "billing_webhook_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_billing_webhook_events_dead_lettered_at",
                table: "billing_webhook_events",
                column: "DeadLetteredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_billing_webhook_events_dead_lettered_at",
                table: "billing_webhook_events");

            migrationBuilder.DropColumn(
                name: "DeadLetterReason",
                table: "billing_webhook_events");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                table: "billing_webhook_events");

            migrationBuilder.DropColumn(
                name: "IsSignatureVerified",
                table: "billing_webhook_events");

            migrationBuilder.DropColumn(
                name: "SignatureVerifiedAt",
                table: "billing_webhook_events");
        }
    }
}
