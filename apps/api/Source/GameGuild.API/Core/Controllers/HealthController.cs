using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GameGuild.API.Controllers;

/// <summary>
///     Health check endpoints for monitoring, load balancers, and orchestration platforms.
///     Covers comprehensive health, readiness/liveness probes, and dependency checks.
/// </summary>
[ApiController]
[Route("[controller]")]
[Route("api/[controller]")]
[Tags("health")]
[AllowAnonymous]
public class HealthController(
    HealthCheckService healthCheckService,
    ILogger<HealthController> logger) : ControllerBase
{
    private readonly HealthCheckService _healthCheckService =
        healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));

    private readonly ILogger<HealthController> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    ///     Comprehensive health check endpoint for application monitoring
    /// </summary>
    /// <returns>Detailed health status including all registered health checks</returns>
    /// <remarks>
    ///     Performs a comprehensive health check of all registered services and dependencies including:
    ///     - Database connectivity
    ///     - External service availability
    ///     - Cache systems (Redis, etc.)
    ///     - Message queues
    ///     - File systems
    ///     Returns HTTP 200 when all checks are healthy, HTTP 503 when any check fails.
    ///     Used by monitoring systems, load balancers, and orchestration platforms to determine application health.
    /// </remarks>
    [HttpGet]
    [EndpointSummary("Comprehensive application health check")]
    [EndpointDescription("Performs a comprehensive health check of all registered services and dependencies. Returns detailed status information for monitoring systems, load balancers, and orchestration platforms.")]
    [ProducesResponseType<HealthinessResponse>(200)]
    [ProducesResponseType<HealthinessResponse>(503)]
    public async Task<ActionResult<HealthinessResponse>> GetHealth()
    {
        try
        {
            var healthReport = await _healthCheckService.CheckHealthAsync().ConfigureAwait(false);

            var response = new HealthinessResponse
            {
                Status = healthReport.Status.ToString(),
                Duration = healthReport.TotalDuration,
                Timestamp = SystemClock.UtcNow,
                Checks = healthReport.Entries.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new HealthinessResponseItem
                    {
                        Status = kvp.Value.Status.ToString(),
                        Duration = kvp.Value.Duration,
                        Description = kvp.Value.Description,
                        Data = kvp.Value.Data
                    })
            };

            var statusCode = healthReport.Status != HealthStatus.Unhealthy ? 200 : 503;

            return StatusCode(statusCode, response);
        }
        catch (Exception ex)
        {
            var errorResponse = new HealthinessResponse
            {
                Status = "Unhealthy",
                Duration = TimeSpan.Zero,
                Timestamp = SystemClock.UtcNow,
                Error = ex.Message
            };

            return StatusCode(503, errorResponse);
        }
    }

    /// <summary>
    ///     Kubernetes-style readiness probe for traffic routing decisions
    /// </summary>
    /// <returns>Application readiness status indicating if it's ready to serve traffic</returns>
    /// <remarks>
    ///     Readiness probes determine whether the application is ready to serve traffic.
    ///     Unlike liveness probes, readiness checks verify that all dependencies are available
    ///     and the application can handle requests properly.
    ///     Kubernetes uses this endpoint to:
    ///     - Remove pods from service endpoints when not ready
    ///     - Prevent traffic routing to initializing instances
    ///     - Handle rolling deployments gracefully
    ///     Returns HTTP 200 when ready to serve traffic, HTTP 503 when not ready.
    ///     Checks services tagged with "ready" in health check registration.
    /// </remarks>
    [HttpGet("/ready")]
    [HttpGet("/api/ready")]
    [EndpointSummary("Readiness probe for traffic routing decisions")]
    [EndpointDescription("Kubernetes-style readiness probe that determines whether the application is ready to serve traffic. Checks all dependencies and services required for proper request handling.")]
    [ProducesResponseType<ReadinessResponse>(200)]
    [ProducesResponseType<ReadinessResponse>(503)]
    public async Task<ActionResult<ReadinessResponse>> GetReadiness()
    {
        var healthReport = await _healthCheckService.CheckHealthAsync(
            check => check.Tags.Contains("ready")).ConfigureAwait(false);

        var response = new ReadinessResponse
        {
            Status = healthReport.Status.ToString(),
            Ready = healthReport.Status != HealthStatus.Unhealthy,
            Timestamp = SystemClock.UtcNow,
            Services = healthReport.Entries.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Status == HealthStatus.Healthy)
        };

        var statusCode = response.Ready ? 200 : 503;

        return StatusCode(statusCode, response);
    }

    /// <summary>
    ///     Kubernetes-style liveness probe for container restart decisions
    /// </summary>
    /// <returns>Application liveness status indicating if the process is running correctly</returns>
    /// <remarks>
    ///     Liveness probes determine whether the application process is running and functioning.
    ///     This is a lightweight check that verifies the application hasn't deadlocked or crashed.
    ///     Kubernetes uses this endpoint to:
    ///     - Restart containers that are in a broken state
    ///     - Detect application deadlocks or memory leaks
    ///     - Ensure long-running processes remain healthy
    ///     Always returns HTTP 200 if the process is running. Includes:
    ///     - Application uptime since startup
    ///     - Current timestamp
    ///     - Application version information
    ///     - Basic process health indicators
    ///     This check is intentionally simple and should not depend on external services.
    /// </remarks>
    [HttpGet("/live")]
    [HttpGet("/api/live")]
    [EndpointSummary("Liveness probe for container restart decisions")]
    [EndpointDescription("Kubernetes-style liveness probe that indicates whether the application process is running correctly. Used by orchestration platforms to determine if containers should be restarted.")]
    [ProducesResponseType<LivenessResponse>(200)]
    public ActionResult<LivenessResponse> GetLiveness()
    {
        var response = new LivenessResponse
        {
            Status = "Healthy",
            Alive = true,
            Timestamp = SystemClock.UtcNow,
            Uptime = SystemClock.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime(),
            Version = GetType().Assembly.GetName().Version!.ToString()
        };

        return Ok(response);
    }

    /// <summary>
    ///     Get detailed dependency health status
    /// </summary>
    /// <returns>Detailed health status of all external dependencies</returns>
    /// <remarks>
    ///     Provides detailed health information for all registered external dependencies including:
    ///     - Database connections (PostgreSQL, Redis, etc.)
    ///     - External APIs and services
    ///     - Message queues
    ///     - File storage systems
    ///     - Cache systems
    ///
    ///     Each dependency includes:
    ///     - Current status (Healthy, Degraded, Unhealthy)
    ///     - Response time
    ///     - Connection details (sanitized)
    ///     - Error information if unhealthy
    /// </remarks>
    [HttpGet("/health/dependencies")]
    [HttpGet("/api/health/dependencies")]
    [EndpointSummary("Detailed dependency health check")]
    [EndpointDescription("Provides comprehensive health status of all external dependencies including databases, APIs, caches, and message queues.")]
    [ProducesResponseType<DependencyHealthResponse>(200)]
    [ProducesResponseType<DependencyHealthResponse>(503)]
    public async Task<ActionResult<DependencyHealthResponse>> GetDependencyHealth()
    {
        var healthReport = await _healthCheckService.CheckHealthAsync(
            check => check.Tags.Contains("dependency") || check.Tags.Contains("ready")).ConfigureAwait(false);

        var dependencies = new List<DependencyHealthItem>();

        foreach (var entry in healthReport.Entries)
        {
            dependencies.Add(new DependencyHealthItem
            {
                Name = entry.Key,
                Status = entry.Value.Status.ToString(),
                Duration = entry.Value.Duration,
                Description = entry.Value.Description,
                IsHealthy = entry.Value.Status == HealthStatus.Healthy,
                Tags = entry.Value.Tags.ToList(),
                Data = entry.Value.Data.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.ToString() ?? string.Empty),
                Exception = entry.Value.Exception?.Message
            });
        }

        var response = new DependencyHealthResponse
        {
            Status = healthReport.Status.ToString(),
            TotalDuration = healthReport.TotalDuration,
            Timestamp = SystemClock.UtcNow,
            HealthyCount = dependencies.Count(d => d.IsHealthy),
            UnhealthyCount = dependencies.Count(d => !d.IsHealthy),
            Dependencies = dependencies
        };

        var statusCode = healthReport.Status != HealthStatus.Unhealthy ? 200 : 503;
        return StatusCode(statusCode, response);
    }
}

#region Health Response Models

/// <summary>
///     Health check response model
/// </summary>
public class HealthinessResponse
{
    public string Status { get; set; } = string.Empty;

    public TimeSpan Duration { get; set; }

    public DateTime Timestamp { get; set; }

    public Dictionary<string, HealthinessResponseItem> Checks { get; set; } = new();

    public string? Error { get; set; }
}

/// <summary>
///     Individual health check item
/// </summary>
public class HealthinessResponseItem
{
    public string Status { get; set; } = string.Empty;

    public TimeSpan Duration { get; set; }

    public string? Description { get; set; }

    public IReadOnlyDictionary<string, object>? Data { get; set; }
}

/// <summary>
///     Readiness check response model
/// </summary>
public class ReadinessResponse
{
    public string Status { get; set; } = string.Empty;

    public bool Ready { get; set; }

    public DateTime Timestamp { get; set; }

    public Dictionary<string, bool> Services { get; set; } = new();

    public string? Error { get; set; }
}

/// <summary>
///     Liveness check response model
/// </summary>
public class LivenessResponse
{
    public string Status { get; set; } = string.Empty;

    public bool Alive { get; set; }

    public DateTime Timestamp { get; set; }

    public TimeSpan Uptime { get; set; }

    public string Version { get; set; } = string.Empty;
}

/// <summary>
///     Dependency health check response model
/// </summary>
public class DependencyHealthResponse
{
    public string Status { get; set; } = string.Empty;

    public TimeSpan TotalDuration { get; set; }

    public DateTime Timestamp { get; set; }

    public int HealthyCount { get; set; }

    public int UnhealthyCount { get; set; }

    public List<DependencyHealthItem> Dependencies { get; set; } = new();

    public string? Error { get; set; }
}

/// <summary>
///     Individual dependency health item
/// </summary>
public class DependencyHealthItem
{
    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public TimeSpan Duration { get; set; }

    public string? Description { get; set; }

    public bool IsHealthy { get; set; }

    public List<string> Tags { get; set; } = new();

    public Dictionary<string, string> Data { get; set; } = new();

    public string? Exception { get; set; }
}

#endregion
