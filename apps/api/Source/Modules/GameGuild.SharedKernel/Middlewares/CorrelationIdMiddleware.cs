using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GameGuild;

/// <summary>
///     Middleware that manages correlation IDs for distributed tracing.
///     Reads X-Correlation-Id from incoming requests or generates a new one,
///     sets it on the response, and adds it to the logging scope.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    /// <summary>
    ///     The header name for the correlation ID.
    /// </summary>
    public const string CorrelationIdHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        // Read correlation ID from request header, or generate a new one
        var correlationId = GetOrGenerateCorrelationId(context);

        // Store in HttpContext.Items for access throughout the request
        context.Items[CorrelationIdHeader] = correlationId;

        // Add correlation ID to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId);
            return Task.CompletedTask;
        });

        // Push correlation ID into logging scope for all downstream log entries
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await next(context).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Maximum allowed length for an incoming correlation ID to prevent abuse.
    /// </summary>
    private const int MaxCorrelationIdLength = 64;

    private static string GetOrGenerateCorrelationId(HttpContext context)
    {
        // Try to get from request header
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existingCorrelationId) 
            && !string.IsNullOrWhiteSpace(existingCorrelationId))
        {
            var raw = existingCorrelationId.ToString();
            // Sanitize: truncate to max length and strip control characters to prevent log injection
            if (raw.Length > MaxCorrelationIdLength)
            {
                raw = raw[..MaxCorrelationIdLength];
            }

            return SanitizeCorrelationId(raw);
        }

        // Generate a new correlation ID
        return GenerateCorrelationId();
    }

    /// <summary>
    ///     Removes control characters and newlines from a correlation ID to prevent log injection attacks.
    /// </summary>
    private static string SanitizeCorrelationId(string value)
    {
        // Only allow printable ASCII characters (space through tilde)
        var span = value.AsSpan();
        Span<char> buffer = stackalloc char[span.Length];
        var pos = 0;
        foreach (var c in span)
        {
            if (c >= ' ' && c <= '~')
            {
                buffer[pos++] = c;
            }
        }
        return new string(buffer[..pos]);
    }

    /// <summary>
    ///     Generates a new correlation ID.
    ///     Uses a GUID for guaranteed uniqueness across distributed systems.
    /// </summary>
    private static string GenerateCorrelationId()
    {
        // Use a GUID without dashes for a clean, unique identifier
        return Guid.NewGuid().ToString("N");
    }
}

/// <summary>
///     Extension methods for adding CorrelationId middleware to the application pipeline.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    ///     Adds the correlation ID middleware to the application pipeline.
    ///     Should be placed early in the pipeline, after exception handling but before logging.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    /// <summary>
    ///     Gets the correlation ID from the current HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>The correlation ID, or null if not set</returns>
    public static string? GetCorrelationId(this HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdHeader, out var correlationId))
        {
            return correlationId?.ToString();
        }

        return context.TraceIdentifier;
    }
}
