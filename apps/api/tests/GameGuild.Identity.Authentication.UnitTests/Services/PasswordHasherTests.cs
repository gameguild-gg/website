using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
/// Unit tests for PasswordHasher service
/// </summary>
public class PasswordHasherTests
{
    private readonly Mock<ILogger<PasswordHasher>> _loggerMock;
    private readonly PasswordHasher _passwordHasher;

    public PasswordHasherTests()
    {
        _loggerMock = new Mock<ILogger<PasswordHasher>>();
        // Use in-memory configuration so GetValue<T>() works correctly
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PasswordPolicy:MinPasswordLength"] = "8",
                ["PasswordPolicy:MaxPasswordLength"] = "128",
                ["PasswordPolicy:RequireUppercase"] = "true",
                ["PasswordPolicy:RequireLowercase"] = "true",
                ["PasswordPolicy:RequireDigit"] = "true",
                ["PasswordPolicy:RequireSpecialChar"] = "true"
            })
            .Build();
        _passwordHasher = new PasswordHasher(_loggerMock.Object, configuration);
    }

    [Fact]
    public void ValidatePasswordStrength_LogsNoPasswordDerivedDetails()
    {
        var result = _passwordHasher.ValidatePasswordStrength("SecurePassword123!");

        result.StrengthScore.Should().BeGreaterThan(0);
        _loggerMock.Verify(logger => logger.Log(
            LogLevel.Debug,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString() == "Password strength validation completed"),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task HashPassword_WithValidPassword_ShouldReturnHash()
    {
        // Arrange
        var password = "SecurePassword123!";

        // Act
        var hash = await _passwordHasher.HashPasswordAsync(password);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe(password);
        hash.Should().StartWith("$2"); // BCrypt hash prefix
    }

    [Fact]
    public async Task HashPassword_WithSamePassword_ShouldReturnDifferentHashes()
    {
        // Arrange
        var password = "SecurePassword123!";

        // Act
        var hash1 = await _passwordHasher.HashPasswordAsync(password);
        var hash2 = await _passwordHasher.HashPasswordAsync(password);

        // Assert
        hash1.Should().NotBe(hash2); // BCrypt uses random salt
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task HashPassword_WithInvalidPassword_ShouldThrowException(string invalidPassword)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _passwordHasher.HashPasswordAsync(invalidPassword));
    }

    [Fact]
    public async Task VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var password = "SecurePassword123!";
        var hash = await _passwordHasher.HashPasswordAsync(password);

        // Act
        var result = await _passwordHasher.VerifyPasswordAsync(hash, password);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var password = "SecurePassword123!";
        var wrongPassword = "WrongPassword456!";
        var hash = await _passwordHasher.HashPasswordAsync(password);

        // Act
        var result = await _passwordHasher.VerifyPasswordAsync(hash, wrongPassword);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidatePasswordStrength_PrefersPresentationLayerAuthenticationPolicy()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PasswordPolicy:MinPasswordLength"] = "8",
                ["PresentationLayer:Authentication:PasswordPolicy:MinPasswordLength"] = "14",
                ["PresentationLayer:Authentication:PasswordPolicy:MaxPasswordLength"] = "128",
                ["PresentationLayer:Authentication:PasswordPolicy:RequireUppercase"] = "true",
                ["PresentationLayer:Authentication:PasswordPolicy:RequireLowercase"] = "true",
                ["PresentationLayer:Authentication:PasswordPolicy:RequireDigit"] = "true",
                ["PresentationLayer:Authentication:PasswordPolicy:RequireSpecialChar"] = "true"
            })
            .Build();
        var hasher = new PasswordHasher(NullLogger<PasswordHasher>.Instance, configuration);

        var result = hasher.ValidatePasswordStrength("Pass1!");

        result.IsValid.Should().BeFalse();
        result.ValidationFailures.Should().Contain("Password must be at least 14 characters long");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task VerifyPassword_WithInvalidPassword_ShouldReturnFalse(string invalidPassword)
    {
        // Arrange
        var hash = await _passwordHasher.HashPasswordAsync("ValidPassword123!");

        // Act
        var result = await _passwordHasher.VerifyPasswordAsync(hash, invalidPassword);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("invalid-hash")]
    public async Task VerifyPassword_WithInvalidHash_ShouldReturnFalse(string invalidHash)
    {
        // Arrange
        var password = "ValidPassword123!";

        // Act
        var result = await _passwordHasher.VerifyPasswordAsync(invalidHash, password);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HashPassword_MultipleTimes_ShouldProduceDifferentHashes()
    {
        // Arrange
        var password = "TestPassword123!";
        var hashes = new List<string>();

        // Act
        for (int i = 0; i < 5; i++)
        {
            hashes.Add(await _passwordHasher.HashPasswordAsync(password));
        }

        // Assert
        hashes.Should().OnlyHaveUniqueItems();
        
        foreach (var hash in hashes)
        {
            var isValid = await _passwordHasher.VerifyPasswordAsync(hash, password);
            isValid.Should().BeTrue();
        }
    }

    [Fact]
    public async Task VerifyPassword_WithModifiedHash_ShouldReturnFalse()
    {
        // Arrange
        var password = "SecurePassword123!";
        var hash = await _passwordHasher.HashPasswordAsync(password);
        var modifiedHash = hash.Substring(0, hash.Length - 1) + "X"; // Modify last character

        // Act
        var result = await _passwordHasher.VerifyPasswordAsync(modifiedHash, password);

        // Assert
        result.Should().BeFalse();
    }

    // --- NeedsUpgrade / NeedsRehashAsync ---

    [Fact]
    public void NeedsUpgrade_WithCurrentWorkFactor_ShouldReturnFalse()
    {
        var hash = _passwordHasher.HashPassword("StrongP@ss1");
        _passwordHasher.NeedsUpgrade(hash).Should().BeFalse();
    }

    [Fact]
    public void NeedsUpgrade_WithLowerWorkFactor_ShouldReturnTrue()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("StrongP@ss1", 10);
        _passwordHasher.NeedsUpgrade(hash).Should().BeTrue();
    }

    [Fact]
    public void NeedsUpgrade_WithInvalidFormat_ShouldReturnTrue()
    {
        _passwordHasher.NeedsUpgrade("not-a-hash").Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NeedsUpgrade_WithNullOrEmpty_ShouldReturnFalse(string? hash)
    {
        _passwordHasher.NeedsUpgrade(hash!).Should().BeFalse();
    }

    [Fact]
    public async Task NeedsRehashAsync_ShouldWorkWithCurrentHash()
    {
        var hash = await _passwordHasher.HashPasswordAsync("StrongP@ss1");
        (await _passwordHasher.NeedsRehashAsync(hash)).Should().BeFalse();
    }

    // --- ValidatePasswordStrength ---

    [Fact]
    public void ValidatePasswordStrength_ValidPassword_ShouldBeValid()
    {
        var result = _passwordHasher.ValidatePasswordStrength("Str0ng!Pass");

        result.IsValid.Should().BeTrue();
        result.ValidationFailures.Should().BeEmpty();
        result.StrengthScore.Should().BeGreaterThan(0);
        result.StrengthLevel.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidatePasswordStrength_NullOrEmpty_ShouldBeInvalid(string? password)
    {
        var result = _passwordHasher.ValidatePasswordStrength(password!);

        result.IsValid.Should().BeFalse();
        result.ValidationFailures.Should().Contain("Password is required");
    }

    [Fact]
    public void ValidatePasswordStrength_TooShort_ShouldFail()
    {
        var result = _passwordHasher.ValidatePasswordStrength("Ab1!");

        result.IsValid.Should().BeFalse();
        result.ValidationFailures.Should().Contain(f => f.Contains("at least"));
    }

    [Fact]
    public void ValidatePasswordStrength_NoUppercase_ShouldFail()
    {
        var result = _passwordHasher.ValidatePasswordStrength("nouppercase1!");

        result.IsValid.Should().BeFalse();
        result.ValidationFailures.Should().Contain(f => f.Contains("uppercase"));
    }

    [Fact]
    public void ValidatePasswordStrength_NoLowercase_ShouldFail()
    {
        var result = _passwordHasher.ValidatePasswordStrength("NOLOWERCASE1!");

        result.IsValid.Should().BeFalse();
        result.ValidationFailures.Should().Contain(f => f.Contains("lowercase"));
    }

    [Fact]
    public void ValidatePasswordStrength_NoDigit_ShouldFail()
    {
        var result = _passwordHasher.ValidatePasswordStrength("NoDigitHere!");

        result.IsValid.Should().BeFalse();
        result.ValidationFailures.Should().Contain(f => f.Contains("digit"));
    }

    [Fact]
    public void ValidatePasswordStrength_NoSpecialChar_ShouldFail()
    {
        var result = _passwordHasher.ValidatePasswordStrength("NoSpecialChar1");

        result.IsValid.Should().BeFalse();
        result.ValidationFailures.Should().Contain(f => f.Contains("special"));
    }

    [Fact]
    public void ValidatePasswordStrength_CommonPassword_ShouldFail()
    {
        var result = _passwordHasher.ValidatePasswordStrength("password");

        result.IsValid.Should().BeFalse();
        result.ValidationFailures.Should().Contain(f => f.Contains("common"));
    }

    [Fact]
    public async Task ValidatePasswordStrengthAsync_ShouldWork()
    {
        var result = await _passwordHasher.ValidatePasswordStrengthAsync("Str0ng!Pass");
        result.IsValid.Should().BeTrue();
    }

    // --- Sync methods ---

    [Fact]
    public void HashPassword_Sync_ShouldReturnHash()
    {
        var hash = _passwordHasher.HashPassword("StrongP@ss1");
        hash.Should().StartWith("$2");
    }

    [Fact]
    public void VerifyPassword_Sync_ShouldVerify()
    {
        var hash = _passwordHasher.HashPassword("StrongP@ss1");
        _passwordHasher.VerifyPassword(hash, "StrongP@ss1").Should().BeTrue();
        _passwordHasher.VerifyPassword(hash, "WrongP@ss1").Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "pass")]
    [InlineData("", "pass")]
    [InlineData("hash", null)]
    [InlineData("hash", "")]
    public void VerifyPassword_Sync_WithNullOrEmpty_ShouldReturnFalse(string? hash, string? password)
    {
        _passwordHasher.VerifyPassword(hash!, password!).Should().BeFalse();
    }
}
