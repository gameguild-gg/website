using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRevenueAuditStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_trails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    OldValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NewValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ChangedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_trails", x => x.Id);
                    table.CheckConstraint("CK_AuditTrail_EntityType_NotEmpty", "LENGTH(\"EntityType\") > 0");
                    table.CheckConstraint("CK_AuditTrail_EntityId_NotEmpty", "\"EntityId\" != '00000000-0000-0000-0000-000000000000'");
                });

            migrationBuilder.CreateIndex(name: "IX_audit_trails_EntityType", table: "audit_trails", column: "EntityType");
            migrationBuilder.CreateIndex(name: "IX_audit_trails_EntityId", table: "audit_trails", column: "EntityId");
            migrationBuilder.CreateIndex(name: "IX_audit_trails_Action", table: "audit_trails", column: "Action");
            migrationBuilder.CreateIndex(name: "IX_audit_trails_ChangedBy", table: "audit_trails", column: "ChangedBy");
            migrationBuilder.CreateIndex(name: "IX_audit_trails_ChangedAt", table: "audit_trails", column: "ChangedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "audit_trails");
        }
    }
}
