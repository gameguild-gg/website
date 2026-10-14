using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPiiRedactionPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PiiRedactionOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    AppliedRuleIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    RedactedValueCount = table.Column<int>(type: "integer", nullable: false),
                    TokenizedValueCount = table.Column<int>(type: "integer", nullable: false),
                    FailClosed = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PiiRedactionOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PiiRedactionRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    RuleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    PatternType = table.Column<int>(type: "integer", nullable: false),
                    RegexPattern = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Preset = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    TargetFields = table.Column<string[]>(type: "text[]", nullable: false),
                    Replacement = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PiiRedactionRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PiiTokenVaultEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EncryptedValue = table.Column<string>(type: "text", nullable: false),
                    SourceRuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UseCount = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PiiTokenVaultEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PiiRedactionOperations_TenantId_AppliedAt",
                table: "PiiRedactionOperations",
                columns: new[] { "TenantId", "AppliedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PiiRedactionOperations_TenantId_FailClosed",
                table: "PiiRedactionOperations",
                columns: new[] { "TenantId", "FailClosed" });

            migrationBuilder.CreateIndex(
                name: "IX_PiiRedactionRules_TenantId_IsEnabled",
                table: "PiiRedactionRules",
                columns: new[] { "TenantId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_PiiTokenVaultEntries_ScopeKey_Token",
                table: "PiiTokenVaultEntries",
                columns: new[] { "ScopeKey", "Token" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PiiTokenVaultEntries_TenantId",
                table: "PiiTokenVaultEntries",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PiiRedactionOperations");

            migrationBuilder.DropTable(
                name: "PiiRedactionRules");

            migrationBuilder.DropTable(
                name: "PiiTokenVaultEntries");
        }
    }
}
