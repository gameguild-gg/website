using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command to initiate an OIDC federation provider sign-in flow.
///     Discovers the provider's authorization endpoint and returns the redirect URL with a CSRF state parameter.
/// </summary>
public sealed record OidcSignInCommand : ICommand<OidcSignInResponse>
{
    /// <summary>
    ///     The configured federation provider slug (<c>Authentication:ExternalProviders:Oidc:&lt;slug&gt;</c>).
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    ///     The redirect URI after provider authentication.
    /// </summary>
    public required string RedirectUri { get; init; }
}

/// <summary>
///     Response for OIDC federation sign-in initiation
/// </summary>
public sealed record OidcSignInResponse
{
    /// <summary>
    ///     The provider's authorization URL with client, redirect, scope, and state parameters applied
    /// </summary>
    public required string AuthUrl { get; init; }

    /// <summary>
    ///     CSRF state parameter embedded in the authorization URL (also returned separately
    ///     so the caller can stash it in its state cookie)
    /// </summary>
    public required string State { get; init; }
}
