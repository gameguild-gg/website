namespace GameGuild.Identity.Authentication;

/// <summary>
/// Service interface for OAuth authentication: GitHub OAuth, Google OAuth, Google ID token, Discord OAuth
/// </summary>
public interface IOAuthAuthService
{
    Task<SignInResponse> GitHubSignInAsync(OAuthSignInRequest request, CancellationToken cancellationToken = default);

    Task<SignInResponse> GoogleSignInAsync(OAuthSignInRequest request, CancellationToken cancellationToken = default);

    Task<SignInResponse> MicrosoftSignInAsync(OAuthSignInRequest request, CancellationToken cancellationToken = default);

    Task<SignInResponse> GoogleIdTokenSignInAsync(GoogleIdTokenRequest request, CancellationToken cancellationToken = default);

    Task<SignInResponse> DiscordSignInAsync(DiscordSignInRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Completes a GitHub authorization-code sign-in: exchanges the authorization
    ///     code for an access token with the provider, resolves/creates the platform
    ///     user through the shared auto-link policy (recording granted scopes/consent),
    ///     and issues a persisted session.
    /// </summary>
    Task<SignInResponse> GitHubCallbackSignInAsync(GitHubCallbackSignInRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Completes a Steam OpenID 2.0 sign-in: verifies the signed callback assertion
    ///     with Steam, normalizes the player summary, and applies the shared
    ///     auto-link/JIT policy. Steam supplies no email claim; first-time users get an
    ///     internal unverified identifier (wallet sign-in policy).
    /// </summary>
    Task<SignInResponse> SteamCallbackSignInAsync(SteamCallbackSignInRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Completes an Epic Games OAuth 2.0 authorization-code sign-in (code exchange,
    ///     userInfo profile normalization, auto-link/JIT policy, granted-scope record).
    /// </summary>
    Task<SignInResponse> EpicCallbackSignInAsync(EpicCallbackSignInRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Completes a Facebook Graph OAuth 2.0 authorization-code sign-in (code
    ///     exchange, /me profile normalization, auto-link/JIT policy, granted-scope record).
    /// </summary>
    Task<SignInResponse> FacebookCallbackSignInAsync(FacebookCallbackSignInRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Completes a Microsoft OIDC v2 authorization-code sign-in (code exchange,
    ///     Graph userinfo normalization, auto-link/JIT policy, granted-scope record).
    /// </summary>
    Task<SignInResponse> MicrosoftCallbackSignInAsync(MicrosoftCallbackSignInRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Completes a generic OIDC federation sign-in: validates the provider ID token,
    ///     applies the same auto-link/JIT policy as the social providers, and enforces the
    ///     platform MFA policy fail-closed against the provider's <c>amr</c> proof.
    /// </summary>
    Task<SignInResponse> OidcSignInAsync(OidcSignInRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Completes a SAML 2.0 federation sign-in (Assertion Consumer Service): validates
    ///     the posted response (provider signature, issuer, audience, destination,
    ///     lifetimes, InResponseTo correlation, replay), applies the same auto-link/JIT
    ///     policy as the social providers, enforces the platform MFA policy fail-closed
    ///     against the assertion's AuthnContext, and binds the session to the provider's
    ///     tenant allowlist when one is configured.
    /// </summary>
    Task<SignInResponse> SamlSignInAsync(SamlSignInRequest request, CancellationToken cancellationToken = default);

    Task<string> GetGitHubAuthUrlAsync(string redirectUri);

    Task<string> GetGoogleAuthUrlAsync(string redirectUri);
}
