using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceAccountCertificateBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "certificate_spki_sha256",
                schema: "gameguild.authentication",
                table: "service_accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_thumbprint",
                schema: "gameguild.authentication",
                table: "service_accounts",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_service_accounts_certificate_spki_sha256",
                schema: "gameguild.authentication",
                table: "service_accounts",
                column: "certificate_spki_sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_service_accounts_certificate_thumbprint",
                schema: "gameguild.authentication",
                table: "service_accounts",
                column: "certificate_thumbprint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_service_accounts_certificate_spki_sha256",
                schema: "gameguild.authentication",
                table: "service_accounts");

            migrationBuilder.DropIndex(
                name: "idx_service_accounts_certificate_thumbprint",
                schema: "gameguild.authentication",
                table: "service_accounts");

            migrationBuilder.DropColumn(
                name: "certificate_spki_sha256",
                schema: "gameguild.authentication",
                table: "service_accounts");

            migrationBuilder.DropColumn(
                name: "certificate_thumbprint",
                schema: "gameguild.authentication",
                table: "service_accounts");
        }
    }
}
