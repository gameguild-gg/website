namespace GameGuild.Social.Feed;

public enum FeedScope
{
    ForYou,
    Following,
    Community,
    Saved
}

public enum SocialFeedItemKind
{
    Post,
    Repost,
    TestingSession
}

public sealed record SocialFeedPageDto(
    IReadOnlyList<SocialFeedItemDto> Items,
    string? NextCursor);

public sealed record SocialFeedItemDto(
    Guid Id,
    SocialFeedItemKind Kind,
    DateTime CreatedAt,
    FeedAuthorDto Author,
    SocialPostContentDto? Post,
    TestingSessionFeedDto? TestingSession,
    FeedEngagementDto Engagement,
    FeedViewerStateDto Viewer,
    IReadOnlyList<string> Tags);

public sealed record FeedAuthorDto(
    Guid UserId,
    string Handle,
    string DisplayName,
    string? AvatarUrl,
    bool IsVerified);

public sealed record SocialPostContentDto(
    string Content,
    string? MediaUrl,
    string? MediaType,
    string Visibility,
    bool IsEdited,
    DateTime? EditedAt,
    OriginalPostDto? RepostedPost);

public sealed record OriginalPostDto(
    Guid Id,
    FeedAuthorDto Author,
    string Content,
    string? MediaUrl,
    string? MediaType,
    DateTime CreatedAt);

public sealed record TestingSessionFeedDto(
    string Name,
    DateTime StartsAt,
    DateTime EndsAt,
    string Mode,
    string Status,
    int MaxTesters,
    int RegisteredTesterCount,
    int AvailableTesterCount);

public sealed record FeedEngagementDto(
    int ReactionsCount,
    int CommentsCount,
    int RepostsCount,
    int ViewsCount);

public sealed record FeedViewerStateDto(
    string? Reaction,
    bool IsSaved,
    bool IsFollowingAuthor,
    bool HasReposted,
    bool CanEdit,
    bool CanDelete);

public sealed record SocialFeedProfileDto(
    Guid Id,
    Guid UserId,
    string Handle,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    string? BannerUrl,
    string? Headline,
    string? Location,
    string? TimeZone,
    string? WebsiteUrl,
    string AvailabilityStatus,
    bool IsVerified,
    int FollowerCount,
    int FollowingCount,
    int PostCount,
    int ProjectCount,
    bool IsFollowing);

public sealed class InvalidFeedCursorException : Exception
{
    public InvalidFeedCursorException() : base("The feed cursor is invalid or has expired.") { }
}
