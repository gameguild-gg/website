using Microsoft.EntityFrameworkCore;

namespace GameGuild.Content.Pages;

/// <summary>EF Core implementation of <see cref="IPageService"/>.</summary>
public sealed class PageService(
    IApplicationDbContext db,
    IContentPagesAuditEventSink auditSink,
    IRequestContextAccessor? requestContextAccessor = null) : IPageService
{
    private Guid? ActingUserId => requestContextAccessor?.CurrentUserId;

    // ──── Pages ────

    public async Task<Page?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Set<Page>()
            .Include(p => p.Sections.Where(s => s.DeletedAt == null))
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            .ConfigureAwait(false);

    public async Task<Page?> GetBySlugAsync(string slug, bool publishedOnly = false, CancellationToken ct = default) =>
        await db.Set<Page>()
            .Include(p => p.Sections.Where(s => s.DeletedAt == null).OrderBy(s => s.SortOrder))
            .Where(p => !publishedOnly || p.Status == PageStatus.Published)
            .FirstOrDefaultAsync(p => p.Slug == slug, ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Page>> GetPagesAsync(
        PageType? type, PageStatus? status, string? locale, Guid? parentId,
        int skip, int take, CancellationToken ct)
    {
        var query = db.Set<Page>().AsQueryable();

        if (type.HasValue) query = query.Where(p => p.PageType == type.Value);
        if (status.HasValue) query = query.Where(p => p.Status == status.Value);
        if (locale is not null) query = query.Where(p => p.Locale == locale);
        if (parentId.HasValue) query = query.Where(p => p.ParentPageId == parentId.Value);

        return await query
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Title)
            .Skip(skip).Take(take)
            .Include(p => p.Sections.Where(s => s.DeletedAt == null).OrderBy(s => s.SortOrder))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<Page> CreateAsync(CreatePageDto dto, CancellationToken ct = default)
    {
        var page = new Page
        {
            Slug = dto.Slug,
            Title = dto.Title,
            Description = dto.Description,
            PageType = dto.PageType,
            Status = PageStatus.Draft,
            Locale = dto.Locale,
            MetaTitle = dto.MetaTitle,
            MetaDescription = dto.MetaDescription,
            MetaKeywords = dto.MetaKeywords,
            CanonicalUrl = dto.CanonicalUrl,
            RobotsDirective = dto.RobotsDirective,
            OgTitle = dto.OgTitle,
            OgDescription = dto.OgDescription,
            OgImageUrl = dto.OgImageUrl,
            OgType = dto.OgType,
            TwitterCard = dto.TwitterCard,
            TwitterSite = dto.TwitterSite,
            StructuredData = dto.StructuredData,
            Body = dto.Body,
            CustomData = dto.CustomData,
            ParentPageId = dto.ParentPageId,
            SortOrder = dto.SortOrder,
        };

        db.Set<Page>().Add(page);
        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.PageCreated", "Page", page.Id.ToString(), ActingUserId, page.TenantId,
                $"Page '{page.Title}' created", Metadata: new { page.Slug, page.PageType, page.Locale }), token),
            ct).ConfigureAwait(false);
        return page;
    }

    public async Task<Page?> UpdateAsync(Guid id, UpdatePageDto dto, CancellationToken ct = default)
    {
        var page = await db.Set<Page>().FindAsync([id], ct).ConfigureAwait(false);
        if (page is null) return null;

        if (dto.Slug is not null) page.Slug = dto.Slug;
        if (dto.Title is not null) page.Title = dto.Title;
        if (dto.Description is not null) page.Description = dto.Description;
        if (dto.PageType.HasValue) page.PageType = dto.PageType.Value;
        if (dto.Status.HasValue) page.Status = dto.Status.Value;
        if (dto.Locale is not null) page.Locale = dto.Locale;
        if (dto.MetaTitle is not null) page.MetaTitle = dto.MetaTitle;
        if (dto.MetaDescription is not null) page.MetaDescription = dto.MetaDescription;
        if (dto.MetaKeywords is not null) page.MetaKeywords = dto.MetaKeywords;
        if (dto.CanonicalUrl is not null) page.CanonicalUrl = dto.CanonicalUrl;
        if (dto.RobotsDirective is not null) page.RobotsDirective = dto.RobotsDirective;
        if (dto.OgTitle is not null) page.OgTitle = dto.OgTitle;
        if (dto.OgDescription is not null) page.OgDescription = dto.OgDescription;
        if (dto.OgImageUrl is not null) page.OgImageUrl = dto.OgImageUrl;
        if (dto.OgType is not null) page.OgType = dto.OgType;
        if (dto.TwitterCard is not null) page.TwitterCard = dto.TwitterCard;
        if (dto.TwitterSite is not null) page.TwitterSite = dto.TwitterSite;
        if (dto.StructuredData is not null) page.StructuredData = dto.StructuredData;
        if (dto.Body is not null) page.Body = dto.Body;
        if (dto.CustomData is not null) page.CustomData = dto.CustomData;
        if (dto.ParentPageId is not null) page.ParentPageId = dto.ParentPageId;
        if (dto.SortOrder.HasValue) page.SortOrder = dto.SortOrder.Value;
        if (dto.ScheduledPublishAt.HasValue) page.ScheduledPublishAt = dto.ScheduledPublishAt;

        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.PageUpdated", "Page", page.Id.ToString(), ActingUserId, page.TenantId,
                $"Page '{page.Title}' updated", Metadata: new { page.Slug, page.Status }), token),
            ct).ConfigureAwait(false);
        return page;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var page = await db.Set<Page>().FindAsync([id], ct).ConfigureAwait(false);
        if (page is null) return false;

        page.SoftDelete();
        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.PageDeleted", "Page", page.Id.ToString(), ActingUserId, page.TenantId,
                $"Page '{page.Title}' deleted", Metadata: new { page.Slug }), token),
            ct).ConfigureAwait(false);
        return true;
    }

    public async Task<Page?> PublishAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var page = await db.Set<Page>().FindAsync([id], ct).ConfigureAwait(false);
        if (page is null) return null;

        page.Status = PageStatus.Published;
        page.PublishedAt ??= DateTime.UtcNow;
        page.PublishedBy = userId;
        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.PagePublished", "Page", page.Id.ToString(), userId, page.TenantId,
                $"Page '{page.Title}' published", Metadata: new { page.Slug, page.Locale }), token),
            ct).ConfigureAwait(false);
        return page;
    }

    public async Task<Page?> UnpublishAsync(Guid id, CancellationToken ct = default)
    {
        var page = await db.Set<Page>().FindAsync([id], ct).ConfigureAwait(false);
        if (page is null) return null;

        page.Status = PageStatus.Draft;
        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.PageUnpublished", "Page", page.Id.ToString(), ActingUserId, page.TenantId,
                $"Page '{page.Title}' unpublished", Metadata: new { page.Slug }), token),
            ct).ConfigureAwait(false);
        return page;
    }

    // ──── Sections ────

    public async Task<IReadOnlyList<PageSection>> GetSectionsAsync(Guid pageId, CancellationToken ct = default) =>
        await db.Set<PageSection>()
            .Where(s => s.PageId == pageId && s.DeletedAt == null)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<PageSection?> GetSectionByIdAsync(Guid sectionId, CancellationToken ct = default) =>
        await db.Set<PageSection>().FindAsync([sectionId], ct).ConfigureAwait(false);

    public async Task<PageSection> CreateSectionAsync(Guid pageId, CreatePageSectionDto dto, CancellationToken ct = default)
    {
        var section = new PageSection
        {
            PageId = pageId,
            SectionType = dto.SectionType,
            Heading = dto.Heading,
            Subheading = dto.Subheading,
            Data = dto.Data,
            SortOrder = dto.SortOrder,
            IsVisible = dto.IsVisible,
            CssClasses = dto.CssClasses,
        };

        db.Set<PageSection>().Add(section);
        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.PageSectionCreated", "PageSection", section.Id.ToString(), ActingUserId, section.TenantId,
                $"Section '{section.Heading}' added to page {pageId}", Metadata: new { PageId = pageId, section.SectionType }), token),
            ct).ConfigureAwait(false);
        return section;
    }

    public async Task<PageSection?> UpdateSectionAsync(Guid sectionId, UpdatePageSectionDto dto, CancellationToken ct = default)
    {
        var section = await db.Set<PageSection>().FindAsync([sectionId], ct).ConfigureAwait(false);
        if (section is null) return null;

        if (dto.SectionType.HasValue) section.SectionType = dto.SectionType.Value;
        if (dto.Heading is not null) section.Heading = dto.Heading;
        if (dto.Subheading is not null) section.Subheading = dto.Subheading;
        if (dto.Data is not null) section.Data = dto.Data;
        if (dto.SortOrder.HasValue) section.SortOrder = dto.SortOrder.Value;
        if (dto.IsVisible.HasValue) section.IsVisible = dto.IsVisible.Value;
        if (dto.CssClasses is not null) section.CssClasses = dto.CssClasses;

        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.PageSectionUpdated", "PageSection", section.Id.ToString(), ActingUserId, section.TenantId,
                $"Section '{section.Heading}' updated", Metadata: new { section.PageId }), token),
            ct).ConfigureAwait(false);
        return section;
    }

    public async Task<bool> DeleteSectionAsync(Guid sectionId, CancellationToken ct = default)
    {
        var section = await db.Set<PageSection>().FindAsync([sectionId], ct).ConfigureAwait(false);
        if (section is null) return false;

        section.SoftDelete();
        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.PageSectionDeleted", "PageSection", section.Id.ToString(), ActingUserId, section.TenantId,
                $"Section '{section.Heading}' deleted", Metadata: new { section.PageId }), token),
            ct).ConfigureAwait(false);
        return true;
    }

    public async Task ReorderSectionsAsync(Guid pageId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default)
    {
        var sections = await db.Set<PageSection>()
            .Where(s => s.PageId == pageId && s.DeletedAt == null)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            var section = sections.FirstOrDefault(s => s.Id == orderedIds[i]);
            if (section is not null) section.SortOrder = i;
        }

        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.PageSectionsReordered", "Page", pageId.ToString(), ActingUserId, sections.FirstOrDefault()?.TenantId,
                $"{orderedIds.Count} sections reordered on page {pageId}", Metadata: new { PageId = pageId, OrderedIds = orderedIds.Count }), token),
            ct).ConfigureAwait(false);
    }
}
