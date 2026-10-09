using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Content.Pages;

/// <summary>REST API controller for ContentResource CRUD.</summary>
[Microsoft.AspNetCore.Http.Tags("content/pages/resources")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/content-resources")]
[Authorize]
public class ContentResourceController(
    IContentResourceService resourceService,
    ISender sender,
    IActorContextAccessor actorContextAccessor) : BaseApiController
{
    /// <summary>
    ///     Content permission keys that unlock unpublished resources. Content resources are
    ///     platform-global (not tenant-owned), so visibility is gated by publication status:
    ///     anything other than <see cref="ContentResourceStatus.Published"/> requires one of
    ///     these content permissions; the public surfaces only ever return published items.
    /// </summary>
    private static readonly string[] ContentManagePermissions = ["content:read", "content:write", "content:admin"];

    /// <summary>
    ///     Editorial permission keys (from GameGuild.Identity.Authorization's ContentPermission
    ///     catalog) required to edit existing content resources. The granular <c>content:edit</c>
    ///     key enables least-privilege editorial grants; <c>content:write</c>/<c>content:admin</c>
    ///     keep broader content managers working. System admins and <c>admin:*</c> holders bypass
    ///     via <see cref="ActorContext"/>.
    /// </summary>
    private static readonly string[] EditorialEditPermissions = ["content:edit", "content:write", "content:admin"];

    /// <summary>List content resources with filtering and search.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ContentResourceDto>>> List(
        [FromQuery] ContentResourceType? type = null,
        [FromQuery] ContentResourceStatus? status = null,
        [FromQuery] string? locale = null,
        [FromQuery] string? category = null,
        [FromQuery] bool? featured = null,
        [FromQuery] string? q = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        // The catalog listing is intentionally public for the marketing site; callers
        // without a content permission are constrained to published resources.
        if (status != ContentResourceStatus.Published && !CanViewUnpublished)
        {
            status = ContentResourceStatus.Published;
        }

        var resources = await resourceService
            .ListAsync(type, status, locale, category, featured, q, skip, take)
            .ConfigureAwait(false);
        return Ok(resources.ToDtos());
    }

    /// <summary>Get a content resource by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContentResourceDto>> GetById(Guid id)
    {
        var resource = await resourceService.GetByIdAsync(id).ConfigureAwait(false);
        if (resource is null) return NotFound();

        // Get-by-id must not leak drafts: unpublished resources require a content permission.
        if (resource.Status != ContentResourceStatus.Published && !CanViewUnpublished)
        {
            return NotFound();
        }

        return Ok(resource.ToDto());
    }

    /// <summary>Get a content resource by slug. Publicly returns published resources only.</summary>
    [HttpGet("by-slug/{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<ContentResourceDto>> GetBySlug(string slug)
    {
        // Intentionally public for SEO-friendly resource URLs; restricting the query to
        // published resources keeps drafts and in-review items unlisted.
        var publishedOnly = !CanViewUnpublished;
        var resource = await resourceService.GetBySlugAsync(slug, publishedOnly).ConfigureAwait(false);
        if (resource is null) return NotFound();

        // The service is scoped to this request and shares its DbContext. Await the
        // update so the request scope cannot dispose the PostgreSQL connection while
        // the command is still consuming protocol messages.
        await resourceService.IncrementViewCountAsync(resource.Id).ConfigureAwait(false);

        return Ok(resource.ToDto());
    }

    /// <summary>Create a new content resource.</summary>
    [HttpPost]
    public async Task<ActionResult<ContentResourceDto>> Create([FromBody] CreateContentResourceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resource = await sender.Send(new CreateContentResourceCommand(dto)).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = resource.Id }, resource.ToDto());
    }

    /// <summary>Update a content resource.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ContentResourceDto), 200)]
    [ProducesResponseType(403)]
    public async Task<ActionResult<ContentResourceDto>> Update(Guid id, [FromBody] UpdateContentResourceDto dto)
    {
        if (!CanEditContent) return Forbid();
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var resource = await sender.Send(new UpdateContentResourceCommand(id, dto)).ConfigureAwait(false);
        if (resource is null) return NotFound();
        return Ok(resource.ToDto());
    }

    /// <summary>Soft-delete a content resource.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var deleted = await sender.Send(new DeleteContentResourceCommand(id)).ConfigureAwait(false);
        if (!deleted) return NotFound();
        return NoContent();
    }

    /// <summary>Publish a content resource.</summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<ContentResourceDto>> Publish(Guid id)
    {
        var userId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!userId.HasValue) return Unauthorized();

        var resource = await sender.Send(new PublishContentResourceCommand(id, userId.Value)).ConfigureAwait(false);
        if (resource is null) return NotFound();
        return Ok(resource);
    }

    private bool CanViewUnpublished =>
        actorContextAccessor.ActorContext.HasAnyPermission(ContentManagePermissions);

    private bool CanEditContent =>
        actorContextAccessor.ActorContext.HasAnyPermission(EditorialEditPermissions);
}
