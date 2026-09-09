using Asp.Versioning;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Content.Pages;

/// <summary>Public lead capture plus authenticated CRM listing.</summary>
[Microsoft.AspNetCore.Http.Tags("content/marketing-leads")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/marketing/leads")]
public class MarketingLeadController(IMarketingLeadService marketingLeadService, ISender sender) : BaseApiController
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<MarketingLeadDto>>> GetLeads(
        [FromQuery] string? source = null,
        [FromQuery] string? status = null,
        [FromQuery] string? topic = null,
        [FromQuery] string? search = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var leads = await marketingLeadService
            .ListAsync(source, status, topic, search, skip, take, ct)
            .ConfigureAwait(false);

        return Ok(leads.ToDtos());
    }

    [HttpGet("{id:guid}", Name = "GetMarketingLeadById")]
    [Authorize]
    public async Task<ActionResult<MarketingLeadDto>> GetLead(Guid id, CancellationToken ct = default)
    {
        var lead = await marketingLeadService.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (lead is null)
        {
            return NotFound();
        }

        return Ok(lead.ToDto());
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<MarketingLeadDto>> CreateLead(
        [FromBody] CreateMarketingLeadDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var errors = Validate(dto);
        if (errors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(errors)
            {
                Title = "Please review the request and try again.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        var lead = await sender.Send(new CreateMarketingLeadCommand(dto), ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetLead), new { id = lead.Id }, lead.ToDto());
    }

    private static Dictionary<string, string[]> Validate(CreateMarketingLeadDto dto)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var source = dto.Source.Trim().ToLowerInvariant();
        var topic = dto.Topic?.Trim().ToLowerInvariant();

        if (!MarketingLeadSources.IsValid(source))
        {
            errors[nameof(dto.Source)] = ["Source must be either 'contact' or 'newsletter'."];
        }

        if (topic is not null && !MarketingLeadTopics.IsValid(topic))
        {
            errors[nameof(dto.Topic)] = ["Topic must be one of: sales, support, partnership, other."];
        }

        if (source == MarketingLeadSources.Contact)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length < 2)
            {
                errors[nameof(dto.Name)] = ["Name is required for contact requests."];
            }

            if (string.IsNullOrWhiteSpace(topic))
            {
                errors[nameof(dto.Topic)] = ["Topic is required for contact requests."];
            }

            if (string.IsNullOrWhiteSpace(dto.Message) || dto.Message.Trim().Length < 10)
            {
                errors[nameof(dto.Message)] = ["Message must be at least 10 characters for contact requests."];
            }
        }

        return errors;
    }
}
