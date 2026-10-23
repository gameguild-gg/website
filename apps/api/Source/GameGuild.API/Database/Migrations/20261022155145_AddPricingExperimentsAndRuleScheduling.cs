using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingExperimentsAndRuleScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsScheduled",
                table: "pricing_rules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "UsageMaxValue",
                table: "pricing_rules",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsageMetricKey",
                table: "pricing_rules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UsageMinValue",
                table: "pricing_rules",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pricing_experiments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrafficPercentage = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_experiments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pricing_experiment_variants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperimentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsControl = table.Column<bool>(type: "boolean", nullable: false),
                    AllocationPercentage = table.Column<int>(type: "integer", nullable: false),
                    DiscountPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    FixedPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_experiment_variants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pricing_experiment_variants_pricing_experiments_ExperimentId",
                        column: x => x.ExperimentId,
                        principalTable: "pricing_experiments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pricing_experiment_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperimentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BucketKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_experiment_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pricing_experiment_assignments_pricing_experiment_variants_~",
                        column: x => x.VariantId,
                        principalTable: "pricing_experiment_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pricing_experiment_assignments_pricing_experiments_Experime~",
                        column: x => x.ExperimentId,
                        principalTable: "pricing_experiments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pricing_experiment_outcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperimentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_experiment_outcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pricing_experiment_outcomes_pricing_experiment_variants_Var~",
                        column: x => x.VariantId,
                        principalTable: "pricing_experiment_variants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pricing_experiment_outcomes_pricing_experiments_ExperimentId",
                        column: x => x.ExperimentId,
                        principalTable: "pricing_experiments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pricing_rules_IsScheduled",
                table: "pricing_rules",
                column: "IsScheduled");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_experiment_assignments_BucketKey",
                table: "pricing_experiment_assignments",
                column: "BucketKey");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_experiment_assignments_ExperimentId",
                table: "pricing_experiment_assignments",
                column: "ExperimentId");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_experiment_assignments_ExperimentId_BucketKey",
                table: "pricing_experiment_assignments",
                columns: new[] { "ExperimentId", "BucketKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pricing_experiment_assignments_VariantId",
                table: "pricing_experiment_assignments",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_experiment_outcomes_ExperimentId",
                table: "pricing_experiment_outcomes",
                column: "ExperimentId");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_experiment_outcomes_OccurredAt",
                table: "pricing_experiment_outcomes",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_experiment_outcomes_VariantId",
                table: "pricing_experiment_outcomes",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_experiment_variants_ExperimentId",
                table: "pricing_experiment_variants",
                column: "ExperimentId");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_experiment_variants_Name",
                table: "pricing_experiment_variants",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_experiments_EndDate",
                table: "pricing_experiments",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_experiments_ProductId",
                table: "pricing_experiments",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_experiments_StartDate",
                table: "pricing_experiments",
                column: "StartDate");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_experiments_Status",
                table: "pricing_experiments",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pricing_experiment_assignments");

            migrationBuilder.DropTable(
                name: "pricing_experiment_outcomes");

            migrationBuilder.DropTable(
                name: "pricing_experiment_variants");

            migrationBuilder.DropTable(
                name: "pricing_experiments");

            migrationBuilder.DropIndex(
                name: "IX_pricing_rules_IsScheduled",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "IsScheduled",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "UsageMaxValue",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "UsageMetricKey",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "UsageMinValue",
                table: "pricing_rules");
        }
    }
}
