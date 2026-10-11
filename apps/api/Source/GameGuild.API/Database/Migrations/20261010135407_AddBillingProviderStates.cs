using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingProviderStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_provider_states",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_provider_states", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_billing_provider_states_provider_key",
                table: "billing_provider_states",
                column: "ProviderKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_provider_states");
        }
    }
}
