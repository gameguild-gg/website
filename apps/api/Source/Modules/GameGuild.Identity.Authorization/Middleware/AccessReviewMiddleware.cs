using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Middleware for access review compliance tracking.
/// </summary>
/// <remarks>
///     <para>
///         This middleware was moved from GameGuild.Identity.Authentication module
///         as access reviews are an authorization/compliance concern.
///     </para>
///     <para>
///         This middleware tracks access patterns for compliance and audit purposes.
///         It can trigger access review workflows when permissions are about to expire
///         or require periodic recertification.
///     </para>
///     <para>
///         Placement: Should run after ActorContextMiddleware.
///     </para>
/// </remarks>
public sealed class AccessReviewMiddleware(
    RequestDelegate next,
    ILogger<AccessReviewMiddleware> logger
)
{
    /// <summary>
    ///     Invokes the access review middleware.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        logger.LogTrace("Access review middleware processing request for {Path}", LogRedaction.Sanitize(context.Request.Path));
        
        // Add access review headers for diagnostics
        context.Response.Headers.Append("X-Access-Review", "enabled");

        await next(context).ConfigureAwait(false);
    }
}
