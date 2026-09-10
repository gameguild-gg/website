using FluentAssertions;
using GameGuild.Assets;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Social.Follows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace GameGuild.Social.Feed.UnitTests;

public sealed class StoryServiceTests
{
    [Fact]
    public async Task CreateAsync_UsesOwnedAssetAndExpiresAfterTwentyFourHours()
    {
        await using var context = CreateContext();
        var authorId = Guid.NewGuid();
        var asset = AddAsset(context, authorId);
        await context.SaveChangesAsync();
        var before = DateTime.UtcNow;
        var service = new StoryService(context);

        var story = await service.CreateAsync(authorId, asset.Id, "First look", default);

        story.AuthorId.Should().Be(authorId);
        story.AssetReferenceId.Should().Be(asset.Id);
        story.ExpiresAt.Should().BeCloseTo(before.AddHours(24), TimeSpan.FromSeconds(2));
        (await context.Set<Story>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_RejectsAnotherUsersMissingOrProcessingAsset()
    {
        await using var context = CreateContext();
        var actorId = Guid.NewGuid();
        var otherAsset = AddAsset(context, Guid.NewGuid());
        var processingAsset = AddAsset(context, actorId, ready: false);
        await context.SaveChangesAsync();
        var service = new StoryService(context);

        var missing = () => service.CreateAsync(actorId, Guid.NewGuid(), null, default);
        var anotherUsers = () => service.CreateAsync(actorId, otherAsset.Id, null, default);
        var processing = () => service.CreateAsync(actorId, processingAsset.Id, null, default);

        await missing.Should().ThrowAsync<StoryAssetUnavailableException>();
        await anotherUsers.Should().ThrowAsync<StoryAssetUnavailableException>();
        await processing.Should().ThrowAsync<StoryAssetUnavailableException>();
    }

    [Fact]
    public async Task MarkViewedAsync_IsIdempotentAndGetActiveReturnsViewerState()
    {
        await using var context = CreateContext();
        var authorId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var asset = AddAsset(context, authorId);
        await context.SaveChangesAsync();
        var service = new StoryService(context);
        var story = await service.CreateAsync(authorId, asset.Id, null, default);

        (await service.MarkViewedAsync(viewerId, story.Id, default)).Should().BeTrue();
        (await service.MarkViewedAsync(viewerId, story.Id, default)).Should().BeFalse();
        var active = await service.GetActiveAsync(viewerId, default);

        (await context.Set<StoryView>().CountAsync()).Should().Be(1);
        active.Should().ContainSingle().Which.IsViewed.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveAsync_ExcludesExpiredBlockedAndMutedAuthors()
    {
        await using var context = CreateContext();
        var viewerId = Guid.NewGuid();
        var visibleAuthor = Guid.NewGuid();
        var blockedAuthor = Guid.NewGuid();
        var mutedAuthor = Guid.NewGuid();
        var service = new StoryService(context);

        var visible = Story.Create(visibleAuthor, AddAsset(context, visibleAuthor).Id, null, DateTime.UtcNow);
        var expired = Story.Create(Guid.NewGuid(), Guid.NewGuid(), null, DateTime.UtcNow.AddDays(-2));
        var blocked = Story.Create(blockedAuthor, AddAsset(context, blockedAuthor).Id, null, DateTime.UtcNow);
        var muted = Story.Create(mutedAuthor, AddAsset(context, mutedAuthor).Id, null, DateTime.UtcNow);
        context.Set<Story>().AddRange(visible, expired, blocked, muted);
        context.Set<Block>().Add(Block.Create(viewerId, blockedAuthor));
        context.Set<Mute>().Add(Mute.Create(viewerId, mutedAuthor));
        await context.SaveChangesAsync();

        var active = await service.GetActiveAsync(viewerId, default);

        active.Select(item => item.Id).Should().Equal(visible.Id);
    }

    [Fact]
    public async Task DeleteAsync_OnlyDeletesAuthorsOwnStory()
    {
        await using var context = CreateContext();
        var authorId = Guid.NewGuid();
        var service = new StoryService(context);
        var asset = AddAsset(context, authorId);
        await context.SaveChangesAsync();
        var story = await service.CreateAsync(authorId, asset.Id, null, default);
        context.Set<Story>().Single(candidate => candidate.Id == story.Id).Version = 1;

        (await service.DeleteAsync(Guid.NewGuid(), story.Id, default)).Should().BeFalse();
        (await service.DeleteAsync(authorId, story.Id, default)).Should().BeTrue();
        (await service.GetActiveAsync(authorId, default)).Should().BeEmpty();
    }

    private static StoryTestDbContext CreateContext()
        => new(new DbContextOptionsBuilder<StoryTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AssetReference AddAsset(StoryTestDbContext context, Guid ownerId, bool ready = true)
    {
        var content = new AssetContent("assets", Guid.NewGuid().ToString("N"), new string('a', 64), "image/png", 128, 1, 1)
        {
            Id = Guid.NewGuid()
        };
        if (ready)
        {
            content.SetVirusScanStatus(VirusScanStatus.Clean);
            content.SetModerationStatus(ModerationStatus.Approved);
        }
        var asset = new AssetReference(content.Id, ownerId, "story", AssetAccessPolicy.Authenticated, "SocialStory", null)
        {
            Id = Guid.NewGuid(),
            Content = content
        };
        context.Set<AssetContent>().Add(content);
        context.Set<AssetReference>().Add(asset);
        return asset;
    }
}

public sealed class StoriesControllerTests
{
    [Fact]
    public async Task Create_UsesAuthenticatedActor()
    {
        var actorId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var dto = new StoryDto(
            Guid.NewGuid(),
            actorId,
            assetId,
            $"/api/assets/{assetId}/content",
            "image/png",
            "caption",
            DateTime.UtcNow.AddHours(24),
            false,
            DateTime.UtcNow);
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(
                It.Is<CreateStoryCommand>(command => command.AuthorId == actorId && command.AssetReferenceId == assetId && command.Caption == "caption"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        var controller = new StoriesController(sender.Object, Actor(actorId));

        var result = await controller.Create(new CreateStoryRequest(assetId, "caption"), default);

        result.Value.Should().Be(dto);
        sender.VerifyAll();
    }

    [Fact]
    public async Task Create_WithoutActor_ReturnsUnauthorized()
    {
        var sender = new Mock<ISender>();
        var controller = new StoriesController(sender.Object, Actor(null));

        var result = await controller.Create(new CreateStoryRequest(Guid.NewGuid()), default);

        result.Result.Should().BeOfType<UnauthorizedResult>();
        sender.VerifyNoOtherCalls();
    }

    private static IActorContextAccessor Actor(Guid? userId)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(userId is null
            ? ActorContext.Anonymous
            : new ActorContext
            {
                ActorKind = ActorKind.User,
                SubjectId = userId.Value.ToString(),
                Roles = new HashSet<string>(),
                Permissions = new HashSet<string>(),
                IsAuthenticated = true
            });
        return accessor.Object;
    }
}

internal sealed class StoryTestDbContext(DbContextOptions<StoryTestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssetReference>().HasKey(asset => asset.Id);
        modelBuilder.Entity<Block>().HasKey(block => block.Id);
        modelBuilder.Entity<Mute>().HasKey(mute => mute.Id);
        new FeedModelConfiguration().Configure(modelBuilder);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
