using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cms.Migrations
{
    /// <inheritdoc />
    public partial class UpdateToBaseEntityStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop old unique indexes
            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            // Drop old columns
            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FirstName", 
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Users");

            // Add new columns for BaseEntity
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Users",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Users",
                type: "TEXT",
                nullable: true);

            // Update existing data - combine FirstName and LastName into Name if they exist
            // This would be done via SQL if there was existing data
            // migrationBuilder.Sql("UPDATE Users SET Name = COALESCE(FirstName || ' ' || LastName, 'Unknown User') WHERE Name = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add back old columns
            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Users",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Users",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            // Remove new columns
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Users");

            // Recreate old unique index
            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }
    }
}
