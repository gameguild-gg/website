using System.Net;
using Asp.Versioning.ApiExplorer;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;
using GameGuild.API.Core.CostAccounting;
using Serilog;

namespace GameGuild.API.Setup;

/// <summary>
///     Extension methods for configuring the HTTP request pipeline.
/// </summary>
public static class PipelineExtensions
{
    /// <summary>
    ///     Configures the complete application pipeline.
    /// </summary>
    /// <param name="app">The web application</param>
    /// <returns>The web application for chaining</returns>
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Request pipeline middleware order matters for correct behavior.

        // 01. Exception Handling (consistent RFC 7807 error responses in all environments)
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        // 02. Forwarded Headers (proxy/load balancer support, must be early)
        app.UseForwardedHeaders();

        // 03. HSTS (HTTP Strict Transport Security, production only)
        ConfigureHsts(app);

        // 04. HTTPS Redirection (force secure connections for external traffic only)
        if (!app.Environment.IsDevelopment())
        {
            app.UseWhen(
                ShouldRedirectToHttps,
                branch => branch.UseHttpsRedirection());
        }

        // 05. Correlation ID (distributed tracing, reads/generates X-Correlation-Id header)
        app.UseCorrelationId();

        // 06. Serilog Request Logging (structured request/response logs with enrichment)
        app.UseSerilogRequestLogging(SerilogExtensions.ConfigureRequestLogging);

        // 07. HTTP Logging (ASP.NET Core built-in request/response diagnostics)
        if (app.Configuration.GetValue<bool>("PresentationLayer:EnableHttpLogging"))
        {
            app.UseHttpLogging();
        }

        // 08. Request Localization (culture/language resolution)
        app.UseRequestLocalization();

        // 09. Security Headers (X-Content-Type-Options, X-Frame-Options, CSP, Referrer-Policy, etc.)
        app.UseSecurityHeaders();

        // 10. Routing (endpoint matching, required before auth)
        app.UseRouting();

        // 11. CORS (Cross-Origin Resource Sharing, after routing)
        app.UseCors();

        // 12. Response Caching (HTTP cache headers)
        // WARNING: With JWT + multi-tenant, ensure proper Vary headers or restrict to public endpoints
        app.UseResponseCaching();

        // 13. Response Compression (gzip/brotli for smaller payloads)
        app.UseResponseCompression();

        // 14. Authentication (identify user from JWT/cookies)
        // SECURITY: Tenant resolution validates authenticated membership and therefore needs the ClaimsPrincipal first.
        app.UseAuthentication();

        // 15. Tenant Resolution (multi-tenant context, after routing and authentication)
        // Resolves tenant from: header > domain > query > route > authenticated claim > anonymous default.
        app.UseTenantResolution();

        // 16. Actor Context (build immutable ActorContext from claims + tenant)
        // SECURITY: Must be after Authentication (needs ClaimsPrincipal) and Tenant Resolution
        // SECURITY: Must be before Authorization (authorization handlers use ActorContext)
        app.UseActorContext();

        app.UseMiddleware<CostTelemetryMiddleware>();

        // 17. Authorization (enforce permissions after user is identified)
        app.UseAuthorization();

        // 18. Rate Limiting (throttle requests per client/endpoint)
        if (app.Configuration.GetValue<bool>("PresentationLayer:EnableRateLimiting"))
        {
            app.UseRateLimiter();
        }

        // 19. Controller Endpoints (REST API routes via [ApiController])
        app.MapControllers();

        // 20. Health Check Endpoints (disabled - HealthController provides /health, /ready, /live instead)
        // We use a controller instead of MapHealthChecks() for more control over response format,
        // HTTP status codes, and Kubernetes probe semantics.
        // app.MapHealthChecks("/health");

        // 21. Minimal API Endpoints (IEndpoint implementations, including RootRedirectEndpoint)
        app.MapEndpoints(null);

        // 23. Swagger JSON (Swashbuckle middleware generates /swagger/{version}/swagger.json)
        if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
        {
            app.UseSwagger();
        }

        // 24. Compatibility OpenAPI URL.
        // The native .NET OpenAPI document generator can over-recurse on a large modular API surface.
        // Keep /openapi/{document}.json stable by pointing callers to the Swashbuckle document.
        app.MapGet("/openapi/{documentName}.json",
            (string documentName) => Results.Redirect($"/swagger/{documentName}/swagger.json"));

        // 25. Swagger UI (interactive API documentation at /documentation)
        if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
        {
            app.UseSwaggerUI(options =>
            {
                options.RoutePrefix = "documentation";

                var provider = app.Services.GetService<IApiVersionDescriptionProvider>();
                if (provider is not null)
                {
                    foreach (var description in provider.ApiVersionDescriptions)
                    {
                        options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
                            $"GameGuild API {description.GroupName.ToUpperInvariant()}");
                    }
                }
                else
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "GameGuild API V1");
                }
            });
        }

        return app;
    }

    internal static bool ShouldRedirectToHttps(HttpContext context) =>
        !IsLoopbackRequest(context) && !IsHealthRequest(context);

    internal static void ConfigureHsts(WebApplication app)
    {
        if (app.Environment.IsProduction()) app.UseHsts();
    }

    private static bool IsHealthRequest(HttpContext context)
    {
        var path = context.Request.Path;
        return path.StartsWithSegments("/health")
               || path.StartsWithSegments("/ready")
               || path.StartsWithSegments("/live");
    }

    private static bool IsLoopbackRequest(HttpContext context)
    {
        var remoteIpAddress = context.Connection.RemoteIpAddress;
        return remoteIpAddress is not null && IPAddress.IsLoopback(remoteIpAddress);
    }
}
