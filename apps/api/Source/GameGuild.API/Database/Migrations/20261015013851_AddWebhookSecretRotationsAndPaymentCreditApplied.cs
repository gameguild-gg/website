using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookSecretRotationsAndPaymentCreditApplied : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreditAppliedAmount",
                table: "payments",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "WebhookSecretRotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EncryptedPreviousSecret = table.Column<string>(type: "text", nullable: false),
                    EncryptedActiveSecret = table.Column<string>(type: "text", nullable: false),
                    RotatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WindowEndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeprecatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RotatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSecretRotations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_webhook_secret_rotations_provider_rotated_at",
                table: "WebhookSecretRotations",
                columns: new[] { "ProviderKey", "RotatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebhookSecretRotations");

            migrationBuilder.DropColumn(
                name: "CreditAppliedAmount",
                table: "payments");
        }
    }
}
