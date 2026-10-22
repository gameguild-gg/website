using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyConfigurationAndFxRateSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "currency_configurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsBaseCurrency = table.Column<bool>(type: "boolean", nullable: false),
                    DisplaySymbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_currency_configurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fx_rate_snapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    QuoteCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(28,12)", nullable: false),
                    EffectiveDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AsOfUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SyncRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsOverride = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fx_rate_snapshots", x => x.Id);
                    table.CheckConstraint("CK_FxRateSnapshot_Pair_Distinct", "\"BaseCurrency\" <> \"QuoteCurrency\"");
                    table.CheckConstraint("CK_FxRateSnapshot_Rate_Positive", "\"Rate\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "fx_rate_sync_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PrimaryProviderName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RatesApplied = table.Column<int>(type: "integer", nullable: false),
                    RatesChanged = table.Column<int>(type: "integer", nullable: false),
                    EffectiveDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsecutiveFailureCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RawResponse = table.Column<string>(type: "text", nullable: false),
                    RawResponseContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fx_rate_sync_runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_currency_configurations_CurrencyCode",
                table: "currency_configurations",
                column: "CurrencyCode");

            migrationBuilder.CreateIndex(
                name: "IX_currency_configurations_IsEnabled",
                table: "currency_configurations",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "ix_currency_configurations_tenant_base",
                table: "currency_configurations",
                column: "TenantId",
                unique: true,
                filter: "\"IsBaseCurrency\" = true");

            migrationBuilder.CreateIndex(
                name: "ix_currency_configurations_tenant_currency",
                table: "currency_configurations",
                columns: new[] { "TenantId", "CurrencyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fx_rate_snapshots_BaseCurrency",
                table: "fx_rate_snapshots",
                column: "BaseCurrency");

            migrationBuilder.CreateIndex(
                name: "ix_fx_rate_snapshots_effective_date",
                table: "fx_rate_snapshots",
                column: "EffectiveDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_fx_rate_snapshots_IsOverride",
                table: "fx_rate_snapshots",
                column: "IsOverride");

            migrationBuilder.CreateIndex(
                name: "ix_fx_rate_snapshots_pair_date",
                table: "fx_rate_snapshots",
                columns: new[] { "BaseCurrency", "QuoteCurrency", "EffectiveDateUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_fx_rate_snapshots_pair_date_provider",
                table: "fx_rate_snapshots",
                columns: new[] { "BaseCurrency", "QuoteCurrency", "EffectiveDateUtc", "ProviderName", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fx_rate_snapshots_ProviderName",
                table: "fx_rate_snapshots",
                column: "ProviderName");

            migrationBuilder.CreateIndex(
                name: "IX_fx_rate_snapshots_QuoteCurrency",
                table: "fx_rate_snapshots",
                column: "QuoteCurrency");

            migrationBuilder.CreateIndex(
                name: "IX_fx_rate_sync_runs_ProviderName",
                table: "fx_rate_sync_runs",
                column: "ProviderName");

            migrationBuilder.CreateIndex(
                name: "ix_fx_rate_sync_runs_started_at",
                table: "fx_rate_sync_runs",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "ix_fx_rate_sync_runs_status",
                table: "fx_rate_sync_runs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "currency_configurations");

            migrationBuilder.DropTable(
                name: "fx_rate_snapshots");

            migrationBuilder.DropTable(
                name: "fx_rate_sync_runs");
        }
    }
}
