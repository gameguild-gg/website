using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Request body for the Facebook OAuth callback endpoint
/// </summary>
public class FacebookCallbackRequestDto
{
    /// <summary>
    ///     OAuth authorization code from the Facebook callback
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
    ///     When true, issues a persistent ("remember me") refresh token using the persistent lifetime.
    /// </summary>
    public bool? RememberMe { get; set; }
}

/// <summary>
///     Domain request for Facebook OAuth callback sign-in
/// </summary>
public class FacebookCallbackSignInRequest : FacebookCallbackRequestDto;
