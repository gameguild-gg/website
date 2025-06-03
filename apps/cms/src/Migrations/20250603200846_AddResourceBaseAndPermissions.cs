using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cms.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceBaseAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceMetadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AdditionalData = table.Column<string>(type: "jsonb", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SeoMetadata = table.Column<string>(type: "jsonb", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceMetadata", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DefaultPermission = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceLocalizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LanguageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceLocalizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceLocalizations_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourcePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResourceRoleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ResourceMetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Permission = table.Column<int>(type: "INTEGER", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
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
                        name: "FK_ResourcePermissions_ResourceRoles_ResourceRoleId",
                        column: x => x.ResourceRoleId,
                        principalTable: "ResourceRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourcePermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Languages_Code",
                table: "Languages",
                column: "Code",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Languages_CreatedAt",
                table: "Languages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Languages_DeletedAt",
                table: "Languages",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Languages_Name",
                table: "Languages",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocalizations_CreatedAt",
                table: "ResourceLocalizations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocalizations_DeletedAt",
                table: "ResourceLocalizations",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocalizations_LanguageId",
                table: "ResourceLocalizations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocalizations_ResourceId_ResourceType",
                table: "ResourceLocalizations",
                columns: new[] { "ResourceId", "ResourceType" },
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocalizations_ResourceId_ResourceType_LanguageId_FieldName",
                table: "ResourceLocalizations",
                columns: new[] { "ResourceId", "ResourceType", "LanguageId", "FieldName" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocalizations_Status",
                table: "ResourceLocalizations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMetadata_CreatedAt",
                table: "ResourceMetadata",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMetadata_DeletedAt",
                table: "ResourceMetadata",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceMetadata_ResourceType",
                table: "ResourceMetadata",
                column: "ResourceType");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_CreatedAt",
                table: "ResourcePermissions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_DeletedAt",
                table: "ResourcePermissions",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_Permission",
                table: "ResourcePermissions",
                column: "Permission");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_ResourceId_ResourceType",
                table: "ResourcePermissions",
                columns: new[] { "ResourceId", "ResourceType" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_ResourceMetadataId",
                table: "ResourcePermissions",
                column: "ResourceMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_ResourceRoleId",
                table: "ResourcePermissions",
                column: "ResourceRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_UserId",
                table: "ResourcePermissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRoles_CreatedAt",
                table: "ResourceRoles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRoles_DeletedAt",
                table: "ResourceRoles",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRoles_Name",
                table: "ResourceRoles",
                column: "Name",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourceLocalizations");

            migrationBuilder.DropTable(
                name: "ResourcePermissions");

            migrationBuilder.DropTable(
                name: "Languages");

            migrationBuilder.DropTable(
                name: "ResourceMetadata");

            migrationBuilder.DropTable(
                name: "ResourceRoles");
        }
    }
}
