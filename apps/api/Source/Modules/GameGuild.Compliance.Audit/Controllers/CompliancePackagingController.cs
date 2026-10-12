using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Compliance.Audit;

/// <summary>Tenant administrator evidence uploads, review, collection and signed package downloads.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/audit/compliance-packaging")]
[Route("api/audit/compliance-packaging")]
[Tags("compliance/audit/compliance-packaging")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Api)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public sealed class CompliancePackagingController(IComplianceEvidencePackagingService service, ISender sender) : ControllerBase
{
    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyList<ComplianceFrameworkTemplate>), StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<ComplianceFrameworkTemplate>>> Templates(CancellationToken cancellationToken) =>
        Execute(async () => await service.GetTemplatesAsync(cancellationToken).ConfigureAwait(false));

    [HttpPost("documents")]
    [RequestSizeLimit(2097152)]
    [ProducesResponseType(typeof(ComplianceDocumentResponse), StatusCodes.Status200OK)]
    public Task<ActionResult<ComplianceDocumentResponse>> Upload(
        [FromBody] UploadComplianceDocumentRequest request, CancellationToken cancellationToken) =>
        Execute(async () => await sender.Send(new UploadComplianceDocumentCommand(request), cancellationToken).ConfigureAwait(false));

    [HttpGet("documents")]
    [ProducesResponseType(typeof(IReadOnlyList<ComplianceDocumentResponse>), StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<ComplianceDocumentResponse>>> Documents(
        [FromQuery] CompliancePackagingListRequest request, CancellationToken cancellationToken) =>
        Execute(async () => await service.ListDocumentsAsync(request.Skip, request.Take, cancellationToken).ConfigureAwait(false));

    [HttpGet("documents/{id:guid}")]
    [ProducesResponseType(typeof(ComplianceDocumentResponse), StatusCodes.Status200OK)]
    public Task<ActionResult<ComplianceDocumentResponse>> Document(Guid id, CancellationToken cancellationToken) =>
        Execute(async () => await service.GetDocumentAsync(id, cancellationToken).ConfigureAwait(false));

    [HttpPost("documents/{id:guid}/review")]
    [ProducesResponseType(typeof(ComplianceDocumentResponse), StatusCodes.Status200OK)]
    public Task<ActionResult<ComplianceDocumentResponse>> Review(Guid id,
        [FromBody] ReviewComplianceDocumentRequest request, CancellationToken cancellationToken) =>
        Execute(async () => await sender.Send(new ReviewComplianceDocumentCommand(id, request), cancellationToken).ConfigureAwait(false));

    [HttpPost]
    [ProducesResponseType(typeof(CompliancePackageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public Task<ActionResult<CompliancePackageResponse>> Prepare(
        [FromBody] CreateCompliancePackageRequest request, CancellationToken cancellationToken) =>
        Execute(async () => await sender.Send(new PrepareCompliancePackageCommand(request), cancellationToken).ConfigureAwait(false));

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CompliancePackageSummary>), StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<CompliancePackageSummary>>> Packages(
        [FromQuery] CompliancePackagingListRequest request, CancellationToken cancellationToken) =>
        Execute(async () => await service.ListPackagesAsync(request.Skip, request.Take, cancellationToken).ConfigureAwait(false));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CompliancePackageResponse), StatusCodes.Status200OK)]
    public Task<ActionResult<CompliancePackageResponse>> Package(Guid id, CancellationToken cancellationToken) =>
        Execute(async () => await service.GetPackageAsync(id, cancellationToken).ConfigureAwait(false));

    [HttpGet("{id:guid}/verification")]
    [ProducesResponseType(typeof(ComplianceArtifactVerification), StatusCodes.Status200OK)]
    public Task<ActionResult<ComplianceArtifactVerification>> Verify(Guid id, CancellationToken cancellationToken) =>
        Execute(async () => await service.VerifyPackageAsync(id, cancellationToken).ConfigureAwait(false));

    [HttpGet("{id:guid}/download")]
    [Produces("application/zip")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var download = await service.DownloadPackageAsync(id, cancellationToken).ConfigureAwait(false);
            if (download is null) { return NotFound(); }
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["X-Artifact-SHA256"] = download.Sha256;
            return File(download.Content, "application/zip", download.FileName);
        }
        catch (Exception exception) when (IsHandled(exception)) { return Error(exception); }
    }

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T?>> operation) where T : class
    {
        try
        {
            var response = await operation().ConfigureAwait(false);
            return response is null ? NotFound() : Ok(response);
        }
        catch (Exception exception) when (IsHandled(exception)) { return Error(exception); }
    }

    private static bool IsHandled(Exception exception) => exception is UnauthorizedAccessException or
        CompliancePackagingValidationException or CompliancePackagingConcurrencyException or
        CompliancePackagingIntegrityException or CompliancePackagingSigningUnavailableException;

    private ActionResult Error(Exception exception) => exception switch
    {
        UnauthorizedAccessException => Forbid(),
        CompliancePackagingValidationException validation => BadRequest(new ValidationProblemDetails(validation.Errors.ToDictionary(item => item.Key, item => item.Value))
            { Status = StatusCodes.Status400BadRequest }),
        CompliancePackagingSigningUnavailableException => StatusCode(StatusCodes.Status503ServiceUnavailable,
            new ProblemDetails { Status = StatusCodes.Status503ServiceUnavailable, Title = exception.Message }),
        _ => Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = exception.Message })
    };
}
