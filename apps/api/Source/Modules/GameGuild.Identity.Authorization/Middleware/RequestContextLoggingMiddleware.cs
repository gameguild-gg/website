using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Middleware that logs request context information (user, tenant, permissions) for debugging and auditing
/// </summary>
public class RequestContextLoggingMiddleware(RequestDelegate next, ILogger<RequestContextLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext, IActorContextAccessor actorContextAccessor)
    {
        var actor = actorContextAccessor.ActorContext;
        var requestId = LogRedaction.Sanitize(httpContext.TraceIdentifier);
        var path = LogRedaction.Sanitize(httpContext.Request.Path);
        var method = LogRedaction.Sanitize(httpContext.Request.Method);

        // Log request start with context (redact PII: TenantId, UserId, Email)
        logger.LogInformation(
            "Request {RequestId} started: {Method} {Path} | User: {UserId} | Tenant: {TenantId} | Authenticated: {IsAuthenticated}",
            requestId,
            method,
            path,
            LogRedaction.RedactId(actor.SubjectId, "uid"),
            LogRedaction.RedactId(actor.TenantId, "tid"),
            actor.IsAuthenticated
        );

        // Add structured logging properties (redact PII)
        using (logger.BeginScope(
                   new Dictionary<string, object>
                   {
                       ["RequestId"] = requestId,
                       ["UserId"] = LogRedaction.RedactId(actor.SubjectId, "uid"),
                       ["TenantId"] = LogRedaction.RedactId(actor.TenantId, "tid"),
                       ["IsAuthenticated"] = actor.IsAuthenticated,
                       ["Roles"] = LogRedaction.Sanitize(string.Join(", ", actor.Roles))
                   }
               ))
        {
            var startTime = SystemClock.UtcNow;

            try
            {
                await next(httpContext).ConfigureAwait(false);

                var duration = SystemClock.UtcNow - startTime;
                var statusCode = httpContext.Response.StatusCode;

                // Log request completion
                logger.LogInformation("Request {RequestId} completed: {StatusCode} in {Duration}ms", requestId, statusCode, duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                var duration = SystemClock.UtcNow - startTime;

                // Log request failure
                logger.LogError("Request {RequestId} failed after {Duration}ms: {ErrorType}",
                    requestId, duration.TotalMilliseconds, ex.GetType().FullName);

                throw;
            }
        }
    }
}
