using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using GameGuild.Data;
using GameGuild.Modules.Auth.Dtos;
using GameGuild.Modules.Auth.Services;
using GameGuild.Modules.User.Models;

namespace GameGuild.Tests.Modules.Auth.Services
{
    public class AuthServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IJwtTokenService> _mockJwtService;
        private readonly Mock<IOAuthService> _mockOAuthService;
        private readonly Mock<IWeb3Service> _mockWeb3Service;
        private readonly Mock<IEmailVerificationService> _mockEmailService;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            
            _context = new ApplicationDbContext(options);
            _mockJwtService = new Mock<IJwtTokenService>();
            _mockOAuthService = new Mock<IOAuthService>();
            _mockWeb3Service = new Mock<IWeb3Service>();
            _mockEmailService = new Mock<IEmailVerificationService>();
            _mockConfiguration = new Mock<IConfiguration>();

            _authService = new AuthService(
                _context,
                _mockJwtService.Object,
                _mockOAuthService.Object,
                _mockConfiguration.Object,
                _mockWeb3Service.Object,
                _mockEmailService.Object
            );
        }

        [Fact]
        public async Task LocalSignUpAsync_ValidRequest_CreatesUserAndCredential()
        {
            // Arrange
            var request = new LocalSignUpRequestDto
            {
                Email = "test@example.com",
                Password = "password123",
                Username = "testuser"
            };

            _mockJwtService.Setup(x => x.GenerateAccessToken(It.IsAny<UserDto>(), It.IsAny<string[]>()))
                .Returns("mock-access-token");

            // Act
            SignInResponseDto result = await _authService.LocalSignUpAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("mock-access-token", result.AccessToken);
            Assert.Equal("test@example.com", result.User.Email);
            
            User? user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            Assert.NotNull(user);
            
            Credential? credential = await _context.Credentials.FirstOrDefaultAsync(c => c.UserId == user.Id);
            Assert.NotNull(credential);
            Assert.Equal("password", credential.Type);
        }

        [Fact]
        public async Task LocalSignUpAsync_DuplicateEmail_ThrowsInvalidOperationException()
        {
            // Arrange
            var existingUser = new User
            {
                Email = "test@example.com",
                Name = "Existing User"
            };
            _context.Users.Add(existingUser);
            await _context.SaveChangesAsync();

            var request = new LocalSignUpRequestDto
            {
                Email = "test@example.com",
                Password = "password123",
                Username = "testuser"
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _authService.LocalSignUpAsync(request)
            );
        }

        [Fact]
        public async Task LocalSignInAsync_ValidCredentials_ReturnsSignInResponse()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                Name = "Test User"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            string hashedPassword = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes("password123")
                )
            );
            var credential = new Credential
            {
                UserId = user.Id,
                Type = "password",
                Value = hashedPassword,
                IsActive = true
            };
            _context.Credentials.Add(credential);
            await _context.SaveChangesAsync();

            var request = new LocalSignInRequestDto
            {
                Email = "test@example.com",
                Password = "password123"
            };

            _mockJwtService.Setup(x => x.GenerateAccessToken(It.IsAny<UserDto>(), It.IsAny<string[]>()))
                .Returns("mock-access-token");

            // Act
            SignInResponseDto result = await _authService.LocalSignInAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("mock-access-token", result.AccessToken);
            Assert.Equal("test@example.com", result.User.Email);
        }

        [Fact]
        public async Task LocalSignInAsync_InvalidCredentials_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var request = new LocalSignInRequestDto
            {
                Email = "nonexistent@example.com",
                Password = "password123"
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _authService.LocalSignInAsync(request)
            );
        }

        [Fact]
        public async Task LocalSignInAsync_InvalidPassword_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var user = new User
            {
                Email = "test@example.com",
                Name = "Test User"
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            string hashedPassword = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes("correctpassword")
                )
            );
            var credential = new Credential
            {
                UserId = user.Id,
                Type = "password",
                Value = hashedPassword,
                IsActive = true
            };
            _context.Credentials.Add(credential);
            await _context.SaveChangesAsync();

            var request = new LocalSignInRequestDto
            {
                Email = "test@example.com",
                Password = "wrongpassword"
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _authService.LocalSignInAsync(request)
            );
        }

        [Fact]
        public async Task GenerateWeb3ChallengeAsync_ValidRequest_ReturnsChallenge()
        {
            // Arrange
            var request = new Web3ChallengeRequestDto
            {
                WalletAddress = "0x742d35Cc6634C0532925a3b8D".ToLower(),
                ChainId = "1"
            };

            var expectedResponse = new Web3ChallengeResponseDto
            {
                Challenge = "mock-challenge",
                Nonce = "mock-nonce",
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            };

            _mockWeb3Service.Setup(x => x.GenerateChallengeAsync(request))
                .ReturnsAsync(expectedResponse);

            // Act
            Web3ChallengeResponseDto result = await _authService.GenerateWeb3ChallengeAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("mock-challenge", result.Challenge);
            Assert.Equal("mock-nonce", result.Nonce);
        }

        [Fact]
        public async Task VerifyWeb3SignatureAsync_ValidSignature_ReturnsSignInResponse()
        {
            // Arrange
            var request = new Web3VerifyRequestDto
            {
                WalletAddress = "0x742d35Cc6634C0532925a3b8D".ToLower(),
                Signature = "mock-signature",
                Nonce = "mock-nonce",
                ChainId = "1"
            };

            var user = new User
            {
                Email = "web3user@example.com",
                Name = "Web3 User"
            };

            _mockWeb3Service.Setup(x => x.VerifySignatureAsync(request))
                .ReturnsAsync(true);
            
            _mockWeb3Service.Setup(x => x.FindOrCreateWeb3UserAsync(request.WalletAddress, request.ChainId))
                .ReturnsAsync(user);

            _mockJwtService.Setup(x => x.GenerateAccessToken(It.IsAny<UserDto>(), It.IsAny<string[]>()))
                .Returns("mock-access-token");
            
            _mockJwtService.Setup(x => x.GenerateRefreshToken())
                .Returns("mock-refresh-token");

            // Act
            SignInResponseDto result = await _authService.VerifyWeb3SignatureAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("mock-access-token", result.AccessToken);
            Assert.Equal("mock-refresh-token", result.RefreshToken);
        }

        [Fact]
        public async Task VerifyWeb3SignatureAsync_InvalidSignature_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var request = new Web3VerifyRequestDto
            {
                WalletAddress = "0x742d35Cc6634C0532925a3b8D".ToLower(),
                Signature = "invalid-signature",
                Nonce = "mock-nonce",
                ChainId = "1"
            };

            _mockWeb3Service.Setup(x => x.VerifySignatureAsync(request))
                .ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _authService.VerifyWeb3SignatureAsync(request)
            );
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
