using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using GameGuild.Assets.Openinary;

namespace GameGuild.Assets.Controllers;

/// <summary>
/// CDN-optimized routes for serving assets.
/// These routes use path-based tokens instead of query strings for better CDN caching.
/// </summary>
/// <remarks>
/// Route patterns:
/// - /assets/{referenceId}/{token} - Direct asset access with path token
/// - /e/{token} - Ephemeral URL with embedded asset reference
/// - /t/{transformation}/{referenceId}/{token} - Transformed asset access
/// </remarks>
[Microsoft.AspNetCore.Http.Tags("assets/cdn")]
[Route("assets")]
[AllowAnonymous] // Token-based authorization, not session-based
public class AssetsCdnController : BaseApiController
{
    private readonly IAssetAccessService _accessService;
    private readonly IAssetStorageService _storageService;
    private readonly IAssetContentRepository _contentRepository;
    private readonly IAssetReferenceRepository _referenceRepository;
    private readonly IDurableEventProducer? _durableEventProducer;
    private readonly OpeninaryOptions? _openinaryOptions;

    public AssetsCdnController(
        IAssetAccessService accessService,
        IAssetStorageService storageService,
        IAssetContentRepository contentRepository,
        IAssetReferenceRepository referenceRepository,
        IDurableEventProducer? durableEventProducer = null,
        IOptions<OpeninaryOptions>? openinaryOptions = null)
    {
        _accessService = accessService;
        _storageService = storageService;
        _contentRepository = contentRepository;
        _referenceRepository = referenceRepository;
        _durableEventProducer = durableEventProducer;
        _openinaryOptions = openinaryOptions?.Value;
    }

    /// <summary>
    /// Serve asset content with path-based token (CDN-friendly).
    /// </summary>
    /// <remarks>
    /// URL format: /assets/{referenceId}/{token}
    /// This format is more CDN-friendly than query-string tokens because:
    /// - Path-based URLs are consistently cached
    /// - No query string parsing issues
    /// - Works with CDNs that strip query strings
    /// </remarks>
    [HttpGet("{referenceId:guid}/{token}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)] // 24 hour cache
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAssetWithPathToken(
        Guid referenceId,
        string token,
        CancellationToken ct = default)
    {
        // Validate token
        var validation = await _accessService.ValidateAccessTokenAsync(referenceId, token, ct).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Access Denied",
                Detail = validation.Error ?? "Invalid or expired access token"
            });
        }

        // Get asset reference
        var reference = await _referenceRepository.GetByIdAsync(referenceId, ct).ConfigureAwait(false);
        if (reference == null)
        {
            return NotFound();
        }

        // Get content
        var content = await _contentRepository.GetByIdAsync(reference.AssetContentId, ct).ConfigureAwait(false);
        if (content == null)
        {
            return NotFound();
        }

        // Stream content
        var stream = await _storageService.DownloadAsync(content.BucketName, content.ObjectKey, ct).ConfigureAwait(false);
        await RecordServedAsync(reference, content.Id, content.SizeBytes, "cdn-path", ct).ConfigureAwait(false);

        // Set cache headers for CDN
        Response.Headers.CacheControl = "public, max-age=86400"; // 24 hours
        Response.Headers.ETag = $"\"{content.ContentHash}\"";

        return File(stream, content.MimeType, reference.DisplayName);
    }

    /// <summary>
    /// Serve ephemeral asset (short-lived URL with embedded reference).
    /// </summary>
    /// <remarks>
    /// URL format: /e/{token}
    /// The token contains the encrypted asset reference ID and expiration.
    /// Useful for temporary share links and secure downloads.
    /// </remarks>
    [HttpGet("/e/{token}")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)] // 5 minute cache (ephemeral)
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> GetEphemeralAsset(
        string token,
        CancellationToken ct = default)
    {
        // Decode ephemeral token (contains asset ID and expiration)
        var ephemeralInfo = await _accessService.ValidateEphemeralTokenAsync(token, ct).ConfigureAwait(false);
        if (!ephemeralInfo.IsValid)
        {
            if (ephemeralInfo.IsExpired)
            {
                return StatusCode(StatusCodes.Status410Gone, new ProblemDetails
                {
                    Title = "Link Expired",
                    Detail = "This download link has expired"
                });
            }

            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Access Denied",
                Detail = ephemeralInfo.Error ?? "Invalid ephemeral token"
            });
        }

        // Get asset reference
        var reference = await _referenceRepository.GetByIdAsync(ephemeralInfo.AssetReferenceId, ct).ConfigureAwait(false);
        if (reference == null)
        {
            return NotFound();
        }

        // Get content
        var content = await _contentRepository.GetByIdAsync(reference.AssetContentId, ct).ConfigureAwait(false);
        if (content == null)
        {
            return NotFound();
        }

        // Stream content
        var stream = await _storageService.DownloadAsync(content.BucketName, content.ObjectKey, ct).ConfigureAwait(false);
        await RecordServedAsync(reference, content.Id, content.SizeBytes, "cdn-ephemeral", ct).ConfigureAwait(false);

        // Set cache headers (shorter for ephemeral)
        Response.Headers.CacheControl = "private, max-age=300"; // 5 minutes

        return File(stream, content.MimeType, reference.DisplayName);
    }

    /// <summary>
    /// Serve transformed asset (resized, cropped, etc.) with CDN caching.
    /// </summary>
    /// <remarks>
    /// URL format: /t/{transformation}/{referenceId}/{token}
    /// Transformations use standard format: w=100,h=100,fit=cover
    /// </remarks>
    [HttpGet("/t/{transformation}/{referenceId:guid}/{token}")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any)] // 7 day cache for transforms
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTransformedAsset(
        string transformation,
        Guid referenceId,
        string token,
        CancellationToken ct = default)
    {
        // Validate token
        var validation = await _accessService.ValidateAccessTokenAsync(referenceId, token, ct).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Access Denied",
                Detail = validation.Error ?? "Invalid or expired access token"
            });
        }

        // Parse transformation spec
        var spec = TransformationSpec.Parse(transformation);
        if (spec.IsIdentity)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Transformation",
                Detail = "No valid transformation parameters provided"
            });
        }

        // Get asset reference
        var reference = await _referenceRepository.GetByIdAsync(referenceId, ct).ConfigureAwait(false);
        if (reference == null)
        {
            return NotFound();
        }

        // Openinary-backed redirect: the /t/ CDN is public and cached ~1yr, so the
        // redirect is allowed ONLY for safe-to-serve content under a public policy.
        var gate = await TryBuildOpeninaryRedirectAsync(reference, spec, ct).ConfigureAwait(false);
        if (gate.Outcome == OpeninaryGateOutcome.Denied)
        {
            return NotFound();
        }

        if (gate.Outcome == OpeninaryGateOutcome.NotApplicable)
        {
            return await ServeLegacyTransformAsync(reference, transformation, spec, ct).ConfigureAwait(false);
        }

        return Redirect(gate.Url!);
    }

    private enum OpeninaryGateOutcome
    {
        NotApplicable,
        Denied,
        Redirect
    }

    private readonly record struct OpeninaryGate(OpeninaryGateOutcome Outcome, string? Url = null);

    private async Task<OpeninaryGate> TryBuildOpeninaryRedirectAsync(
        AssetReference reference,
        TransformationSpec spec,
        CancellationToken ct)
    {
        if (_openinaryOptions == null || string.IsNullOrEmpty(_openinaryOptions.ApiKey))
        {
            return new OpeninaryGate(OpeninaryGateOutcome.NotApplicable);
        }

        var content = await _contentRepository
            .GetByIdAsync(reference.AssetContentId, ct).ConfigureAwait(false);
        if (content == null || !AssetStorageBackend.IsOpeninary(content))
        {
            return new OpeninaryGate(OpeninaryGateOutcome.NotApplicable);
        }

        // Gate order: policy → safety. Fail closed on everything else.
        if (reference.AccessPolicy != AssetAccessPolicy.Public &&
            reference.AccessPolicy != AssetAccessPolicy.TenantPublic)
        {
            return new OpeninaryGate(OpeninaryGateOutcome.Denied);
        }

        if (!content.IsSafeToServe)
        {
            return new OpeninaryGate(OpeninaryGateOutcome.Denied);
        }

        var baseUrl = _openinaryOptions.BaseUrl.TrimEnd('/');
        var segment = Transformation.OpeninaryTransformMapper.ToSegment(spec);
        return new OpeninaryGate(
            OpeninaryGateOutcome.Redirect,
            $"{baseUrl}/t/{segment}/{content.ObjectKey}");
    }

    private async Task<IActionResult> ServeLegacyTransformAsync(
        AssetReference reference,
        string transformation,
        TransformationSpec spec,
        CancellationToken ct)
    {
        // Check if transformation already exists
        var transformedAsset = await _accessService.GetOrCreateTransformationAsync(
            reference.AssetContentId,
            spec,
            ct).ConfigureAwait(false);

        if (transformedAsset == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Transformation Failed",
                Detail = "Unable to generate transformed asset"
            });
        }

        // Stream transformed content
        var stream = await _storageService.DownloadAsync(
            transformedAsset.BucketName,
            transformedAsset.ObjectKey,
            ct).ConfigureAwait(false);
        await RecordServedAsync(reference, transformedAsset.Id, transformedAsset.SizeBytes, "cdn-transformed", ct)
            .ConfigureAwait(false);

        // Set aggressive cache headers for transformed assets
        Response.Headers.CacheControl = "public, max-age=604800, immutable"; // 7 days, immutable
        Response.Headers.ETag = $"\"{transformedAsset.ContentHash}\"";

        return File(stream, transformedAsset.MimeType);
    }

    private Task RecordServedAsync(
        AssetReference reference,
        Guid contentId,
        long byteCount,
        string deliveryUnit,
        CancellationToken ct) => _durableEventProducer?.RecordAsync(new AssetServedEvent(
            reference.Id,
            contentId,
            byteCount,
            deliveryUnit)
        {
            TenantId = reference.TenantId ?? DurableIntegrationEventTenants.Platform,
            ActorId = DurableIntegrationEventActors.System,
            AggregateType = nameof(AssetReference),
            AggregateId = reference.Id.ToString(),
            CorrelationId = Guid.NewGuid()
        }, ct) ?? Task.CompletedTask;
}
