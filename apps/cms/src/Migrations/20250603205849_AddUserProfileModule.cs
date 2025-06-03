using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cms.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserProfileId",
                table: "ResourceRoles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserProfileId",
                table: "ResourcePermissions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserProfileId",
                table: "ResourceLocalizations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    GivenName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FamilyName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Version = table.Column<int>(type: "INTEGER", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    MetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfiles_ResourceMetadata_MetadataId",
                        column: x => x.MetadataId,
                        principalTable: "ResourceMetadata",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRoles_UserProfileId",
                table: "ResourceRoles",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_UserProfileId",
                table: "ResourcePermissions",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceLocalizations_UserProfileId",
                table: "ResourceLocalizations",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_CreatedAt",
                table: "UserProfiles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_DeletedAt",
                table: "UserProfiles",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_MetadataId",
                table: "UserProfiles",
                column: "MetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_OwnerId",
                table: "UserProfiles",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_TenantId",
                table: "UserProfiles",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceLocalizations_UserProfiles_UserProfileId",
                table: "ResourceLocalizations",
                column: "UserProfileId",
                principalTable: "UserProfiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourcePermissions_UserProfiles_UserProfileId",
                table: "ResourcePermissions",
                column: "UserProfileId",
                principalTable: "UserProfiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceRoles_UserProfiles_UserProfileId",
                table: "ResourceRoles",
                column: "UserProfileId",
                principalTable: "UserProfiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ResourceLocalizations_UserProfiles_UserProfileId",
                table: "ResourceLocalizations");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourcePermissions_UserProfiles_UserProfileId",
                table: "ResourcePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_ResourceRoles_UserProfiles_UserProfileId",
                table: "ResourceRoles");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ResourceRoles_UserProfileId",
                table: "ResourceRoles");

            migrationBuilder.DropIndex(
                name: "IX_ResourcePermissions_UserProfileId",
                table: "ResourcePermissions");

            migrationBuilder.DropIndex(
                name: "IX_ResourceLocalizations_UserProfileId",
                table: "ResourceLocalizations");

            migrationBuilder.DropColumn(
                name: "UserProfileId",
                table: "ResourceRoles");

            migrationBuilder.DropColumn(
                name: "UserProfileId",
                table: "ResourcePermissions");

            migrationBuilder.DropColumn(
                name: "UserProfileId",
                table: "ResourceLocalizations");
        }
    }
}
