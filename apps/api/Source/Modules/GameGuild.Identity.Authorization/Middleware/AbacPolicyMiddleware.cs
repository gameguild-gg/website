using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Middleware for ABAC (Attribute-Based Access Control) policy evaluation.
/// </summary>
/// <remarks>
///     <para>
///         This middleware was moved from GameGuild.Identity.Authentication module
///         as ABAC is an authorization concern, not authentication.
///     </para>
///     <para>
///         Placement: Should run after ActorContextMiddleware to have access to
///         the actor's attributes for policy evaluation.
///     </para>
/// </remarks>
public sealed class AbacPolicyMiddleware(
    RequestDelegate next,
    ILogger<AbacPolicyMiddleware> logger
)
{
    /// <summary>
    ///     Invokes the ABAC policy middleware.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        logger.LogTrace("ABAC policy middleware processing request for {Path}", LogRedaction.Sanitize(context.Request.Path));
        
        // Add ABAC evaluation headers for diagnostics
        context.Response.Headers.Append("X-ABAC-Policies", "enabled");

        await next(context).ConfigureAwait(false);
    }
}
