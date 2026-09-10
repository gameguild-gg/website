namespace GameGuild.Social.Feed;

public sealed class Story : EntityBase
{
    public Guid AuthorId { get; private set; }
    public Guid AssetReferenceId { get; private set; }
    public string? Caption { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private Story() { }

    public static Story Create(
        Guid authorId,
        Guid assetReferenceId,
        string? caption,
        DateTime createdAt)
    {
        if (authorId == Guid.Empty) throw new ArgumentException("An author is required.", nameof(authorId));
        if (assetReferenceId == Guid.Empty) throw new ArgumentException("An asset is required.", nameof(assetReferenceId));

        var normalizedCaption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        if (normalizedCaption?.Length > 280) throw new ArgumentOutOfRangeException(nameof(caption), "Story captions are limited to 280 characters.");

        return new Story
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            AssetReferenceId = assetReferenceId,
            Caption = normalizedCaption,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            ExpiresAt = createdAt.AddHours(24)
        };
    }

    public void Delete() => SoftDelete();
}

public sealed class StoryView : EntityBase
{
    public Guid StoryId { get; private set; }
    public Guid ViewerId { get; private set; }
    public DateTime ViewedAt { get; private set; }

    private StoryView() { }

    public static StoryView Create(Guid storyId, Guid viewerId, DateTime viewedAt)
        => new()
        {
            Id = Guid.NewGuid(),
            StoryId = storyId,
            ViewerId = viewerId,
            ViewedAt = viewedAt,
            CreatedAt = viewedAt,
            UpdatedAt = viewedAt
        };
}
