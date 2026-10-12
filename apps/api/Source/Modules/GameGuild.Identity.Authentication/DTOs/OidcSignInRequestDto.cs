using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Request to initiate an OIDC federation provider sign-in
/// </summary>
public class OidcAuthorizeRequestDto
{
    /// <summary>
    ///     The redirect URI registered with the federation provider
    /// </summary>
    [Required]
    public string RedirectUri { get; set; } = string.Empty;
}

/// <summary>
///     Request body for the OIDC federation callback endpoint
/// </summary>
public class OidcCallbackRequestDto
{
    /// <summary>
    ///     OAuth authorization code returned by the federation provider
    /// </summary>
    [Required]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    ///     OAuth state parameter for CSRF protection (validated web-side against the signed state cookie)
    /// </summary>
    [Required]
    public string State { get; set; } = string.Empty;

    /// <summary>
    ///     The same redirect URI used in the authorization request
    /// </summary>
    [Required]
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    ///     Optional tenant context
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    ///     Whether the caller asked for a persistent ("remember me") refresh-token lifetime
    /// </summary>
    public bool? RememberMe { get; set; }
}

/// <summary>
///     Domain request for OIDC federation callback sign-in
/// </summary>
public class OidcSignInRequest
{
    /// <summary>
    ///     The configured federation provider slug (<c>Authentication:ExternalProviders:Oidc:&lt;slug&gt;</c>)
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public Guid? TenantId { get; set; }

    public bool? RememberMe { get; set; }
}

/// <summary>
///     Response for domain-to-provider discovery
/// </summary>
public sealed record OidcDiscoverProviderResponse
{
    /// <summary>
    ///     Federation providers whose configured email domains match the requested address
    /// </summary>
    public required IReadOnlyList<OidcDiscoveredProvider> Providers { get; init; }
}

/// <summary>
///     Response for OIDC logout forwarding
/// </summary>
public sealed record OidcEndSessionUrlResponse
{
    /// <summary>
    ///     The provider's end-session URL with the post-logout redirect applied; null when the
    ///     provider's discovery document does not advertise an end_session_endpoint.
    /// </summary>
    public string? EndSessionUrl { get; init; }
}
