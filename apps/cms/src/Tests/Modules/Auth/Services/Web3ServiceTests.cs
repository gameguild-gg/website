using Xunit;
using Microsoft.EntityFrameworkCore;
using Moq;
using GameGuild.Data;
using GameGuild.Modules.Auth.Dtos;
using GameGuild.Modules.Auth.Services;
using GameGuild.Modules.User.Models;

namespace GameGuild.Tests.Modules.Auth.Services
{
    public class Web3ServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<ILogger<Web3Service>> _mockLogger;
        private readonly Web3Service _web3Service;

        public Web3ServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            
            _context = new ApplicationDbContext(options);
            _mockLogger = new Mock<ILogger<Web3Service>>();
            _web3Service = new Web3Service(_context, _mockLogger.Object);
        }

        [Fact]
        public async Task GenerateChallengeAsync_ValidAddress_ReturnsChallenge()
        {
            // Arrange
            var request = new Web3ChallengeRequestDto
            {
                WalletAddress = "0x742d35Cc6634C0532925a3b8D7fE0a26cfEb00dC".ToLower(),
                ChainId = "1"
            };

            // Act
            Web3ChallengeResponseDto result = await _web3Service.GenerateChallengeAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Challenge);
            Assert.NotEmpty(result.Nonce);
            Assert.True(result.ExpiresAt > DateTime.UtcNow);
            Assert.Contains(request.WalletAddress, result.Challenge);
        }

        [Fact]
        public async Task GenerateChallengeAsync_InvalidAddress_ThrowsArgumentException()
        {
            // Arrange
            var request = new Web3ChallengeRequestDto
            {
                WalletAddress = "invalid-address",
                ChainId = "1"
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _web3Service.GenerateChallengeAsync(request)
            );
        }

        [Theory]
        [InlineData("0x742d35Cc6634C0532925a3b8D7fE0a26cfEb00dC")]
        [InlineData("0x742d35cc6634c0532925a3b8d7fe0a26cfeb00dc")]
        [InlineData("0x0000000000000000000000000000000000000000")]
        [InlineData("0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
        public void IsValidEthereumAddress_ValidAddress_ReturnsTrue(string address)
        {
            // Act
            bool result = _web3Service.IsValidEthereumAddress(address);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("invalid")]
        [InlineData("0x")]
        [InlineData("0x123")]
        [InlineData("742d35Cc6634C0532925a3b8D7fE0a26cfEb00dC")]
        [InlineData("0x742d35Cc6634C0532925a3b8D7fE0a26cfEb00dCG")]
        [InlineData("0x742d35Cc6634C0532925a3b8D7fE0a26cfEb00dC1")]
        public void IsValidEthereumAddress_InvalidAddress_ReturnsFalse(string address)
        {
            // Act
            bool result = _web3Service.IsValidEthereumAddress(address);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task VerifySignatureAsync_MockImplementation_ReturnsTrue()
        {
            // Arrange
            string walletAddress = "0x742d35Cc6634C0532925a3b8D7fE0a26cfEb00dC".ToLower();
            
            // First generate a challenge
            var challengeRequest = new Web3ChallengeRequestDto
            {
                WalletAddress = walletAddress
            };
            Web3ChallengeResponseDto challenge = await _web3Service.GenerateChallengeAsync(challengeRequest);
            
            // Now create the verify request with the generated nonce
            var verifyRequest = new Web3VerifyRequestDto
            {
                WalletAddress = walletAddress,
                Signature = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef1b", // 132 chars
                Nonce = challenge.Nonce,
                ChainId = "1"
            };

            // Act
            bool result = await _web3Service.VerifySignatureAsync(verifyRequest);

            // Assert
            // Note: This should return true for the mock implementation
            // In a real implementation, this would verify the actual signature
            Assert.True(result);
        }

        [Fact]
        public async Task FindOrCreateWeb3UserAsync_NewWallet_CreatesUserAndCredential()
        {
            // Arrange
            string walletAddress = "0x742d35Cc6634C0532925a3b8D7fE0a26cfEb00dC".ToLower();
            var chainId = "1";

            // Act
            User user = await _web3Service.FindOrCreateWeb3UserAsync(walletAddress, chainId);

            // Assert
            Assert.NotNull(user);
            Assert.Equal($"User_{walletAddress[..8]}...", user.Name);
            Assert.Equal($"{walletAddress}@web3.local", user.Email);

            // Verify user was saved to database
            User? savedUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            Assert.NotNull(savedUser);

            // Verify credential was created
            Credential? credential = await _context.Credentials.FirstOrDefaultAsync(c => c.UserId == user.Id);
            Assert.NotNull(credential);
            Assert.Equal("web3_wallet", credential.Type);
            Assert.Equal(walletAddress, credential.Value);
            Assert.True(credential.IsActive);
        }

        [Fact]
        public async Task FindOrCreateWeb3UserAsync_ExistingWallet_ReturnsExistingUser()
        {
            // Arrange
            string walletAddress = "0x742d35Cc6634C0532925a3b8D7fE0a26cfEb00dC".ToLower();
            var chainId = "1";

            // Create existing user first
            var existingUser = new User
            {
                Name = "Existing Web3 User",
                Email = $"{walletAddress}@web3.local"
            };
            _context.Users.Add(existingUser);
            await _context.SaveChangesAsync();

            var existingCredential = new Credential
            {
                UserId = existingUser.Id,
                Type = "web3_wallet",
                Value = walletAddress,
                IsActive = true
            };
            _context.Credentials.Add(existingCredential);
            await _context.SaveChangesAsync();

            // Act
            User user = await _web3Service.FindOrCreateWeb3UserAsync(walletAddress, chainId);

            // Assert
            Assert.NotNull(user);
            Assert.Equal(existingUser.Id, user.Id);
            Assert.Equal("Existing Web3 User", user.Name);

            // Verify no duplicate users were created
            int userCount = await _context.Users.CountAsync(u => u.Email == $"{walletAddress}@web3.local");
            Assert.Equal(1, userCount);
        }

        [Fact]
        public async Task FindOrCreateWeb3UserAsync_MultipleChains_ReturnsSameUser()
        {
            // Arrange
            string walletAddress = "0x742d35Cc6634C0532925a3b8D7fE0a26cfEb00dC".ToLower();
            var chainId1 = "1"; // Ethereum mainnet
            var chainId2 = "137"; // Polygon

            // Act
            User user1 = await _web3Service.FindOrCreateWeb3UserAsync(walletAddress, chainId1);
            User user2 = await _web3Service.FindOrCreateWeb3UserAsync(walletAddress, chainId2);

            // Assert
            Assert.NotNull(user1);
            Assert.NotNull(user2);
            Assert.Equal(user1.Id, user2.Id); // Should be the same user

            // Verify only one credential exists (current implementation behavior)
            var credentials = await _context.Credentials
                .Where(c => c.UserId == user1.Id && c.Type == "web3_wallet")
                .ToListAsync();

            Assert.Equal(1, credentials.Count);
            Assert.Equal(walletAddress, credentials.First().Value);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
