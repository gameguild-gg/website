using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountCreditsGiftsReferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_credit_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    CreditType = table.Column<int>(type: "integer", nullable: false),
                    SourceReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RemainingAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FullyUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_credit_grants", x => x.Id);
                    table.CheckConstraint("ck_account_credit_grants_amounts_positive", "\"Amount\" > 0 AND \"RemainingAmount\" >= 0 AND \"RemainingAmount\" <= \"Amount\"");
                    table.CheckConstraint("ck_account_credit_grants_user_not_empty", "\"UserId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                });

            migrationBuilder.CreateTable(
                name: "account_credit_invoice_settlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_credit_invoice_settlements", x => x.Id);
                    table.CheckConstraint("ck_account_credit_invoice_settlements_amount_positive", "\"Amount\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "gift_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GiverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    DurationMonths = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PersonalMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RedemptionCodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RedemptionCodePrefix = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RedeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreditGrantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gift_subscriptions", x => x.Id);
                    table.CheckConstraint("ck_gift_subscriptions_amount_positive", "\"Amount\" > 0");
                    table.CheckConstraint("ck_gift_subscriptions_duration_range", "\"DurationMonths\" >= 1 AND \"DurationMonths\" <= 36");
                    table.CheckConstraint("ck_gift_subscriptions_giver_not_empty", "\"GiverUserId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                });

            migrationBuilder.CreateTable(
                name: "referrals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferrerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferralCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReferralLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RefereeEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RefereeUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefereeSubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConversionInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReferrerIpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RefereeIpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SharedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClickedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignupAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConversionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FraudReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferrerRewardGrantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefereeRewardGrantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RewardIssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referrals", x => x.Id);
                    table.CheckConstraint("ck_referrals_referrer_not_empty", "\"ReferrerUserId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                });

            migrationBuilder.CreateTable(
                name: "account_credit_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RemainingAfter = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_credit_transactions", x => x.Id);
                    table.CheckConstraint("ck_account_credit_transactions_amount_positive", "\"Amount\" > 0 AND \"RemainingAfter\" >= 0");
                    table.ForeignKey(
                        name: "FK_account_credit_transactions_account_credit_grants_GrantId",
                        column: x => x.GrantId,
                        principalTable: "account_credit_grants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_credit_grants_ExpiresAt",
                table: "account_credit_grants",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_account_credit_grants_Source_SourceReferenceId",
                table: "account_credit_grants",
                columns: new[] { "Source", "SourceReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_account_credit_grants_TenantId_IdempotencyKey",
                table: "account_credit_grants",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_credit_grants_TenantId_UserId_Status",
                table: "account_credit_grants",
                columns: new[] { "TenantId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_account_credit_invoice_settlements_TenantId_IdempotencyKey",
                table: "account_credit_invoice_settlements",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_credit_invoice_settlements_TenantId_InvoiceId",
                table: "account_credit_invoice_settlements",
                columns: new[] { "TenantId", "InvoiceId" });

            migrationBuilder.CreateIndex(
                name: "IX_account_credit_transactions_GrantId",
                table: "account_credit_transactions",
                column: "GrantId");

            migrationBuilder.CreateIndex(
                name: "IX_account_credit_transactions_IdempotencyKey",
                table: "account_credit_transactions",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_account_credit_transactions_ReferenceId",
                table: "account_credit_transactions",
                column: "ReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_account_credit_transactions_TenantId_UserId_CreatedAt",
                table: "account_credit_transactions",
                columns: new[] { "TenantId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_gift_subscriptions_RecipientEmail",
                table: "gift_subscriptions",
                column: "RecipientEmail");

            migrationBuilder.CreateIndex(
                name: "IX_gift_subscriptions_RedemptionCodeHash",
                table: "gift_subscriptions",
                column: "RedemptionCodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gift_subscriptions_Status_ExpiresAt",
                table: "gift_subscriptions",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_gift_subscriptions_TenantId_GiverUserId",
                table: "gift_subscriptions",
                columns: new[] { "TenantId", "GiverUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_gift_subscriptions_TenantId_RecipientUserId",
                table: "gift_subscriptions",
                columns: new[] { "TenantId", "RecipientUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_referrals_Status_ReferrerIpHash",
                table: "referrals",
                columns: new[] { "Status", "ReferrerIpHash" });

            migrationBuilder.CreateIndex(
                name: "IX_referrals_TenantId_RefereeUserId",
                table: "referrals",
                columns: new[] { "TenantId", "RefereeUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_referrals_TenantId_ReferralCode",
                table: "referrals",
                columns: new[] { "TenantId", "ReferralCode" });

            migrationBuilder.CreateIndex(
                name: "IX_referrals_TenantId_ReferrerUserId_Status",
                table: "referrals",
                columns: new[] { "TenantId", "ReferrerUserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_credit_invoice_settlements");

            migrationBuilder.DropTable(
                name: "account_credit_transactions");

            migrationBuilder.DropTable(
                name: "gift_subscriptions");

            migrationBuilder.DropTable(
                name: "referrals");

            migrationBuilder.DropTable(
                name: "account_credit_grants");
        }
    }
}
