using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GameGuild.Assets.Commands;
using GameGuild.Assets.Queries;

namespace GameGuild.Assets.Controllers;

/// <summary>
/// Controller for asset operations.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("assets")]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/assets")]
[Authorize]
public class AssetsController(
    ISender sender,
    IActorContextAccessor actorContextAccessor,
    IAssetUploadAuthorizationService uploadAuthorizationService,
    IAssetTextExtractionService textExtractionService,
    IDurableEventProducer? durableEventProducer = null) : BaseApiController
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <summary>
    /// Upload a new asset.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromQuery] string? displayName = null,
        [FromQuery] AssetAccessPolicy accessPolicy = AssetAccessPolicy.Private,
        [FromQuery] string? parentResourceType = null,
        [FromQuery] Guid? parentResourceId = null,
        [FromQuery] Guid? folderId = null,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "No file provided" });
        }

        if (!Actor.SubjectIdAsGuid.HasValue)
        {
            return Unauthorized();
        }

        await using var stream = file.OpenReadStream();

        var command = new UploadAssetCommand(
            stream,
            file.FileName,
            file.ContentType,
            Actor.SubjectIdAsGuid.Value,
            Actor.TenantId,
            displayName,
            accessPolicy,
            parentResourceType,
            parentResourceId,
            folderId);

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        if (result.Error != null)
        {
            if (result.Error == "Forbidden") return Forbid();
            return BadRequest(new ProblemDetails { Title = result.Error });
        }

        return CreatedAtAction(
            nameof(GetAsset),
            new { id = result.AssetReferenceId },
            result);
    }

    /// <summary>
    /// Upload multiple assets in one request.
    /// </summary>
    [HttpPost("bulk-upload")]
    [RequestSizeLimit(250 * 1024 * 1024)] // 250 MB total request
    [ProducesResponseType(typeof(BulkUploadAssetsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BulkUpload(
        [FromForm] List<IFormFile> files,
        [FromQuery] AssetAccessPolicy accessPolicy = AssetAccessPolicy.Private,
        [FromQuery] string? parentResourceType = null,
        [FromQuery] Guid? parentResourceId = null,
        [FromQuery] Guid? folderId = null,
        CancellationToken ct = default)
    {
        if (!Actor.SubjectIdAsGuid.HasValue || !Actor.TenantId.HasValue)
        {
            return Unauthorized();
        }

        if (files is null || files.Count == 0 || files.All(file => file.Length == 0))
        {
            return BadRequest(new ProblemDetails { Title = "At least one file is required" });
        }

        var openedStreams = new List<Stream>();
        try
        {
            var inputs = new List<BulkUploadAssetInput>();
            foreach (var file in files.Where(file => file.Length > 0))
            {
                var stream = file.OpenReadStream();
                openedStreams.Add(stream);
                inputs.Add(new BulkUploadAssetInput(stream, file.FileName, file.ContentType, file.FileName));
            }

            var result = await sender.Send(
                new BulkUploadAssetsCommand(
                    inputs,
                    Actor.SubjectIdAsGuid.Value,
                    Actor.TenantId.Value,
                    accessPolicy,
                    parentResourceType,
                    parentResourceId,
                    folderId),
                ct).ConfigureAwait(false);

            if (result.Items.Count > 0 && result.Items.All(item => item.Error == "Forbidden")) return Forbid();
            return Ok(result);
        }
        finally
        {
            foreach (var stream in openedStreams)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    #region Chunked Upload Endpoints

    /// <summary>
    /// Initialize a chunked upload for large files.
    /// </summary>
    /// <param name="fileName">Name of the file being uploaded</param>
    /// <param name="mimeType">MIME type of the file</param>
    /// <param name="totalSize">Total size of the file in bytes</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Chunked upload session with upload ID and chunk count</returns>
    [HttpPost("chunked-uploads")]
    [ProducesResponseType(typeof(ChunkedUploadSession), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> InitiateChunkedUpload(
        [FromQuery] string fileName,
        [FromQuery] string mimeType,
        [FromQuery] long totalSize,
        CancellationToken ct = default)
    {
        if (!Actor.SubjectIdAsGuid.HasValue)
        {
            return Unauthorized();
        }

        if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(mimeType) || totalSize <= 0)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid file parameters" });
        }

        var session = await sender.Send(new InitiateChunkedAssetUploadCommand(
            fileName,
            mimeType,
            totalSize,
            Actor.SubjectIdAsGuid.Value),
            ct).ConfigureAwait(false);

        return Ok(session);
    }

    /// <summary>
    /// Upload a chunk for an in-progress chunked upload.
    /// </summary>
    /// <param name="uploadId">The upload session ID</param>
    /// <param name="chunkIndex">Zero-based chunk index</param>
    /// <param name="chunk">The chunk data</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpPost("chunked-uploads/{uploadId}/parts")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB per chunk
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadChunk(
        string uploadId,
        [FromQuery] int chunkIndex,
        IFormFile chunk,
        CancellationToken ct = default)
    {
        if (!Actor.SubjectIdAsGuid.HasValue)
        {
            return Unauthorized();
        }

        if (chunk == null || chunk.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "No chunk data provided" });
        }

        await using var stream = chunk.OpenReadStream();
        var success = await sender.Send(
            new UploadAssetChunkCommand(uploadId, chunkIndex, stream), ct).ConfigureAwait(false);

        if (!success)
        {
            return NotFound(new ProblemDetails { Title = "Upload session not found or expired" });
        }

        return Ok(new { uploadId, chunkIndex, success = true });
    }

    /// <summary>
    /// Complete a chunked upload and create the asset.
    /// </summary>
    /// <param name="uploadId">The upload session ID</param>
    /// <param name="displayName">Display name for the asset</param>
    /// <param name="accessPolicy">Access policy for the asset</param>
    /// <param name="parentResourceType">Optional parent resource type</param>
    /// <param name="parentResourceId">Optional parent resource ID</param>
    /// <param name="folderId">Optional virtual folder inside the parent library</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created asset reference</returns>
    [HttpPost("chunked-uploads/{uploadId}:complete")]
    [ProducesResponseType(typeof(AssetUploadResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteChunkedUpload(
        string uploadId,
        [FromQuery] string? displayName = null,
        [FromQuery] AssetAccessPolicy accessPolicy = AssetAccessPolicy.Private,
        [FromQuery] string? parentResourceType = null,
        [FromQuery] Guid? parentResourceId = null,
        [FromQuery] Guid? folderId = null,
        CancellationToken ct = default)
    {
        if (!Actor.SubjectIdAsGuid.HasValue)
        {
            return Unauthorized();
        }

        if (!await uploadAuthorizationService.CanUploadAsync(
                parentResourceType,
                parentResourceId,
                folderId,
                Actor.SubjectIdAsGuid.Value,
                Actor.TenantId,
                ct).ConfigureAwait(false))
            return Forbid();

        var options = new UploadAssetOptions(
            displayName,
            accessPolicy,
            parentResourceType,
            parentResourceId,
            folderId,
            Actor.TenantId);

        var result = await sender.Send(
            new CompleteChunkedAssetUploadCommand(uploadId, options), ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return BadRequest(new ProblemDetails { Title = result.Error ?? "Failed to complete upload" });
        }

        return CreatedAtAction(
            nameof(GetAsset),
            new { id = result.AssetReferenceId },
            result);
    }

    /// <summary>
    /// Abort an in-progress chunked upload.
    /// </summary>
    /// <param name="uploadId">The upload session ID</param>
    /// <param name="ct">Cancellation token</param>
    [HttpDelete("chunked-uploads/{uploadId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AbortChunkedUpload(
        string uploadId,
        CancellationToken ct = default)
    {
        if (!Actor.SubjectIdAsGuid.HasValue)
        {
            return Unauthorized();
        }

        await sender.Send(new AbortChunkedAssetUploadCommand(uploadId), ct).ConfigureAwait(false);
        return NoContent();
    }

    #endregion

    /// <summary>
    /// Get an asset by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAsset(
        Guid id,
        [FromQuery] bool includeContent = false,
        CancellationToken ct = default)
    {
        var query = new GetAssetQuery(
            id,
            Actor.SubjectIdAsGuid,
            Actor.TenantId,
            includeContent);

        var result = await sender.Send(query, ct).ConfigureAwait(false);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Get the inline preview contract for a document or media asset.
    /// </summary>
    [HttpGet("{id:guid}/preview")]
    [ProducesResponseType(typeof(AssetPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreview(
        Guid id,
        [FromQuery] bool includeExtractedText = false,
        [FromQuery] int thumbnailWidth = 320,
        [FromQuery] int thumbnailHeight = 240,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new GetAssetPreviewQuery(
                id,
                Actor.SubjectIdAsGuid,
                Actor.TenantId,
                thumbnailWidth,
                thumbnailHeight,
                includeExtractedText),
            ct).ConfigureAwait(false);

        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Extract text from an asset when the MIME type supports direct parsing or OCR.
    /// </summary>
    [HttpGet("{id:guid}/extracted-text", Name = "GetAssetExtractedText")]
    [ProducesResponseType(typeof(AssetExtractedTextResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExtractedText(
        Guid id,
        [FromServices] IAssetReferenceRepository referenceRepository,
        [FromServices] IAssetTextExtractionService textExtractionService,
        CancellationToken ct = default)
    {
        if (!Actor.SubjectIdAsGuid.HasValue || !Actor.TenantId.HasValue)
        {
            return Unauthorized();
        }

        var asset = await sender.Send(
            new GetAssetQuery(id, Actor.SubjectIdAsGuid.Value, Actor.TenantId.Value, false),
            ct).ConfigureAwait(false);

        if (asset == null)
        {
            return NotFound();
        }

        var reference = await referenceRepository.GetByIdWithContentAsync(id, ct).ConfigureAwait(false);
        if (reference?.Content == null)
        {
            return NotFound();
        }

        if (reference.Content.VirusScanStatus == VirusScanStatus.Infected ||
            reference.Content.ModerationStatus == ModerationStatus.Blocked)
        {
            return Forbid();
        }

        var result = await textExtractionService.ExtractAsync(reference, ct).ConfigureAwait(false);
        var status = string.IsNullOrWhiteSpace(result.Text)
            ? "empty"
            : result.IsTruncated ? "partial" : "completed";
        var message = result.Warnings.Count > 0 ? string.Join(" ", result.Warnings) : null;

        return Ok(new AssetExtractedTextResponse(
            id,
            reference.Content.MimeType,
            status,
            result.Source,
            result.Text,
            message,
            result.UsedOcr,
            result.IsTruncated));
    }

    /// <summary>
    /// Generate an access URL for an asset.
    /// </summary>
    [HttpPost("{id:guid}:generate-access-url")]
    [AllowAnonymous]
    public async Task<IActionResult> GenerateAccessUrl(
        Guid id,
        [FromQuery] int? width = null,
        [FromQuery] int? height = null,
        [FromQuery] ImageFit? fit = null,
        [FromQuery] ImageFormat? format = null,
        [FromQuery] int? quality = null,
        [FromQuery] bool direct = false,
        CancellationToken ct = default)
    {
        TransformationSpec? transformation = null;
        if (width.HasValue || height.HasValue || fit.HasValue || format.HasValue || quality.HasValue)
        {
            transformation = new TransformationSpec
            {
                Width = width,
                Height = height,
                Fit = fit ?? ImageFit.Contain,
                Format = format ?? ImageFormat.Original,
                Quality = quality ?? 85
            };
        }

        var command = new GenerateAccessUrlCommand(
            id,
            Actor.SubjectIdAsGuid,
            Actor.TenantId,
            transformation,
            direct);

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        if (result == null)
        {
            return BadRequest(new ProblemDetails { Title = "Unable to generate access URL" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Generate secure access URLs for multiple assets.
    /// </summary>
    [HttpPost("bulk-download")]
    [ProducesResponseType(typeof(BulkAssetAccessUrlsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> BulkDownload(
        [FromBody] BulkAssetAccessUrlRequest request,
        CancellationToken ct = default)
    {
        if (request.AssetIds.Count == 0)
        {
            return BadRequest(new ProblemDetails { Title = "At least one asset ID is required" });
        }

        var result = await sender.Send(
            new BulkGenerateAssetAccessUrlsQuery(
                request.AssetIds,
                Actor.SubjectIdAsGuid,
                Actor.TenantId,
                request.DirectStorageUrl),
            ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    /// Get asset content (serve the actual file).
    /// </summary>
    [HttpGet("{id:guid}/content")]
    [AllowAnonymous]
    public async Task<IActionResult> GetContent(
        Guid id,
        [FromQuery] string token,
        [FromServices] IAssetAccessService accessService,
        [FromServices] IAssetStorageService storageService,
        [FromServices] IAssetReferenceRepository referenceRepository,
        [FromQuery] string? transform = null,
        CancellationToken ct = default)
    {
        // Validate token
        if (!accessService.ValidateToken(token, id, Actor.TenantId))
        {
            return Forbid();
        }

        var reference = await referenceRepository.GetByIdWithContentAsync(id, ct).ConfigureAwait(false);
        if (reference?.Content == null)
        {
            return NotFound();
        }

        // Check content status
        if (reference.Content.VirusScanStatus == VirusScanStatus.Infected ||
            reference.Content.ModerationStatus == ModerationStatus.Blocked)
        {
            return Forbid();
        }

        var stream = await storageService.DownloadAsync(
            reference.Content.BucketName,
            reference.Content.ObjectKey,
            ct).ConfigureAwait(false);

        await referenceRepository.RecordAccessAsync(id, ct).ConfigureAwait(false);

        if (durableEventProducer is not null)
        {
            await durableEventProducer.RecordAsync(new AssetServedEvent(
                reference.Id,
                reference.Content.Id,
                reference.Content.SizeBytes,
                "api-download")
            {
                TenantId = reference.TenantId ?? DurableIntegrationEventTenants.Platform,
                ActorId = Actor.SubjectIdAsGuid ?? DurableIntegrationEventActors.System,
                AggregateType = nameof(AssetReference),
                AggregateId = reference.Id.ToString(),
                CorrelationId = Guid.NewGuid()
            }, ct).ConfigureAwait(false);
        }

        return File(stream, reference.Content.MimeType, reference.DisplayName);
    }

    /// <summary>
    /// Get extracted searchable text for an asset.
    /// </summary>
    [HttpGet("{id:guid}:extracted-text", Name = "GetSignedAssetExtractedText")]
    [AllowAnonymous]
    public async Task<IActionResult> GetExtractedText(
        Guid id,
        [FromQuery] string token,
        [FromServices] IAssetAccessService accessService,
        [FromServices] IAssetReferenceRepository referenceRepository,
        CancellationToken ct = default)
    {
        if (!accessService.ValidateToken(token, id, Actor.TenantId))
        {
            return Forbid();
        }

        var reference = await referenceRepository.GetByIdWithContentAsync(id, ct).ConfigureAwait(false);
        if (reference?.Content == null)
        {
            return NotFound();
        }

        if (reference.Content.VirusScanStatus == VirusScanStatus.Infected ||
            reference.Content.ModerationStatus == ModerationStatus.Blocked)
        {
            return Forbid();
        }

        var extraction = await textExtractionService.ExtractAsync(reference, ct).ConfigureAwait(false);
        await referenceRepository.RecordAccessAsync(id, ct).ConfigureAwait(false);

        return Ok(new ExtractedAssetTextResponse(
            id,
            reference.Content.MimeType,
            extraction.Text,
            extraction.Source,
            extraction.UsedOcr,
            extraction.IsTruncated,
            extraction.Warnings));
    }

    /// <summary>
    /// Update asset metadata.
    /// </summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateAsset(
        Guid id,
        [FromBody] UpdateAssetRequest request,
        CancellationToken ct = default)
    {
        if (!Actor.SubjectIdAsGuid.HasValue)
        {
            return Unauthorized();
        }

        var command = new UpdateAssetCommand(
            id,
            Actor.SubjectIdAsGuid.Value,
            request.DisplayName,
            request.AccessPolicy);

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        if (result == null)
        {
            return BadRequest(new ProblemDetails { Title = "Unable to update asset" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Delete an asset.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsset(
        Guid id,
        CancellationToken ct = default)
    {
        if (!Actor.SubjectIdAsGuid.HasValue)
        {
            return Unauthorized();
        }

        var command = new DeleteAssetCommand(id, Actor.SubjectIdAsGuid.Value);

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return BadRequest(new ProblemDetails { Title = "Unable to delete asset" });
        }

        return NoContent();
    }

    /// <summary>
    /// Delete multiple asset references in one request.
    /// </summary>
    [HttpPost("bulk-delete")]
    [ProducesResponseType(typeof(BulkDeleteAssetsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BulkDelete(
        [FromBody] BulkDeleteAssetsRequest request,
        CancellationToken ct = default)
    {
        if (!Actor.SubjectIdAsGuid.HasValue)
        {
            return Unauthorized();
        }

        if (request.AssetIds.Count == 0)
        {
            return BadRequest(new ProblemDetails { Title = "At least one asset ID is required" });
        }

        var result = await sender.Send(
            new BulkDeleteAssetsCommand(request.AssetIds, Actor.SubjectIdAsGuid.Value),
            ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    /// Report an asset for moderation.
    /// </summary>
    [HttpPost("{id:guid}:report")]
    public async Task<IActionResult> ReportAsset(
        Guid id,
        [FromBody] ReportAssetRequest request,
        CancellationToken ct = default)
    {
        if (!Actor.SubjectIdAsGuid.HasValue)
        {
            return Unauthorized();
        }

        var command = new ReportAssetCommand(
            id,
            Actor.SubjectIdAsGuid.Value,
            request.Reason,
            request.Description);

        var result = await sender.Send(command, ct).ConfigureAwait(false);

        if (result == null)
        {
            return BadRequest(new ProblemDetails { Title = "Unable to report asset" });
        }

        return Ok(result);
    }

    /// <summary>
    /// List assets with optional filtering.
    /// Use owner=me to get current user's assets.
    /// Use parentType and parentId to filter by parent resource.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListAssets(
        [FromQuery] string? owner = null,
        [FromQuery] string? parentType = null,
        [FromQuery] Guid? parentId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        // Handle owner=me filter
        if (owner == "me")
        {
            if (!Actor.SubjectIdAsGuid.HasValue)
            {
                return Unauthorized();
            }

            var myAssetsQuery = new GetUserAssetsQuery(
                Actor.SubjectIdAsGuid.Value,
                Actor.TenantId,
                skip,
                take);

            var myResult = await sender.Send(myAssetsQuery, ct).ConfigureAwait(false);
            return Ok(myResult);
        }

        // Handle parentType + parentId filter
        if (!string.IsNullOrEmpty(parentType) && parentId.HasValue)
        {
            var parentQuery = new GetAssetsByParentQuery(
                parentType,
                parentId.Value,
                Actor.SubjectIdAsGuid,
                Actor.TenantId);

            var parentResult = await sender.Send(parentQuery, ct).ConfigureAwait(false);
            return Ok(parentResult);
        }

        // Default: list all accessible assets (requires auth)
        if (!Actor.SubjectIdAsGuid.HasValue)
        {
            return Unauthorized();
        }

        var query = new GetUserAssetsQuery(
            Actor.SubjectIdAsGuid.Value,
            Actor.TenantId,
            skip,
            take);

        var result = await sender.Send(query, ct).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Search document and media assets by metadata, parent, MIME type, and storage key.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(AssetSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery(Name = "q")] string? query = null,
        [FromQuery] AssetKind? kind = null,
        [FromQuery] string? parentType = null,
        [FromQuery] Guid? parentId = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var result = await sender.Send(
            new SearchAssetsQuery(
                query,
                Actor.SubjectIdAsGuid,
                Actor.TenantId,
                kind,
                parentType,
                parentId,
                skip,
                take),
            ct).ConfigureAwait(false);

        return Ok(result);
    }

}

public sealed record UpdateAssetRequest(
    string? DisplayName = null,
    AssetAccessPolicy? AccessPolicy = null);

public sealed record ReportAssetRequest(
    ReportReason Reason,
    string? Description = null);

public sealed record BulkAssetAccessUrlRequest(
    IReadOnlyList<Guid> AssetIds,
    bool DirectStorageUrl = false);

public sealed record BulkDeleteAssetsRequest(
    IReadOnlyList<Guid> AssetIds);

public sealed record ExtractedAssetTextResponse(
    Guid AssetReferenceId,
    string MimeType,
    string Text,
    string Source,
    bool UsedOcr,
    bool IsTruncated,
    IReadOnlyList<string> Warnings);

public sealed record AssetExtractedTextResponse(
    Guid AssetId,
    string MimeType,
    string Status,
    string Source,
    string? Text,
    string? Message,
    bool UsedOcr,
    bool IsPartial);
