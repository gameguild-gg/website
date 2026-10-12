using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedAuditQueryViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedAuditQueries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    QueryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    QueryDsl = table.Column<string>(type: "text", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowedRoles = table.Column<string[]>(type: "text[]", nullable: false),
                    AllowedUserIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    CurrentRevision = table.Column<int>(type: "integer", nullable: false),
                    ExecutionCount = table.Column<int>(type: "integer", nullable: false),
                    LastExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastExecutedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedAuditQueries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedAuditQueryRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SavedAuditQueryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    QueryDsl = table.Column<string>(type: "text", nullable: false),
                    QueryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsCreation = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedAuditQueryRevisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedAuditQueries_IsPublic",
                table: "SavedAuditQueries",
                column: "IsPublic");

            migrationBuilder.CreateIndex(
                name: "IX_SavedAuditQueries_TenantId_CreatedByUserId",
                table: "SavedAuditQueries",
                columns: new[] { "TenantId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedAuditQueries_TenantId_QueryName",
                table: "SavedAuditQueries",
                columns: new[] { "TenantId", "QueryName" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedAuditQueryRevisions_SavedAuditQueryId",
                table: "SavedAuditQueryRevisions",
                column: "SavedAuditQueryId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedAuditQueryRevisions_SavedAuditQueryId_Revision",
                table: "SavedAuditQueryRevisions",
                columns: new[] { "SavedAuditQueryId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedAuditQueries");

            migrationBuilder.DropTable(
                name: "SavedAuditQueryRevisions");
        }
    }
}
