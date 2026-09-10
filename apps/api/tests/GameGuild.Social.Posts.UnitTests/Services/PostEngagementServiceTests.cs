using FluentAssertions;
using GameGuild.Social.Posts.Services;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace GameGuild.Social.Posts.Tests.Services;

/// <summary>
/// Unit tests for PostEngagementService - likes, pins, shares, views, and statistics
/// </summary>
public class PostEngagementServiceTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();
    private readonly PostEngagementService _service;
    private readonly List<Post> _posts = new();
    private readonly List<PostLike> _likes = new();
    private readonly List<PostStatistics> _statistics = new();
    private readonly List<PostView> _views = new();
    private readonly List<PostFollower> _followers = new();

    public PostEngagementServiceTests()
    {
        _service = new PostEngagementService(
            _dbContextMock.Object,
            NullLogger<PostEngagementService>.Instance);
    }

    private void SetupDbSets()
    {
        var mockPostDbSet = _posts.AsQueryable().BuildMockDbSet();
        _dbContextMock.Setup(x => x.Set<Post>()).Returns(mockPostDbSet.Object);

        var mockLikeDbSet = _likes.AsQueryable().BuildMockDbSet();
        _dbContextMock.Setup(x => x.Set<PostLike>()).Returns(mockLikeDbSet.Object);

        var mockStatisticsDbSet = _statistics.AsQueryable().BuildMockDbSet();
        _dbContextMock.Setup(x => x.Set<PostStatistics>()).Returns(mockStatisticsDbSet.Object);

        var mockViewDbSet = _views.AsQueryable().BuildMockDbSet();
        _dbContextMock.Setup(x => x.Set<PostView>()).Returns(mockViewDbSet.Object);

        var mockFollowerDbSet = _followers.AsQueryable().BuildMockDbSet();
        _dbContextMock.Setup(x => x.Set<PostFollower>()).Returns(mockFollowerDbSet.Object);
    }

    #region TogglePostLikeAsync Tests

    [Fact]
    public async Task TogglePostLikeAsync_WhenNotLiked_ShouldAddLike()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Test post", PostVisibility.Public);
        var userId = Guid.NewGuid();
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.TogglePostLikeAsync(post.Id, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue(); // Liked
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TogglePostLikeAsync_WhenAlreadyLiked_ShouldRemoveLike()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Test post", PostVisibility.Public);
        post.IncrementLikes();
        var userId = Guid.NewGuid();
        var existingLike = PostLike.Create(post.Id, userId);
        _posts.Add(post);
        _likes.Add(existingLike);
        SetupDbSets();

        // Act
        var result = await _service.TogglePostLikeAsync(post.Id, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse(); // Unliked
    }

    [Fact]
    public async Task TogglePostLikeAsync_WhenPostNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.TogglePostLikeAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Post.NotFound");
    }

    [Fact]
    public async Task TogglePostLikeAsync_WithCustomReactionType_ShouldUseReactionType()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Test post", PostVisibility.Public);
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.TogglePostLikeAsync(post.Id, Guid.NewGuid(), "love");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    #endregion

    #region TogglePostPinAsync Tests

    [Fact]
    public async Task TogglePostPinAsync_WhenNotPinned_ShouldPin()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Test post", PostVisibility.Public);
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.TogglePostPinAsync(post.Id, post.AuthorId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        post.IsPinned.Should().BeTrue();
    }

    [Fact]
    public async Task TogglePostPinAsync_WhenPinned_ShouldUnpin()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Test post", PostVisibility.Public);
        post.Pin();
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.TogglePostPinAsync(post.Id, post.AuthorId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        post.IsPinned.Should().BeFalse();
    }

    [Fact]
    public async Task TogglePostPinAsync_WhenPostNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.TogglePostPinAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Post.NotFound");
    }

    [Fact]
    public async Task TogglePostPinAsync_WhenActorIsNotAuthor_ShouldReturnForbidden()
    {
        var post = Post.Create(Guid.NewGuid(), "Protected post", PostVisibility.Public);
        _posts.Add(post);
        SetupDbSets();

        var result = await _service.TogglePostPinAsync(post.Id, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Post.Forbidden");
        post.IsPinned.Should().BeFalse();
    }

    #endregion

    #region Repost Tests

    [Fact]
    public async Task CreateRepostAsync_WhenSourceIsPublic_CreatesRepostAndCountsShareOnce()
    {
        var source = Post.Create(Guid.NewGuid(), "Original", PostVisibility.Public);
        var actorId = Guid.NewGuid();
        _posts.Add(source);
        SetupDbSets();

        var result = await _service.CreateRepostAsync(source.Id, actorId, "My take");

        result.IsSuccess.Should().BeTrue();
        result.Value.AuthorId.Should().Be(actorId);
        result.Value.RepostOfPostId.Should().Be(source.Id);
        result.Value.Content.Should().Be("My take");
        source.SharesCount.Should().Be(1);
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateRepostAsync_WhenSourceIsPrivate_DoesNotExposeIt()
    {
        var source = Post.Create(Guid.NewGuid(), "Private", PostVisibility.Private);
        _posts.Add(source);
        SetupDbSets();

        var result = await _service.CreateRepostAsync(source.Id, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Post.NotFound");
        source.SharesCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateRepostAsync_WhenRepeated_ReturnsExistingRepostWithoutDoubleCounting()
    {
        var source = Post.Create(Guid.NewGuid(), "Original", PostVisibility.Public);
        var actorId = Guid.NewGuid();
        var existing = Post.CreateRepost(actorId, source, "Worth sharing");
        source.IncrementShares();
        _posts.AddRange([source, existing]);
        SetupDbSets();

        var result = await _service.CreateRepostAsync(source.Id, actorId, "Ignored duplicate");

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existing.Id);
        source.SharesCount.Should().Be(1);
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region SharePostAsync Tests

    [Fact]
    public async Task SharePostAsync_ShouldIncrementShareCount()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Test post", PostVisibility.Public);
        var stats = PostStatistics.Create(post.Id);
        _posts.Add(post);
        _statistics.Add(stats);
        SetupDbSets();
        var initialShares = post.SharesCount;

        // Act
        var result = await _service.SharePostAsync(post.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        post.SharesCount.Should().Be(initialShares + 1);
    }

    [Fact]
    public async Task SharePostAsync_WhenPostNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.SharePostAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Post.NotFound");
    }

    #endregion

    #region GetPostStatisticsAsync Tests

    [Fact]
    public async Task GetPostStatisticsAsync_WhenExists_ShouldReturnStatistics()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var stats = PostStatistics.Create(postId);
        _statistics.Add(stats);
        SetupDbSets();

        // Act
        var result = await _service.GetPostStatisticsAsync(postId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PostId.Should().Be(postId);
    }

    [Fact]
    public async Task GetPostStatisticsAsync_WhenNotExists_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.GetPostStatisticsAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PostStatistics.NotFound");
    }

    #endregion

    #region RecordPostViewAsync Tests

    [Fact]
    public async Task RecordPostViewAsync_ShouldRecordView()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Test post", PostVisibility.Public);
        var stats = PostStatistics.Create(post.Id);
        _posts.Add(post);
        _statistics.Add(stats);
        SetupDbSets();
        var initialViews = post.ViewsCount;

        // Act
        var result = await _service.RecordPostViewAsync(post.Id, Guid.NewGuid(), "127.0.0.1", "Mozilla/5.0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        post.ViewsCount.Should().Be(initialViews + 1);
    }

    [Fact]
    public async Task RecordPostViewAsync_WhenPostNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.RecordPostViewAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Post.NotFound");
    }

    [Fact]
    public async Task RecordPostViewAsync_AnonymousUser_ShouldRecordWithIpAddress()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Test post", PostVisibility.Public);
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.RecordPostViewAsync(post.Id, null, "192.168.1.1");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region UpdateViewEngagementAsync Tests

    [Fact]
    public async Task UpdateViewEngagementAsync_WhenViewExists_ShouldUpdateDuration()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var view = PostView.Create(postId, Guid.NewGuid());
        var stats = PostStatistics.Create(postId);
        _views.Add(view);
        _statistics.Add(stats);
        SetupDbSets();

        // Act
        var result = await _service.UpdateViewEngagementAsync(view.Id, 30, true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        view.DurationSeconds.Should().Be(30);
        view.IsEngaged.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateViewEngagementAsync_WhenViewNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.UpdateViewEngagementAsync(Guid.NewGuid(), 30);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PostView.NotFound");
    }

    #endregion

    #region RecalculateStatisticsAsync Tests

    [Fact]
    public async Task RecalculateStatisticsAsync_WhenStatisticsExist_ShouldRecalculate()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Test post", PostVisibility.Public);
        var stats = PostStatistics.Create(post.Id);
        _posts.Add(post);
        _statistics.Add(stats);
        SetupDbSets();

        // Act
        var result = await _service.RecalculateStatisticsAsync(post.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecalculateStatisticsAsync_WhenNoStatistics_ShouldCreateStatistics()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Test post", PostVisibility.Public);
        _posts.Add(post);
        // No statistics created
        SetupDbSets();

        // Act
        var result = await _service.RecalculateStatisticsAsync(post.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RecalculateStatisticsAsync_WhenPostNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.RecalculateStatisticsAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Post.NotFound");
    }

    #endregion
}
