
namespace GameGuild.Learning.Experience.Discovery;

/// <summary>
/// Represents a featured course or collection for discovery
/// </summary>
public class FeaturedContent : EntityBase
{
    public Guid? CourseId { get; private set; }
    public Guid? LearningPathId { get; private set; }
    public new Guid? TenantId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Subtitle { get; private set; }
    public string? ImageUrl { get; private set; }
    public string? LinkUrl { get; private set; }
    public FeaturedContentType Type { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime? StartsAt { get; private set; }
    public DateTime? EndsAt { get; private set; }
    public bool IsActive { get; private set; }
    public string? TargetAudience { get; private set; } // JSON filter criteria

    private FeaturedContent() { } // EF Core

    public static FeaturedContent Create(
        FeaturedContentType type,
        string title,
        int displayOrder,
        Guid? courseId = null,
        Guid? learningPathId = null,
        Guid? tenantId = null)
    {
        return new FeaturedContent
        {
            Id = Guid.NewGuid(),
            Type = type,
            Title = title,
            DisplayOrder = displayOrder,
            CourseId = courseId,
            LearningPathId = learningPathId,
            TenantId = tenantId,
            IsActive = true
        };
    }

    public void Update(
        string? title = null,
        string? subtitle = null,
        string? imageUrl = null,
        string? linkUrl = null,
        int? displayOrder = null,
        DateTime? startsAt = null,
        DateTime? endsAt = null,
        bool? isActive = null,
        string? targetAudience = null)
    {
        if (title is not null)
        {
            Title = title;
        }

        if (subtitle is not null)
        {
            Subtitle = subtitle;
        }

        if (imageUrl is not null)
        {
            ImageUrl = imageUrl;
        }

        if (linkUrl is not null)
        {
            LinkUrl = linkUrl;
        }

        if (displayOrder.HasValue)
        {
            DisplayOrder = displayOrder.Value;
        }

        if (startsAt.HasValue)
        {
            StartsAt = startsAt.Value;
        }

        if (endsAt.HasValue)
        {
            EndsAt = endsAt.Value;
        }

        if (isActive.HasValue)
        {
            IsActive = isActive.Value;
        }

        if (targetAudience is not null)
        {
            TargetAudience = targetAudience;
        }

        UpdatedAt = SystemClock.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = SystemClock.UtcNow;
    }

    public bool IsCurrentlyActive()
    {
        if (!IsActive)
        {
            return false;
        }

        var now = SystemClock.UtcNow;
        if (StartsAt.HasValue && now < StartsAt.Value)
        {
            return false;
        }

        if (EndsAt.HasValue && now > EndsAt.Value)
        {
            return false;
        }

        return true;
    }
}

/// <summary>
/// Represents a curated collection of courses
/// </summary>
public class CourseCollection : EntityBase
{
    public new Guid? TenantId { get; private set; }
    public Guid CuratorId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool IsPublished { get; private set; }
    public bool IsFeatured { get; private set; }
    public int CourseCount { get; private set; }
    public CollectionType Type { get; private set; }

    private CourseCollection() { } // EF Core

    public static CourseCollection Create(
        Guid curatorId,
        string title,
        string slug,
        CollectionType type = CollectionType.Curated,
        Guid? tenantId = null)
    {
        return new CourseCollection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CuratorId = curatorId,
            Title = title,
            Slug = slug,
            Type = type,
            IsPublished = false,
            IsFeatured = false,
            CourseCount = 0
        };
    }

    public void Update(
        string? title = null,
        string? description = null,
        string? imageUrl = null,
        bool? isFeatured = null)
    {
        if (title is not null)
        {
            Title = title;
        }

        if (description is not null)
        {
            Description = description;
        }

        if (imageUrl is not null)
        {
            ImageUrl = imageUrl;
        }

        if (isFeatured.HasValue)
        {
            IsFeatured = isFeatured.Value;
        }

        UpdatedAt = SystemClock.UtcNow;
    }

    public void Publish()
    {
        IsPublished = true;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void Unpublish()
    {
        IsPublished = false;
        UpdatedAt = SystemClock.UtcNow;
    }
}

/// <summary>
/// Tracks search history for discovery analytics
/// </summary>
public class SearchHistory : EntityBase
{
    public Guid? UserId { get; private set; }
    public new Guid? TenantId { get; private set; }
    public string Query { get; private set; } = string.Empty;
    public int ResultCount { get; private set; }
    public Guid? ClickedCourseId { get; private set; }
    public int? ClickedPosition { get; private set; }
    public string? Filters { get; private set; } // JSON

    private SearchHistory() { } // EF Core

    public static SearchHistory Create(
        string query, 
        int resultCount, 
        Guid? userId = null,
        string? filters = null,
        Guid? tenantId = null)
    {
        return new SearchHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            Query = query,
            ResultCount = resultCount,
            Filters = filters
        };
    }

    public void RecordClick(Guid courseId, int position)
    {
        ClickedCourseId = courseId;
        ClickedPosition = position;
        UpdatedAt = SystemClock.UtcNow;
    }
}

public enum FeaturedContentType
{
    HeroBanner,
    CategoryHighlight,
    NewRelease,
    TopRated,
    TrendingNow,
    StaffPick,
    SeasonalPromotion
}

public enum CollectionType
{
    Curated,
    Category,
    Skill,
    Career,
    Trending,
    NewReleases
}
