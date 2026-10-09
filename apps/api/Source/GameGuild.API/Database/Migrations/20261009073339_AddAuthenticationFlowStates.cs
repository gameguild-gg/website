using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationFlowStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "authentication_flow_states",
                schema: "gameguild.authentication",
                columns: table => new
                {
                    flow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_step = table.Column<int>(type: "integer", nullable: false),
                    required_steps = table.Column<string>(type: "text", nullable: false),
                    completed_steps = table.Column<string>(type: "text", nullable: false),
                    is_complete = table.Column<bool>(type: "boolean", nullable: false),
                    risk_score = table.Column<double>(type: "double precision", nullable: true),
                    initiated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    device_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    step_data = table.Column<string>(type: "text", nullable: true),
                    abandoned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authentication_flow_states", x => x.flow_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_authentication_flow_states_expires_at",
                schema: "gameguild.authentication",
                table: "authentication_flow_states",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_authentication_flow_states_user_id",
                schema: "gameguild.authentication",
                table: "authentication_flow_states",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authentication_flow_states",
                schema: "gameguild.authentication");
        }
    }
}
