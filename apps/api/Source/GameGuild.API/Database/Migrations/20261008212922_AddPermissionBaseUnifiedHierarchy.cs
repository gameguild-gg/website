using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionBaseUnifiedHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "genericresourcepermission",
                schema: "gameguild.authentication",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Permissions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    GrantedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ResourceTitle = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_genericresourcepermission", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_genericresourcepermission_resource_type",
                schema: "gameguild.authentication",
                table: "genericresourcepermission",
                column: "ResourceType");

            migrationBuilder.CreateIndex(
                name: "ix_genericresourcepermission_tenant_id",
                schema: "gameguild.authentication",
                table: "genericresourcepermission",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ix_genericresourcepermission_tenant_resource",
                schema: "gameguild.authentication",
                table: "genericresourcepermission",
                columns: new[] { "TenantId", "ResourceType", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "ix_genericresourcepermission_user_id",
                schema: "gameguild.authentication",
                table: "genericresourcepermission",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "genericresourcepermission",
                schema: "gameguild.authentication");
        }
    }
}
