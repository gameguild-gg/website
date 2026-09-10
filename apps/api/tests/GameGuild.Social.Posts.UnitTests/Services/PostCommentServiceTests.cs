using System.Reflection;
using FluentAssertions;
using GameGuild.Social.Posts.Services;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace GameGuild.Social.Posts.Tests.Services;

/// <summary>
/// Unit tests for PostCommentService - comment CRUD operations
/// </summary>
public class PostCommentServiceTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();
    private readonly PostCommentService _service;
    private readonly List<Post> _posts = new();
    private readonly List<PostComment> _comments = new();

    public PostCommentServiceTests()
    {
        _service = new PostCommentService(
            _dbContextMock.Object,
            NullLogger<PostCommentService>.Instance);
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

        var mockCommentDbSet = _comments.AsQueryable().BuildMockDbSet();
        _dbContextMock.Setup(x => x.Set<PostComment>()).Returns(mockCommentDbSet.Object);
    }

    #region AddCommentAsync Tests

    [Fact]
    public async Task AddCommentAsync_WhenPostExists_ShouldAddComment()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Test post", PostVisibility.Public);
        var authorId = Guid.NewGuid();
        _posts.Add(post);
        SetupDbSets();
        var initialCommentCount = post.CommentsCount;

        // Act
        var result = await _service.AddCommentAsync(post.Id, authorId, "This is a comment");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PostId.Should().Be(post.Id);
        result.Value.AuthorId.Should().Be(authorId);
        result.Value.Content.Should().Be("This is a comment");
        post.CommentsCount.Should().Be(initialCommentCount + 1);
        _dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddCommentAsync_WhenPostNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.AddCommentAsync(Guid.NewGuid(), Guid.NewGuid(), "Comment");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Post.NotFound");
    }

    [Fact]
    public async Task AddCommentAsync_WithValidParentComment_ShouldAddReply()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Test post", PostVisibility.Public);
        var parentComment = PostComment.Create(post.Id, Guid.NewGuid(), "Parent comment");
        _posts.Add(post);
        _comments.Add(parentComment);
        SetupDbSets();

        // Act
        var result = await _service.AddCommentAsync(post.Id, Guid.NewGuid(), "Reply", parentComment.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ParentCommentId.Should().Be(parentComment.Id);
    }

    [Fact]
    public async Task AddCommentAsync_WithInvalidParentComment_ShouldReturnFailure()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Test post", PostVisibility.Public);
        _posts.Add(post);
        SetupDbSets();

        // Act
        var result = await _service.AddCommentAsync(post.Id, Guid.NewGuid(), "Reply", Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ParentComment.NotFound");
    }

    [Fact]
    public async Task AddCommentAsync_WithParentFromDifferentPost_ShouldReturnFailure()
    {
        // Arrange
        var post1 = Post.Create(Guid.NewGuid(), "Post 1", PostVisibility.Public);
        var post2 = Post.Create(Guid.NewGuid(), "Post 2", PostVisibility.Public);
        var commentOnPost1 = PostComment.Create(post1.Id, Guid.NewGuid(), "Comment on post 1");
        _posts.AddRange([post1, post2]);
        _comments.Add(commentOnPost1);
        SetupDbSets();

        // Act - try to reply on post2 using parent from post1
        var result = await _service.AddCommentAsync(post2.Id, Guid.NewGuid(), "Invalid reply", commentOnPost1.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ParentComment.NotFound");
    }

    #endregion

    #region UpdateCommentAsync Tests

    [Fact]
    public async Task UpdateCommentAsync_WhenCommentExists_ShouldUpdateContent()
    {
        // Arrange
        var comment = PostComment.Create(Guid.NewGuid(), Guid.NewGuid(), "Original content");
        _comments.Add(comment);
        SetupDbSets();

        // Act
        var result = await _service.UpdateCommentAsync(comment.Id, comment.AuthorId, "Updated content");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("Updated content");
        result.Value.IsEdited.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateCommentAsync_WhenCommentNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.UpdateCommentAsync(Guid.NewGuid(), Guid.NewGuid(), "Updated content");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Comment.NotFound");
    }

    [Fact]
    public async Task UpdateCommentAsync_WhenCommentDeleted_ShouldReturnFailure()
    {
        // Arrange
        var comment = PostComment.Create(Guid.NewGuid(), Guid.NewGuid(), "Content");
        SimulatePersisted(comment);
        comment.Delete();
        _comments.Add(comment);
        SetupDbSets();

        // Act
        var result = await _service.UpdateCommentAsync(comment.Id, comment.AuthorId, "Updated content");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateCommentAsync_WhenActorIsNotAuthor_ShouldReturnForbiddenWithoutMutation()
    {
        var comment = PostComment.Create(Guid.NewGuid(), Guid.NewGuid(), "Original");
        _comments.Add(comment);
        SetupDbSets();

        var result = await _service.UpdateCommentAsync(comment.Id, Guid.NewGuid(), "Hijacked");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Comment.Forbidden");
        comment.Content.Should().Be("Original");
    }

    #endregion

    #region DeleteCommentAsync Tests

    [Fact]
    public async Task DeleteCommentAsync_WhenCommentExists_ShouldSoftDelete()
    {
        // Arrange
        var post = Post.Create(Guid.NewGuid(), "Test post", PostVisibility.Public);
        post.IncrementComments(); // Simulate existing comment
        var comment = PostComment.Create(post.Id, Guid.NewGuid(), "To delete");
        SimulatePersisted(comment);
        _posts.Add(post);
        _comments.Add(comment);
        SetupDbSets();

        // Act
        var result = await _service.DeleteCommentAsync(comment.Id, comment.AuthorId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        comment.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteCommentAsync_WhenCommentNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.DeleteCommentAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Comment.NotFound");
    }

    [Fact]
    public async Task DeleteCommentAsync_WhenActorIsNotAuthor_ShouldReturnForbiddenWithoutMutation()
    {
        var comment = PostComment.Create(Guid.NewGuid(), Guid.NewGuid(), "Protected");
        SimulatePersisted(comment);
        _comments.Add(comment);
        SetupDbSets();

        var result = await _service.DeleteCommentAsync(comment.Id, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Comment.Forbidden");
        comment.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteCommentAsync_WhenAlreadyDeleted_DoesNotDecrementCounterAgain()
    {
        var post = Post.Create(Guid.NewGuid(), "Post", PostVisibility.Public);
        var comment = PostComment.Create(post.Id, Guid.NewGuid(), "Comment");
        post.IncrementComments();
        SimulatePersisted(comment);
        comment.Delete();
        _posts.Add(post);
        _comments.Add(comment);
        SetupDbSets();

        var result = await _service.DeleteCommentAsync(comment.Id, comment.AuthorId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Comment.NotFound");
        post.CommentsCount.Should().Be(1);
    }

    #endregion

    #region GetPostCommentsAsync Tests

    [Fact]
    public async Task GetPostCommentsAsync_ShouldReturnCommentsForPost()
    {
        // Arrange
        var postId = Guid.NewGuid();
        _comments.Add(PostComment.Create(postId, Guid.NewGuid(), "Comment 1"));
        _comments.Add(PostComment.Create(postId, Guid.NewGuid(), "Comment 2"));
        _comments.Add(PostComment.Create(Guid.NewGuid(), Guid.NewGuid(), "Comment on other post"));
        SetupDbSets();

        // Act
        var result = await _service.GetPostCommentsAsync(postId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(2);
    }

    [Fact]
    public async Task GetPostCommentsAsync_ShouldExcludeDeletedComments()
    {
        // Arrange
        var postId = Guid.NewGuid();
        var activeComment = PostComment.Create(postId, Guid.NewGuid(), "Active");
        var deletedComment = PostComment.Create(postId, Guid.NewGuid(), "Deleted");
        SimulatePersisted(deletedComment);
        deletedComment.Delete();
        _comments.AddRange([activeComment, deletedComment]);
        SetupDbSets();

        // Act
        var result = await _service.GetPostCommentsAsync(postId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(1);
    }

    [Fact]
    public async Task GetPostCommentsAsync_ShouldRespectPagination()
    {
        // Arrange
        var postId = Guid.NewGuid();
        for (int i = 0; i < 10; i++)
        {
            _comments.Add(PostComment.Create(postId, Guid.NewGuid(), $"Comment {i}"));
        }
        SetupDbSets();

        // Act
        var result = await _service.GetPostCommentsAsync(postId, skip: 2, take: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().Be(5);
    }

    #endregion

    #region GetCommentByIdAsync Tests

    [Fact]
    public async Task GetCommentByIdAsync_WhenExists_ShouldReturnComment()
    {
        // Arrange
        var comment = PostComment.Create(Guid.NewGuid(), Guid.NewGuid(), "Test comment");
        _comments.Add(comment);
        SetupDbSets();

        // Act
        var result = await _service.GetCommentByIdAsync(comment.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(comment.Id);
    }

    [Fact]
    public async Task GetCommentByIdAsync_WhenNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSets();

        // Act
        var result = await _service.GetCommentByIdAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Comment.NotFound");
    }

    [Fact]
    public async Task GetCommentByIdAsync_WhenDeleted_ShouldReturnFailure()
    {
        // Arrange
        var comment = PostComment.Create(Guid.NewGuid(), Guid.NewGuid(), "Deleted comment");
        SimulatePersisted(comment);
        comment.Delete();
        _comments.Add(comment);
        SetupDbSets();

        // Act
        var result = await _service.GetCommentByIdAsync(comment.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion
}
