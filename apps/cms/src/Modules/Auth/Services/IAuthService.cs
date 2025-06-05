using System.Threading.Tasks;
using cms.Modules.Auth.Dtos;

namespace cms.Modules.Auth.Services
{
    public interface IAuthService
    {
        Task<SignInResponseDto> LocalSignInAsync(LocalSignInRequestDto request);

        Task<SignInResponseDto> LocalSignUpAsync(LocalSignUpRequestDto request);

        Task<RefreshTokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);

        Task RevokeRefreshTokenAsync(string token, string ipAddress);

        Task<SignInResponseDto> GitHubSignInAsync(OAuthSignInRequestDto request);

        Task<SignInResponseDto> GoogleSignInAsync(OAuthSignInRequestDto request);

        Task<string> GetGitHubAuthUrlAsync(string redirectUri);

        Task<string> GetGoogleAuthUrlAsync(string redirectUri);

        Task<Web3ChallengeResponseDto> GenerateWeb3ChallengeAsync(Web3ChallengeRequestDto request);

        Task<SignInResponseDto> VerifyWeb3SignatureAsync(Web3VerifyRequestDto request);

        Task<EmailOperationResponseDto> SendEmailVerificationAsync(SendEmailVerificationRequestDto request);

        Task<EmailOperationResponseDto> VerifyEmailAsync(VerifyEmailRequestDto request);

        Task<EmailOperationResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto request);

        Task<EmailOperationResponseDto> ResetPasswordAsync(ResetPasswordRequestDto request);

        Task<EmailOperationResponseDto> ChangePasswordAsync(ChangePasswordRequestDto request, Guid userId);
    }
}
