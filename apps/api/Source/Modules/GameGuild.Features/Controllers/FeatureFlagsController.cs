using Asp.Versioning;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using GameGuild.CQRS;

namespace GameGuild.Features;

/// <summary>
///     Controller for runtime evaluation of feature flags
/// </summary>
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/features")]
[Authorize]
public class FeatureFlagsController(
    IFeatureFlagEvaluationService evaluationService,
    ILogger<FeatureFlagsController> logger,
    ISender? sender = null) : BaseApiController
{
    /// <summary>
    ///     Evaluate a feature flag for runtime decisions
    /// </summary>
    [HttpPost(":evaluate")]
    public async Task<IActionResult> EvaluateFeature([FromBody] FeatureEvaluationRequest request, CancellationToken cancellationToken)
    {
        var context = new FeatureContext
        {
            UserId = request.Context.UserId ?? GetCurrentUserId(),
            TenantId = request.Context.TenantId ?? GetCurrentTenantId(),
            Environment = request.Context.Environment,
            Permissions = request.Context.Permissions,
            CustomAttributes = request.Context.CustomAttributes,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
        };

        var result = await sender!.Send(
            new EvaluateFeatureOperationCommand(request.FeatureKey, context),
            cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Get feature value (boolean/string/number) as resolved for the current context
    /// </summary>
    [HttpGet("{key}/value")]
    public async Task<IActionResult> GetFeatureValue(
        string key,
        [FromQuery] bool defaultValue = false,
        [FromQuery] Guid? userId = null,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string environment = "production",
        CancellationToken cancellationToken = default
    )
    {
        var context = new FeatureContext
        {
            UserId = userId ?? GetCurrentUserId(),
            TenantId = tenantId ?? GetCurrentTenantId(),
            Environment = environment,
            Permissions = GetCurrentUserPermissions(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
        };

        var result = await evaluationService.GetValueAsync(key, context, defaultValue, cancellationToken);

        return Ok(new { featureKey = key, value = result, type = "boolean" });
    }

    /// <summary>
    ///     Bulk evaluate multiple feature flags for runtime decisions
    /// </summary>
    [HttpPost(":evaluate-bulk")]
    public async Task<IActionResult> BulkEvaluateFeatures([FromBody] BulkEvaluationRequest request, CancellationToken cancellationToken)
    {
        logger.LogDebug("Evaluating {FeatureCount} feature flags", request.FeatureKeys.Count);
        // Use the context from request, or create a default one
        var context = request.Context;

        // Fill in missing values from current user context
        context.UserId ??= GetCurrentUserId();
        context.TenantId ??= GetCurrentTenantId();
        context.IpAddress ??= HttpContext.Connection.RemoteIpAddress?.ToString();
        context.UserAgent ??= HttpContext.Request.Headers.UserAgent.ToString();

        if (context.Permissions.Count == 0) { context.Permissions = GetCurrentUserPermissions(); }

        var result = await sender!.Send(
            new BulkEvaluateFeatureOperationCommand(request.FeatureKeys, context),
            cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    ///     Get all enabled feature flags for the current context
    /// </summary>
    [HttpGet("enabled")]
    public async Task<IActionResult> GetEnabled([FromQuery] Guid? userId = null, [FromQuery] Guid? tenantId = null, [FromQuery] string environment = "production", CancellationToken cancellationToken = default)
    {
        var context = new FeatureContext
        {
            UserId = userId ?? GetCurrentUserId(),
            TenantId = tenantId ?? GetCurrentTenantId(),
            Environment = environment,
            Permissions = GetCurrentUserPermissions(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = HttpContext.Request.Headers.UserAgent.ToString()
        };

        var result = await evaluationService.GetEnabledFeaturesAsync(context, cancellationToken).ConfigureAwait(false);

        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        // Implementation would depend on your auth system
        return null;
    }

    private Guid? GetCurrentTenantId()
    {
        // Implementation would depend on your multi-tenancy setup
        return null;
    }

    private List<string> GetCurrentUserPermissions()
    {
        // Implementation would depend on your auth system
        // Example: return User?.FindAll("permission")?.Select(c => c.Value).ToList() ?? [];
        return [];
    }
}
//   /// </summary>
//   [HttpGet]
//   public async Task<IActionResult> GetFeatureFlags([FromQuery] Guid? tenantId = null, [FromQuery] string? environment = null, CancellationToken cancellationToken = default) {
//     try {
//       var flags = await _featureFlagService.GetFeatureFlagsAsync(tenantId ?? GetCurrentTenantId(), environment, cancellationToken);
// 
//       return Ok(
//         flags.Select(f => new {
//           f.Id,
//           f.Key,
//           f.Name,
//           f.Description,
//           f.IsEnabled,
//           f.Type,
//           f.DefaultValue,
//           f.EnabledValue,
//           f.IsGlobal,
//           f.RolloutPercentage,
//           f.Environment,
//           f.TenantId,
//           f.CreatedAt,
//           f.UpdatedAt,
//           TargetCount = f.Targets.Count
//         }
//         )
//       );
//     }
//     catch (Exception ex) {
//       _logger.LogError(ex, "Error getting feature flags");
// 
//       return StatusCode(500, new { error = "Internal server error" });
//     }
//   }
// 
//   /// <summary>
//   /// Get feature flag by ID
//   /// </summary>
//   [HttpGet("{id:guid}")]
//   public async Task<IActionResult> GetFeatureFlag(Guid id, CancellationToken cancellationToken) {
//     try {
//       var flag = await _featureFlagService.GetFeatureFlagByIdAsync(id, cancellationToken);
// 
//       if (flag == null) return NotFound(new { error = "Feature flag not found" });
// 
//       return Ok(flag);
//     }
//     catch (Exception ex) {
//       _logger.LogError(ex, "Error getting feature flag {Id}", id);
// 
//       return StatusCode(500, new { error = "Internal server error" });
//     }
//   }
// 
//   /// <summary>
//   /// Create a new feature flag
//   /// </summary>
//   [HttpPost]
//   public async Task<IActionResult> CreateFeatureFlag([FromBody] CreateFeatureFlagRequest request, CancellationToken cancellationToken) {
//     try {
//       var featureFlag = new FeatureFlag {
//         Key = request.Key,
//         Name = request.Name,
//         Description = request.Description ?? string.Empty,
//         IsEnabled = request.IsEnabled,
//         Type = request.Type,
//         DefaultValue = request.DefaultValue,
//         EnabledValue = request.EnabledValue,
//         IsGlobal = request.IsGlobal,
//         RolloutPercentage = request.RolloutPercentage,
//         Environment = request.Environment ?? "production",
//         TenantId = request.TenantId ?? GetCurrentTenantId()
//       };
// 
//       var created = await _featureFlagService.CreateFeatureFlagAsync(featureFlag, cancellationToken);
// 
//       return CreatedAtAction(nameof(GetFeatureFlag), new { id = created.Id }, created);
//     }
//     catch (Exception ex) {
//       _logger.LogError(ex, "Error creating feature flag");
// 
//       return StatusCode(500, new { error = "Internal server error" });
//     }
//   }
// 
//   /// <summary>
//   /// Update a feature flag
//   /// </summary>
//   [HttpPut("{id:guid}")]
//   public async Task<IActionResult> UpdateFeatureFlag(Guid id, [FromBody] UpdateFeatureFlagRequest request, CancellationToken cancellationToken) {
//     try {
//       var featureFlag = new FeatureFlag {
//         Key = request.Key,
//         Name = request.Name,
//         Description = request.Description ?? string.Empty,
//         IsEnabled = request.IsEnabled,
//         Type = request.Type,
//         DefaultValue = request.DefaultValue,
//         EnabledValue = request.EnabledValue,
//         IsGlobal = request.IsGlobal,
//         RolloutPercentage = request.RolloutPercentage,
//         Environment = request.Environment ?? "production",
//         TenantId = request.TenantId
//       };
// 
//       var updated = await _featureFlagService.UpdateFeatureFlagAsync(id, featureFlag, cancellationToken);
// 
//       if (updated == null) return NotFound(new { error = "Feature flag not found" });
// 
//       return Ok(updated);
//     }
//     catch (Exception ex) {
//       _logger.LogError(ex, "Error updating feature flag {Id}", id);
// 
//       return StatusCode(500, new { error = "Internal server error" });
//     }
//   }
// 
//   /// <summary>
//   /// Delete a feature flag
//   /// </summary>
//   [HttpDelete("{id:guid}")]
//   public async Task<IActionResult> DeleteFeatureFlag(Guid id, CancellationToken cancellationToken) {
//     try {
//       var deleted = await _featureFlagService.DeleteFeatureFlagAsync(id, cancellationToken);
// 
//       if (!deleted) return NotFound(new { error = "Feature flag not found" });
// 
//       return NoContent();
//     }
//     catch (Exception ex) {
//       _logger.LogError(ex, "Error deleting feature flag {Id}", id);
// 
//       return StatusCode(500, new { error = "Internal server error" });
//     }
//   }
// 
//   /// <summary>
//   /// Get feature flag usage analytics
//   /// </summary>
//   [HttpGet("{id:guid}/analytics")]
//   public async Task<IActionResult> GetUsageAnalytics(Guid id, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null, CancellationToken cancellationToken = default) {
//     try {
//       var analytics = await _featureFlagService.GetUsageAnalyticsAsync(id, fromDate, toDate, cancellationToken);
// 
//       return Ok(
//         new {
//           featureFlagId = id,
//           fromDate,
//           toDate,
//           totalEvaluations = analytics.Count(),
//           enabledCount = analytics.Count(a => a.WasEnabled),
//           disabledCount = analytics.Count(a => !a.WasEnabled),
//           usage = analytics.Take(100) // Limit for performance
//         }
//       );
//     }
//     catch (Exception ex) {
//       _logger.LogError(ex, "Error getting usage analytics for feature flag {Id}", id);
// 
//       return StatusCode(500, new { error = "Internal server error" });
//     }
//   }
// 
//   private Guid? GetCurrentUserId() {
//     var userIdClaim = User?.FindFirst("sub")?.Value ?? User?.FindFirst("userId")?.Value;
// 
//     return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
//   }
// 
//   private Guid? GetCurrentTenantId() {
//     var tenantIdClaim = User?.FindFirst("tenantId")?.Value;
// 
//     return Guid.TryParse(tenantIdClaim, out var tenantId) ? tenantId : null;
//   }
// 
//   private List<string> GetCurrentUserRoles() { return User?.FindAll("role")?.Select(c => c.Value).ToList() ?? new List<string>(); }
// }
// 
// /// <summary>
// /// Request models for feature flag operations
// /// </summary>
// public class FeatureEvaluationRequest {
//   public string FeatureKey { get; set; } = string.Empty;
// 
//   public Guid? UserId { get; set; }
// 
//   public Guid? TenantId { get; set; }
// 
//   public string? Environment { get; set; }
// 
//   public List<string>? UserRoles { get; set; }
// 
//   public Dictionary<string, object>? CustomAttributes { get; set; }
// }
// 
// public class CreateFeatureFlagRequest {
//   public string Key { get; set; } = string.Empty;
// 
//   public string Name { get; set; } = string.Empty;
// 
//   public string? Description { get; set; }
// 
//   public bool IsEnabled { get; set; }
// 
//   public FeatureFlagType Type { get; set; } = FeatureFlagType.Toggle;
// 
//   public string? DefaultValue { get; set; }
// 
//   public string? EnabledValue { get; set; }
// 
//   public bool IsGlobal { get; set; } = true;
// 
//   public int RolloutPercentage { get; set; } = 100;
// 
//   public string? Environment { get; set; }
// 
//   public Guid? TenantId { get; set; }
// }
// 
// public class UpdateFeatureFlagRequest {
//   public string Key { get; set; } = string.Empty;
// 
//   public string Name { get; set; } = string.Empty;
// 
//   public string? Description { get; set; }
// 
//   public bool IsEnabled { get; set; }
// 
//   public FeatureFlagType Type { get; set; } = FeatureFlagType.Toggle;
// 
//   public string? DefaultValue { get; set; }
// 
//   public string? EnabledValue { get; set; }
// 
//   public bool IsGlobal { get; set; } = true;
// 
//   public int RolloutPercentage { get; set; } = 100;
// 
//   public string? Environment { get; set; }
// 
//   public Guid? TenantId { get; set; }
// }
