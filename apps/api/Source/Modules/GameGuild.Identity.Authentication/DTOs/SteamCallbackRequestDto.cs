using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Request body for the Steam OpenID callback. <see cref="Code" /> carries the
///     URL-encoded signed <c>openid.*</c> assertion from the Steam redirect.
/// </summary>
public class SteamCallbackRequestDto
{
    /// <summary>
    ///     URL-encoded signed OpenID 2.0 assertion (the full openid.* query string) from the Steam callback
    /// </summary>
    [Required]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    ///     CSRF state parameter (validated web-side against the signed state cookie)
    /// </summary>
    [Required]
    public string State { get; set; } = string.Empty;

    /// <summary>
    ///     The same return-to URL used in the OpenID redirect
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
///     Domain request for Steam OpenID callback sign-in
/// </summary>
public class SteamCallbackSignInRequest : SteamCallbackRequestDto;
