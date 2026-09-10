namespace GameGuild.Social.Posts.Services;

/// <summary>
/// Service interface for post comment CRUD operations
/// </summary>
public interface IPostCommentService
{
    /// <summary>Adds a comment to a post</summary>
    Task<Result<PostComment>> AddCommentAsync(Guid postId, Guid authorId, string content, Guid? parentCommentId = null, CancellationToken cancellationToken = default);

    /// <summary>Updates a comment</summary>
    Task<Result<PostComment>> UpdateCommentAsync(Guid commentId, Guid actorId, string content, CancellationToken cancellationToken = default);

    /// <summary>Deletes a comment</summary>
    Task<Result> DeleteCommentAsync(Guid commentId, Guid actorId, CancellationToken cancellationToken = default);

    /// <summary>Gets comments for a post</summary>
    Task<Result<IEnumerable<PostComment>>> GetPostCommentsAsync(Guid postId, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    /// <summary>Gets a comment by ID</summary>
    Task<Result<PostComment>> GetCommentByIdAsync(Guid commentId, CancellationToken cancellationToken = default);
}
