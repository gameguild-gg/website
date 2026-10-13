using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Feature gate for the SCIM surface: when <c>Scim:Enabled</c> is false every
///     <c>/scim</c> request short-circuits with 404 so the provisioning surface is not
///     discoverable on deployments that have not opted in.
/// </summary>
public sealed class ScimFeatureGateMiddleware(RequestDelegate next)
{
    private const string ScimPathPrefix = "/scim";

    public async Task InvokeAsync(HttpContext context, IOptions<ScimProvisioningOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Value.Enabled
            && context.Request.Path.StartsWithSegments(ScimPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context).ConfigureAwait(false);
    }
}

public static class ScimFeatureGateMiddlewareExtensions
{
    public static IApplicationBuilder UseScimProvisioningFeatureGate(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<ScimFeatureGateMiddleware>();
    }
}
