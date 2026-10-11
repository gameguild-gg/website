using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingDecisionLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pricing_decision_ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderLineItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    BillingCycleNumber = table.Column<int>(type: "integer", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    RuleAdjustmentTotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PromoDiscountTotal = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    FinalAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    BreakdownJson = table.Column<string>(type: "jsonb", nullable: false),
                    AppliedPromoCodes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    AppliedPricingRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppliedStackingRuleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PricingEngineVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorIpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DriftStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecomputedFinalAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    ValidatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DriftReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_decision_ledger", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pricing_decision_ledger_created_at",
                table: "pricing_decision_ledger",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_pricing_decision_ledger_drift_status_created_at",
                table: "pricing_decision_ledger",
                columns: new[] { "DriftStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_pricing_decision_ledger_idempotency_key",
                table: "pricing_decision_ledger",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pricing_decision_ledger_order_id",
                table: "pricing_decision_ledger",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "ix_pricing_decision_ledger_subscription_id",
                table: "pricing_decision_ledger",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "ix_pricing_decision_ledger_tenant_created_at",
                table: "pricing_decision_ledger",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_pricing_decision_ledger_tenant_id",
                table: "pricing_decision_ledger",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pricing_decision_ledger");
        }
    }
}
