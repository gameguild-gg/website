using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.Migrations
{
    /// <inheritdoc />
    public partial class UpdateContentTypePermissionWithPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "PermissionFlags1",
                table: "ContentTypePermissions",
                type: "bigint",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<ulong>(
                name: "PermissionFlags2",
                table: "ContentTypePermissions",
                type: "bigint",
                nullable: false,
                defaultValue: 0ul);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PermissionFlags1",
                table: "ContentTypePermissions");

            migrationBuilder.DropColumn(
                name: "PermissionFlags2",
                table: "ContentTypePermissions");
        }
    }
}
