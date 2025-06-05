using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using cms.Modules.Auth.Dtos;
using cms.Modules.Auth.Services;
using cms.Modules.Auth.Attributes;

namespace cms.Modules.Auth.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("sign-in")]
        public async Task<ActionResult<SignInResponseDto>> LocalSignIn([FromBody] LocalSignInRequestDto request)
        {
            SignInResponseDto result = await _authService.LocalSignInAsync(request);

            return Ok(result);
        }

        [HttpPost("sign-up")]
        public async Task<ActionResult<SignInResponseDto>> LocalSignUp([FromBody] LocalSignUpRequestDto request)
        {
            SignInResponseDto result = await _authService.LocalSignUpAsync(request);

            return Ok(result);
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult<RefreshTokenResponseDto>> RefreshToken([FromBody] RefreshTokenRequestDto request)
        {
            RefreshTokenResponseDto result = await _authService.RefreshTokenAsync(request);

            return Ok(result);
        }

        [HttpPost("revoke-token")]
        public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenRequestDto request)
        {
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            await _authService.RevokeRefreshTokenAsync(request.RefreshToken, ipAddress);

            return Ok(
                new
                {
                    message = "Token revoked successfully"
                }
            );
        }

        [HttpGet("github/signin")]
        [Public]
        public async Task<IActionResult> GitHubSignIn(string redirectUri = "")
        {
            string authUrl = await _authService.GetGitHubAuthUrlAsync(redirectUri);

            return Ok(
                new
                {
                    AuthUrl = authUrl
                }
            );
        }

        [HttpPost("github/callback")]
        [Public]
        public async Task<IActionResult> GitHubCallback([FromBody] OAuthSignInRequestDto request)
        {
            try
            {
                SignInResponseDto result = await _authService.GitHubSignInAsync(request);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(
                    new
                    {
                        message = "GitHub authentication failed", error = ex.Message
                    }
                );
            }
        }

        [HttpGet("google/signin")]
        [Public]
        public async Task<IActionResult> GoogleSignIn(string redirectUri = "")
        {
            string authUrl = await _authService.GetGoogleAuthUrlAsync(redirectUri);

            return Ok(
                new
                {
                    AuthUrl = authUrl
                }
            );
        }

        [HttpPost("google/callback")]
        [Public]
        public async Task<IActionResult> GoogleCallback([FromBody] OAuthSignInRequestDto request)
        {
            try
            {
                SignInResponseDto result = await _authService.GoogleSignInAsync(request);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(
                    new
                    {
                        message = "Google authentication failed", error = ex.Message
                    }
                );
            }
        }

        [HttpPost("web3/challenge")]
        [Public]
        public async Task<ActionResult<Web3ChallengeResponseDto>> GenerateWeb3Challenge([FromBody] Web3ChallengeRequestDto request)
        {
            try
            {
                Web3ChallengeResponseDto result = await _authService.GenerateWeb3ChallengeAsync(request);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(
                    new
                    {
                        message = "Failed to generate Web3 challenge", error = ex.Message
                    }
                );
            }
        }

        [HttpPost("web3/verify")]
        [Public]
        public async Task<ActionResult<SignInResponseDto>> VerifyWeb3Signature([FromBody] Web3VerifyRequestDto request)
        {
            try
            {
                SignInResponseDto result = await _authService.VerifyWeb3SignatureAsync(request);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(
                    new
                    {
                        message = "Web3 authentication failed", error = ex.Message
                    }
                );
            }
        }

        [HttpPost("send-email-verification")]
        [Public]
        public async Task<ActionResult<EmailOperationResponseDto>> SendEmailVerification([FromBody] SendEmailVerificationRequestDto request)
        {
            EmailOperationResponseDto result = await _authService.SendEmailVerificationAsync(request);

            return Ok(result);
        }

        [HttpPost("verify-email")]
        [Public]
        public async Task<ActionResult<EmailOperationResponseDto>> VerifyEmail([FromBody] VerifyEmailRequestDto request)
        {
            EmailOperationResponseDto result = await _authService.VerifyEmailAsync(request);

            return Ok(result);
        }

        [HttpPost("forgot-password")]
        [Public]
        public async Task<ActionResult<EmailOperationResponseDto>> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        {
            EmailOperationResponseDto result = await _authService.ForgotPasswordAsync(request);

            return Ok(result);
        }

        [HttpPost("reset-password")]
        [Public]
        public async Task<ActionResult<EmailOperationResponseDto>> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            EmailOperationResponseDto result = await _authService.ResetPasswordAsync(request);

            return Ok(result);
        }

        [HttpPost("change-password")]
        [RequireRoles("User")]
        public async Task<ActionResult<EmailOperationResponseDto>> ChangePassword([FromBody] ChangePasswordRequestDto request)
        {
            Guid userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException("User ID not found"));
            EmailOperationResponseDto result = await _authService.ChangePasswordAsync(request, userId);

            return Ok(result);
        }
    }
}
