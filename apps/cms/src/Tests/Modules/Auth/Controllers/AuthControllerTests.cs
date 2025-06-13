using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GameGuild.Modules.Auth.Commands;
using GameGuild.Modules.Auth.Controllers;
using GameGuild.Modules.Auth.Dtos;
using GameGuild.Modules.Auth.Services;
using MediatR;

namespace GameGuild.Tests.Modules.Auth.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<IAuthService> _mockAuthService;

        private readonly Mock<IMediator> _mockMediator;

        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _mockAuthService = new Mock<IAuthService>();
            _mockMediator = new Mock<IMediator>();
            _controller = new AuthController(_mockAuthService.Object, _mockMediator.Object);

            // Setup HTTP context
            var httpContext = new DefaultHttpContext
            {
                Connection =
                {
                    RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1")
                }
            };
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };
        }

        [Fact]
        public async Task LocalSignIn_ValidRequest_ReturnsOkWithSignInResponse()
        {
            // Arrange
            var request = new LocalSignInRequestDto
            {
                Email = "test@example.com", Password = "password123"
            };

            var expectedResponse = new SignInResponseDto
            {
                AccessToken = "mock-access-token",
                RefreshToken = "mock-refresh-token",
                User = new UserDto
                {
                    Email = "test@example.com", Username = "testuser"
                }
            };

            // Mock the MediatR response for the LocalSignInCommand
            _mockMediator.Setup(x => x.Send(It.IsAny<LocalSignInCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.LocalSignIn(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<SignInResponseDto>(okResult.Value);
            Assert.Equal("mock-access-token", response.AccessToken);
            Assert.Equal("test@example.com", response.User.Email);
        }

        [Fact]
        public async Task LocalSignUp_ValidRequest_ReturnsOkWithSignInResponse()
        {
            // Arrange
            var request = new LocalSignUpRequestDto
            {
                Email = "test@example.com", Password = "password123", Username = "testuser"
            };

            var expectedResponse = new SignInResponseDto
            {
                AccessToken = "mock-access-token",
                RefreshToken = "mock-refresh-token",
                User = new UserDto
                {
                    Email = "test@example.com", Username = "testuser"
                }
            };

            // Mock the MediatR response for the LocalSignUpCommand
            _mockMediator.Setup(x => x.Send(It.IsAny<LocalSignUpCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.LocalSignUp(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<SignInResponseDto>(okResult.Value);
            Assert.Equal("mock-access-token", response.AccessToken);
        }

        [Fact]
        public async Task RefreshToken_ValidRequest_ReturnsOkWithNewTokens()
        {
            // Arrange
            var request = new RefreshTokenRequestDto
            {
                RefreshToken = "valid-refresh-token"
            };

            var expectedResponse = new RefreshTokenResponseDto
            {
                AccessToken = "new-access-token", RefreshToken = "new-refresh-token"
            };

            _mockAuthService.Setup(x => x.RefreshTokenAsync(request))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.RefreshToken(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<RefreshTokenResponseDto>(okResult.Value);
            Assert.Equal("new-access-token", response.AccessToken);
            Assert.Equal("new-refresh-token", response.RefreshToken);
        }

        [Fact]
        public async Task RevokeToken_ValidRequest_ReturnsOkWithSuccessMessage()
        {
            // Arrange
            var request = new RefreshTokenRequestDto
            {
                RefreshToken = "valid-refresh-token"
            };

            _mockAuthService.Setup(x => x.RevokeRefreshTokenAsync(request.RefreshToken, It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            IActionResult result = await _controller.RevokeToken(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            object? response = okResult.Value;
            Assert.NotNull(response);
        }

        [Fact]
        public async Task GitHubSignIn_ValidRedirectUri_ReturnsOkWithAuthUrl()
        {
            // Arrange
            var redirectUri = "https://example.com/callback";
            string expectedAuthUrl = "https://github.com/login/oauth/authorize?client_id=test&redirect_uri=" + redirectUri;

            _mockAuthService.Setup(x => x.GetGitHubAuthUrlAsync(redirectUri))
                .ReturnsAsync(expectedAuthUrl);

            // Act
            IActionResult result = await _controller.GitHubSignIn(redirectUri);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            object? response = okResult.Value;
            Assert.NotNull(response);
        }

        [Fact]
        public async Task GitHubCallback_ValidRequest_ReturnsOkWithSignInResponse()
        {
            // Arrange
            var request = new OAuthSignInRequestDto
            {
                Code = "github-auth-code", RedirectUri = "https://example.com/callback"
            };

            var expectedResponse = new SignInResponseDto
            {
                AccessToken = "mock-access-token",
                RefreshToken = "mock-refresh-token",
                User = new UserDto
                {
                    Email = "github@example.com", Username = "githubuser"
                }
            };

            _mockAuthService.Setup(x => x.GitHubSignInAsync(request))
                .ReturnsAsync(expectedResponse);

            // Act
            IActionResult result = await _controller.GitHubCallback(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<SignInResponseDto>(okResult.Value);
            Assert.Equal("mock-access-token", response.AccessToken);
        }

        [Fact]
        public async Task GitHubCallback_ServiceThrowsException_ReturnsBadRequest()
        {
            // Arrange
            var request = new OAuthSignInRequestDto
            {
                Code = "invalid-code", RedirectUri = "https://example.com/callback"
            };

            _mockAuthService.Setup(x => x.GitHubSignInAsync(request))
                .ThrowsAsync(new Exception("OAuth failed"));

            // Act
            IActionResult result = await _controller.GitHubCallback(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task GenerateWeb3Challenge_ValidRequest_ReturnsOkWithChallenge()
        {
            // Arrange
            var request = new Web3ChallengeRequestDto
            {
                WalletAddress = "0x742d35Cc6634C0532925a3b8D".ToLower(), ChainId = "1"
            };

            var expectedResponse = new Web3ChallengeResponseDto
            {
                Challenge = "mock-challenge", Nonce = "mock-nonce", ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            };

            _mockAuthService.Setup(x => x.GenerateWeb3ChallengeAsync(request))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GenerateWeb3Challenge(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<Web3ChallengeResponseDto>(okResult.Value);
            Assert.Equal("mock-challenge", response.Challenge);
            Assert.Equal("mock-nonce", response.Nonce);
        }

        [Fact]
        public async Task VerifyWeb3Signature_ValidRequest_ReturnsOkWithSignInResponse()
        {
            // Arrange
            var request = new Web3VerifyRequestDto
            {
                WalletAddress = "0x742d35Cc6634C0532925a3b8D".ToLower(), Signature = "mock-signature", Nonce = "mock-nonce", ChainId = "1"
            };

            var expectedResponse = new SignInResponseDto
            {
                AccessToken = "mock-access-token",
                RefreshToken = "mock-refresh-token",
                User = new UserDto
                {
                    Email = "web3@example.com", Username = "web3user"
                }
            };

            _mockAuthService.Setup(x => x.VerifyWeb3SignatureAsync(request))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.VerifyWeb3Signature(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<SignInResponseDto>(okResult.Value);
            Assert.Equal("mock-access-token", response.AccessToken);
        }

        [Fact]
        public async Task VerifyWeb3Signature_ServiceThrowsException_ReturnsBadRequest()
        {
            // Arrange
            var request = new Web3VerifyRequestDto
            {
                WalletAddress = "0x742d35Cc6634C0532925a3b8D".ToLower(), Signature = "invalid-signature", Nonce = "mock-nonce", ChainId = "1"
            };

            _mockAuthService.Setup(x => x.VerifyWeb3SignatureAsync(request))
                .ThrowsAsync(new UnauthorizedAccessException("Invalid signature"));

            // Act
            var result = await _controller.VerifyWeb3Signature(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task SendEmailVerification_ValidRequest_ReturnsOk()
        {
            // Arrange
            var request = new SendEmailVerificationRequestDto
            {
                Email = "test@example.com"
            };

            var expectedResponse = new EmailOperationResponseDto
            {
                Success = true, Message = "Email verification sent"
            };

            _mockAuthService.Setup(x => x.SendEmailVerificationAsync(request))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.SendEmailVerification(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<EmailOperationResponseDto>(okResult.Value);
            Assert.True(response.Success);
        }

        [Fact]
        public async Task VerifyEmail_ValidToken_ReturnsOk()
        {
            // Arrange
            var request = new VerifyEmailRequestDto
            {
                Token = "valid-verification-token"
            };

            var expectedResponse = new EmailOperationResponseDto
            {
                Success = true, Message = "Email verified successfully"
            };

            _mockAuthService.Setup(x => x.VerifyEmailAsync(request))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.VerifyEmail(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<EmailOperationResponseDto>(okResult.Value);
            Assert.True(response.Success);
        }

        [Fact]
        public async Task ChangePassword_ValidRequest_ReturnsOk()
        {
            // Arrange
            var request = new ChangePasswordRequestDto
            {
                CurrentPassword = "oldpassword", NewPassword = "newpassword123"
            };

            var expectedResponse = new EmailOperationResponseDto
            {
                Success = true, Message = "Password changed successfully"
            };

            // Setup authenticated user
            var userId = Guid.NewGuid();
            var claims = new List<Claim>
            {
                new Claim("sub", userId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "test");
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext.HttpContext.User = principal;

            _mockAuthService.Setup(x => x.ChangePasswordAsync(request, userId))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.ChangePassword(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<EmailOperationResponseDto>(okResult.Value);
            Assert.True(response.Success);
        }
    }
}
