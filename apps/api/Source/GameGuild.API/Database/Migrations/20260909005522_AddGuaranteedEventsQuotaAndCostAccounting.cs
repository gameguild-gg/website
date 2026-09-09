using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddGuaranteedEventsQuotaAndCostAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "gameguild.integration");

            migrationBuilder.CreateTable(
                name: "actual_cloud_cost_entries",
                schema: "resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ExternalLineId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ServiceCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UsageType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BillingPeriodStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BillingPeriodEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourceCost = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    UsdCost = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    IsUsdNormalizationPending = table.Column<bool>(type: "boolean", nullable: false),
                    IsForecast = table.Column<bool>(type: "boolean", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actual_cloud_cost_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cloud_price_rates",
                schema: "resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ServiceCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MetricCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourceUnitPrice = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    UsdExchangeRate = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    ProviderRateId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    EffectiveAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RetrievedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_price_rates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "internal_cost_valuations",
                schema: "resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsageLedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CloudPriceRateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SourceUnitPrice = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    SourceCost = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    UsdExchangeRate = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    UsdCost = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    IsStaleEstimate = table.Column<bool>(type: "boolean", nullable: false),
                    ValuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_internal_cost_valuations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "internal_usage_ledger_entries",
                schema: "resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MetricCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(28,9)", precision: 28, scale: 9, nullable: false),
                    Unit = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AggregateId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValuationStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_internal_usage_ledger_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "gameguild.integration",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SourceModule = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AggregateId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClaimedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeadLetteredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "quota_ledger_entries",
                schema: "resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceType = table.Column<int>(type: "integer", nullable: false),
                    Delta = table.Column<long>(type: "bigint", nullable: false),
                    OperationCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AggregateId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsReservationFinalization = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quota_ledger_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shared_cost_allocation_entries",
                schema: "resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsageLedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    DurationMilliseconds = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DatabaseMilliseconds = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CacheOperations = table.Column<long>(type: "bigint", nullable: false),
                    TransferredBytes = table.Column<long>(type: "bigint", nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RouteTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_cost_allocation_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_receipts",
                schema: "gameguild.integration",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeadLetteredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbox_receipts", x => new { x.EventId, x.ConsumerName });
                    table.ForeignKey(
                        name: "FK_inbox_receipts_outbox_messages_EventId",
                        column: x => x.EventId,
                        principalSchema: "gameguild.integration",
                        principalTable: "outbox_messages",
                        principalColumn: "EventId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_actual_cloud_cost_entries_Provider_Source_ExternalLineId",
                schema: "resources",
                table: "actual_cloud_cost_entries",
                columns: new[] { "Provider", "Source", "ExternalLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_actual_cloud_cost_entries_TenantId_BillingPeriodStartUtc",
                schema: "resources",
                table: "actual_cloud_cost_entries",
                columns: new[] { "TenantId", "BillingPeriodStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_cloud_price_rates_Provider_MetricCode_Region_EffectiveAtUtc",
                schema: "resources",
                table: "cloud_price_rates",
                columns: new[] { "Provider", "MetricCode", "Region", "EffectiveAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_cloud_price_rates_Provider_ProviderRateId_EffectiveAtUtc",
                schema: "resources",
                table: "cloud_price_rates",
                columns: new[] { "Provider", "ProviderRateId", "EffectiveAtUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inbox_retry",
                schema: "gameguild.integration",
                table: "inbox_receipts",
                columns: new[] { "CompletedAtUtc", "DeadLetteredAtUtc", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_internal_cost_valuations_TenantId_ValuedAtUtc",
                schema: "resources",
                table: "internal_cost_valuations",
                columns: new[] { "TenantId", "ValuedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_internal_cost_valuations_UsageLedgerEntryId",
                schema: "resources",
                table: "internal_cost_valuations",
                column: "UsageLedgerEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_internal_usage_ledger_entries_EventId_MetricCode",
                schema: "resources",
                table: "internal_usage_ledger_entries",
                columns: new[] { "EventId", "MetricCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_internal_usage_ledger_entries_TenantId_OccurredAtUtc",
                schema: "resources",
                table: "internal_usage_ledger_entries",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_internal_usage_ledger_entries_ValuationStatus_OccurredAtUtc",
                schema: "resources",
                table: "internal_usage_ledger_entries",
                columns: new[] { "ValuationStatus", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_aggregate_order",
                schema: "gameguild.integration",
                table: "outbox_messages",
                columns: new[] { "AggregateType", "AggregateId", "OccurredAtUtc", "EventId" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_correlation",
                schema: "gameguild.integration",
                table: "outbox_messages",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_dispatch",
                schema: "gameguild.integration",
                table: "outbox_messages",
                columns: new[] { "CompletedAtUtc", "DeadLetteredAtUtc", "ClaimedUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_quota_ledger_entries_EventId",
                schema: "resources",
                table: "quota_ledger_entries",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quota_ledger_entries_TenantId_ResourceType_TimestampUtc",
                schema: "resources",
                table: "quota_ledger_entries",
                columns: new[] { "TenantId", "ResourceType", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_shared_cost_allocation_entries_RequestEventId",
                schema: "resources",
                table: "shared_cost_allocation_entries",
                column: "RequestEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shared_cost_allocation_entries_TenantId_OccurredAtUtc",
                schema: "resources",
                table: "shared_cost_allocation_entries",
                columns: new[] { "TenantId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "actual_cloud_cost_entries",
                schema: "resources");

            migrationBuilder.DropTable(
                name: "cloud_price_rates",
                schema: "resources");

            migrationBuilder.DropTable(
                name: "inbox_receipts",
                schema: "gameguild.integration");

            migrationBuilder.DropTable(
                name: "internal_cost_valuations",
                schema: "resources");

            migrationBuilder.DropTable(
                name: "internal_usage_ledger_entries",
                schema: "resources");

            migrationBuilder.DropTable(
                name: "quota_ledger_entries",
                schema: "resources");

            migrationBuilder.DropTable(
                name: "shared_cost_allocation_entries",
                schema: "resources");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "gameguild.integration");
        }
    }
}
