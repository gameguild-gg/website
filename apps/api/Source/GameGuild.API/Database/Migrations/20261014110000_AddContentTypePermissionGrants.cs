using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddContentTypePermissionGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentTypePermissionGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentTypeName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    DenyPermissions = table.Column<string[]>(type: "text[]", nullable: false),
                    GrantedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTypePermissionGrants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissionGrants_ContentTypeName",
                table: "ContentTypePermissionGrants",
                column: "ContentTypeName");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissionGrants_ExpiresAt",
                table: "ContentTypePermissionGrants",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissionGrants_Subject_Tenant_ContentType",
                table: "ContentTypePermissionGrants",
                columns: new[] { "UserId", "TenantId", "ContentTypeName" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissionGrants_Tenant_ContentType",
                table: "ContentTypePermissionGrants",
                columns: new[] { "TenantId", "ContentTypeName" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissionGrants_TenantId",
                table: "ContentTypePermissionGrants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypePermissionGrants_UserId",
                table: "ContentTypePermissionGrants",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentTypePermissionGrants");
        }
    }
}
