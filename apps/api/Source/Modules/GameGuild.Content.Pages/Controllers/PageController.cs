using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Content.Pages;

/// <summary>REST API controller for Page CRUD.</summary>
[Microsoft.AspNetCore.Http.Tags("content/pages")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/pages")]
[Authorize]
public class PageController(IPageService pageService, ISender sender, IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    ///     Content permission keys that unlock unpublished pages and drafts. Pages are
    ///     platform-global (not tenant-owned), so visibility is gated by publication status:
    ///     anything other than <see cref="PageStatus.Published"/> requires one of these
    ///     content permissions; the anonymous slug surface only ever returns published pages.
    /// </summary>
    private static readonly string[] ContentManagePermissions = ["content:read", "content:write", "content:admin"];

    /// <summary>
    ///     Editorial permission keys (from GameGuild.Identity.Authorization's ContentPermission
    ///     catalog) required to edit existing pages and sections. The granular <c>content:edit</c>
    ///     key enables least-privilege editorial grants; <c>content:write</c>/<c>content:admin</c>
    ///     keep broader content managers working. System admins and <c>admin:*</c> holders bypass
    ///     via <see cref="ActorContext"/>.
    /// </summary>
    private static readonly string[] EditorialEditPermissions = ["content:edit", "content:write", "content:admin"];

    /// <summary>List pages with optional filtering.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PageDto>>> GetPages(
        [FromQuery] PageType? type = null,
        [FromQuery] PageStatus? status = null,
        [FromQuery] string? locale = null,
        [FromQuery] Guid? parentId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        // Non-published listings are restricted to content managers; everyone else
        // is constrained to published pages regardless of the requested filter.
        if (status != PageStatus.Published && !CanViewUnpublished)
        {
            status = PageStatus.Published;
        }

        var pages = await pageService.GetPagesAsync(type, status, locale, parentId, skip, take).ConfigureAwait(false);
        return Ok(pages.ToDtos());
    }

    /// <summary>Get a page by ID (including sections).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PageDto>> GetPage(Guid id)
    {
        var page = await pageService.GetByIdAsync(id).ConfigureAwait(false);
        if (page is null) return NotFound();

        // Get-by-id must not leak drafts: unpublished pages require a content permission.
        if (page.Status != PageStatus.Published && !CanViewUnpublished)
        {
            return NotFound();
        }

        return Ok(page.ToDto());
    }

    /// <summary>Get a page by slug (including sections). Publicly returns published pages only.</summary>
    [HttpGet("by-slug/{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<PageDto>> GetPageBySlug(string slug)
    {
        // The anonymous slug surface is intentionally public for the marketing site and
        // SEO crawlers; restricting the query to published pages keeps drafts unlisted.
        var publishedOnly = !CanViewUnpublished;
        var page = await pageService.GetBySlugAsync(slug, publishedOnly).ConfigureAwait(false);
        if (page is null) return NotFound();
        return Ok(page.ToDto());
    }

    private bool CanViewUnpublished =>
        actorContextAccessor.ActorContext.HasAnyPermission(ContentManagePermissions);

    private bool CanEditContent =>
        actorContextAccessor.ActorContext.HasAnyPermission(EditorialEditPermissions);

    /// <summary>
    ///     Public sitemap feed of published pages — slug + last-modified — for
    ///     SEO crawlers and the marketing site's <c>sitemap.xml</c>.
    /// </summary>
    [HttpGet("sitemap")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<SitemapEntryDto>>> GetSitemap(
        [FromQuery] string? locale = null,
        CancellationToken ct = default)
    {
        var pages = await pageService
            .GetPagesAsync(type: null, status: PageStatus.Published, locale: locale, parentId: null, skip: 0, take: 5000, ct)
            .ConfigureAwait(false);

        var entries = pages.Select(p => new SitemapEntryDto(
            Slug: p.Slug,
            UpdatedAt: p.UpdatedAt,
            Locale: p.Locale));

        return Ok(entries);
    }

    /// <summary>Create a new page.</summary>
    [HttpPost]
    public async Task<ActionResult<PageDto>> CreatePage([FromBody] CreatePageDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var page = await sender.Send(new CreatePageCommand(dto)).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetPage), new { id = page.Id }, page.ToDto());
    }

    /// <summary>Update an existing page.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PageDto), 200)]
    [ProducesResponseType(403)]
    public async Task<ActionResult<PageDto>> UpdatePage(Guid id, [FromBody] UpdatePageDto dto)
    {
        if (!CanEditContent) return Forbid();
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var page = await sender.Send(new UpdatePageCommand(id, dto)).ConfigureAwait(false);
        if (page is null) return NotFound();
        return Ok(page.ToDto());
    }

    /// <summary>Soft-delete a page.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeletePage(Guid id)
    {
        var deleted = await sender.Send(new DeletePageCommand(id)).ConfigureAwait(false);
        if (!deleted) return NotFound();
        return NoContent();
    }

    /// <summary>Publish a page.</summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<PageDto>> Publish(Guid id)
    {
        var userId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!userId.HasValue) return Unauthorized();

        var page = await sender.Send(new PublishPageCommand(id, userId.Value)).ConfigureAwait(false);
        if (page is null) return NotFound();
        return Ok(page);
    }

    /// <summary>Unpublish a page (back to Draft).</summary>
    [HttpPost("{id:guid}/unpublish")]
    public async Task<ActionResult<PageDto>> Unpublish(Guid id)
    {
        var page = await sender.Send(new UnpublishPageCommand(id)).ConfigureAwait(false);
        if (page is null) return NotFound();
        return Ok(page.ToDto());
    }

    // ──── Sections ────

    /// <summary>List sections for a page.</summary>
    [HttpGet("{pageId:guid}/sections")]
    public async Task<ActionResult<IEnumerable<PageSectionDto>>> GetSections(Guid pageId)
    {
        var sections = await pageService.GetSectionsAsync(pageId).ConfigureAwait(false);
        return Ok(sections.Select(s => s.ToDto()));
    }

    /// <summary>Create a section within a page.</summary>
    [HttpPost("{pageId:guid}/sections")]
    public async Task<ActionResult<PageSectionDto>> CreateSection(Guid pageId, [FromBody] CreatePageSectionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var section = await sender.Send(new CreatePageSectionCommand(pageId, dto)).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetSection), new { pageId, sectionId = section.Id }, section.ToDto());
    }

    /// <summary>Get a specific section.</summary>
    [HttpGet("{pageId:guid}/sections/{sectionId:guid}")]
    public async Task<ActionResult<PageSectionDto>> GetSection(Guid pageId, Guid sectionId)
    {
        var section = await pageService.GetSectionByIdAsync(sectionId).ConfigureAwait(false);
        if (section is null || section.PageId != pageId) return NotFound();
        return Ok(section.ToDto());
    }

    /// <summary>Update a section.</summary>
    [HttpPut("{pageId:guid}/sections/{sectionId:guid}")]
    [ProducesResponseType(typeof(PageSectionDto), 200)]
    [ProducesResponseType(403)]
    public async Task<ActionResult<PageSectionDto>> UpdateSection(Guid pageId, Guid sectionId, [FromBody] UpdatePageSectionDto dto)
    {
        if (!CanEditContent) return Forbid();
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var section = await sender.Send(new UpdatePageSectionCommand(pageId, sectionId, dto)).ConfigureAwait(false);
        if (section is null) return NotFound();
        return Ok(section.ToDto());
    }

    /// <summary>Delete a section.</summary>
    [HttpDelete("{pageId:guid}/sections/{sectionId:guid}")]
    public async Task<ActionResult> DeleteSection(Guid pageId, Guid sectionId)
    {
        if (!await sender.Send(new DeletePageSectionCommand(pageId, sectionId)).ConfigureAwait(false)) return NotFound();
        return NoContent();
    }

    /// <summary>Reorder sections within a page.</summary>
    [HttpPost("{pageId:guid}/sections/reorder")]
    public async Task<ActionResult> ReorderSections(Guid pageId, [FromBody] List<Guid> orderedIds)
    {
        await sender.Send(new ReorderPageSectionsCommand(pageId, orderedIds)).ConfigureAwait(false);
        return NoContent();
    }
}
