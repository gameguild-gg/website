namespace GameGuild.Content.Pages;

/// <summary>Entity ↔ DTO mapping extensions.</summary>
public static class MappingExtensions
{
    // ──── Page ────

    public static PageDto ToDto(this Page entity) => new()
    {
        Id = entity.Id,
        Slug = entity.Slug,
        Title = entity.Title,
        Description = entity.Description,
        PageType = entity.PageType.ToString(),
        Status = entity.Status.ToString(),
        Locale = entity.Locale,
        MetaTitle = entity.MetaTitle,
        MetaDescription = entity.MetaDescription,
        MetaKeywords = entity.MetaKeywords,
        CanonicalUrl = entity.CanonicalUrl,
        RobotsDirective = entity.RobotsDirective,
        OgTitle = entity.OgTitle,
        OgDescription = entity.OgDescription,
        OgImageUrl = entity.OgImageUrl,
        OgType = entity.OgType,
        TwitterCard = entity.TwitterCard,
        TwitterSite = entity.TwitterSite,
        StructuredData = entity.StructuredData,
        Body = entity.Body,
        CustomData = entity.CustomData,
        ParentPageId = entity.ParentPageId,
        SortOrder = entity.SortOrder,
        Sections = entity.Sections.Select(s => s.ToDto()).OrderBy(s => s.SortOrder).ToList(),
        PublishedAt = entity.PublishedAt,
        ScheduledPublishAt = entity.ScheduledPublishAt,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };

    public static IEnumerable<PageDto> ToDtos(this IEnumerable<Page> entities) =>
        entities.Select(e => e.ToDto());

    // ──── PageSection ────

    public static PageSectionDto ToDto(this PageSection entity) => new()
    {
        Id = entity.Id,
        PageId = entity.PageId,
        SectionType = entity.SectionType.ToString(),
        Heading = entity.Heading,
        Subheading = entity.Subheading,
        Data = entity.Data,
        SortOrder = entity.SortOrder,
        IsVisible = entity.IsVisible,
        CssClasses = entity.CssClasses,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };

    // ──── ContentResource ────

    public static ContentResourceDto ToDto(this ContentResource entity) => new()
    {
        Id = entity.Id,
        Slug = entity.Slug,
        Title = entity.Title,
        Summary = entity.Summary,
        Body = entity.Body,
        ResourceType = entity.ResourceType.ToString(),
        Status = entity.Status.ToString(),
        Locale = entity.Locale,
        CategorySlug = entity.CategorySlug,
        Tags = entity.Tags,
        AuthorId = entity.AuthorId,
        AuthorName = entity.AuthorName,
        CoverImageUrl = entity.CoverImageUrl,
        VideoUrl = entity.VideoUrl,
        DownloadUrl = entity.DownloadUrl,
        ExternalUrl = entity.ExternalUrl,
        LinkedEntityId = entity.LinkedEntityId,
        LinkedEntityType = entity.LinkedEntityType,
        MetaTitle = entity.MetaTitle,
        MetaDescription = entity.MetaDescription,
        OgImageUrl = entity.OgImageUrl,
        StructuredData = entity.StructuredData,
        ReadingTimeMinutes = entity.ReadingTimeMinutes,
        ViewCount = entity.ViewCount,
        IsFeatured = entity.IsFeatured,
        SortOrder = entity.SortOrder,
        PublishedAt = entity.PublishedAt,
        ScheduledPublishAt = entity.ScheduledPublishAt,
        CustomData = entity.CustomData,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };

    public static IEnumerable<ContentResourceDto> ToDtos(this IEnumerable<ContentResource> entities) =>
        entities.Select(e => e.ToDto());

    // ──── MarketingLead ────

    public static MarketingLeadDto ToDto(this MarketingLead entity) => new()
    {
        Id = entity.Id,
        Source = entity.Source,
        Status = entity.Status,
        Name = entity.Name,
        Email = entity.Email,
        Company = entity.Company,
        Topic = entity.Topic,
        Plan = entity.Plan,
        Message = entity.Message,
        Locale = entity.Locale,
        PagePath = entity.PagePath,
        Referrer = entity.Referrer,
        UserAgent = entity.UserAgent,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };

    public static IEnumerable<MarketingLeadDto> ToDtos(this IEnumerable<MarketingLead> entities) =>
        entities.Select(e => e.ToDto());

    // ──── CourseProposal ────

    public static CourseProposalDto ToDto(this CourseProposal entity) => new()
    {
        Id = entity.Id,
        Slug = entity.Slug,
        Status = entity.Status,
        CourseTitle = entity.CourseTitle,
        Pitch = entity.Pitch,
        Audience = entity.Audience,
        ProposerName = entity.ProposerName,
        ProposerEmail = entity.ProposerEmail,
        Metadata = entity.Metadata,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        PublishedAt = entity.PublishedAt,
    };

    public static IEnumerable<CourseProposalDto> ToDtos(this IEnumerable<CourseProposal> entities) =>
        entities.Select(e => e.ToDto());

    // ──── OpenGraph resolution ────

    public static OpenGraphMetadataDto ToOpenGraphDto(this Page page) => new()
    {
        Slug = page.Slug,
        Title = page.OgTitle ?? page.MetaTitle ?? page.Title,
        Description = page.OgDescription ?? page.MetaDescription ?? page.Description,
        OgTitle = page.OgTitle ?? page.MetaTitle ?? page.Title,
        OgDescription = page.OgDescription ?? page.MetaDescription ?? page.Description,
        OgImageUrl = page.OgImageUrl,
        OgType = page.OgType ?? "website",
        TwitterCard = page.TwitterCard ?? "summary_large_image",
        TwitterSite = page.TwitterSite,
        CanonicalUrl = page.CanonicalUrl,
        RobotsDirective = page.RobotsDirective,
        StructuredData = page.StructuredData,
    };

    public static OpenGraphMetadataDto ToOpenGraphDto(this ContentResource resource) => new()
    {
        Slug = resource.Slug,
        Title = resource.MetaTitle ?? resource.Title,
        Description = resource.MetaDescription ?? resource.Summary,
        OgTitle = resource.MetaTitle ?? resource.Title,
        OgDescription = resource.MetaDescription ?? resource.Summary,
        OgImageUrl = resource.OgImageUrl ?? resource.CoverImageUrl,
        OgType = "article",
        TwitterCard = "summary_large_image",
        CanonicalUrl = null,
        RobotsDirective = null,
        StructuredData = resource.StructuredData,
    };
}
