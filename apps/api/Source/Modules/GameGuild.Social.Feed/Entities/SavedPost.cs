namespace GameGuild.Social.Feed;

/// <summary>
/// A post bookmarked by a user for their private saved feed.
/// </summary>
public sealed class SavedPost : EntityBase
{
    public Guid UserId { get; private set; }
    public Guid PostId { get; private set; }

    private SavedPost() { }

    public static SavedPost Create(Guid userId, Guid postId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A user is required.", nameof(userId));
        if (postId == Guid.Empty) throw new ArgumentException("A post is required.", nameof(postId));

        return new SavedPost
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PostId = postId
        };
    }
}
