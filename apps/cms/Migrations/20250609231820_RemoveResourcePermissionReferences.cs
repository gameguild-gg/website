using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cms.Migrations
{
    /// <inheritdoc />
    public partial class RemoveResourcePermissionReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourcePermissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResourcePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    GrantedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResourceMetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GrantedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourcePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourcePermissions_ResourceMetadata_ResourceMetadataId",
                        column: x => x.ResourceMetadataId,
                        principalTable: "ResourceMetadata",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ResourcePermissions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ResourcePermissions_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourcePermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_CreatedAt",
                table: "ResourcePermissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_DeletedAt",
                table: "ResourcePermissions",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_GrantedByUser",
                table: "ResourcePermissions",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_Resource",
                table: "ResourcePermissions",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_ResourceMetadataId",
                table: "ResourcePermissions",
                column: "ResourceMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_TenantId",
                table: "ResourcePermissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_User_Resource",
                table: "ResourcePermissions",
                columns: new[] { "UserId", "ResourceId" },
                unique: true);
        }
    }
}
