using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionTemplateLifecycleVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "RoleKeys",
                table: "PermissionTemplates",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.CreateTable(
                name: "PermissionTemplateInstantiations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    InstantiatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstantiatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastMigrationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionTemplateInstantiations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermissionTemplateMigrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromVersion = table.Column<int>(type: "integer", nullable: false),
                    ToVersion = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Strategy = table.Column<int>(type: "integer", nullable: false),
                    ScheduledFor = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InitiatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AffectedTenantIds = table.Column<Guid[]>(type: "uuid[]", nullable: true),
                    AffectedUserIds = table.Column<Guid[]>(type: "uuid[]", nullable: true),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCount = table.Column<int>(type: "integer", nullable: false),
                    Errors = table.Column<string>(type: "text", nullable: true),
                    Log = table.Column<string>(type: "text", nullable: true),
                    RollbackPlan = table.Column<string>(type: "text", nullable: true),
                    DryRunResult = table.Column<string>(type: "text", nullable: true),
                    IsDryRun = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionTemplateMigrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermissionTemplateVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeNotes = table.Column<string>(type: "text", nullable: true),
                    ChangeType = table.Column<int>(type: "integer", nullable: false),
                    AddedPermissions = table.Column<string[]>(type: "text[]", nullable: true),
                    RemovedPermissions = table.Column<string[]>(type: "text[]", nullable: true),
                    UnchangedPermissions = table.Column<string[]>(type: "text[]", nullable: true),
                    PreviousVersion = table.Column<int>(type: "integer", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    PermissionHash = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string[]>(type: "text[]", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionTemplateVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionTemplateInstantiations_Template_Version",
                table: "PermissionTemplateInstantiations",
                columns: new[] { "TemplateId", "TemplateVersionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_PermissionTemplateInstantiations_TemplateId",
                table: "PermissionTemplateInstantiations",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionTemplateInstantiations_Unique_Target",
                table: "PermissionTemplateInstantiations",
                columns: new[] { "TemplateId", "UserId", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionTemplateMigrations_TemplateId",
                table: "PermissionTemplateMigrations",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PermissionTemplateVersions_Template_Version",
                table: "PermissionTemplateVersions",
                columns: new[] { "TemplateId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PermissionTemplateInstantiations");

            migrationBuilder.DropTable(
                name: "PermissionTemplateMigrations");

            migrationBuilder.DropTable(
                name: "PermissionTemplateVersions");

            migrationBuilder.DropColumn(
                name: "RoleKeys",
                table: "PermissionTemplates");
        }
    }
}
