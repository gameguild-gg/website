using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.Migrations
{
    /// <inheritdoc />
    public partial class CleanPermissionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop role-based tables if they exist
            migrationBuilder.Sql("DROP TABLE IF EXISTS program_user_roles;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS UserTenantRoles;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS TenantRoles;");

            migrationBuilder.DropColumn(
                name: "CurationPermissions",
                table: "ResourcePermissions");

            migrationBuilder.DropColumn(
                name: "EditorialPermissions",
                table: "ResourcePermissions");

            migrationBuilder.DropColumn(
                name: "InteractionPermissions",
                table: "ResourcePermissions");

            migrationBuilder.DropColumn(
                name: "LifecyclePermissions",
                table: "ResourcePermissions");

            migrationBuilder.DropColumn(
                name: "ModerationPermissions",
                table: "ResourcePermissions");

            migrationBuilder.DropColumn(
                name: "MonetizationPermissions",
                table: "ResourcePermissions");

            migrationBuilder.DropColumn(
                name: "PromotionPermissions",
                table: "ResourcePermissions");

            migrationBuilder.DropColumn(
                name: "PublishingPermissions",
                table: "ResourcePermissions");

            migrationBuilder.DropColumn(
                name: "QualityPermissions",
                table: "ResourcePermissions");

            migrationBuilder.DropColumn(
                name: "CurationPermissions",
                table: "ContentTypePermissions");

            migrationBuilder.DropColumn(
                name: "EditorialPermissions",
                table: "ContentTypePermissions");

            migrationBuilder.DropColumn(
                name: "InteractionPermissions",
                table: "ContentTypePermissions");

            migrationBuilder.DropColumn(
                name: "LifecyclePermissions",
                table: "ContentTypePermissions");

            migrationBuilder.DropColumn(
                name: "ModerationPermissions",
                table: "ContentTypePermissions");

            migrationBuilder.DropColumn(
                name: "MonetizationPermissions",
                table: "ContentTypePermissions");

            migrationBuilder.DropColumn(
                name: "PromotionPermissions",
                table: "ContentTypePermissions");

            migrationBuilder.DropColumn(
                name: "PublishingPermissions",
                table: "ContentTypePermissions");

            migrationBuilder.DropColumn(
                name: "QualityPermissions",
                table: "ContentTypePermissions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurationPermissions",
                table: "ResourcePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EditorialPermissions",
                table: "ResourcePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InteractionPermissions",
                table: "ResourcePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LifecyclePermissions",
                table: "ResourcePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ModerationPermissions",
                table: "ResourcePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MonetizationPermissions",
                table: "ResourcePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PromotionPermissions",
                table: "ResourcePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PublishingPermissions",
                table: "ResourcePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QualityPermissions",
                table: "ResourcePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurationPermissions",
                table: "ContentTypePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EditorialPermissions",
                table: "ContentTypePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InteractionPermissions",
                table: "ContentTypePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LifecyclePermissions",
                table: "ContentTypePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ModerationPermissions",
                table: "ContentTypePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MonetizationPermissions",
                table: "ContentTypePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PromotionPermissions",
                table: "ContentTypePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PublishingPermissions",
                table: "ContentTypePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QualityPermissions",
                table: "ContentTypePermissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "program_user_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProgramId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProgramUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActiveFrom = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ActiveUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_user_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_program_user_roles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_program_user_roles_program_users_ProgramUserId",
                        column: x => x.ProgramUserId,
                        principalTable: "program_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_program_user_roles_programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Permissions = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantRoles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTenantRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TenantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TenantRoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserTenantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTenantRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTenantRoles_TenantRoles_TenantRoleId",
                        column: x => x.TenantRoleId,
                        principalTable: "TenantRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTenantRoles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserTenantRoles_UserTenants_UserTenantId",
                        column: x => x.UserTenantId,
                        principalTable: "UserTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_CreatedAt",
                table: "program_user_roles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_DeletedAt",
                table: "program_user_roles",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_ProgramId",
                table: "program_user_roles",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_ProgramUserId",
                table: "program_user_roles",
                column: "ProgramUserId");

            migrationBuilder.CreateIndex(
                name: "IX_program_user_roles_TenantId",
                table: "program_user_roles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRoles_CreatedAt",
                table: "TenantRoles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRoles_DeletedAt",
                table: "TenantRoles",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRoles_TenantId_Name",
                table: "TenantRoles",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantRoles_CreatedAt",
                table: "UserTenantRoles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantRoles_DeletedAt",
                table: "UserTenantRoles",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantRoles_TenantId",
                table: "UserTenantRoles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantRoles_TenantRoleId",
                table: "UserTenantRoles",
                column: "TenantRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantRoles_UserTenantId_TenantRoleId",
                table: "UserTenantRoles",
                columns: new[] { "UserTenantId", "TenantRoleId" },
                unique: true);
        }
    }
}
