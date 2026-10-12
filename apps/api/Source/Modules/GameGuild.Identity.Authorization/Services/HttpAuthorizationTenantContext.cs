using Microsoft.AspNetCore.Http;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     HTTP context-based implementation of authorization tenant context.
/// </summary>
/// <remarks>
///     <para>
///         This implementation reads tenant ID from HttpContext.Items with fallback behavior:
///         1. First checks "AuthorizationTenantId" (set explicitly for authorization)
///         2. Falls back to "TenantId" (set by TenantMiddleware)
///     </para>
///     <para>
///         <b>SECURITY:</b> Uses Guid? consistently to prevent type confusion attacks.
///         Invalid GUIDs result in null (fail-closed) rather than Guid.Empty.
///     </para>
/// </remarks>
public sealed class HttpAuthorizationTenantContext : IAuthorizationTenantContext
{
    private const string PrimaryTenantIdKey = "AuthorizationTenantId";
    private const string FallbackTenantIdKey = "TenantId";
    
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    ///     Initializes a new instance of <see cref="HttpAuthorizationTenantContext"/>.
    /// </summary>
    public HttpAuthorizationTenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid? TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return null;
            }

            // Try primary key first (explicitly set for authorization)
            if (httpContext.Items.TryGetValue(PrimaryTenantIdKey, out var primaryValue))
            {
                if (primaryValue is Guid guidValue)
                {
                    return guidValue;
                }

                if (primaryValue is string strValue)
                {
                    // SECURITY: Don't accept Guid.Empty as valid tenant
                    if (TryParseTenantId(strValue, out var parsedPrimaryTenantId))
                    {
                        return parsedPrimaryTenantId;
                    }
                }
            }
            
            // Fallback to TenantMiddleware key
            if (httpContext.Items.TryGetValue(FallbackTenantIdKey, out var fallbackValue))
            {
                if (fallbackValue is Guid fallbackGuid)
                {
                    return fallbackGuid != Guid.Empty ? fallbackGuid : null;
                }

                if (fallbackValue is string fallbackStr)
                {
                    if (TryParseTenantId(fallbackStr, out var parsedFallbackTenantId))
                    {
                        return parsedFallbackTenantId;
                    }
                }
            }
            
            return null;
        }
    }

    private static bool TryParseTenantId(string value, out Guid? tenantId)
    {
        if (!Guid.TryParse(value, out var parsedGuid))
        {
            tenantId = null;
            return false;
        }

        tenantId = parsedGuid != Guid.Empty ? parsedGuid : null;
        return true;
    }

    /// <summary>
    ///     Sets the tenant ID for the current request.
    /// </summary>
    /// <param name="tenantId">The tenant ID to set.</param>
    public void SetTenantId(Guid? tenantId)
    {
        if (_httpContextAccessor.HttpContext is not null)
        {
            _httpContextAccessor.HttpContext.Items[PrimaryTenantIdKey] = tenantId;
        }
    }
}
