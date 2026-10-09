using Asp.Versioning;
using GameGuild;
using GameGuild.Commerce.Products;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace GameGuild.Learning.Courses;

[ApiVersion("1.0")]
[Route("v{version:apiVersion}/courses/{courseId:guid}/support/tickets")]
[Authorize]
public sealed class CourseSupportTicketsController(
    ISender sender,
    IActorContextAccessor actorContextAccessor,
    IApplicationDbContext db) : BaseApiController
{
    [HttpPost]
    [RequireCourseCapability(CourseCapability.Edit, "courseId")]
    public async Task<ActionResult<SupportTicketDto>> Create(
        Guid courseId,
        [FromBody] CreateCourseSupportTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId, out var actorName, out var actorEmail)) return Unauthorized();
        var tenantId = actorContextAccessor.ActorContext.TenantId;
        var courseTitle = await db.Set<Program>().AsNoTracking()
            .Where(program => program.Id == courseId && program.DeletedAt == null)
            .Select(program => program.Title)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId is not Guid resolvedTenantId || courseTitle is null) return NotFound();

        var result = await sender.Send(new CreateSupportTicketCommand(
            resolvedTenantId,
            courseId,
            courseTitle,
            actorId,
            actorName,
            actorEmail,
            request.Subject.Trim(),
            request.Body.Trim(),
            request.Priority,
            request.Category), cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(GetById), new { courseId, ticketId = result.Id, version = "1.0" }, result);
    }

    [HttpGet]
    [RequireCourseCapability(CourseCapability.Edit, "courseId")]
    public async Task<ActionResult<PagedResult<SupportTicketDto>>> List(
        Guid courseId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new GetSupportTicketsQuery(Skip: skip, Take: take, CustomerId: courseId),
            cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{ticketId:guid}")]
    [RequireCourseCapability(CourseCapability.Edit, "courseId")]
    public async Task<ActionResult<SupportTicketDto>> GetById(
        Guid courseId,
        Guid ticketId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await GetOwnedTicketAsync(courseId, ticketId, cancellationToken).ConfigureAwait(false);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPost("{ticketId:guid}/messages")]
    [RequireCourseCapability(CourseCapability.Edit, "courseId")]
    public async Task<ActionResult<SupportTicketDto>> AddMessage(
        Guid courseId,
        Guid ticketId,
        [FromBody] CourseSupportTicketMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var ticket = await GetOwnedTicketAsync(courseId, ticketId, cancellationToken).ConfigureAwait(false);
        if (ticket?.TenantId is not Guid tenantId) return NotFound();
        if (!TryGetActor(out var actorId, out var actorName, out var actorEmail)) return Unauthorized();

        var result = await sender.Send(new AddSupportTicketMessageCommand(
            ticketId,
            tenantId,
            actorId,
            actorName,
            actorEmail,
            SupportTicketMessageAuthorType.Agent,
            request.Message.Trim(),
            request.IsInternal), cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    [HttpPost("{ticketId:guid}:resolve")]
    [RequireCourseCapability(CourseCapability.Edit, "courseId")]
    public async Task<ActionResult<SupportTicketDto>> Resolve(
        Guid courseId,
        Guid ticketId,
        [FromBody] ResolveCourseSupportTicketRequest request,
        CancellationToken cancellationToken = default)
    {
        var ticket = await GetOwnedTicketAsync(courseId, ticketId, cancellationToken).ConfigureAwait(false);
        if (ticket?.TenantId is not Guid tenantId) return NotFound();
        if (!TryGetActor(out var actorId, out var actorName, out _)) return Unauthorized();

        var result = await sender.Send(new ResolveSupportTicketCommand(
            ticketId,
            tenantId,
            actorId,
            actorName,
            request.Summary.Trim()), cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    private async Task<SupportTicketDto?> GetOwnedTicketAsync(Guid courseId, Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await sender.Send(new GetSupportTicketByIdQuery(ticketId), cancellationToken).ConfigureAwait(false);
        return ticket?.CustomerId == courseId ? ticket : null;
    }

    private bool TryGetActor(out Guid actorId, out string actorName, out string? actorEmail)
    {
        actorEmail = HttpContext.User.FindFirstValue(ClaimTypes.Email);
        actorName = HttpContext.User.Identity?.Name
            ?? actorEmail
            ?? actorContextAccessor.ActorContext.SubjectId
            ?? "Course staff";
        return Guid.TryParse(actorContextAccessor.ActorContext.SubjectId, out actorId);
    }
}

public sealed record CreateCourseSupportTicketRequest(
    [Required, MinLength(3), MaxLength(200)] string Subject,
    [Required, MinLength(3), MaxLength(8000)] string Body,
    SupportTicketPriority Priority = SupportTicketPriority.Normal,
    string? Category = null);

public sealed record CourseSupportTicketMessageRequest(
    [Required, MinLength(2), MaxLength(4000)] string Message,
    bool IsInternal = false);

public sealed record ResolveCourseSupportTicketRequest(
    [Required, MinLength(3), MaxLength(1000)] string Summary);
