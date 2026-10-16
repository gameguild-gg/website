using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaSmsAndPushEnrollmentColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "push_device_token",
                schema: "gameguild.authentication",
                table: "user_mfa_configuration",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sms_phone_number",
                schema: "gameguild.authentication",
                table: "user_mfa_configuration",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "push_device_token",
                schema: "gameguild.authentication",
                table: "user_mfa_configuration");

            migrationBuilder.DropColumn(
                name: "sms_phone_number",
                schema: "gameguild.authentication",
                table: "user_mfa_configuration");
        }
    }
}
