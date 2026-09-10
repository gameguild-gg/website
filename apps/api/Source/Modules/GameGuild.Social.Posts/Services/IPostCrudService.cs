namespace GameGuild.Social.Posts.Services;

/// <summary>
/// Service interface for post CRUD operations, queries, search, and filtering
/// </summary>
public interface IPostCrudService
{
    #region Basic CRUD Operations

    /// <summary>Gets a post by its ID</summary>
    Task<Result<Post>> GetPostByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets a post by ID with all related data (statistics, comments, likes)</summary>
    Task<Result<Post>> GetPostByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets paginated posts</summary>
    Task<Result<IEnumerable<Post>>> GetPostsAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Creates a new post</summary>
    Task<Result<Post>> CreatePostAsync(
        Guid authorId,
        string content,
        PostVisibility visibility = PostVisibility.Public,
        string? mediaUrl = null,
        MediaType? mediaType = null,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Updates an existing post</summary>
    Task<Result<Post>> UpdatePostAsync(Guid postId, Guid actorId, string content, CancellationToken cancellationToken = default);

    /// <summary>Deletes a post (soft delete)</summary>
    Task<Result> DeletePostAsync(Guid postId, Guid actorId, CancellationToken cancellationToken = default);

    /// <summary>Restores a soft-deleted post</summary>
    Task<Result> RestorePostAsync(Guid postId, CancellationToken cancellationToken = default);

    #endregion

    #region Filtered Queries

    /// <summary>Gets posts by a specific author</summary>
    Task<Result<IEnumerable<Post>>> GetPostsByAuthorAsync(Guid authorId, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Gets posts by visibility level</summary>
    Task<Result<IEnumerable<Post>>> GetPostsByVisibilityAsync(PostVisibility visibility, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Gets pinned posts</summary>
    Task<Result<IEnumerable<Post>>> GetPinnedPostsAsync(Guid? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>Gets public posts</summary>
    Task<Result<IEnumerable<Post>>> GetPublicPostsAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Gets posts by tenant</summary>
    Task<Result<IEnumerable<Post>>> GetPostsByTenantAsync(Guid tenantId, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    #endregion

    #region Search and Advanced Queries

    /// <summary>Searches posts by content</summary>
    Task<Result<IEnumerable<Post>>> SearchPostsAsync(string searchTerm, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Gets posts by tags</summary>
    Task<Result<IEnumerable<Post>>> GetPostsByTagsAsync(string[] tags, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Gets trending posts based on engagement score</summary>
    Task<Result<IEnumerable<Post>>> GetTrendingPostsAsync(int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Gets personalized feed for a user</summary>
    Task<Result<IEnumerable<Post>>> GetFeedPostsAsync(Guid userId, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    #endregion

    #region Validation and Utilities

    /// <summary>Checks if user can perform an action on a post</summary>
    Task<Result<bool>> CanUserPerformActionAsync(Guid postId, Guid userId, string action, CancellationToken cancellationToken = default);

    #endregion
}
