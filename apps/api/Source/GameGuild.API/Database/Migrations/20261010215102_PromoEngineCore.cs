using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class PromoEngineCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ApplyToRenewal",
                table: "promo_codes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CampaignId",
                table: "promo_codes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentUses",
                table: "promo_codes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DurationMonths",
                table: "promo_codes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FreeTrialExtensionDays",
                table: "promo_codes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxDiscountAmount",
                table: "promo_codes",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "promo_code_uses",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "promo_code_uses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "promo_code_uses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrderLineItemId",
                table: "promo_code_uses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UsedAt",
                table: "promo_code_uses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "promo_code_user_usage_counters",
                columns: table => new
                {
                    PromoCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UseCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promo_code_user_usage_counters", x => new { x.PromoCodeId, x.UserId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_promo_codes_CampaignId",
                table: "promo_codes",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_promo_codes_IsActive_ValidUntil",
                table: "promo_codes",
                columns: new[] { "IsActive", "ValidUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_promo_code_uses_IdempotencyKey",
                table: "promo_code_uses",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_promo_code_uses_PromoCodeId_UserId",
                table: "promo_code_uses",
                columns: new[] { "PromoCodeId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_promo_code_uses_UsedAt",
                table: "promo_code_uses",
                column: "UsedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promo_code_user_usage_counters");

            migrationBuilder.DropIndex(
                name: "IX_promo_codes_CampaignId",
                table: "promo_codes");

            migrationBuilder.DropIndex(
                name: "IX_promo_codes_IsActive_ValidUntil",
                table: "promo_codes");

            migrationBuilder.DropIndex(
                name: "IX_promo_code_uses_IdempotencyKey",
                table: "promo_code_uses");

            migrationBuilder.DropIndex(
                name: "IX_promo_code_uses_PromoCodeId_UserId",
                table: "promo_code_uses");

            migrationBuilder.DropIndex(
                name: "IX_promo_code_uses_UsedAt",
                table: "promo_code_uses");

            migrationBuilder.DropColumn(
                name: "ApplyToRenewal",
                table: "promo_codes");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "promo_codes");

            migrationBuilder.DropColumn(
                name: "CurrentUses",
                table: "promo_codes");

            migrationBuilder.DropColumn(
                name: "DurationMonths",
                table: "promo_codes");

            migrationBuilder.DropColumn(
                name: "FreeTrialExtensionDays",
                table: "promo_codes");

            migrationBuilder.DropColumn(
                name: "MaxDiscountAmount",
                table: "promo_codes");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "promo_code_uses");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "promo_code_uses");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "promo_code_uses");

            migrationBuilder.DropColumn(
                name: "OrderLineItemId",
                table: "promo_code_uses");

            migrationBuilder.DropColumn(
                name: "UsedAt",
                table: "promo_code_uses");
        }
    }
}
