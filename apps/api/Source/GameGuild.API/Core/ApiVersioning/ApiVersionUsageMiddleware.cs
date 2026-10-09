using System.Diagnostics;
using Asp.Versioning;
using GameGuild.Configuration.PresentationLayer.ApiVersioning;
using Microsoft.AspNetCore.Routing;
using SharedApiVersioningOptions = GameGuild.Configuration.PresentationLayer.ApiVersioning.ApiVersioningOptions;

namespace GameGuild.API.Core.ApiVersioning;

/// <summary>
/// Records and logs the API version selected by endpoint routing for each request.
/// Place after <c>UseRouting</c> so the matched endpoint and resolved version are available.
/// </summary>
public sealed class ApiVersionUsageMiddleware(
    RequestDelegate next,
    SharedApiVersioningOptions options,
    ApiVersionUsageMetrics metrics,
    ILogger<ApiVersionUsageMiddleware> logger)
{
    public static string CompatibleVersionsHeaderName => "X-API-Compatible-Versions";
    private readonly Dictionary<ApiVersion, string> compatibleVersionHeaders = CreateCompatibilityHeaders(options);

    public async Task InvokeAsync(HttpContext context)
    {
        if (options.CompatibilityMatrix.Count > 0)
        {
            context.Response.OnStarting(() =>
            {
                var endpoint = context.GetEndpoint();
                var selectedVersion = context.Features.Get<IApiVersioningFeature>()?.RequestedApiVersion;
                if (endpoint is RouteEndpoint && selectedVersion is not null &&
                    compatibleVersionHeaders.TryGetValue(selectedVersion, out var compatibleVersions))
                {
                    context.Response.Headers[CompatibleVersionsHeaderName] = compatibleVersions;
                }

                return Task.CompletedTask;
            });
        }

        var stopwatch = Stopwatch.StartNew();
        var failed = false;

        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            if (options.EnableUsageTelemetry || options.EnableUsageLogging)
            {
                var endpoint = context.GetEndpoint();
                var route = endpoint is RouteEndpoint routeEndpoint
                    ? routeEndpoint.RoutePattern.RawText ?? "unmatched"
                    : "unmatched";
                var selectedVersion = context.Features.Get<IApiVersioningFeature>()?.RequestedApiVersion;
                var version = endpoint is RouteEndpoint
                    ? selectedVersion?.ToString() ?? "unspecified"
                    : "unmatched";
                var statusCode = failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode;
                var durationMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

                if (options.EnableUsageTelemetry)
                {
                    metrics.RecordRequest(version, route, context.Request.Method, statusCode, durationMilliseconds);
                }

                if (options.EnableUsageLogging)
                {
                    logger.LogInformation(
                        "API request used version {ApiVersion} for {Route} ({Method}) and returned {StatusCode} in {DurationMilliseconds} ms",
                        LogRedaction.Sanitize(version),
                        LogRedaction.Sanitize(route),
                        LogRedaction.Sanitize(context.Request.Method),
                        statusCode,
                        durationMilliseconds);
                }
            }
        }
    }

    private static Dictionary<ApiVersion, string> CreateCompatibilityHeaders(SharedApiVersioningOptions options)
    {
        var parser = ApiVersioningOptionsBuilder.CreateParser(options.VersionFormat);

        return options.CompatibilityMatrix.ToDictionary(
            entry => parser.Parse(entry.Key.AsSpan()),
            entry => string.Join(", ", entry.Value));
    }
}
