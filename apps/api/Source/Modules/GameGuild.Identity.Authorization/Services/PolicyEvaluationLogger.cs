using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Implementation of policy evaluation logging for debugging authorization decisions.
///     Provides comprehensive tracing and diagnostics for policy evaluation.
/// </summary>
public sealed class PolicyEvaluationLogger : IPolicyEvaluationLogger
{
    private readonly ILogger<PolicyEvaluationLogger> _logger;
    private readonly ConcurrentDictionary<string, PolicyEvaluationTraceState> _activeTraces = new();

    public PolicyEvaluationLogger(ILogger<PolicyEvaluationLogger> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IPolicyEvaluationTrace BeginTrace(
        string policyName,
        ClaimsPrincipal user,
        object? resource = null,
        string? correlationId = null)
    {
        var traceId = correlationId ?? Guid.NewGuid().ToString("N")[..12];
        var trace = new PolicyEvaluationTraceState(
            traceId,
            policyName,
            user,
            resource,
            _logger,
            this);

        _activeTraces.TryAdd(traceId, trace);

        _logger.LogDebug(
            "[PolicyDebug:{TraceId}] Starting evaluation of policy '{PolicyName}' for user '{UserId}'",
            traceId,
            policyName,
            GetUserId(user));

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            LogUserClaims(traceId, user);
            LogResourceContext(traceId, resource);
        }

        return trace;
    }

    /// <inheritdoc />
    public void LogRequirementResult(
        string traceId,
        string requirementName,
        bool succeeded,
        string? reason = null,
        TimeSpan? duration = null)
    {
        var durationMs = duration?.TotalMilliseconds ?? 0;

        if (succeeded)
        {
            _logger.LogDebug(
                "[PolicyDebug:{TraceId}] ✓ Requirement '{RequirementName}' PASSED ({Duration:F2}ms){Reason}",
                traceId,
                requirementName,
                durationMs,
                string.IsNullOrEmpty(reason) ? "" : $" - {reason}");
        }
        else
        {
            _logger.LogWarning(
                "[PolicyDebug:{TraceId}] ✗ Requirement '{RequirementName}' FAILED ({Duration:F2}ms){Reason}",
                traceId,
                requirementName,
                durationMs,
                string.IsNullOrEmpty(reason) ? "" : $" - {reason}");
        }
    }

    /// <inheritdoc />
    public void LogPolicyResult(
        string traceId,
        bool succeeded,
        TimeSpan totalDuration)
    {
        if (_activeTraces.TryRemove(traceId, out var trace))
        {
            var summary = trace.GetSummary();

            if (succeeded)
            {
                _logger.LogInformation(
                    "[PolicyDebug:{TraceId}] ✓ Policy '{PolicyName}' SUCCEEDED - " +
                    "{PassedCount}/{TotalCount} requirements passed in {Duration:F2}ms",
                    traceId,
                    trace.PolicyName,
                    summary.PassedRequirements,
                    summary.TotalRequirements,
                    totalDuration.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "[PolicyDebug:{TraceId}] ✗ Policy '{PolicyName}' FAILED - " +
                    "{PassedCount}/{TotalCount} requirements passed, {FailedCount} failed in {Duration:F2}ms",
                    traceId,
                    trace.PolicyName,
                    summary.PassedRequirements,
                    summary.TotalRequirements,
                    summary.FailedRequirements,
                    totalDuration.TotalMilliseconds);

                // Log failed requirements
                foreach (var failed in summary.FailedRequirementNames)
                {
                    _logger.LogWarning(
                        "[PolicyDebug:{TraceId}]   └─ Failed: {RequirementName}",
                        traceId,
                        failed);
                }
            }
        }
    }

    /// <inheritdoc />
    public void LogPolicyFailure(
        string traceId,
        string failureReason,
        IEnumerable<string>? suggestions = null)
    {
        _logger.LogWarning(
            "[PolicyDebug:{TraceId}] Authorization Failure: {FailureReason}",
            traceId,
            failureReason);

        if (suggestions != null)
        {
            _logger.LogInformation(
                "[PolicyDebug:{TraceId}] Suggestions to resolve:",
                traceId);

            foreach (var suggestion in suggestions)
            {
                _logger.LogInformation(
                    "[PolicyDebug:{TraceId}]   → {Suggestion}",
                    traceId,
                    suggestion);
            }
        }
    }

    /// <inheritdoc />
    public PolicyDebugSettings? GetDebugSettings(object? endpoint)
    {
        if (endpoint == null)
        {
            return null;
        }

        // Check for PolicyDebugAttribute on the endpoint
        if (endpoint is not Microsoft.AspNetCore.Http.Endpoint metadata)
        {
            return null;
        }

        var attribute = metadata.Metadata.GetMetadata<PolicyDebugAttribute>();

        if (attribute == null || !attribute.Enabled)
        {
            return null;
        }

        return new PolicyDebugSettings
        {
            LogLevel = attribute.LogLevel,
            IncludeStackTrace = attribute.IncludeStackTrace,
            IncludeClaims = attribute.IncludeClaims,
            IncludeResourceContext = attribute.IncludeResourceContext,
            CorrelationHeader = attribute.CorrelationHeader,
            PolicyNames = attribute.PolicyNames?.ToHashSet()
        };
    }

    /// <inheritdoc />
    public bool IsDebugEnabled(object? endpoint)
    {
        return GetDebugSettings(endpoint) != null;
    }

    private static string GetUserId(ClaimsPrincipal user)
    {
        var nameIdentifierClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (nameIdentifierClaim is not null)
        {
            return nameIdentifierClaim.Value;
        }

        var subjectClaim = user.FindFirst("sub");
        if (subjectClaim is not null)
        {
            return subjectClaim.Value;
        }

        if (user.Identity is not null && user.Identity.Name is { } identityName)
        {
            return identityName;
        }

        return "(anonymous)";
    }

    private void LogUserClaims(string traceId, ClaimsPrincipal user)
    {
        _logger.LogTrace(
            "[PolicyDebug:{TraceId}] User claims:",
            traceId);

        foreach (var claim in user.Claims)
        {
            _logger.LogTrace(
                "[PolicyDebug:{TraceId}]   {ClaimType}: {ClaimValue}",
                traceId,
                claim.Type,
                claim.Value.Length > 50 ? claim.Value[..50] + "..." : claim.Value);
        }
    }

    private void LogResourceContext(string traceId, object? resource)
    {
        if (resource == null)
        {
            _logger.LogTrace(
                "[PolicyDebug:{TraceId}] No resource context provided",
                traceId);
            return;
        }

        _logger.LogTrace(
            "[PolicyDebug:{TraceId}] Resource context: Type={ResourceType}",
            traceId,
            resource.GetType().Name);

        try
        {
            var json = JsonSerializer.Serialize(resource, new JsonSerializerOptions
            {
                WriteIndented = false,
                MaxDepth = 3
            });

            if (json.Length > 500)
            {
                json = json[..500] + "...";
            }

            _logger.LogTrace(
                "[PolicyDebug:{TraceId}] Resource data: {ResourceJson}",
                traceId,
                json);
        }
        catch
        {
            _logger.LogTrace(
                "[PolicyDebug:{TraceId}] Resource data: (unable to serialize)",
                traceId);
        }
    }

    /// <summary>
    ///     Internal state for tracking a policy evaluation trace.
    /// </summary>
    private sealed class PolicyEvaluationTraceState : IPolicyEvaluationTrace
    {
        private readonly ClaimsPrincipal _user;
        private readonly object? _resource;
        private readonly ILogger _logger;
        private readonly PolicyEvaluationLogger _parent;
        private readonly Stopwatch _stopwatch;
        private readonly List<RequirementResult> _requirements = new();
        private readonly Dictionary<string, object?> _context = new();
        private bool _isDisposed;

        public PolicyEvaluationTraceState(
            string traceId,
            string policyName,
            ClaimsPrincipal user,
            object? resource,
            ILogger logger,
            PolicyEvaluationLogger parent)
        {
            TraceId = traceId;
            PolicyName = policyName;
            StartTime = SystemClock.UtcNow;
            _user = user;
            _resource = resource;
            _logger = logger;
            _parent = parent;
            _stopwatch = Stopwatch.StartNew();
        }

        public string TraceId { get; }
        public string PolicyName { get; }
        public DateTime StartTime { get; }

        public void LogRequirement(string requirementName, bool succeeded, string? reason = null)
        {
            var result = new RequirementResult(requirementName, succeeded, reason);
            _requirements.Add(result);
            _parent.LogRequirementResult(TraceId, requirementName, succeeded, reason);
        }

        public void Complete(bool succeeded)
        {
            _stopwatch.Stop();
            _parent.LogPolicyResult(TraceId, succeeded, _stopwatch.Elapsed);
        }

        public void AddContext(string key, object? value)
        {
            _context[key] = value;

            _logger.LogTrace(
                "[PolicyDebug:{TraceId}] Context added: {Key}={Value}",
                TraceId,
                key,
                value?.ToString() ?? "(null)");
        }

        public TraceSummary GetSummary()
        {
            return new TraceSummary
            {
                TotalRequirements = _requirements.Count,
                PassedRequirements = _requirements.Count(r => r.Succeeded),
                FailedRequirements = _requirements.Count(r => !r.Succeeded),
                FailedRequirementNames = _requirements
                    .Where(r => !r.Succeeded)
                    .Select(r => r.Name)
                    .ToList()
            };
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _stopwatch.Stop();

            // Clean up from parent if not already removed
            _parent._activeTraces.TryRemove(TraceId, out _);
        }

        private readonly record struct RequirementResult(string Name, bool Succeeded, string? Reason);
    }

    /// <summary>
    ///     Summary of a trace evaluation.
    /// </summary>
    public class TraceSummary
    {
        public int TotalRequirements { get; set; }
        public int PassedRequirements { get; set; }
        public int FailedRequirements { get; set; }
        public List<string> FailedRequirementNames { get; set; } = new();
    }
}
