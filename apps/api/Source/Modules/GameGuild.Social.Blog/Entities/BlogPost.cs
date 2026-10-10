using System.Text.Json;

namespace GameGuild.Social.Blog;

/// <summary>
/// A long-form blog article. The post row IS the draft — <see cref="Revision"/> guards
/// concurrent co-author edits. TenantId is quota/policy metadata only; blog content is
/// global and user-scoped.
/// </summary>
public sealed class BlogPost : EntityBase
{
    public const int MaxTags = 20;
    public const int MaxTagLength = 50;

    public Guid PrimaryAuthorId { get; private set; }
    public new Guid? TenantId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Excerpt { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string? JsonBody { get; private set; }
    public BlogContentFormat Format { get; private set; }
    public List<string> Tags { get; private set; } = [];
    public string? MetaTitle { get; private set; }
    public string? MetaDescription { get; private set; }
    public string? OgImageUrl { get; private set; }
    public string? CanonicalUrlOverride { get; private set; }
    public string TwitterCard { get; private set; } = "summary_large_image";
    public string? StructuredDataOverride { get; private set; }
    public bool AllowComments { get; private set; }
    public BlogPostStatus Status { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public int ViewsCount { get; private set; }
    public int CommentsCount { get; private set; }
    public int ReadTimeMinutes { get; private set; }
    public int Revision { get; private set; }

    private BlogPost() { } // EF Core

    public static BlogPost Create(
        Guid primaryAuthorId,
        string title,
        string slug,
        BlogContentFormat format,
        Guid? tenantId = null,
        DateTimeOffset? now = null)
    {
        if (primaryAuthorId == Guid.Empty)
        {
            throw new ArgumentException("Primary author ID is required.", nameof(primaryAuthorId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        var at = now ?? DateTimeOffset.UtcNow;

        return new BlogPost
        {
            Id = Guid.NewGuid(),
            PrimaryAuthorId = primaryAuthorId,
            TenantId = tenantId,
            Title = title.Trim(),
            Slug = slug.Trim(),
            Content = string.Empty,
            Format = format,
            AllowComments = true,
            Status = BlogPostStatus.Draft,
            Revision = 1,
            ReadTimeMinutes = 1,
            CreatedAt = at.UtcDateTime,
            UpdatedAt = at.UtcDateTime,
        };
    }

    /// <summary>
    /// Record a saved edit. Revision-guarded: a stale expected revision throws
    /// (mapped to 409 by the caller). Every successful edit bumps Revision.
    /// </summary>
    public void ApplyDraftEdit(
        int expectedRevision,
        string? title,
        string? content,
        string? jsonBody,
        string? excerpt,
        IReadOnlyList<string>? tags,
        string? metaTitle,
        string? metaDescription,
        string? ogImageUrl,
        string? canonicalUrlOverride,
        string? twitterCard,
        string? structuredDataOverride,
        bool? allowComments,
        int? readTimeMinutes,
        DateTimeOffset? now = null)
    {
        if (expectedRevision != Revision)
        {
            throw new BlogRevisionConflictException(expectedRevision, Revision);
        }

        if (title is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
        }

        if (jsonBody is not null && Format != BlogContentFormat.Lexical)
        {
            throw new InvalidOperationException("Only Lexical-format posts carry a JSON body.");
        }

        var at = now ?? DateTimeOffset.UtcNow;
        if (title is not null)
        {
            Title = title.Trim();
        }

        if (content is not null)
        {
            Content = content;
        }

        if (jsonBody is not null)
        {
            JsonBody = jsonBody;
        }

        if (excerpt is not null)
        {
            Excerpt = excerpt;
        }

        if (tags is not null)
        {
            Tags = [.. NormalizeTags(tags)];
        }

        if (metaTitle is not null)
        {
            MetaTitle = metaTitle;
        }

        if (metaDescription is not null)
        {
            MetaDescription = metaDescription;
        }

        if (ogImageUrl is not null)
        {
            OgImageUrl = ogImageUrl;
        }

        if (canonicalUrlOverride is not null)
        {
            CanonicalUrlOverride = canonicalUrlOverride;
        }

        if (twitterCard is not null)
        {
            TwitterCard = twitterCard;
        }

        if (structuredDataOverride is not null)
        {
            StructuredDataOverride = structuredDataOverride;
        }

        if (allowComments.HasValue)
        {
            AllowComments = allowComments.Value;
        }

        if (readTimeMinutes.HasValue)
        {
            ReadTimeMinutes = Math.Max(1, readTimeMinutes.Value);
        }

        Revision = checked(Revision + 1);
        UpdatedAt = at.UtcDateTime;
    }

    /// <summary>Change the slug (primary author only, service records slug history).</summary>
    public void ChangeSlug(string newSlug, DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newSlug);
        Slug = newSlug.Trim();
        Revision = checked(Revision + 1);
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).UtcDateTime;
    }

    /// <summary>Transfer primary authorship (service validates the new primary is a co-author and records history).</summary>
    public void TransferPrimary(Guid newPrimaryAuthorId, DateTimeOffset? now = null)
    {
        if (newPrimaryAuthorId == Guid.Empty)
        {
            throw new ArgumentException("New primary author ID is required.", nameof(newPrimaryAuthorId));
        }

        PrimaryAuthorId = newPrimaryAuthorId;
        Revision = checked(Revision + 1);
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).UtcDateTime;
    }

    public void Publish(DateTimeOffset? now = null)
    {
        Status = BlogPostStatus.Published;
        PublishedAt = (now ?? DateTimeOffset.UtcNow).UtcDateTime;
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).UtcDateTime;
    }

    public void Unpublish(DateTimeOffset? now = null)
    {
        Status = BlogPostStatus.Draft;
        UpdatedAt = (now ?? DateTimeOffset.UtcNow).UtcDateTime;
    }

    /// <summary>View beacon — must NOT bump UpdatedAt (keeps JSON-LD dateModified stable).</summary>
    public void IncrementViews() => ViewsCount++;

    /// <summary>Comment count maintenance — must NOT bump UpdatedAt.</summary>
    public void IncrementComments() => CommentsCount++;

    /// <summary>Comment count maintenance — must NOT bump UpdatedAt.</summary>
    public void DecrementComments()
    {
        if (CommentsCount > 0)
        {
            CommentsCount--;
        }
    }

    /// <summary>Serialized tags for persistence (jsonb column).</summary>
    public string TagsJson
    {
        get => JsonSerializer.Serialize(Tags);
        private set => Tags = string.IsNullOrWhiteSpace(value)
            ? []
            : JsonSerializer.Deserialize<List<string>>(value) ?? [];
    }

    public static IReadOnlyList<string> NormalizeTags(IEnumerable<string> tags)
    {
        var normalized = tags
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxTags)
            .ToList();
        foreach (var tag in normalized)
        {
            if (tag.Length > MaxTagLength)
            {
                throw new ArgumentException($"Tag '{tag}' exceeds {MaxTagLength} characters.");
            }
        }

        return normalized;
    }
}

public enum BlogPostStatus
{
    Draft,
    Published,
}

/// <summary>Thrown when a draft edit or proposal apply arrives with a stale revision.</summary>
public sealed class BlogRevisionConflictException(int expectedRevision, int currentRevision)
    : InvalidOperationException($"Blog post revision {expectedRevision} is stale; current revision is {currentRevision}.")
{
    public int ExpectedRevision { get; } = expectedRevision;
    public int CurrentRevision { get; } = currentRevision;
}
