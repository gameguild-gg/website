using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPromoPlanEligibilityConversionSubscriptionApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #418: plan-scoped promo eligibility.
            migrationBuilder.AddColumn<Guid>(
                name: "PlanId",
                table: "promo_codes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_promo_codes_plan_id",
                table: "promo_codes",
                column: "PlanId");

            // Issue #418: link promo usage to subscriptions and the invoices that settle them.
            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionId",
                table: "promo_code_uses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                table: "promo_code_uses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_promo_code_uses_subscription_id",
                table: "promo_code_uses",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "ix_promo_code_uses_invoice_id",
                table: "promo_code_uses",
                column: "InvoiceId");

            // Issue #418: conversion tracking — one row per :validate call.
            migrationBuilder.CreateTable(
                name: "promo_code_validation_attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PromoCodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promo_code_validation_attempts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_promo_code_validation_attempts_attempted_at",
                table: "promo_code_validation_attempts",
                column: "AttemptedAt");

            migrationBuilder.CreateIndex(
                name: "ix_promo_code_validation_attempts_code",
                table: "promo_code_validation_attempts",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "ix_promo_code_validation_attempts_promo_code_id",
                table: "promo_code_validation_attempts",
                column: "PromoCodeId");

            migrationBuilder.CreateIndex(
                name: "ix_promo_code_validation_attempts_user_id",
                table: "promo_code_validation_attempts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promo_code_validation_attempts");

            migrationBuilder.DropIndex(
                name: "ix_promo_code_uses_invoice_id",
                table: "promo_code_uses");

            migrationBuilder.DropIndex(
                name: "ix_promo_code_uses_subscription_id",
                table: "promo_code_uses");

            migrationBuilder.DropIndex(
                name: "ix_promo_codes_plan_id",
                table: "promo_codes");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "promo_code_uses");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "promo_code_uses");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "promo_codes");
        }
    }
}
