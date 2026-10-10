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
    ///     Completes a generic OIDC federation sign-in: validates the provider ID token,
    ///     applies the same auto-link/JIT policy as the social providers, and enforces the
    ///     platform MFA policy fail-closed against the provider's <c>amr</c> proof.
    /// </summary>
    Task<SignInResponse> OidcSignInAsync(OidcSignInRequest request, CancellationToken cancellationToken = default);

    Task<string> GetGitHubAuthUrlAsync(string redirectUri);

    Task<string> GetGoogleAuthUrlAsync(string redirectUri);
}
