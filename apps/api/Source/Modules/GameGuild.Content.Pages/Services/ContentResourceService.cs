using Microsoft.EntityFrameworkCore;

namespace GameGuild.Content.Pages;

/// <summary>EF Core implementation of <see cref="IContentResourceService"/>.</summary>
public sealed class ContentResourceService(
    IApplicationDbContext db,
    IContentPagesAuditEventSink auditSink,
    IRequestContextAccessor? requestContextAccessor = null) : IContentResourceService
{
    private Guid? ActingUserId => requestContextAccessor?.CurrentUserId;
    public async Task<ContentResource?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Set<ContentResource>().FindAsync([id], ct).ConfigureAwait(false);

    public async Task<ContentResource?> GetBySlugAsync(string slug, bool publishedOnly = false, CancellationToken ct = default) =>
        await db.Set<ContentResource>()
            .Where(r => !publishedOnly || r.Status == ContentResourceStatus.Published)
            .FirstOrDefaultAsync(r => r.Slug == slug, ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ContentResource>> ListAsync(
        ContentResourceType? type, ContentResourceStatus? status, string? locale,
        string? categorySlug, bool? isFeatured, string? search,
        int skip, int take, CancellationToken ct)
    {
        var query = db.Set<ContentResource>().AsQueryable();

        if (type.HasValue) query = query.Where(r => r.ResourceType == type.Value);
        if (status.HasValue) query = query.Where(r => r.Status == status.Value);
        if (locale is not null) query = query.Where(r => r.Locale == locale);
        if (categorySlug is not null) query = query.Where(r => r.CategorySlug == categorySlug);
        if (isFeatured.HasValue) query = query.Where(r => r.IsFeatured == isFeatured.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLowerInvariant();
            query = query.Where(r =>
                r.Title.ToLower().Contains(term) ||
                (r.Summary != null && r.Summary.ToLower().Contains(term)) ||
                (r.Tags != null && r.Tags.ToLower().Contains(term)));
        }

        return await query
            .OrderByDescending(r => r.IsFeatured)
            .ThenBy(r => r.SortOrder)
            .ThenByDescending(r => r.PublishedAt ?? r.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<ContentResource> CreateAsync(CreateContentResourceDto dto, CancellationToken ct = default)
    {
        var resource = new ContentResource
        {
            Slug = dto.Slug,
            Title = dto.Title,
            Summary = dto.Summary,
            Body = dto.Body,
            ResourceType = dto.ResourceType,
            Status = ContentResourceStatus.Draft,
            Locale = dto.Locale,
            CategorySlug = dto.CategorySlug,
            Tags = dto.Tags,
            CoverImageUrl = dto.CoverImageUrl,
            VideoUrl = dto.VideoUrl,
            DownloadUrl = dto.DownloadUrl,
            ExternalUrl = dto.ExternalUrl,
            LinkedEntityId = dto.LinkedEntityId,
            LinkedEntityType = dto.LinkedEntityType,
            MetaTitle = dto.MetaTitle,
            MetaDescription = dto.MetaDescription,
            OgImageUrl = dto.OgImageUrl,
            StructuredData = dto.StructuredData,
            ReadingTimeMinutes = dto.ReadingTimeMinutes,
            IsFeatured = dto.IsFeatured,
            SortOrder = dto.SortOrder,
            CustomData = dto.CustomData,
        };

        db.Set<ContentResource>().Add(resource);
        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.ContentResourceCreated", "ContentResource", resource.Id.ToString(), ActingUserId, resource.TenantId,
                $"Content resource '{resource.Title}' created", Metadata: new { resource.Slug, resource.ResourceType, resource.Locale }), token),
            ct).ConfigureAwait(false);
        return resource;
    }

    public async Task<ContentResource?> UpdateAsync(Guid id, UpdateContentResourceDto dto, CancellationToken ct = default)
    {
        var resource = await db.Set<ContentResource>().FindAsync([id], ct).ConfigureAwait(false);
        if (resource is null) return null;

        if (dto.Slug is not null) resource.Slug = dto.Slug;
        if (dto.Title is not null) resource.Title = dto.Title;
        if (dto.Summary is not null) resource.Summary = dto.Summary;
        if (dto.Body is not null) resource.Body = dto.Body;
        if (dto.ResourceType.HasValue) resource.ResourceType = dto.ResourceType.Value;
        if (dto.Status.HasValue) resource.Status = dto.Status.Value;
        if (dto.Locale is not null) resource.Locale = dto.Locale;
        if (dto.CategorySlug is not null) resource.CategorySlug = dto.CategorySlug;
        if (dto.Tags is not null) resource.Tags = dto.Tags;
        if (dto.CoverImageUrl is not null) resource.CoverImageUrl = dto.CoverImageUrl;
        if (dto.VideoUrl is not null) resource.VideoUrl = dto.VideoUrl;
        if (dto.DownloadUrl is not null) resource.DownloadUrl = dto.DownloadUrl;
        if (dto.ExternalUrl is not null) resource.ExternalUrl = dto.ExternalUrl;
        if (dto.LinkedEntityId is not null) resource.LinkedEntityId = dto.LinkedEntityId;
        if (dto.LinkedEntityType is not null) resource.LinkedEntityType = dto.LinkedEntityType;
        if (dto.MetaTitle is not null) resource.MetaTitle = dto.MetaTitle;
        if (dto.MetaDescription is not null) resource.MetaDescription = dto.MetaDescription;
        if (dto.OgImageUrl is not null) resource.OgImageUrl = dto.OgImageUrl;
        if (dto.StructuredData is not null) resource.StructuredData = dto.StructuredData;
        if (dto.ReadingTimeMinutes.HasValue) resource.ReadingTimeMinutes = dto.ReadingTimeMinutes;
        if (dto.IsFeatured.HasValue) resource.IsFeatured = dto.IsFeatured.Value;
        if (dto.SortOrder.HasValue) resource.SortOrder = dto.SortOrder.Value;
        if (dto.ScheduledPublishAt.HasValue) resource.ScheduledPublishAt = dto.ScheduledPublishAt;
        if (dto.CustomData is not null) resource.CustomData = dto.CustomData;

        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.ContentResourceUpdated", "ContentResource", resource.Id.ToString(), ActingUserId, resource.TenantId,
                $"Content resource '{resource.Title}' updated", Metadata: new { resource.Slug, resource.Status }), token),
            ct).ConfigureAwait(false);
        return resource;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var resource = await db.Set<ContentResource>().FindAsync([id], ct).ConfigureAwait(false);
        if (resource is null) return false;

        resource.SoftDelete();
        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.ContentResourceDeleted", "ContentResource", resource.Id.ToString(), ActingUserId, resource.TenantId,
                $"Content resource '{resource.Title}' deleted", Metadata: new { resource.Slug }), token),
            ct).ConfigureAwait(false);
        return true;
    }

    public async Task<ContentResource?> PublishAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var resource = await db.Set<ContentResource>().FindAsync([id], ct).ConfigureAwait(false);
        if (resource is null) return null;

        resource.Status = ContentResourceStatus.Published;
        resource.PublishedAt ??= DateTime.UtcNow;
        resource.PublishedBy = userId;
        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.ContentResourcePublished", "ContentResource", resource.Id.ToString(), userId, resource.TenantId,
                $"Content resource '{resource.Title}' published", Metadata: new { resource.Slug, resource.Locale }), token),
            ct).ConfigureAwait(false);
        return resource;
    }

    public async Task IncrementViewCountAsync(Guid id, CancellationToken ct = default)
    {
        var resource = await db.Set<ContentResource>().FindAsync([id], ct).ConfigureAwait(false);
        if (resource is null) return;

        resource.ViewCount++;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
