using System.ComponentModel.DataAnnotations;
using System.Text;
using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Compliance.Audit;

public sealed class CaptureAuditReplayRequest
{
    [Required]
    public DateTimeOffset? Start { get; init; }
    [Required]
    public DateTimeOffset? End { get; init; }
}

/// <summary>Documents the exact canonical JSON response; its digest is delivered in a response header.</summary>
public sealed record AuditReplaySnapshotResponse(int SchemaVersion, string? TenantId, int TotalCount,
    string StartUtc, string EndUtc, string CapturedAtUtc, IReadOnlyList<AuditReplaySnapshotEvent> Events);

/// <summary>Client-held snapshot plus the digest it was captured with (issue #169).</summary>
public sealed record ReconstructAuditReplayRequest(string? Sha256, AuditReplaySnapshotDocument? Document);

/// <summary>Reconstructed timeline of one verified snapshot (issue #169): chronological events,
/// per-user journeys and per-session summaries. The digest is repeated in a response header.</summary>
public sealed record AuditReplayReconstructionResponse(string Sha256, string? TenantId, int TotalCount,
    int FailedCount, string StartUtc, string EndUtc, string CapturedAtUtc,
    IReadOnlyList<AuditReplaySnapshotEvent> Events,
    IReadOnlyList<AuditReplayUserJourney> Users,
    IReadOnlyList<AuditReplaySessionSummary> Sessions);

/// <summary>System-administrator capture of retained audit rows in the authenticated tenant.
/// There is no caller-selectable tenant or identity and no durable replay session in this operation.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/admin/audit-logs/replay")]
[Route("api/audit/replay")]
[Tags("compliance/audit/replay")]
// Issue #386: audit snapshot capture and replay are backup/restore operations for
// compliance data - gated by the backup/restore permission tier (SystemAdmin role or a
// delegated system:backup-restore grant). Platform-scoped.
[Authorize(Policy = Policies.SystemBackupRestore)]
[EnableRateLimiting(RateLimitPolicies.Api)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class AuditReplayController(ISender sender) : ControllerBase
{
    /// <summary>Captures up to10000events and16MiB of safe canonical JSON; oversized selections fail atomically.</summary>
    /// <remarks>Null tenant context selects global-only rows. SHA256 detects content changes against
    /// an independently protected expected digest; it does not certify event provenance or synchronize source clocks.</remarks>
    [HttpPost("snapshots")]
    [RequestSizeLimit(16 * 1024)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AuditReplaySnapshotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Capture([FromBody] CaptureAuditReplayRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (request is null || !request.Start.HasValue || !request.End.HasValue)
        {
            return InvalidRange();
        }

        try
        {
            var snapshot = await sender.Send(new CaptureAuditReplayCommand(
                new AuditReplayCaptureRequest(request.Start.Value, request.End.Value)),
                cancellationToken).ConfigureAwait(false);
            Response.Headers["X-Audit-Snapshot-SHA256"] = snapshot.Sha256;
            // Return the bytes represented by the digest, without reserializing its document.
            return Content(snapshot.Json, "application/json", Encoding.UTF8);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException) { return InvalidRange(); }
        catch (AuditReplayCaptureAuditException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Required replay access audit is unavailable."
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Audit replay snapshot could not be captured."
            });
        }
    }

    private BadRequestObjectResult InvalidRange() => BadRequest(new ValidationProblemDetails(
        new Dictionary<string, string[]>
        {
            ["Range"] = ["Supply both range endpoints in chronological order and select at most10000valid retained events within16MiB."]
        }) { Status = StatusCodes.Status400BadRequest });

    /// <summary>Readable capture alias (issue #169): the same bounded, durably audited
    /// selection as the POST capture, addressed by query parameters so an investigator can
    /// link and re-fetch one frozen range. The response is never cacheable.</summary>
    [HttpGet("snapshots")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(AuditReplaySnapshotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> CaptureViaGet([FromQuery] DateTimeOffset? start, [FromQuery] DateTimeOffset? end,
        CancellationToken cancellationToken) =>
        Capture(new CaptureAuditReplayRequest { Start = start, End = end }, cancellationToken);

    /// <summary>Reconstructs the chronological incident timeline of one client-held snapshot
    /// (issue #169). The document is revalidated against every capture invariant and its
    /// canonical serialization must match the expected digest; otherwise nothing is returned.</summary>
    /// <remarks>The reconstruction adds per-user journeys and per-session summaries. A digest
    /// match detects content changes; it does not establish provenance, actor attribution or
    /// source clock synchronization.</remarks>
    [HttpPost("snapshots:reconstruct")]
    [RequestSizeLimit(16 * 1024 * 1024 + 16 * 1024)]
    [ProducesResponseType(typeof(AuditReplayReconstructionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Reconstruct([FromBody] ReconstructAuditReplayRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (request is null || request.Document is null || string.IsNullOrWhiteSpace(request.Sha256))
        {
            return ReconstructRejected("Supply the captured snapshot document and its integrity digest.");
        }

        try
        {
            var reconstruction = await sender.Send(
                new ReconstructAuditReplayCommand(request.Document, request.Sha256.Trim()),
                cancellationToken).ConfigureAwait(false);
            Response.Headers["X-Audit-Snapshot-SHA256"] = reconstruction.Sha256;
            return Ok(new AuditReplayReconstructionResponse(
                reconstruction.Sha256, reconstruction.TenantId?.ToString("D"), reconstruction.TotalCount,
                reconstruction.FailedCount, reconstruction.StartUtc, reconstruction.EndUtc,
                reconstruction.CapturedAtUtc, reconstruction.Events, reconstruction.Users, reconstruction.Sessions));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException)
        {
            return ReconstructRejected("The snapshot document failed replay validation or does not match its digest.");
        }
        catch (AuditReplayCaptureAuditException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Required replay access audit is unavailable."
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Audit replay timeline could not be reconstructed."
            });
        }
    }

    private BadRequestObjectResult ReconstructRejected(string message) => BadRequest(new ValidationProblemDetails(
        new Dictionary<string, string[]>
        {
            ["Snapshot"] = [message]
        }) { Status = StatusCodes.Status400BadRequest });
}
