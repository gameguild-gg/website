using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUniversalEntityChangeAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "gameguild.audit");

            migrationBuilder.CreateTable(
                name: "entity_change_audit_records",
                schema: "gameguild.audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    KeyId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ChangeKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BeforeValues = table.Column<string>(type: "jsonb", nullable: true),
                    AfterValues = table.Column<string>(type: "jsonb", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_change_audit_records", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_entity_change_audit_correlation",
                schema: "gameguild.audit",
                table: "entity_change_audit_records",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "ix_entity_change_audit_entity",
                schema: "gameguild.audit",
                table: "entity_change_audit_records",
                columns: new[] { "EntityName", "KeyId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_entity_change_audit_time",
                schema: "gameguild.audit",
                table: "entity_change_audit_records",
                column: "OccurredAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entity_change_audit_records",
                schema: "gameguild.audit");
        }
    }
}
