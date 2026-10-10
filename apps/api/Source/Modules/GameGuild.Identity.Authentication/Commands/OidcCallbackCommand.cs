using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command to handle an OIDC federation provider callback: exchange the authorization
///     code at the discovered token endpoint, validate the returned ID token, and complete
///     sign-in with the shared external-identity auto-link/JIT policy.
/// </summary>
public sealed class OidcCallbackCommand : ICommand<SignInResponse>
{
    /// <summary>
    ///     The configured federation provider slug.
    /// </summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>
    ///     OAuth authorization code from the provider callback
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

    /// <summary>
    ///     Whether the caller asked for a persistent ("remember me") refresh-token lifetime
    /// </summary>
    public bool? RememberMe { get; init; }
}
