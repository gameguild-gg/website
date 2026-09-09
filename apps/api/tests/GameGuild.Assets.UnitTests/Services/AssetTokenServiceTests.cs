using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using GameGuild.Assets;

namespace GameGuild.Assets.UnitTests.Services;

public class AssetTokenServiceTests
{
    private readonly AssetTokenOptions _options;
    private readonly AssetTokenService _service;
    private readonly byte[] _secretKey;

    public AssetTokenServiceTests()
    {
        // Generate a proper 32-byte secret key for testing
        _secretKey = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(_secretKey);
        }
        
        _options = new AssetTokenOptions
        {
            SecretKey = Convert.ToBase64String(_secretKey),
            DefaultExpiryHours = 24,
            TimeWindowHours = 8
        };

        _service = new AssetTokenService(Options.Create(_options));
    }

    #region GenerateToken Tests

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        // Arrange
        var assetReferenceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        var token = _service.GenerateToken(
            assetReferenceId,
            tenantId,
            AssetAccessPolicy.Public);

        // Assert
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateToken_DifferentAssets_ProduceDifferentTokens()
    {
        // Arrange
        var assetId1 = Guid.NewGuid();
        var assetId2 = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        var token1 = _service.GenerateToken(assetId1, tenantId, AssetAccessPolicy.Public);
        var token2 = _service.GenerateToken(assetId2, tenantId, AssetAccessPolicy.Public);

        // Assert
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GenerateToken_DifferentTenants_ProduceDifferentTokens()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var tenantId1 = Guid.NewGuid();
        var tenantId2 = Guid.NewGuid();

        // Act
        var token1 = _service.GenerateToken(assetId, tenantId1, AssetAccessPolicy.Public);
        var token2 = _service.GenerateToken(assetId, tenantId2, AssetAccessPolicy.Public);

        // Assert
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GenerateToken_WithTransformation_ReturnsToken()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var transformation = new TransformationSpec
        {
            Width = 100,
            Height = 100,
            Format = ImageFormat.Webp,
            Quality = 90
        };

        // Act
        var token = _service.GenerateToken(
            assetId,
            tenantId,
            AssetAccessPolicy.SignedUrl,
            transformation);

        // Assert
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateToken_WithCustomExpiry_ReturnsToken()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var customExpiry = TimeSpan.FromMinutes(30);

        // Act
        var token = _service.GenerateToken(
            assetId,
            tenantId,
            AssetAccessPolicy.SignedUrl,
            null,
            customExpiry);

        // Assert
        token.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region ValidateToken Tests

    [Fact]
    public void ValidateToken_InvalidToken_ReturnsNull()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var invalidToken = "invalid-token";

        // Act
        var payload = _service.ValidateToken(invalidToken, assetId, tenantId);

        // Assert
        payload.Should().BeNull();
    }

    [Fact]
    public void ValidateToken_EmptyToken_ReturnsNull()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        var payload = _service.ValidateToken(string.Empty, assetId, tenantId);

        // Assert
        payload.Should().BeNull();
    }

    [Fact]
    public void ValidateToken_TooShortToken_ReturnsNull()
    {
        // Arrange - token bytes need to be at least 22 bytes when decoded
        var shortToken = "abc";
        var assetId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        var payload = _service.ValidateToken(shortToken, assetId, tenantId);

        // Assert
        payload.Should().BeNull();
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(-2, false)]
    public void ValidateToken_LegacyWindowEncoding_HandlesSupportedWindows(int windowOffset, bool expectedValid)
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        var token = CreateLegacyToken(assetId, tenantId, AssetAccessPolicy.Private, windowOffset);
        var payload = _service.ValidateToken(token, assetId, tenantId);

        // Assert
        if (expectedValid)
            payload.Should().NotBeNull();
        else
            payload.Should().BeNull();
    }

    #endregion

    #region Token Expiry Tests

    [Fact]
    public void GetCurrentTimeWindow_ReturnsNonNegativeValue()
    {
        // Act
        var timeWindow = _service.GetCurrentTimeWindow();

        // Assert
        timeWindow.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void GetCurrentTimeWindow_IsStableWithinWindow()
    {
        // Act - call twice
        var timeWindow1 = _service.GetCurrentTimeWindow();
        var timeWindow2 = _service.GetCurrentTimeWindow();

        // Assert - should return same value within time window
        timeWindow1.Should().Be(timeWindow2);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithEmptySecretKey_GeneratesRandomKey()
    {
        // Arrange
        var options = new AssetTokenOptions
        {
            SecretKey = string.Empty,
            DefaultExpiryHours = 24,
            TimeWindowHours = 8
        };

        // Act
        var service = new AssetTokenService(Options.Create(options));

        // Assert
        var token = service.GenerateToken(Guid.NewGuid(), Guid.NewGuid(), AssetAccessPolicy.Public);
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void EmptySecretKey_UsesProcessWideDevelopmentKey()
    {
        // Arrange
        var options = Options.Create(new AssetTokenOptions { SecretKey = string.Empty });
        var issuer = new AssetTokenService(options);
        var validator = new AssetTokenService(options);
        var assetId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        var token = issuer.GenerateToken(assetId, tenantId, AssetAccessPolicy.Private);
        var payload = validator.ValidateToken(token, assetId, tenantId);

        // Assert
        payload.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithInvalidExpiryHours_UsesDefault()
    {
        // Arrange
        var options = new AssetTokenOptions
        {
            SecretKey = Convert.ToBase64String(new byte[32]),
            DefaultExpiryHours = 0,
            TimeWindowHours = 0
        };

        // Act
        var service = new AssetTokenService(Options.Create(options));

        // Assert - should still work with default values
        var token = service.GenerateToken(Guid.NewGuid(), Guid.NewGuid(), AssetAccessPolicy.Public);
        token.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Access Policy Tests

    [Theory]
    [InlineData(AssetAccessPolicy.Public)]
    [InlineData(AssetAccessPolicy.Private)]
    [InlineData(AssetAccessPolicy.SignedUrl)]
    [InlineData(AssetAccessPolicy.TenantPublic)]
    [InlineData(AssetAccessPolicy.Authenticated)]
    [InlineData(AssetAccessPolicy.OwnerOnly)]
    public void GenerateToken_WithDifferentPolicies_ReturnsNonEmptyToken(AssetAccessPolicy policy)
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Act
        var token = _service.GenerateToken(assetId, tenantId, policy);

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Length.Should().BeGreaterThan(10); // Token should have meaningful length
    }

    #endregion

    private string CreateLegacyToken(Guid assetId, Guid tenantId, AssetAccessPolicy policy, int windowOffset)
    {
        var timeWindow = _service.GetCurrentTimeWindow() + windowOffset;
        var expiryTimestamp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var baseTimestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        var payload = $"{assetId}|{timeWindow}|{expiryTimestamp}|{(int)policy}||{tenantId}";

        using var hmac = new HMACSHA256(_secretKey);
        var signature = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        var tokenBytes = new byte[22];
        BitConverter.GetBytes((short)timeWindow).CopyTo(tokenBytes, 0);
        BitConverter.GetBytes((int)(expiryTimestamp - baseTimestamp)).CopyTo(tokenBytes, 2);
        signature.AsSpan(0, 16).CopyTo(tokenBytes.AsSpan(6));

        return Convert.ToBase64String(tokenBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
