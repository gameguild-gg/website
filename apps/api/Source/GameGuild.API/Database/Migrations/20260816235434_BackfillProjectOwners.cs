using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class BackfillProjectOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "ProjectCollaborators" ("Id", "ProjectId", "UserId", "Role", "Permissions", "IsActive", "JoinedAt", "Version", "CreatedAt", "UpdatedAt", "TenantId")
                SELECT gen_random_uuid(), p."Id", p."CreatedById", 'Owner', 'Read,Edit,Delete,Publish,Unpublish,Archive,Create,Approve,Manage', true, now(), 1, now(), now(), p."TenantId"
                FROM projects p
                WHERE p."CreatedById" IS NOT NULL
                  AND p."DeletedAt" IS NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM "ProjectCollaborators" c
                      WHERE c."ProjectId" = p."Id"
                        AND c."UserId" = p."CreatedById"
                        AND c."IsActive"
                        AND c."LeftAt" IS NULL
                        AND c."DeletedAt" IS NULL);

                UPDATE "ProjectCollaborators" c
                SET "Role" = 'Owner', "UpdatedAt" = now()
                FROM projects p
                WHERE c."ProjectId" = p."Id"
                  AND c."UserId" = p."CreatedById"
                  AND c."IsActive"
                  AND c."Role" <> 'Owner';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The backfill does not record which collaborator rows or roles it
            // changed, so rollback cannot distinguish them from existing owners.
        }
    }
}
