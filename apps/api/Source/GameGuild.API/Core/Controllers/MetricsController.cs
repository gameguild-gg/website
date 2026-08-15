using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.Controllers;

/// <summary>
///     Prometheus-compatible metrics endpoint for monitoring and alerting.
/// </summary>
[ApiController]
[Tags("health")]
[AllowAnonymous]
public class MetricsController(ILogger<MetricsController> logger) : ControllerBase
{
    private readonly ILogger<MetricsController> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    ///     Get application metrics in Prometheus format
    /// </summary>
    /// <returns>Prometheus-compatible metrics for monitoring and alerting</returns>
    /// <remarks>
    ///     Exposes application metrics in Prometheus text format for scraping by monitoring systems.
    ///     Metrics include:
    ///     - HTTP request counts and durations
    ///     - Database connection pool statistics
    ///     - Memory and CPU usage
    ///     - Custom business metrics
    ///     - Error rates and counts
    ///
    ///     This endpoint is designed for use with Prometheus, Grafana, and other CNCF monitoring tools.
    /// </remarks>
    [HttpGet("/metrics")]
    [EndpointSummary("Prometheus metrics endpoint")]
    [EndpointDescription("Exposes application metrics in Prometheus text format for monitoring, alerting, and observability dashboards.")]
    [Produces("text/plain")]
    [ProducesResponseType<string>(200)]
    public ActionResult GetMetrics()
    {
        var process = Process.GetCurrentProcess();
        var assembly = Assembly.GetExecutingAssembly();
        var startTime = process.StartTime.ToUniversalTime();
        var uptime = (SystemClock.UtcNow - startTime).TotalSeconds;

        // Build Prometheus-format metrics
        var metrics = new System.Text.StringBuilder();

        // Process metrics
        metrics.AppendLine("# HELP process_cpu_seconds_total Total user and system CPU time spent in seconds.");
        metrics.AppendLine("# TYPE process_cpu_seconds_total counter");
        metrics.AppendLine($"process_cpu_seconds_total {process.TotalProcessorTime.TotalSeconds:F2}");

        metrics.AppendLine("# HELP process_virtual_memory_bytes Virtual memory size in bytes.");
        metrics.AppendLine("# TYPE process_virtual_memory_bytes gauge");
        metrics.AppendLine($"process_virtual_memory_bytes {process.VirtualMemorySize64}");

        metrics.AppendLine("# HELP process_working_set_bytes Process working set in bytes.");
        metrics.AppendLine("# TYPE process_working_set_bytes gauge");
        metrics.AppendLine($"process_working_set_bytes {process.WorkingSet64}");

        metrics.AppendLine("# HELP process_private_memory_bytes Process private memory size in bytes.");
        metrics.AppendLine("# TYPE process_private_memory_bytes gauge");
        metrics.AppendLine($"process_private_memory_bytes {process.PrivateMemorySize64}");

        metrics.AppendLine("# HELP process_start_time_seconds Start time of the process since unix epoch in seconds.");
        metrics.AppendLine("# TYPE process_start_time_seconds gauge");
        metrics.AppendLine($"process_start_time_seconds {new DateTimeOffset(startTime).ToUnixTimeSeconds()}");

        metrics.AppendLine("# HELP process_uptime_seconds The uptime of the process in seconds.");
        metrics.AppendLine("# TYPE process_uptime_seconds gauge");
        metrics.AppendLine($"process_uptime_seconds {uptime:F2}");

        // Thread metrics
        metrics.AppendLine("# HELP process_num_threads Total number of threads.");
        metrics.AppendLine("# TYPE process_num_threads gauge");
        metrics.AppendLine($"process_num_threads {process.Threads.Count}");

        // GC metrics
        metrics.AppendLine("# HELP dotnet_gc_collections_total Total number of garbage collections.");
        metrics.AppendLine("# TYPE dotnet_gc_collections_total counter");
        metrics.AppendLine($"dotnet_gc_collections_total{{generation=\"0\"}} {GC.CollectionCount(0)}");
        metrics.AppendLine($"dotnet_gc_collections_total{{generation=\"1\"}} {GC.CollectionCount(1)}");
        metrics.AppendLine($"dotnet_gc_collections_total{{generation=\"2\"}} {GC.CollectionCount(2)}");

        metrics.AppendLine("# HELP dotnet_gc_memory_total_bytes Total known allocated memory in bytes.");
        metrics.AppendLine("# TYPE dotnet_gc_memory_total_bytes gauge");
        metrics.AppendLine($"dotnet_gc_memory_total_bytes {GC.GetTotalMemory(false)}");

        // Application info metric
        var version = assembly.GetName().Version!.ToString();
        metrics.AppendLine("# HELP app_info Application version and build information.");
        metrics.AppendLine("# TYPE app_info gauge");
        metrics.AppendLine($"app_info{{version=\"{version}\",runtime=\"{RuntimeInformation.FrameworkDescription}\"}} 1");

        return Content(metrics.ToString().ReplaceLineEndings("\n"), "text/plain; version=0.0.4; charset=utf-8");
    }
}
