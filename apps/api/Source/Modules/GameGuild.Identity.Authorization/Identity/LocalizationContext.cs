using Microsoft.AspNetCore.Http;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Implementation of ILocalizationContext that extracts localization settings from HttpContext
/// </summary>
public class LocalizationContext(IHttpContextAccessor httpContextAccessor) : ILocalizationContext
{
    private const string CultureClaimType = "culture";
    private const string UICultureClaimType = "ui_culture";
    private const string TimeZoneClaimType = "timezone";
    private const string DateFormatClaimType = "date_format";
    private const string NumberFormatClaimType = "number_format";
    private const string AcceptLanguageHeader = "Accept-Language";

    public string? CultureCode
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            // Try claim first
            var cultureClaim = httpContext.User.FindFirst(CultureClaimType)?.Value;
            if (!string.IsNullOrEmpty(cultureClaim))
            {
                return cultureClaim;
            }

            // Fallback to Accept-Language header
            if (httpContext.Request.Headers.TryGetValue(AcceptLanguageHeader, out var acceptLanguage))
            {
                var firstLanguage = acceptLanguage.ToString().Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(firstLanguage))
                {
                    // Remove quality value if present (e.g., "en-US;q=0.9" -> "en-US")
                    return firstLanguage.Split(';')[0].Trim();
                }
            }

            return null;
        }
    }

    public string? UICultureCode
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            var uiCultureClaim = httpContext.User.FindFirst(UICultureClaimType)?.Value;
            return !string.IsNullOrEmpty(uiCultureClaim) ? uiCultureClaim : CultureCode;
        }
    }

    public string? TimeZone
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            return httpContext?.User.FindFirst(TimeZoneClaimType)?.Value;
        }
    }

    public string? DateFormat
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            return httpContext?.User.FindFirst(DateFormatClaimType)?.Value;
        }
    }

    public string? NumberFormat
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            return httpContext?.User.FindFirst(NumberFormatClaimType)?.Value;
        }
    }
}
