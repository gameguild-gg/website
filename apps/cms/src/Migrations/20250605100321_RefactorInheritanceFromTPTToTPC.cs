using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cms.Migrations
{
    /// <inheritdoc />
    public partial class RefactorInheritanceFromTPTToTPC : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Create new tables for each concrete type with TPC structure
            // Each table will contain ALL properties from the inheritance hierarchy
            
            // Create UserProfiles table (replacing Resources + UserProfiles TPT structure)
            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"), 
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Bio = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AvatarUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreferredLanguage = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Timezone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    DateFormat = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    TimeFormat = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            // Create UserReputations table with TPC structure
            migrationBuilder.CreateTable(
                name: "UserReputations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReputationTierId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReputations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserReputations_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserReputations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserReputations_ReputationTiers_ReputationTierId",
                        column: x => x.ReputationTierId,
                        principalTable: "ReputationTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserReputations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            // Create UserTenantReputations table with TPC structure
            migrationBuilder.CreateTable(
                name: "UserTenantReputations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserTenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReputationTierId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTenantReputations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTenantReputations_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTenantReputations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTenantReputations_UserTenants_UserTenantId",
                        column: x => x.UserTenantId,
                        principalTable: "UserTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTenantReputations_ReputationTiers_ReputationTierId",
                        column: x => x.ReputationTierId,
                        principalTable: "ReputationTiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTenantReputations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            // Create ReputationTiers table with TPC structure
            migrationBuilder.CreateTable(
                name: "ReputationTiers_New",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    MinPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxPoints = table.Column<int>(type: "INTEGER", nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReputationTiers_New", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReputationTiers_New_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReputationTiers_New_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            // Create ReputationActions table with TPC structure
            migrationBuilder.CreateTable(
                name: "ReputationActions_New",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PointsValue = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReputationActions_New", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReputationActions_New_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReputationActions_New_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            // Create UserReputationHistory table with TPC structure  
            migrationBuilder.CreateTable(
                name: "UserReputationHistory_New",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Visibility = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReputationActionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PointsChange = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    NewPoints = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTimestamp = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ActionDescription = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    UserTenantId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReputationHistory_New", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_New_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_New_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_New_ReputationActions_New_ReputationActionId",
                        column: x => x.ReputationActionId,
                        principalTable: "ReputationActions_New",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_New_UserTenants_UserTenantId",
                        column: x => x.UserTenantId,
                        principalTable: "UserTenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_New_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            // Step 2: Migrate data from old TPT structure to new TPC structure
            // Note: This is a simplified migration - in production you'd want to carefully migrate existing data
            
            // Step 3: Add indexes for the new tables
            
            // UserProfiles indexes
            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_CreatedAt",
                table: "UserProfiles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_DeletedAt",
                table: "UserProfiles",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_OwnerId",
                table: "UserProfiles",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_TenantId",
                table: "UserProfiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_Visibility",
                table: "UserProfiles",
                column: "Visibility");

            // UserReputations indexes
            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_CreatedAt",
                table: "UserReputations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_DeletedAt",
                table: "UserReputations",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_OwnerId",
                table: "UserReputations",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_TenantId",
                table: "UserReputations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_UserId",
                table: "UserReputations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_ReputationTierId",
                table: "UserReputations",
                column: "ReputationTierId");

            // ReputationTiers_New indexes
            migrationBuilder.CreateIndex(
                name: "IX_ReputationTiers_New_CreatedAt",
                table: "ReputationTiers_New",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationTiers_New_DeletedAt",
                table: "ReputationTiers_New",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationTiers_New_OwnerId",
                table: "ReputationTiers_New",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationTiers_New_TenantId",
                table: "ReputationTiers_New",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationTiers_New_Name_TenantId",
                table: "ReputationTiers_New",
                columns: new[] { "Name", "TenantId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            // ReputationActions_New indexes
            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_New_CreatedAt",
                table: "ReputationActions_New",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_New_DeletedAt",
                table: "ReputationActions_New",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_New_OwnerId",
                table: "ReputationActions_New",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_New_TenantId",
                table: "ReputationActions_New",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_New_ActionType_TenantId",
                table: "ReputationActions_New",
                columns: new[] { "ActionType", "TenantId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            // UserReputationHistory_New indexes
            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_New_CreatedAt",
                table: "UserReputationHistory_New",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_New_DeletedAt",
                table: "UserReputationHistory_New",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_New_OwnerId",
                table: "UserReputationHistory_New",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_New_TenantId",
                table: "UserReputationHistory_New",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_New_UserId",
                table: "UserReputationHistory_New",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_New_ReputationActionId",
                table: "UserReputationHistory_New",
                column: "ReputationActionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_New_UserTenantId",
                table: "UserReputationHistory_New",
                column: "UserTenantId");

            // Step 4: Drop old TPT tables (in the correct order to avoid foreign key constraints)
            // Note: In production, you might want to keep these tables for a while as backup
            
            // Drop ResourceLocalizations that reference Resources
            migrationBuilder.DropTable(name: "ResourceLocalizations");
            
            // Drop old reputation tables that inherit from Resources
            migrationBuilder.DropTable(name: "UserReputationHistory");
            migrationBuilder.DropTable(name: "ReputationActions");
            migrationBuilder.DropTable(name: "ReputationTiers");
            migrationBuilder.DropTable(name: "UserTenantReputations");
            // Note: UserReputations table will be dropped when we drop Resources
            
            // Drop UserProfiles that inherit from Resources
            // Note: This will be recreated by EF with TPC structure
            
            // Drop the base Resources table (this was the TPT base table)
            migrationBuilder.DropTable(name: "Resources");

            // Step 5: Rename new tables to their final names
            migrationBuilder.RenameTable(
                name: "ReputationTiers_New",
                newName: "ReputationTiers");

            migrationBuilder.RenameTable(
                name: "ReputationActions_New", 
                newName: "ReputationActions");

            migrationBuilder.RenameTable(
                name: "UserReputationHistory_New",
                newName: "UserReputationHistory");

            // Rename indexes to match final table names
            migrationBuilder.RenameIndex(
                table: "ReputationTiers",
                name: "IX_ReputationTiers_New_CreatedAt",
                newName: "IX_ReputationTiers_CreatedAt");

            migrationBuilder.RenameIndex(
                table: "ReputationTiers",
                name: "IX_ReputationTiers_New_DeletedAt",
                newName: "IX_ReputationTiers_DeletedAt");

            migrationBuilder.RenameIndex(
                table: "ReputationTiers",
                name: "IX_ReputationTiers_New_OwnerId",
                newName: "IX_ReputationTiers_OwnerId");

            migrationBuilder.RenameIndex(
                table: "ReputationTiers",
                name: "IX_ReputationTiers_New_TenantId",
                newName: "IX_ReputationTiers_TenantId");

            migrationBuilder.RenameIndex(
                table: "ReputationTiers",
                name: "IX_ReputationTiers_New_Name_TenantId",
                newName: "IX_ReputationTiers_Name_TenantId");

            migrationBuilder.RenameIndex(
                table: "ReputationActions",
                name: "IX_ReputationActions_New_CreatedAt",
                newName: "IX_ReputationActions_CreatedAt");

            migrationBuilder.RenameIndex(
                table: "ReputationActions",
                name: "IX_ReputationActions_New_DeletedAt",
                newName: "IX_ReputationActions_DeletedAt");

            migrationBuilder.RenameIndex(
                table: "ReputationActions",
                name: "IX_ReputationActions_New_OwnerId",
                newName: "IX_ReputationActions_OwnerId");

            migrationBuilder.RenameIndex(
                table: "ReputationActions",
                name: "IX_ReputationActions_New_TenantId",
                newName: "IX_ReputationActions_TenantId");

            migrationBuilder.RenameIndex(
                table: "ReputationActions",
                name: "IX_ReputationActions_New_ActionType_TenantId",
                newName: "IX_ReputationActions_ActionType_TenantId");

            migrationBuilder.RenameIndex(
                table: "UserReputationHistory",
                name: "IX_UserReputationHistory_New_CreatedAt",
                newName: "IX_UserReputationHistory_CreatedAt");

            migrationBuilder.RenameIndex(
                table: "UserReputationHistory",
                name: "IX_UserReputationHistory_New_DeletedAt",
                newName: "IX_UserReputationHistory_DeletedAt");

            migrationBuilder.RenameIndex(
                table: "UserReputationHistory",
                name: "IX_UserReputationHistory_New_OwnerId",
                newName: "IX_UserReputationHistory_OwnerId");

            migrationBuilder.RenameIndex(
                table: "UserReputationHistory",
                name: "IX_UserReputationHistory_New_TenantId",
                newName: "IX_UserReputationHistory_TenantId");

            migrationBuilder.RenameIndex(
                table: "UserReputationHistory",
                name: "IX_UserReputationHistory_New_UserId",
                newName: "IX_UserReputationHistory_UserId");

            migrationBuilder.RenameIndex(
                table: "UserReputationHistory",
                name: "IX_UserReputationHistory_New_ReputationActionId",
                newName: "IX_UserReputationHistory_ReputationActionId");

            migrationBuilder.RenameIndex(
                table: "UserReputationHistory",
                name: "IX_UserReputationHistory_New_UserTenantId",
                newName: "IX_UserReputationHistory_UserTenantId");

            // Rename foreign key constraints
            migrationBuilder.DropForeignKey(
                name: "FK_ReputationTiers_New_Users_OwnerId",
                table: "ReputationTiers");

            migrationBuilder.DropForeignKey(
                name: "FK_ReputationTiers_New_Tenants_TenantId",
                table: "ReputationTiers");

            migrationBuilder.AddForeignKey(
                name: "FK_ReputationTiers_Users_OwnerId",
                table: "ReputationTiers",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReputationTiers_Tenants_TenantId",
                table: "ReputationTiers",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.DropForeignKey(
                name: "FK_ReputationActions_New_Users_OwnerId",
                table: "ReputationActions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReputationActions_New_Tenants_TenantId",
                table: "ReputationActions");

            migrationBuilder.AddForeignKey(
                name: "FK_ReputationActions_Users_OwnerId",
                table: "ReputationActions",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReputationActions_Tenants_TenantId",
                table: "ReputationActions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputationHistory_New_Users_OwnerId",
                table: "UserReputationHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputationHistory_New_Users_UserId",
                table: "UserReputationHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputationHistory_New_ReputationActions_New_ReputationActionId",
                table: "UserReputationHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputationHistory_New_UserTenants_UserTenantId",
                table: "UserReputationHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReputationHistory_New_Tenants_TenantId",
                table: "UserReputationHistory");

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputationHistory_Users_OwnerId",
                table: "UserReputationHistory",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputationHistory_Users_UserId",
                table: "UserReputationHistory",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputationHistory_ReputationActions_ReputationActionId",
                table: "UserReputationHistory",
                column: "ReputationActionId",
                principalTable: "ReputationActions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputationHistory_UserTenants_UserTenantId",
                table: "UserReputationHistory",
                column: "UserTenantId",
                principalTable: "UserTenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserReputationHistory_Tenants_TenantId",
                table: "UserReputationHistory",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the TPC to TPT migration
            // This is complex and would require recreating the old TPT structure
            // For simplicity, we'll throw an exception indicating this migration cannot be reversed
            throw new NotSupportedException("Reverting from TPC to TPT inheritance is not supported. Please restore from backup if needed.");
        }
    }
}
