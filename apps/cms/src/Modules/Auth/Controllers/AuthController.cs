using Microsoft.AspNetCore.Mvc;
using GameGuild.Modules.Auth.Attributes;
using GameGuild.Modules.Auth.Commands;
using GameGuild.Modules.Auth.Dtos;
using GameGuild.Modules.Auth.Services;
using MediatR;

namespace GameGuild.Modules.Auth.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController(IAuthService authService, IMediator mediator) : ControllerBase
    {
        [HttpPost("sign-in")]
        public async Task<ActionResult<SignInResponseDto>> LocalSignIn([FromBody] LocalSignInRequestDto request)
        {
            try
            {
                var command = new LocalSignInCommand
                {
                    Email = request.Email, Password = request.Password
                };

                SignInResponseDto result = await mediator.Send(command);

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(
                    new
                    {
                        message = ex.Message
                    }
                );
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        message = "Internal server error", details = ex.Message
                    }
                );
            }
        }

        [HttpPost("sign-up")]
        public async Task<ActionResult<SignInResponseDto>> LocalSignUp([FromBody] LocalSignUpRequestDto request)
        {
            try
            {
                var command = new LocalSignUpCommand
                {
                    Email = request.Email, Password = request.Password, Username = request.Username!
                };

                SignInResponseDto result = await mediator.Send(command);

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(
                    new
                    {
                        message = ex.Message
                    }
                );
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        message = "Internal server error", details = ex.Message
                    }
                );
            }
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult<RefreshTokenResponseDto>> RefreshToken([FromBody] RefreshTokenRequestDto request)
        {
            RefreshTokenResponseDto result = await authService.RefreshTokenAsync(request);

            return Ok(result);
        }

        [HttpPost("revoke-token")]
        public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenRequestDto request)
        {
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            await authService.RevokeRefreshTokenAsync(request.RefreshToken, ipAddress);

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
            string authUrl = await authService.GetGitHubAuthUrlAsync(redirectUri);

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
                SignInResponseDto result = await authService.GitHubSignInAsync(request);

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
            string authUrl = await authService.GetGoogleAuthUrlAsync(redirectUri);

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
                SignInResponseDto result = await authService.GoogleSignInAsync(request);

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
                Web3ChallengeResponseDto result = await authService.GenerateWeb3ChallengeAsync(request);

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
                SignInResponseDto result = await authService.VerifyWeb3SignatureAsync(request);

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
            EmailOperationResponseDto result = await authService.SendEmailVerificationAsync(request);

            return Ok(result);
        }

        [HttpPost("verify-email")]
        [Public]
        public async Task<ActionResult<EmailOperationResponseDto>> VerifyEmail([FromBody] VerifyEmailRequestDto request)
        {
            EmailOperationResponseDto result = await authService.VerifyEmailAsync(request);

            return Ok(result);
        }

        [HttpPost("forgot-password")]
        [Public]
        public async Task<ActionResult<EmailOperationResponseDto>> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        {
            EmailOperationResponseDto result = await authService.ForgotPasswordAsync(request);

            return Ok(result);
        }

        [HttpPost("reset-password")]
        [Public]
        public async Task<ActionResult<EmailOperationResponseDto>> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            EmailOperationResponseDto result = await authService.ResetPasswordAsync(request);

            return Ok(result);
        }

        [HttpPost("change-password")]
        public async Task<ActionResult<EmailOperationResponseDto>> ChangePassword([FromBody] ChangePasswordRequestDto request)
        {
            Guid userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException("User ID not found"));
            EmailOperationResponseDto result = await authService.ChangePasswordAsync(request, userId);

            return Ok(result);
        }
    }
}
