using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Features;

/// <summary>
///     Service for evaluating feature flags with targeting, rollout, and caching capabilities.
///     Implements IFeatureFlagEvaluationService following the Interface Segregation Principle.
///     Refactored to use Strategy pattern for different feature types.
/// </summary>
public class FeatureFlagEvaluationService(
    IFeatureFlagQueryRepository queryRepository,
    IEnumerable<IFeatureEvaluationStrategy> strategies,
    ILogger<FeatureFlagEvaluationService> logger,
    IOptions<FeatureFlagOptions> options
) : IFeatureFlagEvaluationService
{
    private readonly ILogger<FeatureFlagEvaluationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly FeatureFlagOptions _options = options.Value ?? throw new ArgumentNullException(nameof(options));

    private readonly IFeatureFlagQueryRepository _queryRepository = queryRepository ?? throw new ArgumentNullException(nameof(queryRepository));

    private readonly IEnumerable<IFeatureEvaluationStrategy> _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));

    /// <inheritdoc />
    public async Task<FeatureEvaluationResult> EvaluateAsync(string featureKey, FeatureContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureKey);
        ArgumentNullException.ThrowIfNull(context);

        var startTime = SystemClock.UtcNow;

        // Security Note: Missing TenantId is logged for observability, but actual fail-closed 
        // enforcement happens in TenantTargetingHandler when tenant-specific rules exist.
        // This ensures tenant-agnostic features work correctly while tenant-specific features
        // fail-closed to prevent cross-tenant feature leakage.
        if (!context.TenantId.HasValue)
        {
            _logger.LogDebug(
                "Feature flag '{FeatureKey}' evaluated without TenantId context. " +
                "Tenant-targeting handlers will apply fail-closed policy if tenant rules exist.",
                LogRedaction.Sanitize(featureKey));
        }

        try
        {
            // Get feature flag from repository
            var featureFlag = await _queryRepository.GetByKeyAsync(featureKey, cancellationToken).ConfigureAwait(false);

            if (featureFlag == null)
            {
                _logger.LogWarning("Feature flag '{FeatureKey}' not found", LogRedaction.Sanitize(featureKey));

                return CreateNotFoundResult(featureKey, startTime);
            }

            // Validate environment
            var requestEnvironment = context.Environment;

            if (!string.IsNullOrEmpty(featureFlag.Environment) && !string.Equals(featureFlag.Environment, requestEnvironment, StringComparison.OrdinalIgnoreCase))
            {
                return CreateEnvironmentMismatchResult(featureKey, featureFlag.Environment, requestEnvironment, startTime);
            }

            // Use strategy pattern to evaluate based on feature type
            var strategy = _strategies.FirstOrDefault(s => s.FeatureType == featureFlag.Type);

            if (strategy != null)
            {
                var result = await strategy.EvaluateAsync(featureFlag, context, cancellationToken).ConfigureAwait(false);
                result.FeatureKey = featureKey;
                result.EvaluatedAt = startTime;

                return result;
            }

            // Fallback: simple enabled/disabled evaluation
            _logger.LogWarning("No strategy found for feature type {FeatureType}, using fallback", featureFlag.Type);

            return new FeatureEvaluationResult
            {
                FeatureKey = featureKey,
                IsEnabled = featureFlag.IsEnabled,
                Value = featureFlag.IsEnabled ? featureFlag.EnabledValue : featureFlag.DefaultValue,
                Reason = "Fallback evaluation - no strategy found",
                EvaluatedAt = startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating feature flag '{FeatureKey}'", LogRedaction.Sanitize(featureKey));

            return CreateErrorResult(featureKey, ex.Message, startTime);
        }
    }

    /// <inheritdoc />
    public async Task<BulkEvaluateFeaturesResponse> EvaluateBulkAsync(BulkEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Context);

        if (request.FeatureKeys == null || !request.FeatureKeys.Any()) { throw new ArgumentException("Feature keys cannot be empty", nameof(request.FeatureKeys)); }

        // Enforce max bulk size
        var featureKeys = request.FeatureKeys.Take(_options.MaxBulkEvaluationSize).ToList();

        if (featureKeys.Count < request.FeatureKeys.Count())
        {
            _logger.LogWarning("Bulk evaluation request exceeded max size. Requested {RequestedCount}, evaluating {ActualCount}", request.FeatureKeys.Count(), featureKeys.Count);
        }

        var response = new BulkEvaluateFeaturesResponse { Environment = request.Context.Environment };

        // Parallel evaluation for better performance
        var tasks = featureKeys.Select(async key =>
            {
                var result = await EvaluateAsync(key, request.Context, cancellationToken).ConfigureAwait(false);

                return new { Key = key, Result = result };
            }
        );

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var result in results) { response.Results[result.Key] = result.Result; }

        return response;
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(string featureKey, FeatureContext context, CancellationToken cancellationToken = default)
    {
        var result = await EvaluateAsync(featureKey, context, cancellationToken).ConfigureAwait(false);

        return result.IsEnabled;
    }

    /// <inheritdoc />
    public async Task<T> GetValueAsync<T>(string featureKey, FeatureContext context, T defaultValue, CancellationToken cancellationToken = default)
    {
        var result = await EvaluateAsync(featureKey, context, cancellationToken).ConfigureAwait(false);

        if (!result.IsEnabled || string.IsNullOrEmpty(result.Value)) { return defaultValue; }

        try
        {
            // Attempt to convert the value to the requested type
            if (typeof(T) == typeof(string)) { return (T) (object) result.Value; }

            if (typeof(T) == typeof(bool)) { return (T) (object) bool.Parse(result.Value); }

            if (typeof(T) == typeof(int)) { return (T) (object) int.Parse(result.Value); }

            if (typeof(T) == typeof(double)) { return (T) (object) double.Parse(result.Value); }

            // Try JSON deserialization for complex types
            return JsonSerializer.Deserialize<T>(result.Value) ?? defaultValue;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert feature value '{Value}' to type {Type}", result.Value, typeof(T).Name);

            return defaultValue;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetEnabledFeaturesAsync(FeatureContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var environment = context.Environment;
            var allFeatures = await _queryRepository.GetByEnvironmentAsync(environment, cancellationToken).ConfigureAwait(false);

            var evaluationTasks = allFeatures.Select(async feature =>
                {
                    var isEnabled = await IsEnabledAsync(feature.Key, context, cancellationToken).ConfigureAwait(false);

                    return new { feature.Key, IsEnabled = isEnabled };
                }
            );

            var results = await Task.WhenAll(evaluationTasks).ConfigureAwait(false);

            return results.Where(r => r.IsEnabled).Select(r => r.Key).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting enabled features");

            return [];
        }
    }

    #region Private Helper Methods

    private static FeatureEvaluationResult CreateNotFoundResult(string featureKey, DateTime evaluatedAt)
    {
        return new FeatureEvaluationResult { FeatureKey = featureKey, IsEnabled = false, Reason = "Feature flag not found", EvaluatedAt = evaluatedAt };
    }

    private static FeatureEvaluationResult CreateEnvironmentMismatchResult(string featureKey, string expectedEnvironment, string? actualEnvironment, DateTime evaluatedAt)
    {
        return new FeatureEvaluationResult
        {
            FeatureKey = featureKey, IsEnabled = false, Reason = $"Environment mismatch: expected '{expectedEnvironment}', got '{actualEnvironment ?? "null"}'", EvaluatedAt = evaluatedAt
        };
    }

    private static FeatureEvaluationResult CreateErrorResult(string featureKey, string errorMessage, DateTime evaluatedAt)
    {
        return new FeatureEvaluationResult { FeatureKey = featureKey, IsEnabled = false, Reason = $"Evaluation error: {errorMessage}", EvaluatedAt = evaluatedAt };
    }

    #endregion
}
