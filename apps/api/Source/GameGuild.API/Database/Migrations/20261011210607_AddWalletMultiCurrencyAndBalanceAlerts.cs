using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletMultiCurrencyAndBalanceAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_wallets_UserId",
                table: "user_wallets");

            migrationBuilder.AddColumn<DateTime>(
                name: "LowBalanceAlertedAt",
                table: "user_wallets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_wallets_UserId_Currency",
                table: "user_wallets",
                columns: new[] { "UserId", "Currency" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_wallets_UserId_Currency",
                table: "user_wallets");

            migrationBuilder.DropColumn(
                name: "LowBalanceAlertedAt",
                table: "user_wallets");

            migrationBuilder.CreateIndex(
                name: "IX_user_wallets_UserId",
                table: "user_wallets",
                column: "UserId",
                unique: true);
        }
    }
}
