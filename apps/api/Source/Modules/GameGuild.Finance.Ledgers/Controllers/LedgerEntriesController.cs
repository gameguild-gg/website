using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GameGuild.CQRS;
using GameGuild.Finance.Ledgers.Commands;
using GameGuild.Finance.Ledgers.Enums;
using GameGuild.Finance.Ledgers.Models;
using GameGuild.Finance.Ledgers.Queries;

namespace GameGuild.Finance.Ledgers.Controllers;

/// <summary>
/// API controller for ledger entry operations.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("finance/ledgers")]
[ApiController]
[Route("api/v1/ledgers")]
[Produces("application/json")]
public class LedgerEntriesController(ISender sender) : ControllerBase
{
    // ========================================================================
    // Entry Queries
    // ========================================================================

    /// <summary>
    /// Gets an entry by ID.
    /// </summary>
    [HttpGet("entries/{entryId:guid}")]
    [ProducesResponseType(typeof(LedgerEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LedgerEntryDto>> GetEntryById(Guid entryId, CancellationToken ct)
    {
        var result = await sender.Send(new GetEntryByIdQuery(entryId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Gets entries for a ledger (direct only).
    /// </summary>
    [HttpGet("{ledgerId:guid}/entries")]
    [ProducesResponseType(typeof(IReadOnlyList<LedgerEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LedgerEntryDto>>> GetEntriesByLedger(
        Guid ledgerId,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetEntriesByLedgerQuery(ledgerId, fromDate, toDate), ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets entries for a ledger including all descendants (rollup).
    /// </summary>
    [HttpGet("{ledgerId:guid}/entries/rollup")]
    [ProducesResponseType(typeof(IReadOnlyList<LedgerEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LedgerEntryDto>>> GetEntriesWithDescendants(
        Guid ledgerId,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetEntriesIncludingDescendantsQuery(ledgerId, fromDate, toDate), ct);
        return Ok(result);
    }

    // ========================================================================
    // Entry Commands
    // ========================================================================

    /// <summary>
    /// Posts a new entry to a ledger.
    /// </summary>
    [HttpPost("{ledgerId:guid}/entries")]
    [ProducesResponseType(typeof(LedgerEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LedgerEntryDto>> PostEntry(
        Guid ledgerId,
        [FromBody] PostEntryRequest request,
        CancellationToken ct)
    {
        var command = new PostEntryCommand(
            ledgerId,
            request.Type,
            request.Category,
            request.Amount,
            request.TransactionDate,
            request.Description,
            request.CreatedByUserId,
            request.ExternalReferenceId,
            request.CounterpartyName,
            request.BudgetId,
            request.BudgetCategoryCode,
            request.Tags);

        var result = (LedgerEntryDto)await sender.Send(command, ct)!;
        return CreatedAtAction(nameof(GetEntryById), new { entryId = result.Id }, result);
    }

    /// <summary>
    /// Posts a transfer between two ledgers.
    /// </summary>
    [HttpPost("transfers")]
    [ProducesResponseType(typeof(TransferResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TransferResult>> PostTransfer(
        [FromBody] PostTransferRequest request,
        CancellationToken ct)
    {
        var command = new PostTransferCommand(
            request.SourceLedgerId,
            request.DestinationLedgerId,
            request.Amount,
            request.TransactionDate,
            request.Description,
            request.CreatedByUserId);

        var result = await sender.Send(command, ct);
        return Created("", result);
    }

    /// <summary>
    /// Updates a mutable entry.
    /// </summary>
    [HttpPatch("entries/{entryId:guid}")]
    [ProducesResponseType(typeof(LedgerEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LedgerEntryDto>> UpdateEntry(
        Guid entryId,
        [FromBody] UpdateEntryRequest request,
        CancellationToken ct)
    {
        var command = new UpdateEntryCommand(
            entryId,
            request.Description,
            request.UpdatedByUserId,
            request.Notes);

        var result = await sender.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Deletes an entry that has not been reconciled, voided, transferred, or linked to a reversal.
    /// </summary>
    [HttpDelete("entries/{entryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEntry(
        Guid entryId,
        [FromQuery] Guid deletedByUserId,
        CancellationToken ct)
    {
        await sender.Send(new DeleteEntryCommand(entryId, deletedByUserId), ct);
        return NoContent();
    }

    /// <summary>
    /// Reverses/voids an entry.
    /// </summary>
    [HttpPost("entries/{entryId:guid}/reverse")]
    [ProducesResponseType(typeof(LedgerEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LedgerEntryDto>> ReverseEntry(
        Guid entryId,
        [FromBody] ReverseEntryRequest request,
        CancellationToken ct)
    {
        var command = new ReverseEntryCommand(
            entryId,
            request.Reason,
            request.ReversedByUserId);

        var result = await sender.Send(command, ct);
        return Ok(result);
    }

    // ========================================================================
    // Balance Queries
    // ========================================================================

    /// <summary>
    /// Gets balance for a ledger (direct entries only).
    /// </summary>
    [HttpGet("{ledgerId:guid}/balance")]
    [ProducesResponseType(typeof(LedgerBalance), StatusCodes.Status200OK)]
    public async Task<ActionResult<LedgerBalance>> GetBalance(
        Guid ledgerId,
        [FromQuery] DateOnly? asOfDate = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetLedgerBalanceQuery(ledgerId, asOfDate), ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets consolidated balance including all descendants.
    /// </summary>
    [HttpGet("{ledgerId:guid}/balance/consolidated")]
    [ProducesResponseType(typeof(LedgerBalance), StatusCodes.Status200OK)]
    public async Task<ActionResult<LedgerBalance>> GetConsolidatedBalance(
        Guid ledgerId,
        [FromQuery] DateOnly? asOfDate = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetConsolidatedBalanceQuery(ledgerId, asOfDate), ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets hierarchical rollup for a subtree.
    /// </summary>
    [HttpGet("{ledgerId:guid}/rollup")]
    [ProducesResponseType(typeof(LedgerRollupNode), StatusCodes.Status200OK)]
    public async Task<ActionResult<LedgerRollupNode>> GetHierarchicalRollup(
        Guid ledgerId,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetHierarchicalRollupQuery(ledgerId, fromDate, toDate), ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets category-based rollup for a ledger.
    /// </summary>
    [HttpGet("{ledgerId:guid}/rollup/categories")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryRollup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryRollup>>> GetCategoryRollup(
        Guid ledgerId,
        [FromQuery] bool includeDescendants = false,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetCategoryRollupQuery(ledgerId, includeDescendants, fromDate, toDate),
            ct);

        return Ok(result);
    }

    /// <summary>
    /// Gets period-based rollup for a ledger.
    /// </summary>
    [HttpGet("{ledgerId:guid}/rollup/periods")]
    [ProducesResponseType(typeof(IReadOnlyList<PeriodRollup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PeriodRollup>>> GetPeriodRollup(
        Guid ledgerId,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        [FromQuery] PeriodGranularity granularity = PeriodGranularity.Month,
        [FromQuery] bool includeDescendants = false,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetPeriodRollupQuery(ledgerId, fromDate, toDate, granularity, includeDescendants),
            ct);

        return Ok(result);
    }
}

// ============================================================================
// Request DTOs
// ============================================================================

public record PostEntryRequest(
    EntryType Type,
    EntryCategory Category,
    decimal Amount,
    DateOnly TransactionDate,
    string Description,
    Guid CreatedByUserId,
    string? ExternalReferenceId = null,
    string? CounterpartyName = null,
    Guid? BudgetId = null,
    string? BudgetCategoryCode = null,
    IEnumerable<string>? Tags = null);

public record PostTransferRequest(
    Guid SourceLedgerId,
    Guid DestinationLedgerId,
    decimal Amount,
    DateOnly TransactionDate,
    string Description,
    Guid CreatedByUserId);

public record UpdateEntryRequest(
    string Description,
    Guid UpdatedByUserId,
    string? Notes = null);

public record ReverseEntryRequest(
    string Reason,
    Guid ReversedByUserId);
