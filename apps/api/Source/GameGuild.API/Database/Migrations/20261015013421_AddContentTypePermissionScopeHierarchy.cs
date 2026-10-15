using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddContentTypePermissionScopeHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppliesTo",
                schema: "gameguild.authentication",
                table: "contenttypepermission",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRegistered",
                schema: "gameguild.authentication",
                table: "contenttypepermission",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ParentContentTypeName",
                schema: "gameguild.authentication",
                table: "contenttypepermission",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Restrictions",
                schema: "gameguild.authentication",
                table: "contenttypepermission",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_contenttypepermission_parent_contenttype",
                schema: "gameguild.authentication",
                table: "contenttypepermission",
                column: "ParentContentTypeName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_contenttypepermission_parent_contenttype",
                schema: "gameguild.authentication",
                table: "contenttypepermission");

            migrationBuilder.DropColumn(
                name: "AppliesTo",
                schema: "gameguild.authentication",
                table: "contenttypepermission");

            migrationBuilder.DropColumn(
                name: "IsRegistered",
                schema: "gameguild.authentication",
                table: "contenttypepermission");

            migrationBuilder.DropColumn(
                name: "ParentContentTypeName",
                schema: "gameguild.authentication",
                table: "contenttypepermission");

            migrationBuilder.DropColumn(
                name: "Restrictions",
                schema: "gameguild.authentication",
                table: "contenttypepermission");
        }
    }
}
