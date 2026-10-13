using System.ComponentModel.DataAnnotations;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Request body for the GitHub OAuth callback endpoint
/// </summary>
public class GitHubCallbackRequestDto
{
    /// <summary>
    ///     OAuth authorization code from the GitHub callback
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
///     Domain request for GitHub OAuth callback sign-in
/// </summary>
public class GitHubCallbackSignInRequest : GitHubCallbackRequestDto;
