using Xunit;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GameGuild.Modules.Auth.Dtos;
using GameGuild.Modules.Auth.Services;

namespace GameGuild.Tests.Modules.Auth.Services
{
    public class JwtTokenServiceTests
    {
        private readonly JwtTokenService _jwtTokenService;
        private readonly IConfiguration _configuration;

        public JwtTokenServiceTests()
        {
            var configData = new Dictionary<string, string>
            {
                {"Jwt:Key", "your-very-long-secret-key-for-testing-purposes-at-least-256-bits"},
                {"Jwt:Issuer", "test-issuer"},
                {"Jwt:Audience", "test-audience"},
                {"Jwt:AccessTokenExpiryMinutes", "15"},
                {"Jwt:RefreshTokenExpiryDays", "7"}
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData!)
                .Build();

            _jwtTokenService = new JwtTokenService(_configuration);
        }

        [Fact]
        public void GenerateAccessToken_ValidUser_ReturnsValidJwtToken()
        {
            // Arrange
            var user = new UserDto
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                Email = "test@example.com"
            };
            var roles = new[] { "User", "Admin" };

            // Act
            string token = _jwtTokenService.GenerateAccessToken(user, roles);

            // Assert
            Assert.NotNull(token);
            Assert.NotEmpty(token);

            // Verify token structure
            var handler = new JwtSecurityTokenHandler();
            JwtSecurityToken? jsonToken = handler.ReadJwtToken(token);
            
            Assert.Equal("test-issuer", jsonToken.Issuer);
            Assert.Contains(jsonToken.Audiences, a => a == "test-audience");
            Assert.Equal(user.Id.ToString(), jsonToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
            Assert.Equal(user.Email, jsonToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
            Assert.Equal(user.Username, jsonToken.Claims.First(c => c.Type == "username").Value);
        }

        [Fact]
        public void ValidateToken_ValidToken_ReturnsClaimsPrincipal()
        {
            // Arrange
            var user = new UserDto
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                Email = "test@example.com"
            };
            var roles = new[] { "User" };
            string token = _jwtTokenService.GenerateAccessToken(user, roles);

            // Act
            ClaimsPrincipal? principal = _jwtTokenService.ValidateToken(token);

            // Assert
            Assert.NotNull(principal);
            Assert.True(principal.Identity?.IsAuthenticated);
            
            // Debug: Check all claims to understand what's available
            var allClaims = principal.Claims.ToList();
            Claim? subClaim = allClaims.FirstOrDefault(c => 
                c.Type == JwtRegisteredClaimNames.Sub || 
                c.Type == "sub" ||
                c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
            Claim? emailClaim = allClaims.FirstOrDefault(c => 
                c.Type == JwtRegisteredClaimNames.Email || 
                c.Type == "email" ||
                c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
            
            // Debug output - remove after fixing
            // foreach (var claim in allClaims)
            // {
            //     Console.WriteLine($"Claim Type: '{claim.Type}', Value: '{claim.Value}'");
            // }
            Assert.NotNull(subClaim);
            Assert.NotNull(emailClaim);
            Assert.Equal(user.Id.ToString(), subClaim.Value);
            Assert.Equal(user.Email, emailClaim.Value);
        }

        [Fact]
        public void ValidateToken_InvalidToken_ReturnsNull()
        {
            // Arrange
            var invalidToken = "invalid-token";

            // Act
            ClaimsPrincipal? principal = _jwtTokenService.ValidateToken(invalidToken);

            // Assert
            Assert.Null(principal);
        }

        [Fact]
        public void ValidateToken_ExpiredToken_ReturnsNull()
        {
            // Arrange - Create a token that expires immediately
            var expiredConfigData = new Dictionary<string, string>
            {
                {"Jwt:Key", "your-very-long-secret-key-for-testing-purposes-at-least-256-bits"},
                {"Jwt:Issuer", "test-issuer"},
                {"Jwt:Audience", "test-audience"},
                {"Jwt:AccessTokenExpiryMinutes", "-1"}, // Expired
                {"Jwt:RefreshTokenExpiryDays", "7"}
            };

            IConfigurationRoot expiredConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(expiredConfigData!)
                .Build();

            var expiredTokenService = new JwtTokenService(expiredConfig);

            var user = new UserDto
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                Email = "test@example.com"
            };
            var roles = new[] { "User" };
            string expiredToken = expiredTokenService.GenerateAccessToken(user, roles);

            // Act
            ClaimsPrincipal? principal = _jwtTokenService.ValidateToken(expiredToken);

            // Assert
            Assert.Null(principal);
        }

        [Fact]
        public void GetPrincipalFromExpiredToken_ExpiredToken_ReturnsClaimsPrincipal()
        {
            // Arrange - Create a token that expires immediately
            var expiredConfigData = new Dictionary<string, string>
            {
                {"Jwt:Key", "your-very-long-secret-key-for-testing-purposes-at-least-256-bits"},
                {"Jwt:Issuer", "test-issuer"},
                {"Jwt:Audience", "test-audience"},
                {"Jwt:AccessTokenExpiryMinutes", "-1"}, // Expired
                {"Jwt:RefreshTokenExpiryDays", "7"}
            };

            IConfigurationRoot expiredConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(expiredConfigData!)
                .Build();

            var expiredTokenService = new JwtTokenService(expiredConfig);

            var user = new UserDto
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                Email = "test@example.com"
            };
            var roles = new[] { "User" };
            string expiredToken = expiredTokenService.GenerateAccessToken(user, roles);

            // Act
            ClaimsPrincipal? principal = _jwtTokenService.GetPrincipalFromExpiredToken(expiredToken);

            // Assert
            Assert.NotNull(principal);
            Claim? subClaim = principal.Claims.FirstOrDefault(c => 
                c.Type == JwtRegisteredClaimNames.Sub || 
                c.Type == "sub" ||
                c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
            Claim? emailClaim = principal.Claims.FirstOrDefault(c => 
                c.Type == JwtRegisteredClaimNames.Email || 
                c.Type == "email" ||
                c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
            
            Assert.Equal(user.Id.ToString(), subClaim?.Value);
            Assert.Equal(user.Email, emailClaim?.Value);
        }

        [Fact]
        public void GenerateRefreshToken_ReturnsSecureToken()
        {
            // Act
            string refreshToken1 = _jwtTokenService.GenerateRefreshToken();
            string refreshToken2 = _jwtTokenService.GenerateRefreshToken();

            // Assert
            Assert.NotNull(refreshToken1);
            Assert.NotNull(refreshToken2);
            Assert.NotEmpty(refreshToken1);
            Assert.NotEmpty(refreshToken2);
            Assert.NotEqual(refreshToken1, refreshToken2); // Should be unique
            Assert.True(refreshToken1.Length >= 32); // Should be reasonably long
        }

        [Fact]
        public void GenerateAccessToken_MultipleRoles_IncludesAllRoles()
        {
            // Arrange
            var user = new UserDto
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                Email = "test@example.com"
            };
            var roles = new[] { "User", "Admin", "Moderator" };

            // Act
            string token = _jwtTokenService.GenerateAccessToken(user, roles);

            // Assert
            var handler = new JwtSecurityTokenHandler();
            JwtSecurityToken? jsonToken = handler.ReadJwtToken(token);
            
            var roleClaims = jsonToken.Claims.Where(c => c.Type == ClaimTypes.Role).ToArray();
            Assert.Equal(3, roleClaims.Length);
            Assert.Contains(roleClaims, c => c.Value == "User");
            Assert.Contains(roleClaims, c => c.Value == "Admin");
            Assert.Contains(roleClaims, c => c.Value == "Moderator");
        }

        [Fact]
        public void GenerateAccessToken_EmptyRoles_CreatesTokenWithoutRoles()
        {
            // Arrange
            var user = new UserDto
            {
                Id = Guid.NewGuid(),
                Username = "testuser",
                Email = "test@example.com"
            };
            string[] roles = Array.Empty<string>();

            // Act
            string token = _jwtTokenService.GenerateAccessToken(user, roles);

            // Assert
            Assert.NotNull(token);
            var handler = new JwtSecurityTokenHandler();
            JwtSecurityToken? jsonToken = handler.ReadJwtToken(token);
            
            var roleClaims = jsonToken.Claims.Where(c => c.Type == ClaimTypes.Role);
            Assert.Empty(roleClaims);
        }
    }
}
