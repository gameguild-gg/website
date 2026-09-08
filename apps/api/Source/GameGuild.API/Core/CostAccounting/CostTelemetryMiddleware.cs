using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Routing;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.API.Core.CostAccounting;

internal sealed class CostTelemetryMiddleware(
    RequestDelegate next,
    ILogger<CostTelemetryMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IDurableEventProducer durableEventProducer,
        IActorContextAccessor actorContextAccessor,
        CostTelemetryContext costTelemetry)
    {
        costTelemetry.Reset();
        var stopwatch = Stopwatch.StartNew();
        Exception? requestFailure = null;
        try
        {
            await next(httpContext).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            requestFailure = exception;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var endpoint = httpContext.GetEndpoint();
            var routeTemplate = endpoint is RouteEndpoint routeEndpoint
                ? routeEndpoint.RoutePattern.RawText ?? "unmatched"
                : "unmatched";
            var correlationId = CreateStableGuid(httpContext.TraceIdentifier);
            var actor = actorContextAccessor.ActorContext;
            var measured = new ApiRequestMeasuredEventV1(
                httpContext.Request.Method,
                routeTemplate,
                requestFailure is null ? httpContext.Response.StatusCode : StatusCodes.Status500InternalServerError,
                (decimal)stopwatch.Elapsed.TotalMilliseconds,
                costTelemetry.DatabaseMilliseconds,
                costTelemetry.CacheOperations,
                (httpContext.Request.ContentLength ?? 0) + (httpContext.Response.ContentLength ?? 0))
            {
                TenantId = actor.TenantId ?? DurableIntegrationEventTenants.Platform,
                ActorId = actor.SubjectIdAsGuid ?? DurableIntegrationEventActors.System,
                AggregateType = "HttpRequest",
                AggregateId = correlationId.ToString(),
                CorrelationId = correlationId,
                OccurredAt = DateTime.UtcNow
            };

            try
            {
                // A disconnected caller must not cancel accounting for an already completed operation.
                // Bound the independent write so a telemetry outage cannot hold the request indefinitely.
                using var telemetryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await durableEventProducer.RecordAsync(measured, telemetryTimeout.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to persist internal request cost telemetry");
            }
        }
    }

    private static Guid CreateStableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }
}
