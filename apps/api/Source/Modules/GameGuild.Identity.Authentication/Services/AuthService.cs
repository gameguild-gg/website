namespace GameGuild.Identity.Authentication;

/// <summary>
/// Composite implementation of IAuthService that delegates to focused sub-services.
/// Kept for backward compatibility with code that depends on IAuthService.
/// </summary>
public class AuthService(
    ILocalAuthService localAuthService,
    IOAuthAuthService oauthAuthService,
    IPasswordService passwordService,
    IWeb3AuthService web3AuthService
) : IAuthService
{
    // Local auth delegations
    public Task<SignInResponse> LocalSignInAsync(LocalSignInRequest request, CancellationToken cancellationToken = default) =>
        localAuthService.LocalSignInAsync(request, cancellationToken);

    public Task<SignInResponse> LocalSignUpAsync(LocalSignUpRequest request, CancellationToken cancellationToken = default) =>
        localAuthService.LocalSignUpAsync(request, cancellationToken);

    public Task<SignInResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default) =>
        localAuthService.RefreshTokenAsync(request, cancellationToken);

    public Task RevokeRefreshTokenAsync(string token, string ipAddress, CancellationToken cancellationToken = default) =>
        localAuthService.RevokeRefreshTokenAsync(token, ipAddress, cancellationToken);

    // OAuth delegations
    public Task<SignInResponse> GitHubSignInAsync(OAuthSignInRequest request, CancellationToken cancellationToken = default) =>
        oauthAuthService.GitHubSignInAsync(request, cancellationToken);

    public Task<SignInResponse> GoogleSignInAsync(OAuthSignInRequest request, CancellationToken cancellationToken = default) =>
        oauthAuthService.GoogleSignInAsync(request, cancellationToken);

    public Task<SignInResponse> MicrosoftSignInAsync(OAuthSignInRequest request, CancellationToken cancellationToken = default) =>
        oauthAuthService.MicrosoftSignInAsync(request, cancellationToken);

    public Task<SignInResponse> GoogleIdTokenSignInAsync(GoogleIdTokenRequest request, CancellationToken cancellationToken = default) =>
        oauthAuthService.GoogleIdTokenSignInAsync(request, cancellationToken);

    public Task<SignInResponse> DiscordSignInAsync(DiscordSignInRequest request, CancellationToken cancellationToken = default) =>
        oauthAuthService.DiscordSignInAsync(request, cancellationToken);

    public Task<SignInResponse> OidcSignInAsync(OidcSignInRequest request, CancellationToken cancellationToken = default) =>
        oauthAuthService.OidcSignInAsync(request, cancellationToken);

    public Task<string> GetGitHubAuthUrlAsync(string redirectUri) =>
        oauthAuthService.GetGitHubAuthUrlAsync(redirectUri);

    public Task<string> GetGoogleAuthUrlAsync(string redirectUri) =>
        oauthAuthService.GetGoogleAuthUrlAsync(redirectUri);

    // Password delegations
    public Task<EmailOperationResponse> SendEmailVerificationAsync(SendEmailVerificationRequest request, CancellationToken cancellationToken = default) =>
        passwordService.SendEmailVerificationAsync(request, cancellationToken);

    public Task<EmailOperationResponse> VerifyEmailAsync(EmailVerificationRequest verificationRequest, CancellationToken cancellationToken = default) =>
        passwordService.VerifyEmailAsync(verificationRequest, cancellationToken);

    public Task<EmailOperationResponse> ForgotPasswordAsync(PasswordResetRequest request, CancellationToken cancellationToken = default) =>
        passwordService.ForgotPasswordAsync(request, cancellationToken);

    public Task<EmailOperationResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default) =>
        passwordService.ResetPasswordAsync(request, cancellationToken);

    public Task<EmailOperationResponse> ChangePasswordAsync(ChangePasswordRequest request, Guid userId, CancellationToken cancellationToken = default) =>
        passwordService.ChangePasswordAsync(request, userId, cancellationToken);

    // Web3 delegations
    public Task<Web3ChallengeResponse> GenerateWeb3ChallengeAsync(Web3ChallengeRequest request, CancellationToken cancellationToken = default) =>
        web3AuthService.GenerateWeb3ChallengeAsync(request, cancellationToken);

    public Task<SignInResponse> VerifyWeb3SignatureAsync(Web3VerificationRequest request, CancellationToken cancellationToken = default) =>
        web3AuthService.VerifyWeb3SignatureAsync(request, cancellationToken);
}
