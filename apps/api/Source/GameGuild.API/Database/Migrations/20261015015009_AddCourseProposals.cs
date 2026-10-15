using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "course_proposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "submitted"),
                    CourseTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Pitch = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Audience = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProposerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ProposerEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Metadata = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_course_proposals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_course_proposals_Slug",
                table: "course_proposals",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_course_proposals_Status_CreatedAt",
                table: "course_proposals",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "course_proposals");
        }
    }
}
