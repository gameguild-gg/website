using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Social.Posts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace GameGuild.Social.Feed.UnitTests;

public sealed class SavedPostServiceTests
{
    [Fact]
    public async Task SaveAsync_Twice_CreatesOneRow()
    {
        await using var context = CreateContext();
        var post = await AddPostAsync(context);
        var userId = Guid.NewGuid();
        var service = new SavedPostService(context);

        var first = await service.SaveAsync(userId, post.Id, default);
        var duplicate = await service.SaveAsync(userId, post.Id, default);

        first.Should().BeTrue();
        duplicate.Should().BeFalse();
        (await context.Set<SavedPost>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UnsaveAsync_Twice_RemovesOneRowAndRemainsIdempotent()
    {
        await using var context = CreateContext();
        var post = await AddPostAsync(context);
        var userId = Guid.NewGuid();
        var service = new SavedPostService(context);
        await service.SaveAsync(userId, post.Id, default);

        var first = await service.UnsaveAsync(userId, post.Id, default);
        var duplicate = await service.UnsaveAsync(userId, post.Id, default);

        first.Should().BeTrue();
        duplicate.Should().BeFalse();
        (await context.Set<SavedPost>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SaveAsync_RejectsMissingDeletedAndOtherUsersPrivatePosts()
    {
        await using var context = CreateContext();
        var actorId = Guid.NewGuid();
        var deleted = await AddPostAsync(context);
        deleted.Version = 1;
        deleted.Delete();
        var privatePost = Post.Create(Guid.NewGuid(), "private", PostVisibility.Private);
        context.Set<Post>().Add(privatePost);
        await context.SaveChangesAsync();
        var service = new SavedPostService(context);

        var missing = () => service.SaveAsync(actorId, Guid.NewGuid(), default);
        var deletedAttempt = () => service.SaveAsync(actorId, deleted.Id, default);
        var privateAttempt = () => service.SaveAsync(actorId, privatePost.Id, default);

        await missing.Should().ThrowAsync<SavedPostUnavailableException>();
        await deletedAttempt.Should().ThrowAsync<SavedPostUnavailableException>();
        await privateAttempt.Should().ThrowAsync<SavedPostUnavailableException>();
        (await context.Set<SavedPost>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task IsSavedAsync_IsScopedToActor()
    {
        await using var context = CreateContext();
        var post = await AddPostAsync(context);
        var userId = Guid.NewGuid();
        var service = new SavedPostService(context);
        await service.SaveAsync(userId, post.Id, default);

        (await service.IsSavedAsync(userId, post.Id, default)).Should().BeTrue();
        (await service.IsSavedAsync(Guid.NewGuid(), post.Id, default)).Should().BeFalse();
    }

    private static SavedPostTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SavedPostTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SavedPostTestDbContext(options);
    }

    private static async Task<Post> AddPostAsync(SavedPostTestDbContext context)
    {
        var post = Post.Create(Guid.NewGuid(), "available post");
        context.Set<Post>().Add(post);
        await context.SaveChangesAsync();
        return post;
    }
}

public sealed class SavedPostsControllerTests
{
    [Fact]
    public async Task Put_UsesAuthenticatedActorAndReturnsCurrentState()
    {
        var actorId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(
                It.Is<SavePostCommand>(command => command.UserId == actorId && command.PostId == postId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SavedPostStateDto(postId, true));
        var controller = new SavedPostsController(sender.Object, Actor(actorId));

        var result = await controller.Save(postId, default);

        result.Value.Should().Be(new SavedPostStateDto(postId, true));
        sender.VerifyAll();
    }

    [Fact]
    public async Task Put_WithoutAuthenticatedActor_ReturnsUnauthorized()
    {
        var sender = new Mock<ISender>();
        var controller = new SavedPostsController(sender.Object, Actor(null));

        var result = await controller.Save(Guid.NewGuid(), default);

        result.Result.Should().BeOfType<UnauthorizedResult>();
        sender.Verify(value => value.Send(It.IsAny<SavePostCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Put_UnavailablePost_ReturnsNotFound()
    {
        var actorId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(It.IsAny<SavePostCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SavedPostUnavailableException(postId));
        var controller = new SavedPostsController(sender.Object, Actor(actorId));

        var result = await controller.Save(postId, default);

        result.Result.Should().BeOfType<NotFoundResult>();
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

public sealed class SavedPostInfrastructureTests
{
    [Fact]
    public void ModelConfiguration_UsesUniqueUserPostPair()
    {
        using var context = new SavedPostTestDbContext(
            new DbContextOptionsBuilder<SavedPostTestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        var entity = context.Model.FindEntityType(typeof(SavedPost));

        entity.Should().NotBeNull();
        var savedPost = entity!;
        savedPost.GetTableName().Should().Be("social_saved_posts");
        savedPost.GetIndexes().Should().Contain(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(SavedPost.UserId), nameof(SavedPost.PostId) }));
    }
}

internal sealed class SavedPostTestDbContext(DbContextOptions<SavedPostTestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Post>().HasKey(post => post.Id);
        new FeedModelConfiguration().Configure(modelBuilder);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
