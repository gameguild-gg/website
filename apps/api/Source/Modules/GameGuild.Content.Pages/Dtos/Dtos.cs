using System.ComponentModel.DataAnnotations;

namespace GameGuild.Content.Pages;

// ──────────────────────────── Page DTOs ────────────────────────────

/// <summary>Lightweight entry for the public sitemap feed.</summary>
public sealed record SitemapEntryDto(string Slug, DateTime? UpdatedAt, string? Locale);

public record PageDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string PageType { get; init; } = nameof(Pages.PageType.Landing);
    public string Status { get; init; } = nameof(PageStatus.Draft);
    public string? Locale { get; init; }

    // SEO
    public string? MetaTitle { get; init; }
    public string? MetaDescription { get; init; }
    public string? MetaKeywords { get; init; }
    public string? CanonicalUrl { get; init; }
    public string? RobotsDirective { get; init; }

    // OpenGraph
    public string? OgTitle { get; init; }
    public string? OgDescription { get; init; }
    public string? OgImageUrl { get; init; }
    public string? OgType { get; init; }

    // Twitter
    public string? TwitterCard { get; init; }
    public string? TwitterSite { get; init; }

    // Content
    public string? StructuredData { get; init; }
    public string? Body { get; init; }
    public string? CustomData { get; init; }

    // Hierarchy
    public Guid? ParentPageId { get; init; }
    public int SortOrder { get; init; }

    // Sections
    public List<PageSectionDto> Sections { get; init; } = [];

    // Publishing
    public DateTime? PublishedAt { get; init; }
    public DateTime? ScheduledPublishAt { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record CreatePageDto
{
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public PageType PageType { get; init; } = Pages.PageType.Landing;
    public string? Locale { get; init; }
    public string? MetaTitle { get; init; }
    public string? MetaDescription { get; init; }
    public string? MetaKeywords { get; init; }
    public string? CanonicalUrl { get; init; }
    public string? RobotsDirective { get; init; }
    public string? OgTitle { get; init; }
    public string? OgDescription { get; init; }
    public string? OgImageUrl { get; init; }
    public string? OgType { get; init; }
    public string? TwitterCard { get; init; }
    public string? TwitterSite { get; init; }
    public string? StructuredData { get; init; }
    public string? Body { get; init; }
    public string? CustomData { get; init; }
    public Guid? ParentPageId { get; init; }
    public int SortOrder { get; init; }
}

public record UpdatePageDto
{
    public string? Slug { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public PageType? PageType { get; init; }
    public PageStatus? Status { get; init; }
    public string? Locale { get; init; }
    public string? MetaTitle { get; init; }
    public string? MetaDescription { get; init; }
    public string? MetaKeywords { get; init; }
    public string? CanonicalUrl { get; init; }
    public string? RobotsDirective { get; init; }
    public string? OgTitle { get; init; }
    public string? OgDescription { get; init; }
    public string? OgImageUrl { get; init; }
    public string? OgType { get; init; }
    public string? TwitterCard { get; init; }
    public string? TwitterSite { get; init; }
    public string? StructuredData { get; init; }
    public string? Body { get; init; }
    public string? CustomData { get; init; }
    public Guid? ParentPageId { get; init; }
    public int? SortOrder { get; init; }
    public DateTime? ScheduledPublishAt { get; init; }
}

// ──────────────────────────── PageSection DTOs ────────────────────────────

public record PageSectionDto
{
    public Guid Id { get; init; }
    public Guid PageId { get; init; }
    public string SectionType { get; init; } = nameof(Pages.SectionType.Custom);
    public string? Heading { get; init; }
    public string? Subheading { get; init; }
    public string? Data { get; init; }
    public int SortOrder { get; init; }
    public bool IsVisible { get; init; }
    public string? CssClasses { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record CreatePageSectionDto
{
    public SectionType SectionType { get; init; }
    public string? Heading { get; init; }
    public string? Subheading { get; init; }
    public string? Data { get; init; }
    public int SortOrder { get; init; }
    public bool IsVisible { get; init; } = true;
    public string? CssClasses { get; init; }
}

public record UpdatePageSectionDto
{
    public SectionType? SectionType { get; init; }
    public string? Heading { get; init; }
    public string? Subheading { get; init; }
    public string? Data { get; init; }
    public int? SortOrder { get; init; }
    public bool? IsVisible { get; init; }
    public string? CssClasses { get; init; }
}

// ──────────────────────────── ContentResource DTOs ────────────────────────────

public record ContentResourceDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? Body { get; init; }
    public string ResourceType { get; init; } = nameof(ContentResourceType.Article);
    public string Status { get; init; } = nameof(ContentResourceStatus.Draft);
    public string? Locale { get; init; }
    public string? CategorySlug { get; init; }
    public string? Tags { get; init; }
    public Guid? AuthorId { get; init; }
    public string? AuthorName { get; init; }
    public string? CoverImageUrl { get; init; }
    public string? VideoUrl { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ExternalUrl { get; init; }
    public Guid? LinkedEntityId { get; init; }
    public string? LinkedEntityType { get; init; }
    public string? MetaTitle { get; init; }
    public string? MetaDescription { get; init; }
    public string? OgImageUrl { get; init; }
    public string? StructuredData { get; init; }
    public int? ReadingTimeMinutes { get; init; }
    public long ViewCount { get; init; }
    public bool IsFeatured { get; init; }
    public int SortOrder { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime? ScheduledPublishAt { get; init; }
    public string? CustomData { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record CreateContentResourceDto
{
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? Body { get; init; }
    public ContentResourceType ResourceType { get; init; } = ContentResourceType.Article;
    public string? Locale { get; init; }
    public string? CategorySlug { get; init; }
    public string? Tags { get; init; }
    public string? CoverImageUrl { get; init; }
    public string? VideoUrl { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ExternalUrl { get; init; }
    public Guid? LinkedEntityId { get; init; }
    public string? LinkedEntityType { get; init; }
    public string? MetaTitle { get; init; }
    public string? MetaDescription { get; init; }
    public string? OgImageUrl { get; init; }
    public string? StructuredData { get; init; }
    public int? ReadingTimeMinutes { get; init; }
    public bool IsFeatured { get; init; }
    public int SortOrder { get; init; }
    public string? CustomData { get; init; }
}

public record UpdateContentResourceDto
{
    public string? Slug { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public string? Body { get; init; }
    public ContentResourceType? ResourceType { get; init; }
    public ContentResourceStatus? Status { get; init; }
    public string? Locale { get; init; }
    public string? CategorySlug { get; init; }
    public string? Tags { get; init; }
    public string? CoverImageUrl { get; init; }
    public string? VideoUrl { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ExternalUrl { get; init; }
    public Guid? LinkedEntityId { get; init; }
    public string? LinkedEntityType { get; init; }
    public string? MetaTitle { get; init; }
    public string? MetaDescription { get; init; }
    public string? OgImageUrl { get; init; }
    public string? StructuredData { get; init; }
    public int? ReadingTimeMinutes { get; init; }
    public bool? IsFeatured { get; init; }
    public int? SortOrder { get; init; }
    public DateTime? ScheduledPublishAt { get; init; }
    public string? CustomData { get; init; }
}

// ──────────────────────────── MarketingLead DTOs ────────────────────────────

public record MarketingLeadDto
{
    public Guid Id { get; init; }
    public string Source { get; init; } = MarketingLeadSources.Contact;
    public string Status { get; init; } = MarketingLeadStatuses.New;
    public string? Name { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? Company { get; init; }
    public string? Topic { get; init; }
    public string? Plan { get; init; }
    public string? Message { get; init; }
    public string? Locale { get; init; }
    public string? PagePath { get; init; }
    public string? Referrer { get; init; }
    public string? UserAgent { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record CreateMarketingLeadDto
{
    [Required]
    [MaxLength(40)]
    public string Source { get; init; } = MarketingLeadSources.Contact;

    [MaxLength(120)]
    public string? Name { get; init; }

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; init; } = string.Empty;

    [MaxLength(200)]
    public string? Company { get; init; }

    [MaxLength(40)]
    public string? Topic { get; init; }

    [MaxLength(60)]
    public string? Plan { get; init; }

    [MaxLength(4000)]
    public string? Message { get; init; }

    [MaxLength(10)]
    public string? Locale { get; init; }

    [MaxLength(300)]
    public string? PagePath { get; init; }

    [MaxLength(2000)]
    public string? Referrer { get; init; }

    [MaxLength(500)]
    public string? UserAgent { get; init; }
}


// ──────────────────────────── CourseProposal DTOs ────────────────────────────

/// <summary>Course proposal as rendered by the CRM or the public detail page.</summary>
public record CourseProposalDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string Status { get; init; } = CourseProposalStatuses.Submitted;
    public string CourseTitle { get; init; } = string.Empty;
    public string? Pitch { get; init; }
    public string? Audience { get; init; }
    public string? ProposerName { get; init; }
    public string? ProposerEmail { get; init; }
    public string? Metadata { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime? PublishedAt { get; init; }
}

/// <summary>Public submission payload for the creator pipeline (issue #105).</summary>
public record CreateCourseProposalDto
{
    [Required]
    [MaxLength(200)]
    public string CourseTitle { get; init; } = string.Empty;

    [MaxLength(4000)]
    public string? Pitch { get; init; }

    [MaxLength(2000)]
    public string? Audience { get; init; }

    [Required]
    [MaxLength(120)]
    public string ProposerName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string ProposerEmail { get; init; } = string.Empty;

    [MaxLength(4000)]
    public string? Metadata { get; init; }
}

/// <summary>CRM-side update payload: status transitions and content edits.</summary>
public record UpdateCourseProposalDto
{
    [MaxLength(40)]
    public string? Status { get; init; }

    [MaxLength(200)]
    public string? CourseTitle { get; init; }

    [MaxLength(4000)]
    public string? Pitch { get; init; }

    [MaxLength(2000)]
    public string? Audience { get; init; }

    [MaxLength(4000)]
    public string? Metadata { get; init; }
}

// ──────────────────────────── OpenGraph DTO ────────────────────────────

/// <summary>
///     Resolved OpenGraph / SEO metadata for a given slug — returned by the public OG endpoint.
/// </summary>
public record OpenGraphMetadataDto
{
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? OgTitle { get; init; }
    public string? OgDescription { get; init; }
    public string? OgImageUrl { get; init; }
    public string? OgType { get; init; }
    public string? TwitterCard { get; init; }
    public string? TwitterSite { get; init; }
    public string? CanonicalUrl { get; init; }
    public string? RobotsDirective { get; init; }
    public string? StructuredData { get; init; }
}
