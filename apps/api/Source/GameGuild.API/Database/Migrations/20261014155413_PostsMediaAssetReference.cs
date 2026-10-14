using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameGuild.API.Database.Migrations
{
    /// <inheritdoc />
    public partial class PostsMediaAssetReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MediaAssetReferenceId",
                table: "posts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_posts_MediaAssetReferenceId",
                table: "posts",
                column: "MediaAssetReferenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_posts_MediaAssetReferenceId",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "MediaAssetReferenceId",
                table: "posts");
        }
    }
}
