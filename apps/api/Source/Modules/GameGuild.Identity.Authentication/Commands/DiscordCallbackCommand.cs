using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command to handle Discord OAuth callback: exchange the authorization code
///     for tokens and complete sign-in
/// </summary>
public class DiscordCallbackCommand : ICommand<SignInResponse>
{
    /// <summary>
    ///     OAuth authorization code from the Discord callback
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    ///     OAuth state parameter for CSRF protection (validated web-side)
    /// </summary>
    public string State { get; init; } = string.Empty;

    /// <summary>
    ///     The same redirect URI used in the authorization request
    /// </summary>
    public string RedirectUri { get; init; } = string.Empty;

    /// <summary>
    ///     Optional tenant context
    /// </summary>
    public Guid? TenantId { get; init; }
}
