using Microsoft.AspNetCore.Http;
using OpenFeature.Model;

namespace GameGuild.Features;

/// <summary>
///     Factory for creating and converting feature evaluation contexts
///     Centralizes context creation logic to avoid duplication
/// </summary>
public class FeatureContextFactory
{
    /// <summary>
    ///     Creates a FeatureContext from an HTTP context
    /// </summary>
    /// <param name="httpContext">The HTTP context</param>
    /// <param name="userId">Optional user ID override</param>
    /// <param name="tenantId">Optional tenant ID override</param>
    /// <param name="environment">Optional environment override</param>
    /// <returns>Populated FeatureContext</returns>
    public static FeatureContext CreateFromHttpContext(HttpContext httpContext, Guid? userId = null, Guid? tenantId = null, string? environment = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return new FeatureContext
        {
            UserId = userId ?? GetUserIdFromContext(httpContext),
            TenantId = tenantId ?? GetTenantIdFromContext(httpContext),
            Environment = environment ?? FeatureFlagConstants.DefaultEnvironment,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext.Request.Headers.UserAgent.ToString(),
            Country = GetCountryFromContext(httpContext),
            Permissions = GetUserPermissionsFromContext(httpContext),
            RequestTime = SystemClock.UtcNow,
            CustomAttributes = []
        };
    }

    /// <summary>
    ///     Creates a minimal FeatureContext with basic information
    /// </summary>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="userId">User ID</param>
    /// <param name="environment">Environment (defaults to production)</param>
    /// <returns>Basic FeatureContext</returns>
    public static FeatureContext CreateBasic(Guid? tenantId = null, Guid? userId = null, string? environment = null)
    {
        return new FeatureContext { TenantId = tenantId, UserId = userId, Environment = environment ?? FeatureFlagConstants.DefaultEnvironment, RequestTime = SystemClock.UtcNow, CustomAttributes = [] };
    }

    /// <summary>
    ///     Converts our FeatureContext to OpenFeature EvaluationContext
    /// </summary>
    /// <param name="context">Feature context to convert</param>
    /// <returns>OpenFeature EvaluationContext</returns>
    public static OpenFeature.Model.EvaluationContext ToOpenFeatureContext(FeatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var builder = OpenFeature.Model.EvaluationContext.Builder();

        // Add core properties
        if (context.UserId.HasValue)
        {
            builder.Set("userId", context.UserId.Value.ToString());
        }

        if (context.TenantId.HasValue)
        {
            builder.Set("tenantId", context.TenantId.Value.ToString());
        }

        if (!string.IsNullOrEmpty(context.Environment))
        {
            builder.Set("environment", context.Environment);
        }

        if (!string.IsNullOrEmpty(context.IpAddress))
        {
            builder.Set("ipAddress", context.IpAddress);
        }

        if (!string.IsNullOrEmpty(context.UserAgent))
        {
            builder.Set("userAgent", context.UserAgent);
        }

        if (!string.IsNullOrEmpty(context.Country))
        {
            builder.Set("country", context.Country);
        }

        if (!string.IsNullOrEmpty(context.SubscriptionPlanId))
        {
            builder.Set("subscriptionPlanId", context.SubscriptionPlanId);
        }

        // Add user permissions as comma-separated string
        if (context.Permissions.Count > 0)
        {
            builder.Set("permissions", string.Join(",", context.Permissions));
        }

        // Add custom attributes
        foreach (var kvp in context.CustomAttributes)
        {
            var value = ConvertToOpenFeatureValue(kvp.Value);
            builder.Set(kvp.Key, value);
        }

        return builder.Build();
    }

    /// <summary>
    ///     Converts an object to OpenFeature Value type
    /// </summary>
    private static Value ConvertToOpenFeatureValue(object? obj)
    {
        return obj switch
        {
            null => new Value(string.Empty),
            string s => new Value(s),
            int i => new Value(i),
            long l => new Value(l),
            double d => new Value(d),
            bool b => new Value(b),
            DateTime dt => new Value(dt.ToString("O")), // ISO 8601 format
            _ => new Value(obj.ToString() ?? string.Empty)
        };
    }

    /// <summary>
    ///     Extracts user ID from HTTP context (implement based on your auth system)
    /// </summary>
    private static Guid? GetUserIdFromContext(HttpContext httpContext)
    {
        // Extract user ID from JWT claims ("sub" or "userId")
        var userIdClaim = httpContext.User.FindFirst("sub")?.Value ?? httpContext.User.FindFirst("userId")?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    /// <summary>
    ///     Extracts tenant ID from HTTP context (implement based on your multi-tenancy setup)
    /// </summary>
    private static Guid? GetTenantIdFromContext(HttpContext httpContext)
    {
        return Identity.Tenants.Utilities.TenantIdExtractor.FromHeader(httpContext);
    }

    /// <summary>
    ///     Extracts user permissions from HTTP context
    /// </summary>
    private static List<string> GetUserPermissionsFromContext(HttpContext httpContext)
    {
        var permissions = httpContext.User.Claims.Where(c => c.Type == "permission" || c.Type == "permissions").Select(c => c.Value).ToList();

        return permissions;
    }

    /// <summary>
    ///     Extracts country from HTTP context (from headers or IP geolocation)
    /// </summary>
    private static string? GetCountryFromContext(HttpContext httpContext)
    {
        // Try CloudFlare header first
        var country = httpContext.Request.Headers["CF-IPCountry"].FirstOrDefault();

        if (!string.IsNullOrEmpty(country))
        {
            return country;
        }

        // Try other common headers
        country = httpContext.Request.Headers["X-Country-Code"].FirstOrDefault();

        return country;
    }

    /// <summary>
    ///     Enriches a context with additional custom attributes
    /// </summary>
    public static FeatureContext Enrich(FeatureContext context, Dictionary<string, object> attributes)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(attributes);

        foreach (var kvp in attributes) { context.CustomAttributes[kvp.Key] = kvp.Value; }

        return context;
    }
}
