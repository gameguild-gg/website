using FluentAssertions;
using GameGuild;
using GameGuild.Social.Posts.Services;
using Moq;
using Xunit;

// Helper to shorten Task.FromResult usage
using TFR = System.Threading.Tasks.Task;

namespace GameGuild.Social.Posts.Tests;

/// <summary>
/// Tests that PostService correctly delegates all calls to sub-services.
/// </summary>
public class PostServiceFacadeTests
{
    private readonly Mock<IPostCrudService> _crudMock = new();
    private readonly Mock<IPostEngagementService> _engagementMock = new();
    private readonly Mock<IPostCommentService> _commentMock = new();
    private readonly Mock<IPostTagService> _tagMock = new();
    private readonly Mock<IPostContentReferenceService> _contentRefMock = new();
    private readonly PostService _sut;

    public PostServiceFacadeTests()
    {
        _sut = new PostService(
            _crudMock.Object,
            _engagementMock.Object,
            _commentMock.Object,
            _tagMock.Object,
            _contentRefMock.Object);
    }

    // ── CRUD delegations ─────────────────────────────────────────

    [Fact]
    public async Task GetPostByIdAsync_ShouldDelegate()
    {
        var id = Guid.NewGuid();
        var post = Post.Create(Guid.NewGuid(), "c");
        _crudMock.Setup(s => s.GetPostByIdAsync(id, default)).ReturnsAsync(Result<Post>.Success(post));
        var result = await _sut.GetPostByIdAsync(id);
        result.IsSuccess.Should().BeTrue();
        _crudMock.Verify(s => s.GetPostByIdAsync(id, default), Times.Once);
    }

    [Fact]
    public async Task GetPostByIdWithDetailsAsync_ShouldDelegate()
    {
        var id = Guid.NewGuid();
        _crudMock.Setup(s => s.GetPostByIdWithDetailsAsync(id, default)).ReturnsAsync(Result<Post>.Success(Post.Create(Guid.NewGuid(), "c")));
        await _sut.GetPostByIdWithDetailsAsync(id);
        _crudMock.Verify(s => s.GetPostByIdWithDetailsAsync(id, default), Times.Once);
    }

    [Fact]
    public async Task GetPostsAsync_ShouldDelegate()
    {
        _crudMock.Setup(s => s.GetPostsAsync(5, 10, default)).Returns(Task.FromResult(Result<IEnumerable<Post>>.Success(Enumerable.Empty<Post>())));
        await _sut.GetPostsAsync(5, 10);
        _crudMock.Verify(s => s.GetPostsAsync(5, 10, default), Times.Once);
    }

    [Fact]
    public async Task CreatePostAsync_ShouldDelegate()
    {
        var authorId = Guid.NewGuid();
        _crudMock.Setup(s => s.CreatePostAsync(authorId, "content", PostVisibility.Public, null, null, null, default))
            .ReturnsAsync(Result<Post>.Success(Post.Create(authorId, "content")));
        await _sut.CreatePostAsync(authorId, "content");
        _crudMock.Verify(s => s.CreatePostAsync(authorId, "content", PostVisibility.Public, null, null, null, default), Times.Once);
    }

    [Fact]
    public async Task UpdatePostAsync_ShouldDelegate()
    {
        var id = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        _crudMock.Setup(s => s.UpdatePostAsync(id, actorId, "new", default)).ReturnsAsync(Result<Post>.Success(Post.Create(actorId, "new")));
        await _sut.UpdatePostAsync(id, actorId, "new");
        _crudMock.Verify(s => s.UpdatePostAsync(id, actorId, "new", default), Times.Once);
    }

    [Fact]
    public async Task DeletePostAsync_ShouldDelegate()
    {
        var id = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        _crudMock.Setup(s => s.DeletePostAsync(id, actorId, default)).ReturnsAsync(Result.Success());
        await _sut.DeletePostAsync(id, actorId);
        _crudMock.Verify(s => s.DeletePostAsync(id, actorId, default), Times.Once);
    }

    [Fact]
    public async Task RestorePostAsync_ShouldDelegate()
    {
        var id = Guid.NewGuid();
        _crudMock.Setup(s => s.RestorePostAsync(id, default)).ReturnsAsync(Result.Success());
        await _sut.RestorePostAsync(id);
        _crudMock.Verify(s => s.RestorePostAsync(id, default), Times.Once);
    }

    [Fact]
    public async Task GetPostsByAuthorAsync_ShouldDelegate()
    {
        var authorId = Guid.NewGuid();
        _crudMock.Setup(s => s.GetPostsByAuthorAsync(authorId, 0, 50, default)).Returns(Task.FromResult(Result<IEnumerable<Post>>.Success(Enumerable.Empty<Post>())));
        await _sut.GetPostsByAuthorAsync(authorId);
        _crudMock.Verify(s => s.GetPostsByAuthorAsync(authorId, 0, 50, default), Times.Once);
    }

    [Fact]
    public async Task GetPostsByVisibilityAsync_ShouldDelegate()
    {
        _crudMock.Setup(s => s.GetPostsByVisibilityAsync(PostVisibility.Private, 0, 50, default)).Returns(Task.FromResult(Result<IEnumerable<Post>>.Success(Enumerable.Empty<Post>())));
        await _sut.GetPostsByVisibilityAsync(PostVisibility.Private);
        _crudMock.Verify(s => s.GetPostsByVisibilityAsync(PostVisibility.Private, 0, 50, default), Times.Once);
    }

    [Fact]
    public async Task GetPinnedPostsAsync_ShouldDelegate()
    {
        var tenantId = Guid.NewGuid();
        _crudMock.Setup(s => s.GetPinnedPostsAsync(tenantId, default)).Returns(Task.FromResult(Result<IEnumerable<Post>>.Success(Enumerable.Empty<Post>())));
        await _sut.GetPinnedPostsAsync(tenantId);
        _crudMock.Verify(s => s.GetPinnedPostsAsync(tenantId, default), Times.Once);
    }

    [Fact]
    public async Task GetPublicPostsAsync_ShouldDelegate()
    {
        _crudMock.Setup(s => s.GetPublicPostsAsync(0, 20, default)).Returns(Task.FromResult(Result<IEnumerable<Post>>.Success(Enumerable.Empty<Post>())));
        await _sut.GetPublicPostsAsync(0, 20);
        _crudMock.Verify(s => s.GetPublicPostsAsync(0, 20, default), Times.Once);
    }

    [Fact]
    public async Task GetPostsByTenantAsync_ShouldDelegate()
    {
        var tenantId = Guid.NewGuid();
        _crudMock.Setup(s => s.GetPostsByTenantAsync(tenantId, 0, 50, default)).Returns(Task.FromResult(Result<IEnumerable<Post>>.Success(Enumerable.Empty<Post>())));
        await _sut.GetPostsByTenantAsync(tenantId);
        _crudMock.Verify(s => s.GetPostsByTenantAsync(tenantId, 0, 50, default), Times.Once);
    }

    [Fact]
    public async Task SearchPostsAsync_ShouldDelegate()
    {
        _crudMock.Setup(s => s.SearchPostsAsync("term", 0, 50, default)).Returns(Task.FromResult(Result<IEnumerable<Post>>.Success(Enumerable.Empty<Post>())));
        await _sut.SearchPostsAsync("term");
        _crudMock.Verify(s => s.SearchPostsAsync("term", 0, 50, default), Times.Once);
    }

    [Fact]
    public async Task GetPostsByTagsAsync_ShouldDelegate()
    {
        var tags = new[] { "a", "b" };
        _crudMock.Setup(s => s.GetPostsByTagsAsync(tags, 0, 50, default)).Returns(Task.FromResult(Result<IEnumerable<Post>>.Success(Enumerable.Empty<Post>())));
        await _sut.GetPostsByTagsAsync(tags);
        _crudMock.Verify(s => s.GetPostsByTagsAsync(tags, 0, 50, default), Times.Once);
    }

    [Fact]
    public async Task GetTrendingPostsAsync_ShouldDelegate()
    {
        _crudMock.Setup(s => s.GetTrendingPostsAsync(0, 50, default)).Returns(Task.FromResult(Result<IEnumerable<Post>>.Success(Enumerable.Empty<Post>())));
        await _sut.GetTrendingPostsAsync();
        _crudMock.Verify(s => s.GetTrendingPostsAsync(0, 50, default), Times.Once);
    }

    [Fact]
    public async Task GetFeedPostsAsync_ShouldDelegate()
    {
        var userId = Guid.NewGuid();
        _crudMock.Setup(s => s.GetFeedPostsAsync(userId, 0, 50, default)).Returns(Task.FromResult(Result<IEnumerable<Post>>.Success(Enumerable.Empty<Post>())));
        await _sut.GetFeedPostsAsync(userId);
        _crudMock.Verify(s => s.GetFeedPostsAsync(userId, 0, 50, default), Times.Once);
    }

    [Fact]
    public async Task CanUserPerformActionAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _crudMock.Setup(s => s.CanUserPerformActionAsync(postId, userId, "edit", default)).ReturnsAsync(Result<bool>.Success(true));
        await _sut.CanUserPerformActionAsync(postId, userId, "edit");
        _crudMock.Verify(s => s.CanUserPerformActionAsync(postId, userId, "edit", default), Times.Once);
    }

    // ── Engagement delegations ───────────────────────────────────

    [Fact]
    public async Task TogglePostLikeAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _engagementMock.Setup(s => s.TogglePostLikeAsync(postId, userId, "like", default)).ReturnsAsync(Result<bool>.Success(true));
        await _sut.TogglePostLikeAsync(postId, userId);
        _engagementMock.Verify(s => s.TogglePostLikeAsync(postId, userId, "like", default), Times.Once);
    }

    [Fact]
    public async Task TogglePostPinAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        _engagementMock.Setup(s => s.TogglePostPinAsync(postId, actorId, default)).ReturnsAsync(Result<bool>.Success(true));
        await _sut.TogglePostPinAsync(postId, actorId);
        _engagementMock.Verify(s => s.TogglePostPinAsync(postId, actorId, default), Times.Once);
    }

    [Fact]
    public async Task SharePostAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        _engagementMock.Setup(s => s.SharePostAsync(postId, default)).ReturnsAsync(Result.Success());
        await _sut.SharePostAsync(postId);
        _engagementMock.Verify(s => s.SharePostAsync(postId, default), Times.Once);
    }

    [Fact]
    public async Task GetPostStatisticsAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        _engagementMock.Setup(s => s.GetPostStatisticsAsync(postId, default)).ReturnsAsync(Result<PostStatistics>.Success(PostStatistics.Create(postId)));
        await _sut.GetPostStatisticsAsync(postId);
        _engagementMock.Verify(s => s.GetPostStatisticsAsync(postId, default), Times.Once);
    }

    [Fact]
    public async Task RecordPostViewAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _engagementMock.Setup(s => s.RecordPostViewAsync(postId, userId, "ip", "ua", "ref", default)).ReturnsAsync(Result.Success());
        await _sut.RecordPostViewAsync(postId, userId, "ip", "ua", "ref");
        _engagementMock.Verify(s => s.RecordPostViewAsync(postId, userId, "ip", "ua", "ref", default), Times.Once);
    }

    [Fact]
    public async Task UpdateViewEngagementAsync_ShouldDelegate()
    {
        var viewId = Guid.NewGuid();
        _engagementMock.Setup(s => s.UpdateViewEngagementAsync(viewId, 120, true, default)).ReturnsAsync(Result.Success());
        await _sut.UpdateViewEngagementAsync(viewId, 120, true);
        _engagementMock.Verify(s => s.UpdateViewEngagementAsync(viewId, 120, true, default), Times.Once);
    }

    [Fact]
    public async Task RecalculateStatisticsAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        _engagementMock.Setup(s => s.RecalculateStatisticsAsync(postId, default)).ReturnsAsync(Result.Success());
        await _sut.RecalculateStatisticsAsync(postId);
        _engagementMock.Verify(s => s.RecalculateStatisticsAsync(postId, default), Times.Once);
    }

    [Fact]
    public async Task RecalculateAllTrendingScoresAsync_ShouldDelegate()
    {
        _engagementMock.Setup(s => s.RecalculateAllTrendingScoresAsync(default)).ReturnsAsync(Result<int>.Success(42));
        await _sut.RecalculateAllTrendingScoresAsync();
        _engagementMock.Verify(s => s.RecalculateAllTrendingScoresAsync(default), Times.Once);
    }

    [Fact]
    public async Task FollowPostAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var follower = PostFollower.Create(postId, userId);
        _engagementMock.Setup(s => s.FollowPostAsync(postId, userId, true, false, false, true, default))
            .ReturnsAsync(Result<PostFollower>.Success(follower));
        await _sut.FollowPostAsync(postId, userId);
        _engagementMock.Verify(s => s.FollowPostAsync(postId, userId, true, false, false, true, default), Times.Once);
    }

    [Fact]
    public async Task UnfollowPostAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _engagementMock.Setup(s => s.UnfollowPostAsync(postId, userId, default)).ReturnsAsync(Result.Success());
        await _sut.UnfollowPostAsync(postId, userId);
        _engagementMock.Verify(s => s.UnfollowPostAsync(postId, userId, default), Times.Once);
    }

    [Fact]
    public async Task UpdateFollowPreferencesAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _engagementMock.Setup(s => s.UpdateFollowPreferencesAsync(postId, userId, true, null, null, null, default)).ReturnsAsync(Result.Success());
        await _sut.UpdateFollowPreferencesAsync(postId, userId, true);
        _engagementMock.Verify(s => s.UpdateFollowPreferencesAsync(postId, userId, true, null, null, null, default), Times.Once);
    }

    [Fact]
    public async Task GetPostFollowersAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        _engagementMock.Setup(s => s.GetPostFollowersAsync(postId, default)).Returns(Task.FromResult(Result<IEnumerable<PostFollower>>.Success(Enumerable.Empty<PostFollower>())));
        await _sut.GetPostFollowersAsync(postId);
        _engagementMock.Verify(s => s.GetPostFollowersAsync(postId, default), Times.Once);
    }

    [Fact]
    public async Task IsFollowingPostAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _engagementMock.Setup(s => s.IsFollowingPostAsync(postId, userId, default)).ReturnsAsync(Result<bool>.Success(true));
        await _sut.IsFollowingPostAsync(postId, userId);
        _engagementMock.Verify(s => s.IsFollowingPostAsync(postId, userId, default), Times.Once);
    }

    // ── Comment delegations ──────────────────────────────────────

    [Fact]
    public async Task GetCommentByIdAsync_ShouldDelegate()
    {
        var id = Guid.NewGuid();
        _commentMock.Setup(s => s.GetCommentByIdAsync(id, default)).ReturnsAsync(Result<PostComment>.Success(PostComment.Create(Guid.NewGuid(), Guid.NewGuid(), "c")));
        await _sut.GetCommentByIdAsync(id);
        _commentMock.Verify(s => s.GetCommentByIdAsync(id, default), Times.Once);
    }

    [Fact]
    public async Task AddCommentAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        _commentMock.Setup(s => s.AddCommentAsync(postId, authorId, "comment", null, default))
            .ReturnsAsync(Result<PostComment>.Success(PostComment.Create(postId, authorId, "comment")));
        await _sut.AddCommentAsync(postId, authorId, "comment");
        _commentMock.Verify(s => s.AddCommentAsync(postId, authorId, "comment", null, default), Times.Once);
    }

    [Fact]
    public async Task UpdateCommentAsync_ShouldDelegate()
    {
        var id = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        _commentMock.Setup(s => s.UpdateCommentAsync(id, actorId, "new", default)).ReturnsAsync(Result<PostComment>.Success(PostComment.Create(Guid.NewGuid(), actorId, "new")));
        await _sut.UpdateCommentAsync(id, actorId, "new");
        _commentMock.Verify(s => s.UpdateCommentAsync(id, actorId, "new", default), Times.Once);
    }

    [Fact]
    public async Task DeleteCommentAsync_ShouldDelegate()
    {
        var id = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        _commentMock.Setup(s => s.DeleteCommentAsync(id, actorId, default)).ReturnsAsync(Result.Success());
        await _sut.DeleteCommentAsync(id, actorId);
        _commentMock.Verify(s => s.DeleteCommentAsync(id, actorId, default), Times.Once);
    }

    [Fact]
    public async Task GetPostCommentsAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        _commentMock.Setup(s => s.GetPostCommentsAsync(postId, 0, 50, default)).Returns(Task.FromResult(Result<IEnumerable<PostComment>>.Success(Enumerable.Empty<PostComment>())));
        await _sut.GetPostCommentsAsync(postId);
        _commentMock.Verify(s => s.GetPostCommentsAsync(postId, 0, 50, default), Times.Once);
    }

    // ── Tag delegations ──────────────────────────────────────────

    [Fact]
    public async Task AddTagsToPostAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        var tags = new[] { "a" };
        _tagMock.Setup(s => s.AddTagsToPostAsync(postId, tags, default)).ReturnsAsync(Result.Success());
        await _sut.AddTagsToPostAsync(postId, tags);
        _tagMock.Verify(s => s.AddTagsToPostAsync(postId, tags, default), Times.Once);
    }

    [Fact]
    public async Task RemoveTagsFromPostAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        var tags = new[] { "a" };
        _tagMock.Setup(s => s.RemoveTagsFromPostAsync(postId, tags, default)).ReturnsAsync(Result.Success());
        await _sut.RemoveTagsFromPostAsync(postId, tags);
        _tagMock.Verify(s => s.RemoveTagsFromPostAsync(postId, tags, default), Times.Once);
    }

    [Fact]
    public async Task GetPostTagsAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        _tagMock.Setup(s => s.GetPostTagsAsync(postId, default)).Returns(Task.FromResult(Result<IEnumerable<PostTag>>.Success(Enumerable.Empty<PostTag>())));
        await _sut.GetPostTagsAsync(postId);
        _tagMock.Verify(s => s.GetPostTagsAsync(postId, default), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateTagAsync_ShouldDelegate()
    {
        _tagMock.Setup(s => s.GetOrCreateTagAsync("test", "general", default)).ReturnsAsync(Result<PostTag>.Success(PostTag.Create("test")));
        await _sut.GetOrCreateTagAsync("test");
        _tagMock.Verify(s => s.GetOrCreateTagAsync("test", "general", default), Times.Once);
    }

    [Fact]
    public async Task GetPopularTagsAsync_ShouldDelegate()
    {
        _tagMock.Setup(s => s.GetPopularTagsAsync(20, default)).Returns(Task.FromResult(Result<IEnumerable<PostTag>>.Success(Enumerable.Empty<PostTag>())));
        await _sut.GetPopularTagsAsync();
        _tagMock.Verify(s => s.GetPopularTagsAsync(20, default), Times.Once);
    }

    // ── ContentReference delegations ─────────────────────────────

    [Fact]
    public async Task AddContentReferenceAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        var resId = Guid.NewGuid();
        var reference = PostContentReference.Create(postId, resId, "Course");
        _contentRefMock.Setup(s => s.AddContentReferenceAsync(postId, resId, "Course", "mention", null, default))
            .ReturnsAsync(Result<PostContentReference>.Success(reference));
        await _sut.AddContentReferenceAsync(postId, resId, "Course");
        _contentRefMock.Verify(s => s.AddContentReferenceAsync(postId, resId, "Course", "mention", null, default), Times.Once);
    }

    [Fact]
    public async Task RemoveContentReferenceAsync_ShouldDelegate()
    {
        var id = Guid.NewGuid();
        _contentRefMock.Setup(s => s.RemoveContentReferenceAsync(id, default)).ReturnsAsync(Result.Success());
        await _sut.RemoveContentReferenceAsync(id);
        _contentRefMock.Verify(s => s.RemoveContentReferenceAsync(id, default), Times.Once);
    }

    [Fact]
    public async Task GetPostContentReferencesAsync_ShouldDelegate()
    {
        var postId = Guid.NewGuid();
        _contentRefMock.Setup(s => s.GetPostContentReferencesAsync(postId, default)).Returns(Task.FromResult(Result<IEnumerable<PostContentReference>>.Success(Enumerable.Empty<PostContentReference>())));
        await _sut.GetPostContentReferencesAsync(postId);
        _contentRefMock.Verify(s => s.GetPostContentReferencesAsync(postId, default), Times.Once);
    }
}
