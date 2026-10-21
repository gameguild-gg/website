using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Command to initiate a SAML 2.0 federation provider sign-in flow (SP-initiated):
///     issues an AuthnRequest and returns the identity provider redirect URL with a
///     RelayState token for the caller's state cookie.
/// </summary>
public sealed record SamlSignInCommand : ICommand<SamlSignInResponse>
{
    /// <summary>
    ///     The configured federation provider slug (<c>Authentication:ExternalProviders:Saml:&lt;slug&gt;</c>).
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    ///     Optional RelayState the provider echoes on callback (defaults to a generated token).
    /// </summary>
    public string? RelayState { get; init; }
}

/// <summary>
///     Response for SAML federation sign-in initiation.
/// </summary>
public sealed record SamlSignInResponse
{
    /// <summary>
    ///     The identity provider's sign-on URL carrying the deflated AuthnRequest
    ///     (HTTP-Redirect binding) with the RelayState applied.
    /// </summary>
    public required string RedirectUrl { get; init; }

    /// <summary>
    ///     The RelayState token embedded in the redirect URL (returned separately so the
    ///     caller can stash it in its state cookie, mirroring the OIDC flow).
    /// </summary>
    public required string RelayState { get; init; }
}
