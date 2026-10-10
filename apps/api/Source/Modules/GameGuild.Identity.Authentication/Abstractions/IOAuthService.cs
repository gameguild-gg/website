namespace GameGuild.Identity.Authentication;

/// <summary>
///     Service for handling OAuth authentication flows with external providers.
///     Manages authorization URLs, callback handling, and token exchange.
/// </summary>
public interface IOAuthService
{
    /// <summary>
    ///     Generates the authorization URL for a specific OAuth provider.
    /// </summary>
    /// <param name="provider">The OAuth provider (Google, GitHub, etc.)</param>
    /// <param name="redirectUri">The callback URL after authentication</param>
    /// <param name="state">CSRF protection state parameter</param>
    /// <param name="scopes">Optional custom scopes to request</param>
    /// <returns>The complete authorization URL to redirect the user to</returns>
    Task<string> GetAuthorizationUrlAsync(string provider, string redirectUri, string state, string[ ]? scopes = null);

    /// <summary>
    ///     Resolves the exact scope list an authorization request for the provider will ask
    ///     for: explicitly requested scopes win, else the configured provider scopes, else
    ///     the provider's safe defaults (Microsoft always unions openid/email/profile).
    ///     Callback and link handlers persist this list as the granted scopes — the single
    ///     source of truth shared with <see cref="GetAuthorizationUrlAsync" />.
    /// </summary>
    /// <param name="provider">The OAuth provider (Google, GitHub, etc.)</param>
    /// <param name="requestedScopes">Optional caller-requested scopes</param>
    /// <returns>The scope tokens embedded in the authorization URL, order preserved</returns>
    string[] ResolveAuthorizationScopes(string provider, string[]? requestedScopes = null);

    /// <summary>
    ///     Handles the OAuth callback and exchanges authorization code for access token.
    /// </summary>
    /// <param name="provider">The OAuth provider</param>
    /// <param name="code">Authorization code from provider</param>
    /// <param name="state">State parameter for CSRF validation</param>
    /// <param name="redirectUri">The same redirect URI used in authorization request</param>
    /// <returns>OAuth user profile information</returns>
    Task<OAuthUserProfile> HandleCallbackAsync(string provider, string code, string state, string redirectUri);

    /// <summary>
    ///     Gets user profile information using an access token.
    /// </summary>
    /// <param name="provider">The OAuth provider</param>
    /// <param name="accessToken">The access token</param>
    /// <returns>User profile information</returns>
    Task<OAuthUserProfile> GetUserProfileAsync(string provider, string accessToken);

    /// <summary>
    ///     Revokes an OAuth token with the provider.
    /// </summary>
    /// <param name="provider">The OAuth provider</param>
    /// <param name="token">The token to revoke</param>
    /// <returns>True if revocation was successful</returns>
    Task<bool> RevokeTokenAsync(string provider, string token);
}
