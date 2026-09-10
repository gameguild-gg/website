using Microsoft.Extensions.Logging;

namespace GameGuild.Social.Posts.Services;

/// <summary>
/// Composite implementation of IPostService that delegates to focused sub-services.
/// Kept for backward compatibility with code that depends on IPostService.
/// </summary>
public class PostService : IPostService
{
    private readonly IPostCrudService _crudService;
    private readonly IPostEngagementService _engagementService;
    private readonly IPostCommentService _commentService;
    private readonly IPostTagService _tagService;
    private readonly IPostContentReferenceService _contentReferenceService;

    public PostService(
        IPostCrudService crudService,
        IPostEngagementService engagementService,
        IPostCommentService commentService,
        IPostTagService tagService,
        IPostContentReferenceService contentReferenceService)
    {
        _crudService = crudService;
        _engagementService = engagementService;
        _commentService = commentService;
        _tagService = tagService;
        _contentReferenceService = contentReferenceService;
    }

    // CRUD delegations
    public Task<Result<Post>> GetPostByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _crudService.GetPostByIdAsync(id, cancellationToken);

    public Task<Result<Post>> GetPostByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _crudService.GetPostByIdWithDetailsAsync(id, cancellationToken);

    public Task<Result<IEnumerable<Post>>> GetPostsAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default) =>
        _crudService.GetPostsAsync(skip, take, cancellationToken);

    public Task<Result<Post>> CreatePostAsync(Guid authorId, string content, PostVisibility visibility = PostVisibility.Public, string? mediaUrl = null, MediaType? mediaType = null, Guid? tenantId = null, CancellationToken cancellationToken = default) =>
        _crudService.CreatePostAsync(authorId, content, visibility, mediaUrl, mediaType, tenantId, cancellationToken);

    public Task<Result<Post>> UpdatePostAsync(Guid postId, Guid actorId, string content, CancellationToken cancellationToken = default) =>
        _crudService.UpdatePostAsync(postId, actorId, content, cancellationToken);

    public Task<Result> DeletePostAsync(Guid postId, Guid actorId, CancellationToken cancellationToken = default) =>
        _crudService.DeletePostAsync(postId, actorId, cancellationToken);

    public Task<Result> RestorePostAsync(Guid postId, CancellationToken cancellationToken = default) =>
        _crudService.RestorePostAsync(postId, cancellationToken);

    public Task<Result<IEnumerable<Post>>> GetPostsByAuthorAsync(Guid authorId, int skip = 0, int take = 50, CancellationToken cancellationToken = default) =>
        _crudService.GetPostsByAuthorAsync(authorId, skip, take, cancellationToken);

    public Task<Result<IEnumerable<Post>>> GetPostsByVisibilityAsync(PostVisibility visibility, int skip = 0, int take = 50, CancellationToken cancellationToken = default) =>
        _crudService.GetPostsByVisibilityAsync(visibility, skip, take, cancellationToken);

    public Task<Result<IEnumerable<Post>>> GetPinnedPostsAsync(Guid? tenantId = null, CancellationToken cancellationToken = default) =>
        _crudService.GetPinnedPostsAsync(tenantId, cancellationToken);

    public Task<Result<IEnumerable<Post>>> GetPublicPostsAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default) =>
        _crudService.GetPublicPostsAsync(skip, take, cancellationToken);

    public Task<Result<IEnumerable<Post>>> GetPostsByTenantAsync(Guid tenantId, int skip = 0, int take = 50, CancellationToken cancellationToken = default) =>
        _crudService.GetPostsByTenantAsync(tenantId, skip, take, cancellationToken);

    public Task<Result<IEnumerable<Post>>> SearchPostsAsync(string searchTerm, int skip = 0, int take = 50, CancellationToken cancellationToken = default) =>
        _crudService.SearchPostsAsync(searchTerm, skip, take, cancellationToken);

    public Task<Result<IEnumerable<Post>>> GetPostsByTagsAsync(string[] tags, int skip = 0, int take = 50, CancellationToken cancellationToken = default) =>
        _crudService.GetPostsByTagsAsync(tags, skip, take, cancellationToken);

    public Task<Result<IEnumerable<Post>>> GetTrendingPostsAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default) =>
        _crudService.GetTrendingPostsAsync(skip, take, cancellationToken);

    public Task<Result<IEnumerable<Post>>> GetFeedPostsAsync(Guid userId, int skip = 0, int take = 50, CancellationToken cancellationToken = default) =>
        _crudService.GetFeedPostsAsync(userId, skip, take, cancellationToken);

    public Task<Result<bool>> CanUserPerformActionAsync(Guid postId, Guid userId, string action, CancellationToken cancellationToken = default) =>
        _crudService.CanUserPerformActionAsync(postId, userId, action, cancellationToken);

    // Engagement delegations
    public Task<Result<bool>> TogglePostLikeAsync(Guid postId, Guid userId, string reactionType = "like", CancellationToken cancellationToken = default) =>
        _engagementService.TogglePostLikeAsync(postId, userId, reactionType, cancellationToken);

    public Task<Result<bool>> TogglePostPinAsync(Guid postId, Guid actorId, CancellationToken cancellationToken = default) =>
        _engagementService.TogglePostPinAsync(postId, actorId, cancellationToken);

    public Task<Result<Post>> CreateRepostAsync(Guid sourcePostId, Guid actorId, string? content = null, CancellationToken cancellationToken = default) =>
        _engagementService.CreateRepostAsync(sourcePostId, actorId, content, cancellationToken);

    public Task<Result> SharePostAsync(Guid postId, CancellationToken cancellationToken = default) =>
        _engagementService.SharePostAsync(postId, cancellationToken);

    public Task<Result<PostStatistics>> GetPostStatisticsAsync(Guid postId, CancellationToken cancellationToken = default) =>
        _engagementService.GetPostStatisticsAsync(postId, cancellationToken);

    public Task<Result> RecordPostViewAsync(Guid postId, Guid? userId, string? ipAddress = null, string? userAgent = null, string? referrer = null, CancellationToken cancellationToken = default) =>
        _engagementService.RecordPostViewAsync(postId, userId, ipAddress, userAgent, referrer, cancellationToken);

    public Task<Result> UpdateViewEngagementAsync(Guid viewId, int durationSeconds, bool engaged = false, CancellationToken cancellationToken = default) =>
        _engagementService.UpdateViewEngagementAsync(viewId, durationSeconds, engaged, cancellationToken);

    public Task<Result> RecalculateStatisticsAsync(Guid postId, CancellationToken cancellationToken = default) =>
        _engagementService.RecalculateStatisticsAsync(postId, cancellationToken);

    public Task<Result<int>> RecalculateAllTrendingScoresAsync(CancellationToken cancellationToken = default) =>
        _engagementService.RecalculateAllTrendingScoresAsync(cancellationToken);

    public Task<Result<PostFollower>> FollowPostAsync(Guid postId, Guid userId, bool notifyOnComments = true, bool notifyOnLikes = false, bool notifyOnShares = false, bool notifyOnUpdates = true, CancellationToken cancellationToken = default) =>
        _engagementService.FollowPostAsync(postId, userId, notifyOnComments, notifyOnLikes, notifyOnShares, notifyOnUpdates, cancellationToken);

    public Task<Result> UnfollowPostAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default) =>
        _engagementService.UnfollowPostAsync(postId, userId, cancellationToken);

    public Task<Result> UpdateFollowPreferencesAsync(Guid postId, Guid userId, bool? notifyOnComments = null, bool? notifyOnLikes = null, bool? notifyOnShares = null, bool? notifyOnUpdates = null, CancellationToken cancellationToken = default) =>
        _engagementService.UpdateFollowPreferencesAsync(postId, userId, notifyOnComments, notifyOnLikes, notifyOnShares, notifyOnUpdates, cancellationToken);

    public Task<Result<IEnumerable<PostFollower>>> GetPostFollowersAsync(Guid postId, CancellationToken cancellationToken = default) =>
        _engagementService.GetPostFollowersAsync(postId, cancellationToken);

    public Task<Result<bool>> IsFollowingPostAsync(Guid postId, Guid userId, CancellationToken cancellationToken = default) =>
        _engagementService.IsFollowingPostAsync(postId, userId, cancellationToken);

    // Comment delegations
    public Task<Result<PostComment>> GetCommentByIdAsync(Guid commentId, CancellationToken cancellationToken = default) =>
        _commentService.GetCommentByIdAsync(commentId, cancellationToken);

    public Task<Result<PostComment>> AddCommentAsync(Guid postId, Guid authorId, string content, Guid? parentCommentId = null, CancellationToken cancellationToken = default) =>
        _commentService.AddCommentAsync(postId, authorId, content, parentCommentId, cancellationToken);

    public Task<Result<PostComment>> UpdateCommentAsync(Guid commentId, Guid actorId, string content, CancellationToken cancellationToken = default) =>
        _commentService.UpdateCommentAsync(commentId, actorId, content, cancellationToken);

    public Task<Result> DeleteCommentAsync(Guid commentId, Guid actorId, CancellationToken cancellationToken = default) =>
        _commentService.DeleteCommentAsync(commentId, actorId, cancellationToken);

    public Task<Result<IEnumerable<PostComment>>> GetPostCommentsAsync(Guid postId, int skip = 0, int take = 50, CancellationToken cancellationToken = default) =>
        _commentService.GetPostCommentsAsync(postId, skip, take, cancellationToken);

    // Tag delegations
    public Task<Result> AddTagsToPostAsync(Guid postId, string[] tagNames, CancellationToken cancellationToken = default) =>
        _tagService.AddTagsToPostAsync(postId, tagNames, cancellationToken);

    public Task<Result> RemoveTagsFromPostAsync(Guid postId, string[] tagNames, CancellationToken cancellationToken = default) =>
        _tagService.RemoveTagsFromPostAsync(postId, tagNames, cancellationToken);

    public Task<Result<IEnumerable<PostTag>>> GetPostTagsAsync(Guid postId, CancellationToken cancellationToken = default) =>
        _tagService.GetPostTagsAsync(postId, cancellationToken);

    public Task<Result<PostTag>> GetOrCreateTagAsync(string name, string category = "general", CancellationToken cancellationToken = default) =>
        _tagService.GetOrCreateTagAsync(name, category, cancellationToken);

    public Task<Result<IEnumerable<PostTag>>> GetPopularTagsAsync(int count = 20, CancellationToken cancellationToken = default) =>
        _tagService.GetPopularTagsAsync(count, cancellationToken);

    // Content Reference delegations
    public Task<Result<PostContentReference>> AddContentReferenceAsync(Guid postId, Guid resourceId, string resourceType, string referenceType = "mention", string? context = null, CancellationToken cancellationToken = default) =>
        _contentReferenceService.AddContentReferenceAsync(postId, resourceId, resourceType, referenceType, context, cancellationToken);

    public Task<Result> RemoveContentReferenceAsync(Guid referenceId, CancellationToken cancellationToken = default) =>
        _contentReferenceService.RemoveContentReferenceAsync(referenceId, cancellationToken);

    public Task<Result<IEnumerable<PostContentReference>>> GetPostContentReferencesAsync(Guid postId, CancellationToken cancellationToken = default) =>
        _contentReferenceService.GetPostContentReferencesAsync(postId, cancellationToken);
}
