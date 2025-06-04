using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cms.Migrations
{
    /// <inheritdoc />
    public partial class FixReputationSystemTPTInheritance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ResourcePermissions_ResourceRoles_ResourceRoleId",
                table: "ResourcePermissions");

            migrationBuilder.DropTable(
                name: "ResourceRoles");

            migrationBuilder.DropIndex(
                name: "IX_ResourcePermissions_ResourceRoleId",
                table: "ResourcePermissions");

            migrationBuilder.DropColumn(
                name: "ResourceRoleId",
                table: "ResourcePermissions");

            migrationBuilder.CreateTable(
                name: "ReputationLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MinimumScore = table.Column<int>(type: "INTEGER", nullable: false),
                    MaximumScore = table.Column<int>(type: "INTEGER", nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReputationLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReputationLevels_Resources_Id",
                        column: x => x.Id,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReputationActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ActionType = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Points = table.Column<int>(type: "INTEGER", nullable: false),
                    DailyLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalLimit = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiredLevelId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReputationActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReputationActions_ReputationLevels_RequiredLevelId",
                        column: x => x.RequiredLevelId,
                        principalTable: "ReputationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReputationActions_Resources_Id",
                        column: x => x.Id,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserReputations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentLevelId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLevelCalculation = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PositiveChanges = table.Column<int>(type: "INTEGER", nullable: false),
                    NegativeChanges = table.Column<int>(type: "INTEGER", nullable: false),
                    ReputationLevelId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReputations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserReputations_ReputationLevels_CurrentLevelId",
                        column: x => x.CurrentLevelId,
                        principalTable: "ReputationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserReputations_ReputationLevels_ReputationLevelId",
                        column: x => x.ReputationLevelId,
                        principalTable: "ReputationLevels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserReputations_Resources_Id",
                        column: x => x.Id,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserReputations_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTenantReputations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserTenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentLevelId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLevelCalculation = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PositiveChanges = table.Column<int>(type: "INTEGER", nullable: false),
                    NegativeChanges = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTenantReputations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTenantReputations_ReputationLevels_CurrentLevelId",
                        column: x => x.CurrentLevelId,
                        principalTable: "ReputationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserTenantReputations_Resources_Id",
                        column: x => x.Id,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTenantReputations_UserTenants_UserTenantId",
                        column: x => x.UserTenantId,
                        principalTable: "UserTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserReputationHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserTenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReputationActionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PointsChange = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousScore = table.Column<int>(type: "INTEGER", nullable: false),
                    NewScore = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousLevelId = table.Column<Guid>(type: "TEXT", nullable: true),
                    NewLevelId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TriggeredByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RelatedResourceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserReputationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UserTenantReputationId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReputationHistory", x => x.Id);
                    table.CheckConstraint("CK_UserReputationHistory_UserOrUserTenant", "(\"UserId\" IS NOT NULL AND \"UserTenantId\" IS NULL) OR (\"UserId\" IS NULL AND \"UserTenantId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_ReputationActions_ReputationActionId",
                        column: x => x.ReputationActionId,
                        principalTable: "ReputationActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_ReputationLevels_NewLevelId",
                        column: x => x.NewLevelId,
                        principalTable: "ReputationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_ReputationLevels_PreviousLevelId",
                        column: x => x.PreviousLevelId,
                        principalTable: "ReputationLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_Resources_Id",
                        column: x => x.Id,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_Resources_RelatedResourceId",
                        column: x => x.RelatedResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_UserReputations_UserReputationId",
                        column: x => x.UserReputationId,
                        principalTable: "UserReputations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_UserTenantReputations_UserTenantReputationId",
                        column: x => x.UserTenantReputationId,
                        principalTable: "UserTenantReputations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_UserTenants_UserTenantId",
                        column: x => x.UserTenantId,
                        principalTable: "UserTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_Users_TriggeredByUserId",
                        column: x => x.TriggeredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserReputationHistory_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_ActionType",
                table: "ReputationActions",
                column: "ActionType",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_IsActive",
                table: "ReputationActions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_Points",
                table: "ReputationActions",
                column: "Points");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationActions_RequiredLevelId",
                table: "ReputationActions",
                column: "RequiredLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_MinimumScore",
                table: "ReputationLevels",
                column: "MinimumScore");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_Name",
                table: "ReputationLevels",
                column: "Name",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReputationLevels_SortOrder",
                table: "ReputationLevels",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_NewLevelId",
                table: "UserReputationHistory",
                column: "NewLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_OccurredAt",
                table: "UserReputationHistory",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_PointsChange",
                table: "UserReputationHistory",
                column: "PointsChange");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_PreviousLevelId",
                table: "UserReputationHistory",
                column: "PreviousLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_RelatedResourceId",
                table: "UserReputationHistory",
                column: "RelatedResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_ReputationActionId",
                table: "UserReputationHistory",
                column: "ReputationActionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_TriggeredByUserId",
                table: "UserReputationHistory",
                column: "TriggeredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_UserId",
                table: "UserReputationHistory",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_UserReputationId",
                table: "UserReputationHistory",
                column: "UserReputationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_UserTenantId",
                table: "UserReputationHistory",
                column: "UserTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputationHistory_UserTenantReputationId",
                table: "UserReputationHistory",
                column: "UserTenantReputationId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_CurrentLevelId",
                table: "UserReputations",
                column: "CurrentLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_ReputationLevelId",
                table: "UserReputations",
                column: "ReputationLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_Score",
                table: "UserReputations",
                column: "Score");

            migrationBuilder.CreateIndex(
                name: "IX_UserReputations_UserId",
                table: "UserReputations",
                column: "UserId",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_CurrentLevelId",
                table: "UserTenantReputations",
                column: "CurrentLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_Score",
                table: "UserTenantReputations",
                column: "Score");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantReputations_UserTenantId",
                table: "UserTenantReputations",
                column: "UserTenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserReputationHistory");

            migrationBuilder.DropTable(
                name: "ReputationActions");

            migrationBuilder.DropTable(
                name: "UserReputations");

            migrationBuilder.DropTable(
                name: "UserTenantReputations");

            migrationBuilder.DropTable(
                name: "ReputationLevels");

            migrationBuilder.AddColumn<Guid>(
                name: "ResourceRoleId",
                table: "ResourcePermissions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ResourceRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DefaultPermission = table.Column<int>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ResourceBaseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "INTEGER", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceRoles_Resources_ResourceBaseId",
                        column: x => x.ResourceBaseId,
                        principalTable: "Resources",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePermissions_ResourceRoleId",
                table: "ResourcePermissions",
                column: "ResourceRoleId");

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

            migrationBuilder.CreateIndex(
                name: "IX_ResourceRoles_ResourceBaseId",
                table: "ResourceRoles",
                column: "ResourceBaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_ResourcePermissions_ResourceRoles_ResourceRoleId",
                table: "ResourcePermissions",
                column: "ResourceRoleId",
                principalTable: "ResourceRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
