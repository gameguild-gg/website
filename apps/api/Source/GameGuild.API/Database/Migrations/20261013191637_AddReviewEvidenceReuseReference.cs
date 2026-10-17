using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewEvidenceReuseReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceReviewEvidenceId",
                table: "ReviewEvidence",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewEvidence_SourceReviewEvidenceId",
                table: "ReviewEvidence",
                column: "SourceReviewEvidenceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewEvidence_ReviewEvidence_SourceReviewEvidenceId",
                table: "ReviewEvidence",
                column: "SourceReviewEvidenceId",
                principalTable: "ReviewEvidence",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewEvidence_ReviewEvidence_SourceReviewEvidenceId",
                table: "ReviewEvidence");

            migrationBuilder.DropIndex(
                name: "IX_ReviewEvidence_SourceReviewEvidenceId",
                table: "ReviewEvidence");

            migrationBuilder.DropColumn(
                name: "SourceReviewEvidenceId",
                table: "ReviewEvidence");
        }
    }
}
