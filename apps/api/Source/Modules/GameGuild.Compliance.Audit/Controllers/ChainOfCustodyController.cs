using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Chain-of-custody evidence tracking for forensics (issue #177): evidence preservation with
///     cryptographic integrity verification, append-only custody documentation with handling logs
///     and custodian records, isolation-enforced access, challenge-verified digital transfers with
///     ECDSA proof of handoff, legal hold management with automated preservation and stakeholder
///     notification, and court-admissible evidence packaging with expert certification.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/audit/chain-of-custody")]
[Route("api/audit/chain-of-custody")]
[Tags("compliance/audit/chain-of-custody")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Api)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class ChainOfCustodyController(
    IChainOfCustodyService custody,
    IEvidenceCustodianService custodians,
    IEvidenceTransferService transfers,
    ILegalHoldService legalHolds,
    ICourtAdmissiblePackagingService packaging,
    ISender sender) : ControllerBase
{
    /// <summary>Lists preserved evidence for the tenant (custody-gated access; every read is logged).</summary>
    [HttpGet("evidence")]
    [ProducesResponseType(typeof(IReadOnlyList<CustodyEvidenceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustodyEvidenceResponse>>> ListEvidence(
        [FromQuery] int skip, [FromQuery] int take, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await custody.ListAsync(skip, take is < 1 or > 200 ? 50 : take, cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Returns one evidence item with its full custody timeline (access logged; broken seals require forensic scope).</summary>
    [HttpGet("evidence/{evidenceId:guid}")]
    [ProducesResponseType(typeof(CustodyEvidenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustodyEvidenceResponse>> GetEvidence(
        Guid evidenceId, [FromQuery(Name = "purpose")] string? purpose, CancellationToken cancellationToken)
    {
        try
        {
            var response = await custody.GetAsync(evidenceId, purpose, cancellationToken).ConfigureAwait(false);
            return response is null ? NotFound() : Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Preserves evidence under chain of custody with content hashing (audit logs, security events, value snapshots, sealed packages, or raw content).</summary>
    [HttpPost("evidence")]
    [ProducesResponseType(typeof(CustodyEvidenceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustodyEvidenceResponse>> PreserveEvidence(
        [FromBody] PreserveEvidenceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var evidence = await sender.Send(new PreserveEvidenceCommand(request), cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetEvidence), new { evidenceId = evidence.Id }, evidence);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotValidationException exception)
        {
            return ValidationProblem(exception);
        }
    }

    /// <summary>Deliberately breaks the evidence seal for forensic inspection (forensic scope required; custody event recorded).</summary>
    [HttpPost("evidence/{evidenceId:guid}:break-seal")]
    [ProducesResponseType(typeof(CustodyEvidenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustodyEvidenceResponse>> BreakSeal(
        Guid evidenceId, [FromBody] BreakSealRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sender.Send(new BreakEvidenceSealCommand(evidenceId, request.Purpose), cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotValidationException exception)
        {
            return ValidationProblem(exception);
        }
    }

    /// <summary>Releases evidence from custody (release scope required; blocked while a legal hold is active).</summary>
    [HttpPost("evidence/{evidenceId:guid}:release")]
    [ProducesResponseType(typeof(CustodyEvidenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustodyEvidenceResponse>> ReleaseEvidence(
        Guid evidenceId, [FromBody] ReleaseEvidenceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sender.Send(new ReleaseEvidenceCommand(evidenceId, request.Reason), cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotValidationException exception)
        {
            return ValidationProblem(exception);
        }
    }

    /// <summary>Verifies the preserved content against its recorded SHA-256 hash.</summary>
    [HttpGet("evidence/{evidenceId:guid}/integrity")]
    [ProducesResponseType(typeof(EvidenceIntegrityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EvidenceIntegrityResponse>> VerifyIntegrity(Guid evidenceId, CancellationToken cancellationToken)
    {
        try
        {
            var verified = await custody.VerifyIntegrityAsync(evidenceId, cancellationToken).ConfigureAwait(false);
            return verified ? Ok(new EvidenceIntegrityResponse(true)) : NotFound(new EvidenceIntegrityResponse(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Lists registered custodians with their authorization scopes.</summary>
    [HttpGet("custodians")]
    [ProducesResponseType(typeof(IReadOnlyList<EvidenceCustodianResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EvidenceCustodianResponse>>> ListCustodians(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await custodians.ListAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Registers a custodian with authorization scopes. Fail-closed: only registered active custodians may act on evidence.</summary>
    [HttpPost("custodians")]
    [ProducesResponseType(typeof(EvidenceCustodianResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EvidenceCustodianResponse>> RegisterCustodian(
        [FromBody] RegisterCustodianRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var custodian = await sender.Send(new RegisterCustodianCommand(request), cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(ListCustodians), new { custodianId = custodian.Id }, custodian);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotValidationException exception)
        {
            return ValidationProblem(exception);
        }
    }

    /// <summary>Revokes a custodian's authorization (revocation is effective immediately and logged).</summary>
    [HttpPost("custodians/{custodianId:guid}:revoke")]
    [ProducesResponseType(typeof(EvidenceCustodianResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EvidenceCustodianResponse>> RevokeCustodian(
        Guid custodianId, [FromBody] RevokeCustodianRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sender.Send(new RevokeCustodianCommand(custodianId, request.Reason), cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Lists evidence transfers with proof-verification status.</summary>
    [HttpGet("transfers")]
    [ProducesResponseType(typeof(IReadOnlyList<EvidenceTransferResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EvidenceTransferResponse>>> ListTransfers(
        [FromQuery(Name = "evidenceId")] Guid? evidenceId,
        [FromQuery] int skip,
        [FromQuery] int take,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await transfers.ListAsync(evidenceId, skip, take is < 1 or > 200 ? 50 : take, cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Initiates a custody transfer: challenge nonce + ECDSA-signed proof binding evidence hash and both custodians.</summary>
    [HttpPost("transfers")]
    [ProducesResponseType(typeof(EvidenceTransferResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EvidenceTransferResponse>> InitiateTransfer(
        [FromBody] InitiateTransferRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var transfer = await sender.Send(new InitiateEvidenceTransferCommand(request), cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(ListTransfers), new { evidenceId = transfer.EvidenceId }, transfer);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotValidationException exception)
        {
            return ValidationProblem(exception);
        }
    }

    /// <summary>Acknowledges a transfer as the receiving custodian: verifies the proof, then counter-signs and takes custody.</summary>
    [HttpPost("transfers/{transferId:guid}:acknowledge")]
    [ProducesResponseType(typeof(EvidenceTransferResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EvidenceTransferResponse>> AcknowledgeTransfer(
        Guid transferId, [FromBody] AcknowledgeTransferRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sender.Send(new AcknowledgeEvidenceTransferCommand(transferId, request ?? new AcknowledgeTransferRequest()), cancellationToken)
                .ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotValidationException exception)
        {
            return ValidationProblem(exception);
        }
    }

    /// <summary>Lists legal holds with their preserved evidence and notification status.</summary>
    [HttpGet("legal-holds")]
    [ProducesResponseType(typeof(IReadOnlyList<LegalHoldResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LegalHoldResponse>>> ListLegalHolds(
        [FromQuery] int skip, [FromQuery] int take, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await legalHolds.ListAsync(skip, take is < 1 or > 200 ? 50 : take, cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Returns one legal hold with preserved evidence ids and notifications.</summary>
    [HttpGet("legal-holds/{holdId:guid}")]
    [ProducesResponseType(typeof(LegalHoldResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LegalHoldResponse>> GetLegalHold(Guid holdId, CancellationToken cancellationToken)
    {
        try
        {
            var hold = await legalHolds.GetAsync(holdId, cancellationToken).ConfigureAwait(false);
            return hold is null ? NotFound() : Ok(hold);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Creates a legal hold: immediately preserves every in-scope target under custody and records stakeholder notifications.</summary>
    [HttpPost("legal-holds")]
    [ProducesResponseType(typeof(LegalHoldResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LegalHoldResponse>> CreateLegalHold(
        [FromBody] CreateLegalHoldRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var hold = await sender.Send(new CreateLegalHoldCommand(request), cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetLegalHold), new { holdId = hold.Id }, hold);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotValidationException exception)
        {
            return ValidationProblem(exception);
        }
    }

    /// <summary>Releases a legal hold (preserved evidence and custody chains are retained as investigation records).</summary>
    [HttpPost("legal-holds/{holdId:guid}:release")]
    [ProducesResponseType(typeof(LegalHoldResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LegalHoldResponse>> ReleaseLegalHold(
        Guid holdId, [FromBody] ReleaseHoldRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sender.Send(new ReleaseLegalHoldCommand(holdId, request.Reason), cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotValidationException exception)
        {
            return ValidationProblem(exception);
        }
    }

    /// <summary>Dispatches pending hold notifications (automated stakeholder notification workflow).</summary>
    [HttpPost("legal-holds/{holdId:guid}:notify")]
    [ProducesResponseType(typeof(LegalHoldResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LegalHoldResponse>> DispatchNotifications(Guid holdId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sender.Send(new DispatchLegalHoldNotificationsCommand(holdId), cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotValidationException exception)
        {
            return ValidationProblem(exception);
        }
    }

    /// <summary>Builds a court-admissible custody package: full documentation, verification summary, and signed expert certification.</summary>
    [HttpPost("packages")]
    [ProducesResponseType(typeof(CourtAdmissiblePackageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CourtAdmissiblePackageResponse>> BuildPackage(
        [FromBody] BuildCustodyPackageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await sender.Send(new BuildCustodyPackageCommand(request), cancellationToken).ConfigureAwait(false));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValueSnapshotValidationException exception)
        {
            return ValidationProblem(exception);
        }
    }

    private ActionResult ValidationProblem(ValueSnapshotValidationException exception)
    {
        var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
        foreach (var (key, messages) in exception.Errors)
        {
            modelState.AddModelError(key, string.Join(" ", messages));
        }

        return BadRequest(new ValidationProblemDetails(modelState) { Status = StatusCodes.Status400BadRequest });
    }
}

public sealed record BreakSealRequest
{
    public required string Purpose { get; init; }
}

public sealed record ReleaseEvidenceRequest
{
    public required string Reason { get; init; }
}

public sealed record RevokeCustodianRequest
{
    public required string Reason { get; init; }
}

public sealed record ReleaseHoldRequest
{
    public required string Reason { get; init; }
}

public sealed record EvidenceIntegrityResponse(bool Verified);
