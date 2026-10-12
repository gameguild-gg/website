using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Middleware for permission caching optimization.
/// </summary>
/// <remarks>
///     <para>
///         This middleware was moved from GameGuild.Identity.Authentication module
///         as it performs authorization-related logic (permission caching).
///     </para>
///     <para>
///         Placement: Should run after ActorContextMiddleware, before request handling.
///     </para>
/// </remarks>
public sealed class PermissionCachingMiddleware(
    RequestDelegate next,
    ILogger<PermissionCachingMiddleware> logger
)
{
    /// <summary>
    ///     Invokes the permission caching middleware.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        logger.LogTrace("Permission caching middleware invoked for {Path}", LogRedaction.Sanitize(context.Request.Path));
        
        // Add permission caching headers for diagnostics
        context.Response.Headers.Append("X-Permission-Cache", "enabled");

        await next(context).ConfigureAwait(false);
    }
}
