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
