using FluentAssertions;
using GameGuild.API.Database.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GameGuild.API.UnitTests.Database;

public sealed class SocialFeedMigrationTests
{
    [Fact]
    public void ProductionSocialFeedMigration_UpIsAdditiveAndComplete()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        new ExposedSocialFeedMigration().BuildUp(builder);

        builder.Operations.OfType<CreateTableOperation>()
            .Select(operation => operation.Name)
            .Should().BeEquivalentTo("social_saved_posts", "social_stories", "social_story_views");
        builder.Operations.OfType<CreateIndexOperation>()
            .Should().Contain(operation => operation.Name == "IX_posts_AuthorId_RepostOfPostId" && operation.IsUnique)
            .And.Contain(operation => operation.Name == "IX_social_saved_posts_UserId_PostId" && operation.IsUnique)
            .And.Contain(operation => operation.Name == "IX_social_story_views_StoryId_ViewerId" && operation.IsUnique);
        builder.Operations.OfType<DropTableOperation>().Should().BeEmpty();
        builder.Operations.OfType<DropColumnOperation>().Should().BeEmpty();
        builder.Operations.OfType<AlterColumnOperation>().Should().BeEmpty();
    }

    private sealed class ExposedSocialFeedMigration : AddProductionSocialFeed
    {
        public void BuildUp(MigrationBuilder migrationBuilder) => Up(migrationBuilder);
    }
}
