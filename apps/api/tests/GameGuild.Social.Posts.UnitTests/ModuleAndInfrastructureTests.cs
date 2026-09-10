using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Assets.SocialMedia;
using GameGuild.Identity.Context.Actors;
using GameGuild.Social.Posts.Configuration;
using GameGuild.Social.Posts.Controllers;
using GameGuild.Social.Posts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Social.Posts.Tests;

/// <summary>
/// Tests for PostsModule DI registration.
/// </summary>
public class PostsModuleTests
{
    [Fact]
    public void AddPostsModule_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IApplicationDbContext>(_ => Mock.Of<IApplicationDbContext>());
        services.AddLogging();

        // Act
        services.AddPostsModule();

        // Assert
        var provider = services.BuildServiceProvider();
        provider.GetService<IPostCrudService>().Should().NotBeNull();
        provider.GetService<IPostEngagementService>().Should().NotBeNull();
        provider.GetService<IPostCommentService>().Should().NotBeNull();
        provider.GetService<IPostTagService>().Should().NotBeNull();
        provider.GetService<IPostContentReferenceService>().Should().NotBeNull();
        provider.GetService<IPostService>().Should().NotBeNull();
        provider.GetService<IPostAnnouncementService>().Should().NotBeNull();
    }
}

/// <summary>
/// Tests for Social.Posts service constructors.
/// </summary>
public class PostsServiceConstructorTests
{
    private readonly IApplicationDbContext _mockContext = Mock.Of<IApplicationDbContext>();

    [Fact]
    public void PostCrudService_CanBeInstantiated()
    {
        var sut = new PostCrudService(_mockContext, NullLogger<PostCrudService>.Instance);
        sut.Should().NotBeNull();
    }

    [Fact]
    public void PostEngagementService_CanBeInstantiated()
    {
        var sut = new PostEngagementService(_mockContext, NullLogger<PostEngagementService>.Instance);
        sut.Should().NotBeNull();
    }

    [Fact]
    public void PostCommentService_CanBeInstantiated()
    {
        var sut = new PostCommentService(_mockContext, NullLogger<PostCommentService>.Instance);
        sut.Should().NotBeNull();
    }

    [Fact]
    public void PostTagService_CanBeInstantiated()
    {
        var sut = new PostTagService(_mockContext, NullLogger<PostTagService>.Instance);
        sut.Should().NotBeNull();
    }

    [Fact]
    public void PostContentReferenceService_CanBeInstantiated()
    {
        var sut = new PostContentReferenceService(_mockContext, NullLogger<PostContentReferenceService>.Instance);
        sut.Should().NotBeNull();
    }

    [Fact]
    public void PostAnnouncementService_CanBeInstantiated()
    {
        var sut = new PostAnnouncementService(_mockContext, Mock.Of<IPostService>(), NullLogger<PostAnnouncementService>.Instance);
        sut.Should().NotBeNull();
    }

    [Fact]
    public void PostService_CanBeInstantiated()
    {
        var sut = new PostService(
            Mock.Of<IPostCrudService>(),
            Mock.Of<IPostEngagementService>(),
            Mock.Of<IPostCommentService>(),
            Mock.Of<IPostTagService>(),
            Mock.Of<IPostContentReferenceService>());
        sut.Should().NotBeNull();
    }
}

/// <summary>
/// Tests for Social.Posts controller constructors.
/// </summary>
public class PostsControllerConstructorTests
{
    [Fact]
    public void PostsCrudController_CanBeInstantiated()
    {
        var sut = new PostsCrudController(
            Mock.Of<IPostService>(),
            Mock.Of<IActorContextAccessor>(),
            Mock.Of<ISender>(),
            Mock.Of<ISocialMediaAssetService>());
        sut.Should().NotBeNull();
    }

    [Fact]
    public void PostCommentsController_CanBeInstantiated()
    {
        var sut = new PostCommentsController(Mock.Of<IPostService>(), Mock.Of<IActorContextAccessor>(), Mock.Of<ISender>());
        sut.Should().NotBeNull();
    }

    [Fact]
    public void PostInteractionsController_CanBeInstantiated()
    {
        var sut = new PostInteractionsController(Mock.Of<IPostService>(), Mock.Of<IActorContextAccessor>(), Mock.Of<ISender>());
        sut.Should().NotBeNull();
    }
}

/// <summary>
/// Tests for Social.Posts EF configurations.
/// </summary>
public class PostsEfConfigurationTests
{
    private readonly ModelBuilder _modelBuilder = new(new ConventionSet());

    [Fact]
    public void PostEntityConfiguration_AppliesWithoutError()
    {
        var config = new PostEntityConfiguration();
        var act = () => config.Configure(_modelBuilder.Entity<Post>());
        act.Should().NotThrow();
    }

    [Fact]
    public void PostCommentEntityConfiguration_AppliesWithoutError()
    {
        var config = new PostCommentEntityConfiguration();
        var act = () => config.Configure(_modelBuilder.Entity<PostComment>());
        act.Should().NotThrow();
    }

    [Fact]
    public void PostStatisticsEntityConfiguration_AppliesWithoutError()
    {
        var config = new PostStatisticsEntityConfiguration();
        var act = () => config.Configure(_modelBuilder.Entity<PostStatistics>());
        act.Should().NotThrow();
    }

    [Fact]
    public void PostContentReferenceEntityConfiguration_AppliesWithoutError()
    {
        var config = new PostContentReferenceEntityConfiguration();
        var act = () => config.Configure(_modelBuilder.Entity<PostContentReference>());
        act.Should().NotThrow();
    }

    [Fact]
    public void PostFollowerEntityConfiguration_AppliesWithoutError()
    {
        var config = new PostFollowerEntityConfiguration();
        var act = () => config.Configure(_modelBuilder.Entity<PostFollower>());
        act.Should().NotThrow();
    }

    [Fact]
    public void PostTagEntityConfiguration_AppliesWithoutError()
    {
        var config = new PostTagEntityConfiguration();
        var act = () => config.Configure(_modelBuilder.Entity<PostTag>());
        act.Should().NotThrow();
    }

    [Fact]
    public void PostTagAssignmentEntityConfiguration_AppliesWithoutError()
    {
        var config = new PostTagAssignmentEntityConfiguration();
        var act = () => config.Configure(_modelBuilder.Entity<PostTagAssignment>());
        act.Should().NotThrow();
    }

    [Fact]
    public void PostViewEntityConfiguration_AppliesWithoutError()
    {
        var config = new PostViewEntityConfiguration();
        var act = () => config.Configure(_modelBuilder.Entity<PostView>());
        act.Should().NotThrow();
    }

    [Fact]
    public void PostLikeEntityConfiguration_AppliesWithoutError()
    {
        var config = new PostLikeEntityConfiguration();
        var act = () => config.Configure(_modelBuilder.Entity<PostLike>());
        act.Should().NotThrow();
    }
}
