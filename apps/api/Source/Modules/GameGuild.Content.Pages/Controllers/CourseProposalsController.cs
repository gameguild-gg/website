using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Content.Pages;

/// <summary>
///     Public course-proposal pipeline (issue #105): anonymous submissions, a
///     published-only slug lookup for the /proposals/[slug] page, and an
///     authenticated CRM surface for the review lifecycle.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("content/course-proposals")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/content/course-proposals")]
public class CourseProposalsController(ICourseProposalService courseProposalService, ISender sender) : BaseApiController
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<CourseProposalDto>>> GetProposals(
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var proposals = await courseProposalService
            .ListAsync(status, search, skip, take, ct)
            .ConfigureAwait(false);

        return Ok(proposals.ToDtos());
    }

    [HttpGet("{id:guid}", Name = "GetCourseProposalById")]
    [Authorize]
    public async Task<ActionResult<CourseProposalDto>> GetProposal(Guid id, CancellationToken ct = default)
    {
        var proposal = await courseProposalService.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (proposal is null)
        {
            return NotFound();
        }

        return Ok(proposal.ToDto());
    }

    /// <summary>
    ///     Public detail lookup for /proposals/[slug]; anonymous callers receive
    ///     published proposals only, so drafts and rejections stay unlisted.
    /// </summary>
    [HttpGet("by-slug/{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<CourseProposalDto>> GetProposalBySlug(string slug, CancellationToken ct = default)
    {
        var proposal = await courseProposalService.GetPublishedBySlugAsync(slug, ct).ConfigureAwait(false);
        if (proposal is null)
        {
            return NotFound();
        }

        return Ok(proposal.ToDto());
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<CourseProposalDto>> CreateProposal(
        [FromBody] CreateCourseProposalDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var proposal = await sender.Send(new CreateCourseProposalCommand(dto), ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetProposal), new { id = proposal.Id }, proposal.ToDto());
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<CourseProposalDto>> UpdateProposal(
        Guid id,
        [FromBody] UpdateCourseProposalDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (dto.Status is not null && !CourseProposalStatuses.IsValid(dto.Status))
        {
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Please review the request and try again.",
                Status = StatusCodes.Status400BadRequest,
                Errors =
                {
                    [nameof(dto.Status)] = ["Status must be one of: submitted, in-review, approved, rejected, published, archived."],
                },
            });
        }

        var updated = await sender.Send(new UpdateCourseProposalCommand(id, dto), ct).ConfigureAwait(false);
        if (updated is null)
        {
            return NotFound();
        }

        return Ok(updated.ToDto());
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteProposal(Guid id, CancellationToken ct = default)
    {
        var deleted = await sender.Send(new DeleteCourseProposalCommand(id), ct).ConfigureAwait(false);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
