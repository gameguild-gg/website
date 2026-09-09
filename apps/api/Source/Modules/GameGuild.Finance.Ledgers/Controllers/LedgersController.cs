using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GameGuild.CQRS;
using GameGuild.Finance.Ledgers.Commands;
using GameGuild.Finance.Ledgers.Enums;
using GameGuild.Finance.Ledgers.Models;
using GameGuild.Finance.Ledgers.Queries;

namespace GameGuild.Finance.Ledgers.Controllers;

/// <summary>
/// API controller for ledger management operations.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("finance/ledgers")]
[ApiController]
[Route("api/v1/ledgers")]
[Produces("application/json")]
public class LedgersController(ISender sender) : ControllerBase
{
    // ========================================================================
    // Ledger CRUD
    // ========================================================================

    /// <summary>
    /// Gets a ledger by ID.
    /// </summary>
    [HttpGet("{ledgerId:guid}")]
    [ProducesResponseType(typeof(LedgerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LedgerDto>> GetById(Guid ledgerId, CancellationToken ct)
    {
        var result = await sender.Send(new GetLedgerByIdQuery(ledgerId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Gets a ledger by code within tenant.
    /// </summary>
    [HttpGet("by-code/{tenantId:guid}/{code}")]
    [ProducesResponseType(typeof(LedgerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LedgerDto>> GetByCode(Guid tenantId, string code, CancellationToken ct)
    {
        var result = await sender.Send(new GetLedgerByCodeQuery(tenantId, code), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Gets all ledgers for a tenant.
    /// </summary>
    [HttpGet("tenant/{tenantId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<LedgerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LedgerDto>>> GetByTenant(
        Guid tenantId,
        [FromQuery] LedgerStatus? status = null,
        [FromQuery] LedgerType? type = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetLedgersByTenantQuery(tenantId, status, type), ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets root ledgers for a tenant.
    /// </summary>
    [HttpGet("tenant/{tenantId:guid}/roots")]
    [ProducesResponseType(typeof(IReadOnlyList<LedgerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LedgerDto>>> GetRootLedgers(Guid tenantId, CancellationToken ct)
    {
        var result = await sender.Send(new GetRootLedgersQuery(tenantId), ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets direct children of a ledger.
    /// </summary>
    [HttpGet("{ledgerId:guid}/children")]
    [ProducesResponseType(typeof(IReadOnlyList<LedgerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LedgerDto>>> GetChildren(Guid ledgerId, CancellationToken ct)
    {
        var result = await sender.Send(new GetLedgerChildrenQuery(ledgerId), ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets the hierarchy tree starting from a ledger.
    /// </summary>
    [HttpGet("{ledgerId:guid}/hierarchy")]
    [ProducesResponseType(typeof(LedgerTreeNode), StatusCodes.Status200OK)]
    public async Task<ActionResult<LedgerTreeNode>> GetHierarchy(
        Guid ledgerId,
        [FromQuery] int? maxDepth = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetLedgerHierarchyQuery(ledgerId, maxDepth), ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets the ancestor path from root to ledger.
    /// </summary>
    [HttpGet("{ledgerId:guid}/ancestors")]
    [ProducesResponseType(typeof(IReadOnlyList<LedgerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LedgerDto>>> GetAncestors(Guid ledgerId, CancellationToken ct)
    {
        var result = await sender.Send(new GetLedgerAncestorsQuery(ledgerId), ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets descendants of a ledger.
    /// </summary>
    [HttpGet("{ledgerId:guid}/descendants")]
    [ProducesResponseType(typeof(IReadOnlyList<LedgerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LedgerDto>>> GetDescendants(
        Guid ledgerId,
        [FromQuery] int? maxDepth = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetLedgerDescendantsQuery(ledgerId, maxDepth), ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets virtual ledgers for a tenant.
    /// </summary>
    [HttpGet("tenant/{tenantId:guid}/virtual")]
    [ProducesResponseType(typeof(IReadOnlyList<LedgerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LedgerDto>>> GetVirtualLedgers(Guid tenantId, CancellationToken ct)
    {
        var result = await sender.Send(new GetVirtualLedgersQuery(tenantId), ct);
        return Ok(result);
    }

    // ========================================================================
    // Create Operations
    // ========================================================================

    /// <summary>
    /// Creates a new root ledger.
    /// </summary>
    [HttpPost("root")]
    [ProducesResponseType(typeof(LedgerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LedgerDto>> CreateRootLedger(
        [FromBody] CreateRootLedgerRequest request,
        CancellationToken ct)
    {
        var command = new CreateRootLedgerCommand(
            request.TenantId,
            request.Code,
            request.Name,
            request.CurrencyCode,
            request.CreatedByUserId,
            request.Description,
            request.Tags);

        var result = (LedgerDto)await sender.Send(command, ct)!;
        return CreatedAtAction(nameof(GetById), new { ledgerId = result.Id }, result);
    }

    /// <summary>
    /// Creates a new child ledger under a parent.
    /// </summary>
    [HttpPost("{parentLedgerId:guid}/children")]
    [ProducesResponseType(typeof(LedgerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LedgerDto>> CreateChildLedger(
        Guid parentLedgerId,
        [FromBody] CreateChildLedgerRequest request,
        CancellationToken ct)
    {
        var command = new CreateChildLedgerCommand(
            parentLedgerId,
            request.Type,
            request.Code,
            request.Name,
            request.CreatedByUserId,
            request.Description,
            request.CurrencyCode,
            request.IsShared,
            request.BudgetLimit,
            request.ProjectStartDate,
            request.ProjectEndDate,
            request.Tags);

        var result = (LedgerDto)await sender.Send(command, ct)!;
        return CreatedAtAction(nameof(GetById), new { ledgerId = result.Id }, result);
    }
}

// ============================================================================
// Request DTOs
// ============================================================================

public record CreateRootLedgerRequest(
    Guid TenantId,
    string Code,
    string Name,
    string CurrencyCode,
    Guid CreatedByUserId,
    string? Description = null,
    IEnumerable<string>? Tags = null);

public record CreateChildLedgerRequest(
    LedgerType Type,
    string Code,
    string Name,
    Guid CreatedByUserId,
    string? Description = null,
    string? CurrencyCode = null,
    bool IsShared = false,
    decimal? BudgetLimit = null,
    DateOnly? ProjectStartDate = null,
    DateOnly? ProjectEndDate = null,
    IEnumerable<string>? Tags = null);
