using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentFraudAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FraudDecision",
                table: "payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FraudDecisionReason",
                table: "payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FraudEvidence",
                table: "payments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FraudReviewNotes",
                table: "payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FraudReviewStatus",
                table: "payments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FraudReviewedAt",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FraudReviewedByUserId",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FraudScore",
                table: "payments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_fraud_velocity_lookup",
                table: "payments",
                columns: new[] { "TenantId", "ExternalCustomerId", "CreatedAt" },
                filter: "\"Status\" <> 4 AND (\"FraudDecision\" IS NULL OR \"FraudDecision\" = 0)")
                .Annotation("Npgsql:CreatedConcurrently", true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_open_fraud_reviews",
                table: "payments",
                columns: new[] { "TenantId", "CreatedAt" },
                filter: "\"FraudDecision\" = 1 AND \"FraudReviewStatus\" = 0")
                .Annotation("Npgsql:CreatedConcurrently", true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payments_fraud_velocity_lookup",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_payments_open_fraud_reviews",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "FraudDecision",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "FraudDecisionReason",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "FraudEvidence",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "FraudReviewNotes",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "FraudReviewStatus",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "FraudReviewedAt",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "FraudReviewedByUserId",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "FraudScore",
                table: "payments");
        }
    }
}
