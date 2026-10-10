using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddScimProvisioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scim_group_mappings",
                schema: "gameguild.authentication",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scim_group_mappings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scim_provisioning_tokens",
                schema: "gameguild.authentication",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    key_prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    scopes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usage_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    replaces_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rotation_grace_ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scim_provisioning_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scim_user_mappings",
                schema: "gameguild.authentication",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scim_user_mappings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_scim_group_mappings_role_id",
                schema: "gameguild.authentication",
                table: "scim_group_mappings",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_scim_group_mappings_tenant_id_external_id",
                schema: "gameguild.authentication",
                table: "scim_group_mappings",
                columns: new[] { "tenant_id", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scim_provisioning_tokens_expires_at",
                schema: "gameguild.authentication",
                table: "scim_provisioning_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_scim_provisioning_tokens_is_active",
                schema: "gameguild.authentication",
                table: "scim_provisioning_tokens",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_scim_provisioning_tokens_key_hash",
                schema: "gameguild.authentication",
                table: "scim_provisioning_tokens",
                column: "key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scim_provisioning_tokens_replaces_token_id",
                schema: "gameguild.authentication",
                table: "scim_provisioning_tokens",
                column: "replaces_token_id");

            migrationBuilder.CreateIndex(
                name: "ix_scim_provisioning_tokens_tenant_id",
                schema: "gameguild.authentication",
                table: "scim_provisioning_tokens",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_scim_user_mappings_tenant_id_external_id",
                schema: "gameguild.authentication",
                table: "scim_user_mappings",
                columns: new[] { "tenant_id", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scim_user_mappings_user_id",
                schema: "gameguild.authentication",
                table: "scim_user_mappings",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scim_group_mappings",
                schema: "gameguild.authentication");

            migrationBuilder.DropTable(
                name: "scim_provisioning_tokens",
                schema: "gameguild.authentication");

            migrationBuilder.DropTable(
                name: "scim_user_mappings",
                schema: "gameguild.authentication");
        }
    }
}
