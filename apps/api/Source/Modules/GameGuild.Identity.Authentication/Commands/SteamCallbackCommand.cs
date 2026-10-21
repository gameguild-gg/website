using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command to handle the Steam OpenID callback: verify the signed assertion
///     with Steam and complete sign-in
/// </summary>
public class SteamCallbackCommand : ICommand<SignInResponse>
{
    /// <summary>
    ///     URL-encoded signed OpenID 2.0 assertion (the full openid.* query string) from the Steam callback
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    ///     OAuth state parameter for CSRF protection (validated web-side)
    /// </summary>
    public string State { get; init; } = string.Empty;

    /// <summary>
    ///     The same return-to URL used in the OpenID redirect
    /// </summary>
    public string RedirectUri { get; init; } = string.Empty;

    /// <summary>
    ///     Optional tenant context
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    ///     When true, issues a persistent ("remember me") refresh token using the persistent lifetime.
    /// </summary>
    public bool? RememberMe { get; init; }
}
