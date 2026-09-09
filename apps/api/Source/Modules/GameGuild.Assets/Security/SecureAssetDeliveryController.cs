using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GameGuild.Assets.Commands;
using GameGuild.CQRS;

namespace GameGuild.Assets.Security;

/// <summary>
/// Secure content delivery endpoint with all threat mitigations.
/// </summary>
[Microsoft.AspNetCore.Http.Tags("assets/secure-delivery")]
[Route("api/assets")]
public class SecureAssetDeliveryController : BaseApiController
{
    private readonly IAssetAccessService _accessService;
    private readonly IAssetRateLimitService _rateLimitService;
    private readonly ITenantAssetValidationService _tenantValidation;
    private readonly ITransformationValidator _transformationValidator;
    private readonly IDownloadWindowService _downloadWindowService;
    private readonly IAssetContentRepository _contentRepository;
    private readonly IAssetReferenceRepository _referenceRepository;
    private readonly IActorContextAccessor _actorContextAccessor;
    private readonly IAssetStorageService _storageService;
    private readonly AssetAccessOptions _accessOptions;
    private readonly ILogger<SecureAssetDeliveryController> _logger;
    private readonly ISender? _sender;

    public SecureAssetDeliveryController(
        IAssetAccessService accessService,
        IAssetRateLimitService rateLimitService,
        ITenantAssetValidationService tenantValidation,
        ITransformationValidator transformationValidator,
        IDownloadWindowService downloadWindowService,
        IAssetContentRepository contentRepository,
        IAssetReferenceRepository referenceRepository,
        IActorContextAccessor actorContextAccessor,
        IAssetStorageService storageService,
        IOptions<AssetAccessOptions> accessOptions,
        ILogger<SecureAssetDeliveryController> logger,
        ISender? sender = null)
    {
        _accessService = accessService;
        _rateLimitService = rateLimitService;
        _tenantValidation = tenantValidation;
        _transformationValidator = transformationValidator;
        _downloadWindowService = downloadWindowService;
        _contentRepository = contentRepository;
        _referenceRepository = referenceRepository;
        _actorContextAccessor = actorContextAccessor;
        _storageService = storageService;
        _accessOptions = accessOptions.Value;
        _logger = logger;
        _sender = sender;
    }

    /// <summary>
    /// Serves asset content with full security checks.
    /// </summary>
    [HttpGet("{assetId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetContent(
        [FromRoute] Guid assetId,
        [FromQuery] string? token,
        [FromQuery] string? transform,
        CancellationToken ct)
    {
        var clientIp = GetClientIp();
        var actor = _actorContextAccessor.ActorContext;

        // Threat #3: Check if IP is blocked (brute force protection)
        if (await _rateLimitService.IsIpBlockedAsync(clientIp, ct))
        {
            _logger.LogWarning("Blocked IP {IP} attempted access to asset {AssetId}", clientIp, assetId);
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Access Denied",
                Detail = "Your IP has been temporarily blocked due to excessive failed requests"
            });
        }

        // Threat #1: Check asset rate limit (hotlinking protection)
        var rateLimit = await _rateLimitService.CheckAssetAccessRateAsync(assetId, ct).ConfigureAwait(false);
        if (!rateLimit.IsAllowed)
        {
            Response.Headers["Retry-After"] = rateLimit.RetryAfter?.TotalSeconds.ToString() ?? "3600";
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Title = "Rate Limit Exceeded",
                Detail = rateLimit.Reason ?? "Asset access rate limit exceeded"
            });
        }

        // Get asset reference
        var reference = await _referenceRepository.GetByIdWithContentAsync(assetId, ct).ConfigureAwait(false);
        if (reference == null || reference.Content == null)
        {
            await Record403IfApplicable(clientIp, ct).ConfigureAwait(false);
            return NotFound();
        }

        var hasSignedToken = !string.IsNullOrEmpty(token);
        var assetTenantId = reference.TenantId ?? Guid.Empty;
        var transformSpec = ValidateTransformation(transform, reference.Content.Kind, out var transformationError);
        if (transformationError != null)
        {
            return transformationError;
        }

        // Threat #2 & #4: validate bearer tokens against the persisted asset tenant.
        if (hasSignedToken && !_accessService.ValidateToken(token!, assetId, reference.TenantId, transformSpec))
        {
            await Record403IfApplicable(clientIp, ct).ConfigureAwait(false);
            return ForbiddenProblem("Invalid or expired token");
        }

        // Threat #6: Fail-closed tenant validation
        var tenantValidation = hasSignedToken
            ? _tenantValidation.ValidateTokenTenant(assetTenantId, actor.TenantId)
            : _tenantValidation.ValidateTenantAccess(
                actor.TenantId,
                assetTenantId,
                actor);

        if (!tenantValidation.IsValid)
        {
            _logger.LogWarning(
                "Tenant validation failed for asset {AssetId}: {Error}",
                assetId, tenantValidation.Error);
            await Record403IfApplicable(clientIp, ct).ConfigureAwait(false);
            return ForbiddenProblem(tenantValidation.Error ?? "Tenant access denied");
        }

        // Threat #7: Block serving content pending or failed virus scan
        if (reference.Content.VirusScanStatus == VirusScanStatus.Pending)
        {
            _logger.LogInformation("Attempted access to content pending virus scan: {AssetId}", assetId);
            return StatusCode(StatusCodes.Status202Accepted, new ProblemDetails
            {
                Title = "Content Processing",
                Detail = "This content is being scanned for security. Please try again shortly."
            });
        }

        if (reference.Content.VirusScanStatus == VirusScanStatus.Infected)
        {
            _logger.LogWarning("Attempted access to infected content: {AssetId}", assetId);
            return StatusCode(StatusCodes.Status410Gone, new ProblemDetails
            {
                Title = "Content Unavailable",
                Detail = "This content has been removed due to policy violation"
            });
        }

        // Threat #8: Block serving content with moderation issues
        if (reference.Content.ModerationStatus == ModerationStatus.Blocked ||
            reference.Content.ModerationStatus == ModerationStatus.Rejected)
        {
            return StatusCode(StatusCodes.Status410Gone, new ProblemDetails
            {
                Title = "Content Unavailable",
                Detail = "This content has been removed due to policy violation"
            });
        }

        // Threat #8: Block serving content pending moderation for high-risk types
        // Low-risk types (text, JSON) can be served while pending async moderation
        if (reference.Content.ModerationStatus == ModerationStatus.Pending &&
            IsHighRiskMimeType(reference.Content.MimeType))
        {
            return StatusCode(StatusCodes.Status202Accepted, new ProblemDetails
            {
                Title = "Content Processing",
                Detail = "This content is being reviewed. Please try again shortly."
            });
        }

        // Threat #8: Always block content explicitly flagged for review
        if (reference.Content.ModerationStatus == ModerationStatus.NeedsReview)
        {
            return StatusCode(StatusCodes.Status202Accepted, new ProblemDetails
            {
                Title = "Content Pending Review",
                Detail = "This content is pending moderation review"
            });
        }

        // Threat #12: Validate download window for paid content
        if (reference.AccessPolicy == AssetAccessPolicy.PaidContent)
        {
            if (!actor.SubjectIdAsGuid.HasValue)
            {
                return Unauthorized("Authentication required for paid content");
            }

            var windowValidation = await _downloadWindowService.ValidateDownloadWindowAsync(
                assetId, actor.SubjectIdAsGuid.Value, ct).ConfigureAwait(false);

            if (!windowValidation.IsValid)
            {
                return StatusCode(StatusCodes.Status402PaymentRequired, new ProblemDetails
                {
                    Title = "Payment Required",
                    Detail = windowValidation.Error ?? "Valid purchase required for access"
                });
            }
        }

        if (hasSignedToken)
        {
            var bucketName = reference.Content.BucketName;
            var objectKey = reference.Content.ObjectKey;

            if (transformSpec is { IsIdentity: false })
            {
                var transformed = await _accessService.GetOrCreateTransformationAsync(
                    reference.Content.Id,
                    transformSpec,
                    ct).ConfigureAwait(false);
                if (transformed == null)
                {
                    return ForbiddenProblem("Transformation is unavailable");
                }

                bucketName = transformed.BucketName;
                objectKey = transformed.ObjectKey;
            }

            var expiry = TimeSpan.FromMinutes(Math.Max(1, _accessOptions.DefaultExpiryMinutes));
            var storageUrl = await _storageService.GeneratePresignedUrlAsync(
                bucketName,
                objectKey,
                expiry,
                isDownload: true,
                ct).ConfigureAwait(false);
            await _referenceRepository.RecordAccessAsync(assetId, ct).ConfigureAwait(false);
            return Redirect(storageUrl);
        }

        var accessUrl = await _accessService.GenerateDirectStorageUrlAsync(
            assetId,
            actor.SubjectIdAsGuid,
            actor.TenantId,
            ct).ConfigureAwait(false);

        if (accessUrl == null)
        {
            await Record403IfApplicable(clientIp, ct).ConfigureAwait(false);
            return ForbiddenProblem("Access denied");
        }

        return Redirect(accessUrl.Url);
    }

    /// <summary>
    /// Gets asset access URL with security checks.
    /// </summary>
    [HttpPost("{assetId:guid}/access-url")]
    [ProducesResponseType(typeof(AssetAccessUrl), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccessUrl(
        [FromRoute] Guid assetId,
        [FromBody] AccessUrlRequest? request,
        CancellationToken ct)
    {
        var actor = _actorContextAccessor.ActorContext;
        var clientIp = GetClientIp();

        // Rate limit check
        var rateLimit = await _rateLimitService.CheckAssetAccessRateAsync(assetId, ct).ConfigureAwait(false);
        if (!rateLimit.IsAllowed)
        {
            Response.Headers["Retry-After"] = rateLimit.RetryAfter?.TotalSeconds.ToString() ?? "3600";
            return StatusCode(StatusCodes.Status429TooManyRequests, new ProblemDetails
            {
                Title = "Rate Limit Exceeded",
                Detail = rateLimit.Reason
            });
        }

        // Parse and validate transformation
        TransformationSpec? transformSpec = null;
        if (!string.IsNullOrEmpty(request?.Transform))
        {
            transformSpec = TransformationSpec.Parse(request.Transform);
            if (transformSpec != null)
            {
                var reference = await _referenceRepository.GetByIdWithContentAsync(assetId, ct).ConfigureAwait(false);
                if (reference?.Content != null)
                {
                    var transformValidation = _transformationValidator.Validate(
                        transformSpec, reference.Content.Kind);

                    if (!transformValidation.IsValid)
                    {
                        return BadRequest(new ProblemDetails
                        {
                            Title = "Invalid Transformation",
                            Detail = transformValidation.Error
                        });
                    }

                    transformSpec = transformValidation.SanitizedSpec;
                }
            }
        }

        var accessUrl = await _sender!.Send(new GenerateAccessUrlCommand(
            assetId,
            actor.SubjectIdAsGuid,
            actor.TenantId,
            transformSpec),
            ct).ConfigureAwait(false);

        if (accessUrl == null)
        {
            await Record403IfApplicable(clientIp, ct).ConfigureAwait(false);
            return Forbid();
        }

        return Ok(accessUrl);
    }

    private string GetClientIp()
    {
        // Check for forwarded headers first (reverse proxy)
        var forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private ObjectResult ForbiddenProblem(string detail)
    {
        return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Title = "Access Denied",
            Detail = detail,
            Status = StatusCodes.Status403Forbidden
        });
    }

    private TransformationSpec? ValidateTransformation(
        string? transform,
        AssetKind assetKind,
        out ObjectResult? error)
    {
        error = null;
        if (string.IsNullOrEmpty(transform))
        {
            return null;
        }

        var transformSpec = TransformationSpec.Parse(transform);
        if (transformSpec == null)
        {
            return null;
        }

        var validation = _transformationValidator.Validate(transformSpec, assetKind);
        if (validation.IsValid)
        {
            return validation.SanitizedSpec;
        }

        error = BadRequest(new ProblemDetails
        {
            Title = "Invalid Transformation",
            Detail = validation.Error
        });
        return null;
    }

    private async Task Record403IfApplicable(string clientIp, CancellationToken ct)
    {
        // Record 403 for brute force protection
        var result = await _rateLimitService.Record403ResponseAsync(clientIp, ct).ConfigureAwait(false);
        if (!result.IsAllowed)
        {
            _logger.LogWarning(
                "IP {IP} blocked after {Count} failed access attempts",
                clientIp, result.CurrentCount);
        }
    }

    private static bool IsHighRiskMimeType(string mimeType)
    {
        return mimeType.StartsWith("image/") ||
               mimeType.StartsWith("video/") ||
               mimeType == "application/pdf";
    }
}

/// <summary>
/// Request for access URL generation.
/// </summary>
public sealed record AccessUrlRequest(
    string? Transform = null,
    bool DirectStorage = false);
