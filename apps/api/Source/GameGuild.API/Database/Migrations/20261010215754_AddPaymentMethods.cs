using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PaymentMethodId",
                table: "Subscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payment_methods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GatewayProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GatewayPaymentMethodId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CardBrand = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CardLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    CardExpMonth = table.Column<int>(type: "integer", nullable: true),
                    CardExpYear = table.Column<int>(type: "integer", nullable: true),
                    CardFingerprint = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BillingAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryNotice30SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryNotice14SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryNotice7SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_methods", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_tenant_payment_method",
                table: "Subscriptions",
                columns: new[] { "TenantId", "PaymentMethodId" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_methods_ExpiresAt",
                table: "payment_methods",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_payment_methods_GatewayPaymentMethodId",
                table: "payment_methods",
                column: "GatewayPaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "ix_payment_methods_single_default",
                table: "payment_methods",
                columns: new[] { "TenantId", "UserId" },
                unique: true,
                filter: "\"IsDefault\" AND \"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payment_methods_tenant_gateway_unique",
                table: "payment_methods",
                columns: new[] { "TenantId", "GatewayProvider", "GatewayPaymentMethodId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_methods_TenantId_UserId_IsDefault",
                table: "payment_methods",
                columns: new[] { "TenantId", "UserId", "IsDefault" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_methods");

            migrationBuilder.DropIndex(
                name: "ix_subscriptions_tenant_payment_method",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PaymentMethodId",
                table: "Subscriptions");
        }
    }
}
