using FluentAssertions;
using GameGuild.Social.Follows;
using GameGuild.Social.Follows.Services;
using GameGuild.Social.Posts;
using GameGuild.Social.Profiles;
using GameGuild.Social.Reactions;
using GameGuild.TestingLab;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.Social.Feed.UnitTests;

public sealed class SocialFeedQueryServiceTests
{
    [Fact]
    public async Task ForYou_ExcludesPrivateBlockedAndMutedPosts_AndProjectsViewerState()
    {
        await using var context = CreateContext();
        var viewerId = Guid.NewGuid();
        var visibleAuthor = Guid.NewGuid();
        var blockedAuthor = Guid.NewGuid();
        var mutedAuthor = Guid.NewGuid();
        var visible = AddPost(context, visibleAuthor, "visible", minutesAgo: 1);
        AddPost(context, Guid.NewGuid(), "private", PostVisibility.Private, minutesAgo: 2);
        AddPost(context, blockedAuthor, "blocked", minutesAgo: 3);
        AddPost(context, mutedAuthor, "muted", minutesAgo: 4);
        AddProfile(context, visibleAuthor, "visible-creator", "Visible Creator");
        context.Set<Block>().Add(Block.Create(viewerId, blockedAuthor));
        context.Set<Mute>().Add(Mute.Create(viewerId, mutedAuthor));
        context.Set<Reaction>().Add(Reaction.Create(viewerId, visible.Id, ReactionTargetType.Post, ReactionType.Love));
        context.Set<SavedPost>().Add(SavedPost.Create(viewerId, visible.Id));
        context.Set<Follow>().Add(Follow.Create(viewerId, visibleAuthor, FollowableEntityTypes.User));
        await context.SaveChangesAsync();

        var page = await new SocialFeedQueryService(context)
            .GetAsync(viewerId, FeedScope.ForYou, null, 30, null, default);

        page.Items.Should().ContainSingle();
        var item = page.Items[0];
        item.Id.Should().Be(visible.Id);
        item.Author.Handle.Should().Be("visible-creator");
        item.Viewer.Reaction.Should().Be("Love");
        item.Viewer.IsSaved.Should().BeTrue();
        item.Viewer.IsFollowingAuthor.Should().BeTrue();
    }

    [Fact]
    public async Task Following_ReturnsOnlyFollowedAuthorsAndFollowersVisibility()
    {
        await using var context = CreateContext();
        var viewerId = Guid.NewGuid();
        var followedAuthor = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var expected = AddPost(context, followedAuthor, "followers only", PostVisibility.Followers);
        AddPost(context, stranger, "not followed");
        context.Set<Follow>().Add(Follow.Create(viewerId, followedAuthor, FollowableEntityTypes.User));
        await context.SaveChangesAsync();

        var page = await new SocialFeedQueryService(context)
            .GetAsync(viewerId, FeedScope.Following, null, 30, null, default);

        page.Items.Select(item => item.Id).Should().Equal(expected.Id);
    }

    [Fact]
    public async Task Saved_ReturnsOnlyActorsSavedPostsInSaveOrder()
    {
        await using var context = CreateContext();
        var viewerId = Guid.NewGuid();
        var first = AddPost(context, Guid.NewGuid(), "first");
        var latest = AddPost(context, Guid.NewGuid(), "latest");
        var otherUsersPost = AddPost(context, Guid.NewGuid(), "other save");
        context.Set<SavedPost>().AddRange(
            Saved(viewerId, first.Id, minutesAgo: 3),
            Saved(viewerId, latest.Id, minutesAgo: 1),
            Saved(Guid.NewGuid(), otherUsersPost.Id, minutesAgo: 0));
        await context.SaveChangesAsync();

        var page = await new SocialFeedQueryService(context)
            .GetAsync(viewerId, FeedScope.Saved, null, 30, null, default);

        page.Items.Select(item => item.Id).Should().Equal(latest.Id, first.Id);
        page.Items.Should().OnlyContain(item => item.Viewer.IsSaved);
    }

    [Fact]
    public async Task Community_ReturnsPublishedTestingSessionsAsDistinctItems()
    {
        await using var context = CreateContext();
        var viewerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var published = AddSession(context, managerId, "Creator playtest", SessionStatus.Scheduled);
        AddSession(context, managerId, "Cancelled", SessionStatus.Cancelled);
        AddPost(context, managerId, "normal post");
        await context.SaveChangesAsync();

        var page = await new SocialFeedQueryService(context)
            .GetAsync(viewerId, FeedScope.Community, null, 30, null, default);

        page.Items.Should().ContainSingle();
        page.Items[0].Id.Should().Be(published.Id);
        page.Items[0].Kind.Should().Be(SocialFeedItemKind.TestingSession);
        page.Items[0].TestingSession!.Name.Should().Be("Creator playtest");
    }

    [Fact]
    public async Task TagAndCursor_ProduceStableNonOverlappingPages()
    {
        await using var context = CreateContext();
        var viewerId = Guid.NewGuid();
        var tag = PostTag.Create("indiedev");
        var newest = AddPost(context, Guid.NewGuid(), "new", minutesAgo: 1);
        var second = AddPost(context, Guid.NewGuid(), "second", minutesAgo: 2);
        AddPost(context, Guid.NewGuid(), "untagged", minutesAgo: 3);
        context.Set<PostTag>().Add(tag);
        context.Set<PostTagAssignment>().AddRange(
            PostTagAssignment.Create(newest.Id, tag.Id),
            PostTagAssignment.Create(second.Id, tag.Id));
        await context.SaveChangesAsync();
        var service = new SocialFeedQueryService(context);

        var firstPage = await service.GetAsync(viewerId, FeedScope.ForYou, null, 1, "#IndieDev", default);
        var secondPage = await service.GetAsync(viewerId, FeedScope.ForYou, firstPage.NextCursor, 1, "indiedev", default);

        firstPage.Items.Select(item => item.Id).Should().Equal(newest.Id);
        secondPage.Items.Select(item => item.Id).Should().Equal(second.Id);
        firstPage.NextCursor.Should().NotBeNull();
        secondPage.Items.Should().NotIntersectWith(firstPage.Items);
    }

    private static SocialFeedTestDbContext CreateContext()
        => new(new DbContextOptionsBuilder<SocialFeedTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Post AddPost(
        SocialFeedTestDbContext context,
        Guid authorId,
        string content,
        PostVisibility visibility = PostVisibility.Public,
        int minutesAgo = 0)
    {
        var post = Post.Create(authorId, content, visibility);
        post.CreatedAt = DateTime.UtcNow.AddMinutes(-minutesAgo);
        context.Set<Post>().Add(post);
        return post;
    }

    private static SavedPost Saved(Guid userId, Guid postId, int minutesAgo)
    {
        var saved = SavedPost.Create(userId, postId);
        saved.CreatedAt = DateTime.UtcNow.AddMinutes(-minutesAgo);
        return saved;
    }

    private static void AddProfile(SocialFeedTestDbContext context, Guid userId, string handle, string name)
        => context.Set<SocialProfile>().Add(new SocialProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Handle = handle,
            DisplayName = name,
            Visibility = ProfileVisibility.Public
        });

    private static TestingSession AddSession(
        SocialFeedTestDbContext context,
        Guid managerId,
        string name,
        SessionStatus status)
    {
        var startsAt = DateTime.UtcNow.AddDays(2);
        var session = new TestingSession
        {
            Id = Guid.NewGuid(),
            TestingRequestId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            SessionName = name,
            SessionDate = startsAt.Date,
            StartTime = startsAt,
            EndTime = startsAt.AddHours(2),
            MaxTesters = 20,
            ManagerId = managerId,
            Status = status,
            ManagerUserId = managerId,
            CreatedById = managerId,
            CreatedAt = DateTime.UtcNow
        };
        context.Set<TestingSession>().Add(session);
        return session;
    }
}

internal sealed class SocialFeedTestDbContext(DbContextOptions<SocialFeedTestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Post>().HasKey(entity => entity.Id);
        modelBuilder.Entity<PostComment>().HasKey(entity => entity.Id);
        modelBuilder.Entity<PostTag>().HasKey(entity => entity.Id);
        modelBuilder.Entity<PostTagAssignment>().HasKey(entity => entity.Id);
        modelBuilder.Entity<SocialProfile>().HasKey(entity => entity.Id);
        modelBuilder.Entity<Follow>().HasKey(entity => entity.Id);
        modelBuilder.Entity<Block>().HasKey(entity => entity.Id);
        modelBuilder.Entity<Mute>().HasKey(entity => entity.Id);
        modelBuilder.Entity<Reaction>().HasKey(entity => entity.Id);
        modelBuilder.Entity<TestingSession>().HasKey(entity => entity.Id);
        new FeedModelConfiguration().Configure(modelBuilder);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
