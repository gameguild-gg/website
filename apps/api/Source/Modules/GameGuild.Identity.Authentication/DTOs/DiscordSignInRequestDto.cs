using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Request to initiate Discord OAuth sign-in
/// </summary>
public class DiscordAuthorizeRequest
{
    /// <summary>
    ///     The redirect URI registered for the Discord application
    /// </summary>
    [Required]
    public string RedirectUri { get; set; } = string.Empty;
}

/// <summary>
///     Request body for the Discord OAuth callback endpoint
/// </summary>
public class DiscordCallbackRequestDto
{
    /// <summary>
    ///     OAuth authorization code from the Discord callback
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
///     Domain request for Discord OAuth callback sign-in
/// </summary>
public class DiscordSignInRequest : DiscordCallbackRequestDto;
