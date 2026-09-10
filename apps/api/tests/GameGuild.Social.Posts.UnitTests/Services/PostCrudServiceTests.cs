using System.Reflection;
using FluentAssertions;
using GameGuild.Social.Posts.Services;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace GameGuild.Social.Posts.Tests.Services;

/// <summary>
/// Unit tests for PostCrudService - CRUD operations, queries, and search
/// </summary>
public class PostCrudServiceTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();
    private readonly PostCrudService _service;
    private readonly List<Post> _posts = new();
    private readonly List<PostStatistics> _statistics = new();
    private readonly List<PostTag> _tags = new();
    private readonly List<PostTagAssignment> _tagAssignments = new();

    public PostCrudServiceTests()
    {
        _service = new PostCrudService(
            _dbContextMock.Object,
            NullLogger<PostCrudService>.Instance);
    }

    /// <summary>
    /// Simulates entity being persisted by setting Version > 0
    /// </summary>
    private static void SimulatePersisted<T>(T entity) where T : EntityBase
    {
        var versionProperty = typeof(EntityBase).GetProperty("Version", BindingFlags.Instance | BindingFlags.Public);
        versionProperty?.SetValue(entity, 1);
    }

    private void SetupDbSets()
    {
        var mockPostDbSet = _posts.AsQueryable().BuildMockDbSet();
        _dbContextMock.Setup(x => x.Set<Post>()).Returns(mockPostDbSet.Object);

        var mockStatisticsDbSet = _statistics.AsQueryable().BuildMockDbSet();
        _dbContextMock.Setup(x => x.Set<PostStatistics>()).Returns(mockStatisticsDbSet.Object);

        var mockTagDbSet = _tags.AsQueryable().BuildMockDbSet();
        _dbContextMock.Setup(x => x.Set<PostTag>()).Returns(mockTagDbSet.Object);

        var mockTagAssignmentDbSet = _tagAssignments.AsQueryable().BuildMockDbSet();
        _dbContextMock.Setup(x => x.Set<PostTagAssignment>()).Returns(mockTagAssignmentDbSet.Object);
    }

    #region GetPostByIdAsync Tests

    [Fact]
    public async Task GetPostByIdAsync_WhenPostExists_ShouldReturnPost()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var post = Post.Create(authorId, "Test content", PostVisibility.Public);
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.GetPostByIdAsync(post.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(post.Id);
        result.Value.Content.Should().Be("Test content");
    }

    [Fact]
    public async Task GetPostByIdAsync_WhenPostNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.GetPostByIdAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Post.NotFound");
    }

    [Fact]
    public async Task GetPostByIdAsync_WhenPostDeleted_ShouldReturnFailure()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Deleted content", PostVisibility.Public);
        SimulatePersisted(post);
        post.Delete();
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.GetPostByIdAsync(post.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region GetPostsAsync Tests

    [Fact]
    public async Task GetPostsAsync_ShouldReturnPaginatedPosts()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            _posts.Add(Post.Create(Guid.NewGuid(), $"Post {i}", PostVisibility.Public));
        }
        SetupDbSets();

        // Act
        var result = await _service.GetPostsAsync(skip: 2, take: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(5);
    }

    [Fact]
    public async Task GetPostsAsync_ShouldExcludeDeletedPosts()
    {
        // Arrange
        var activePost = Post.Create(Guid.NewGuid(), "Active", PostVisibility.Public);
        var deletedPost = Post.Create(Guid.NewGuid(), "Deleted", PostVisibility.Public);
        SimulatePersisted(deletedPost);
        deletedPost.Delete();
        _posts.AddRange([activePost, deletedPost]);
        SetupDbSets();

        // Act
        var result = await _service.GetPostsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(1);
        result.Value.First().Content.Should().Be("Active");
    }

    #endregion

    #region CreatePostAsync Tests

    [Fact]
    public async Task CreatePostAsync_ShouldCreatePostAndStatistics()
    {
        // Arrange
        SetupDbSets();
        var authorId = Guid.NewGuid();

        // Act
        var result = await _service.CreatePostAsync(authorId, "New post content", PostVisibility.Public);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AuthorId.Should().Be(authorId);
        result.Value.Content.Should().Be("New post content");
        result.Value.Visibility.Should().Be(PostVisibility.Public);
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePostAsync_WithTenantId_ShouldAssociateTenant()
    {
        // Arrange
        SetupDbSets();
        var tenantId = Guid.NewGuid();

        // Act
        var result = await _service.CreatePostAsync(
            Guid.NewGuid(), "Tenant post", PostVisibility.Public, tenantId: tenantId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task CreatePostAsync_WithPrivateVisibility_ShouldSetVisibility()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.CreatePostAsync(
            Guid.NewGuid(), "Private post", PostVisibility.Private);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Visibility.Should().Be(PostVisibility.Private);
    }

    #endregion

    #region UpdatePostAsync Tests

    [Fact]
    public async Task UpdatePostAsync_WhenPostExists_ShouldUpdateContent()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Original content", PostVisibility.Public);
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.UpdatePostAsync(post.Id, post.AuthorId, "Updated content");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("Updated content");
        result.Value.IsEdited.Should().BeTrue();
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePostAsync_WhenPostNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.UpdatePostAsync(Guid.NewGuid(), Guid.NewGuid(), "Updated content");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Post.NotFound");
    }

    [Fact]
    public async Task UpdatePostAsync_WhenActorIsNotAuthor_ShouldReturnForbiddenWithoutMutation()
    {
        var post = Post.Create(Guid.NewGuid(), "Original content", PostVisibility.Public);
        _posts.Add(post);
        SetupDbSets();

        var result = await _service.UpdatePostAsync(post.Id, Guid.NewGuid(), "Hijacked content");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Post.Forbidden");
        post.Content.Should().Be("Original content");
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region DeletePostAsync Tests

    [Fact]
    public async Task DeletePostAsync_WhenPostExists_ShouldSoftDelete()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "To delete", PostVisibility.Public);
        SimulatePersisted(post);
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.DeletePostAsync(post.Id, post.AuthorId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        post.IsDeleted.Should().BeTrue();
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePostAsync_WhenPostNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.DeletePostAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Post.NotFound");
    }

    [Fact]
    public async Task DeletePostAsync_WhenActorIsNotAuthor_ShouldReturnForbiddenWithoutMutation()
    {
        var post = Post.Create(Guid.NewGuid(), "Protected content", PostVisibility.Public);
        SimulatePersisted(post);
        _posts.Add(post);
        SetupDbSets();

        var result = await _service.DeletePostAsync(post.Id, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Post.Forbidden");
        post.IsDeleted.Should().BeFalse();
    }

    #endregion

    #region RestorePostAsync Tests

    [Fact]
    public async Task RestorePostAsync_WhenPostDeleted_ShouldRestore()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Deleted post", PostVisibility.Public);
        SimulatePersisted(post);
        post.Delete();
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.RestorePostAsync(post.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        post.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task RestorePostAsync_WhenPostNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.RestorePostAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region GetPostsByAuthorAsync Tests

    [Fact]
    public async Task GetPostsByAuthorAsync_ShouldReturnOnlyAuthorPosts()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var otherAuthorId = Guid.NewGuid();
        _posts.Add(Post.Create(authorId, "Author post 1", PostVisibility.Public));
        _posts.Add(Post.Create(authorId, "Author post 2", PostVisibility.Public));
        _posts.Add(Post.Create(otherAuthorId, "Other post", PostVisibility.Public));
        SetupDbSets();

        // Act
        var result = await _service.GetPostsByAuthorAsync(authorId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(2);
        result.Value.All(p => p.AuthorId == authorId).Should().BeTrue();
    }

    [Fact]
    public async Task GetPostsByAuthorAsync_ShouldRespectPagination()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        for (int i = 0; i < 10; i++)
        {
            _posts.Add(Post.Create(authorId, $"Post {i}", PostVisibility.Public));
        }
        SetupDbSets();

        // Act
        var result = await _service.GetPostsByAuthorAsync(authorId, skip: 3, take: 4);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(4);
    }

    #endregion

    #region GetPostsByVisibilityAsync Tests

    [Fact]
    public async Task GetPostsByVisibilityAsync_ShouldFilterByVisibility()
    {
        // Arrange
        _posts.Add(Post.Create(Guid.NewGuid(), "Public post", PostVisibility.Public));
        _posts.Add(Post.Create(Guid.NewGuid(), "Private post", PostVisibility.Private));
        _posts.Add(Post.Create(Guid.NewGuid(), "Followers post", PostVisibility.Followers));
        SetupDbSets();

        // Act
        var result = await _service.GetPostsByVisibilityAsync(PostVisibility.Public);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(1);
        result.Value.First().Visibility.Should().Be(PostVisibility.Public);
    }

    #endregion

    #region GetPinnedPostsAsync Tests

    [Fact]
    public async Task GetPinnedPostsAsync_ShouldReturnOnlyPinnedPosts()
    {
        // Arrange
        var pinnedPost = Post.Create(Guid.NewGuid(), "Pinned", PostVisibility.Public);
        pinnedPost.Pin();
        var unpinnedPost = Post.Create(Guid.NewGuid(), "Not pinned", PostVisibility.Public);
        _posts.AddRange([pinnedPost, unpinnedPost]);
        SetupDbSets();

        // Act
        var result = await _service.GetPinnedPostsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(1);
        result.Value.First().IsPinned.Should().BeTrue();
    }

    [Fact]
    public async Task GetPinnedPostsAsync_WithTenantFilter_ShouldFilterByTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var postWithTenant = Post.Create(Guid.NewGuid(), "Tenant post", PostVisibility.Public, tenantId);
        postWithTenant.Pin();
        var postWithoutTenant = Post.Create(Guid.NewGuid(), "No tenant", PostVisibility.Public);
        postWithoutTenant.Pin();
        _posts.AddRange([postWithTenant, postWithoutTenant]);
        SetupDbSets();

        // Act
        var result = await _service.GetPinnedPostsAsync(tenantId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(1);
        result.Value.First().TenantId.Should().Be(tenantId);
    }

    #endregion

    #region GetPublicPostsAsync Tests

    [Fact]
    public async Task GetPublicPostsAsync_ShouldReturnOnlyPublicPosts()
    {
        // Arrange
        _posts.Add(Post.Create(Guid.NewGuid(), "Public", PostVisibility.Public));
        _posts.Add(Post.Create(Guid.NewGuid(), "Private", PostVisibility.Private));
        _posts.Add(Post.Create(Guid.NewGuid(), "Unlisted", PostVisibility.Unlisted));
        SetupDbSets();

        // Act
        var result = await _service.GetPublicPostsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(1);
        result.Value.All(p => p.Visibility == PostVisibility.Public).Should().BeTrue();
    }

    #endregion

    #region GetPostsByTenantAsync Tests

    [Fact]
    public async Task GetPostsByTenantAsync_ShouldFilterByTenant()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _posts.Add(Post.Create(Guid.NewGuid(), "Tenant post", PostVisibility.Public, tenantId));
        _posts.Add(Post.Create(Guid.NewGuid(), "Other tenant", PostVisibility.Public, Guid.NewGuid()));
        _posts.Add(Post.Create(Guid.NewGuid(), "No tenant", PostVisibility.Public));
        SetupDbSets();

        // Act
        var result = await _service.GetPostsByTenantAsync(tenantId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(1);
    }

    #endregion

    #region SearchPostsAsync Tests

    [Fact]
    public async Task SearchPostsAsync_ShouldFindMatchingPosts()
    {
        // Arrange
        _posts.Add(Post.Create(Guid.NewGuid(), "Hello world", PostVisibility.Public));
        _posts.Add(Post.Create(Guid.NewGuid(), "Goodbye world", PostVisibility.Public));
        _posts.Add(Post.Create(Guid.NewGuid(), "Something else", PostVisibility.Public));
        SetupDbSets();

        // Act
        var result = await _service.SearchPostsAsync("world");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(2);
    }

    [Fact]
    public async Task SearchPostsAsync_ShouldExcludeDeletedPosts()
    {
        // Arrange
        var activePost = Post.Create(Guid.NewGuid(), "Hello world", PostVisibility.Public);
        var deletedPost = Post.Create(Guid.NewGuid(), "Deleted world", PostVisibility.Public);
        SimulatePersisted(deletedPost);
        deletedPost.Delete();
        _posts.AddRange([activePost, deletedPost]);
        SetupDbSets();

        // Act
        var result = await _service.SearchPostsAsync("world");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(1);
    }

    #endregion

    #region GetPostsByTagsAsync Tests

    [Fact]
    public async Task GetPostsByTagsAsync_ShouldReturnPostsWithMatchingTags()
    {
        // Arrange
        var post1 = Post.Create(Guid.NewGuid(), "Post 1", PostVisibility.Public);
        var post2 = Post.Create(Guid.NewGuid(), "Post 2", PostVisibility.Public);
        var tag = PostTag.Create("gamedev", "Game Development");
        _posts.AddRange([post1, post2]);
        _tags.Add(tag);
        _tagAssignments.Add(PostTagAssignment.Create(post1.Id, tag.Id));
        SetupDbSets();

        // Act
        var result = await _service.GetPostsByTagsAsync(["gamedev"]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(1);
        result.Value.First().Id.Should().Be(post1.Id);
    }

    #endregion

    #region GetTrendingPostsAsync Tests

    [Fact]
    public async Task GetTrendingPostsAsync_ShouldReturnPostsOrderedByTrendingScore()
    {
        // Arrange
        var post1 = Post.Create(Guid.NewGuid(), "Low trending", PostVisibility.Public);
        var post2 = Post.Create(Guid.NewGuid(), "High trending", PostVisibility.Public);
        _posts.AddRange([post1, post2]);

        var stats1 = PostStatistics.Create(post1.Id);
        var stats2 = PostStatistics.Create(post2.Id);
        stats2.RecalculateScores(100, 50, 20, 1); // High trending score
        stats1.RecalculateScores(5, 2, 1, 24); // Low trending score
        _statistics.AddRange([stats1, stats2]);

        SetupDbSets();

        // Act
        var result = await _service.GetTrendingPostsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(2);
        // First should be the higher trending one
        result.Value.First().Id.Should().Be(post2.Id);
    }

    #endregion

    #region GetFeedPostsAsync Tests

    [Fact]
    public async Task GetFeedPostsAsync_ShouldIncludePublicAndOwnPosts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        _posts.Add(Post.Create(otherUserId, "Public post", PostVisibility.Public));
        _posts.Add(Post.Create(userId, "Own private post", PostVisibility.Private));
        _posts.Add(Post.Create(otherUserId, "Other private", PostVisibility.Private));
        SetupDbSets();

        // Act
        var result = await _service.GetFeedPostsAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(2); // Public + own private
    }

    #endregion

    #region CanUserPerformActionAsync Tests

    [Fact]
    public async Task CanUserPerformActionAsync_Author_CanDoAnyAction()
    {
        // Arrange
        var authorId = Guid.NewGuid();
        var post = Post.Create(authorId, "Author's post", PostVisibility.Private);
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.CanUserPerformActionAsync(post.Id, authorId, "delete");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task CanUserPerformActionAsync_NonAuthor_CanViewPublicPost()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Public post", PostVisibility.Public);
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.CanUserPerformActionAsync(post.Id, Guid.NewGuid(), "view");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task CanUserPerformActionAsync_NonAuthor_CannotViewPrivatePost()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Private post", PostVisibility.Private);
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.CanUserPerformActionAsync(post.Id, Guid.NewGuid(), "view");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task CanUserPerformActionAsync_NonAuthor_CannotEditOthersPosts()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Public post", PostVisibility.Public);
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.CanUserPerformActionAsync(post.Id, Guid.NewGuid(), "edit");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task CanUserPerformActionAsync_NonAuthor_CanLikePublicPost()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Public post", PostVisibility.Public);
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.CanUserPerformActionAsync(post.Id, Guid.NewGuid(), "like");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task CanUserPerformActionAsync_NonAuthor_CannotLikePrivatePost()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Private post", PostVisibility.Private);
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.CanUserPerformActionAsync(post.Id, Guid.NewGuid(), "like");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    [Fact]
    public async Task CanUserPerformActionAsync_WhenPostNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.CanUserPerformActionAsync(Guid.NewGuid(), Guid.NewGuid(), "view");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Post.NotFound");
    }

    #endregion
}
