using System.Security.Cryptography;
using FluentAssertions;
using GameGuild.Identity.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public class EncryptionServiceTests
{
    private readonly Mock<ILogger<EncryptionService>> _loggerMock;
    private readonly EncryptionService _service;

    public EncryptionServiceTests()
    {
        _loggerMock = new Mock<ILogger<EncryptionService>>();
        var configurationMock = new Mock<IConfiguration>();
        // The service fails closed without a key, so tests supply one (>= 32 bytes).
        configurationMock.Setup(c => c["Encryption:EncryptionKey"]).Returns((string?)null);
        configurationMock.Setup(c => c["Encryption:Key"]).Returns("unit-test-encryption-key-with-at-least-32-bytes");
        _service = new EncryptionService(_loggerMock.Object, configurationMock.Object);
    }

    [Fact]
    public async Task EncryptAsync_EncryptsPlaintext()
    {
        // Arrange
        var plaintext = "sensitive data";

        // Act
        var encrypted = await _service.EncryptAsync(plaintext);

        // Assert
        encrypted.Should().NotBeNullOrEmpty();
        encrypted.Should().NotBe(plaintext);
    }

    [Fact]
    public async Task EncryptAsync_ThrowsException_WhenPlaintextIsEmpty()
    {
        // Arrange
        var plaintext = "";

        // Act
        var act = async () => await _service.EncryptAsync(plaintext);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task EncryptAsync_ProducesDifferentCiphertext_ForSamePlaintext()
    {
        // Arrange
        var plaintext = "same data";

        // Act
        var encrypted1 = await _service.EncryptAsync(plaintext);
        var encrypted2 = await _service.EncryptAsync(plaintext);

        // Assert - Different because of random nonce
        encrypted1.Should().NotBe(encrypted2);
    }

    [Fact]
    public async Task DecryptAsync_DecryptsEncryptedData()
    {
        // Arrange
        var plaintext = "secret message";
        var encrypted = await _service.EncryptAsync(plaintext);

        // Act
        var decrypted = await _service.DecryptAsync(encrypted);

        // Assert
        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public async Task DecryptAsync_ThrowsException_WhenCiphertextIsInvalid()
    {
        // Arrange
        var invalidCiphertext = "invalid-base64";

        // Act
        var act = async () => await _service.DecryptAsync(invalidCiphertext);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task DecryptAsync_ThrowsException_WhenCiphertextIsEmpty()
    {
        // Arrange
        var ciphertext = "";

        // Act
        var act = async () => await _service.DecryptAsync(ciphertext);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task EncryptDecrypt_RoundTrip_PreservesData()
    {
        // Arrange
        var originalData = "Test data with special chars: @#$%^&*()";

        // Act
        var encrypted = await _service.EncryptAsync(originalData);
        var decrypted = await _service.DecryptAsync(encrypted);

        // Assert
        decrypted.Should().Be(originalData);
    }

    [Fact]
    public async Task EncryptAsync_HandlesLongText()
    {
        // Arrange
        var longText = new string('a', 10000);

        // Act
        var encrypted = await _service.EncryptAsync(longText);
        var decrypted = await _service.DecryptAsync(encrypted);

        // Assert
        decrypted.Should().Be(longText);
    }

    [Fact]
    public async Task EncryptAsync_HandlesUnicodeCharacters()
    {
        // Arrange
        var unicode = "Hello 世界 🌍";

        // Act
        var encrypted = await _service.EncryptAsync(unicode);
        var decrypted = await _service.DecryptAsync(encrypted);

        // Assert
        decrypted.Should().Be(unicode);
    }

    [Fact]
    public async Task EncryptAsync_ProducesBase64Output()
    {
        // Arrange
        var plaintext = "test";

        // Act
        var encrypted = await _service.EncryptAsync(plaintext);

        // Assert - Should be valid Base64
        var act = () => Convert.FromBase64String(encrypted);
        act.Should().NotThrow();
    }

    // --- Synchronous Encrypt/Decrypt ---

    [Fact]
    public void Encrypt_ShouldReturnNonEmptyBase64()
    {
        var result = _service.Encrypt("Hello World");

        result.Should().NotBeNullOrEmpty();
        var act = () => Convert.FromBase64String(result);
        act.Should().NotThrow();
    }

    [Fact]
    public void Encrypt_WithNullOrEmpty_ShouldThrow()
    {
        FluentActions.Invoking(() => _service.Encrypt(null!)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => _service.Encrypt("")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Decrypt_ShouldRoundTrip()
    {
        var original = "round-trip test data!";
        var encrypted = _service.Encrypt(original);
        var decrypted = _service.Decrypt(encrypted);

        decrypted.Should().Be(original);
    }

    [Fact]
    public void Decrypt_WithNullOrEmpty_ShouldThrow()
    {
        FluentActions.Invoking(() => _service.Decrypt(null!)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => _service.Decrypt("")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Decrypt_WithInvalidData_ShouldThrow()
    {
        var act = () => _service.Decrypt(Convert.ToBase64String(new byte[10]));
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Encrypt_SameInput_ProducesDifferentCiphertext()
    {
        var a = _service.Encrypt("same");
        var b = _service.Encrypt("same");
        a.Should().NotBe(b);
    }

    // --- GenerateSecureRandomString ---

    [Fact]
    public void GenerateSecureRandomString_ShouldReturnCorrectLength()
    {
        _service.GenerateSecureRandomString(20).Should().HaveLength(20);
    }

    [Fact]
    public void GenerateSecureRandomString_ShouldContainOnlyAlphanumeric()
    {
        _service.GenerateSecureRandomString(100).Should().MatchRegex("^[A-Za-z0-9]+$");
    }

    [Fact]
    public void GenerateSecureRandomString_WithZeroOrNegative_ShouldThrow()
    {
        FluentActions.Invoking(() => _service.GenerateSecureRandomString(0))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => _service.GenerateSecureRandomString(-5))
            .Should().Throw<ArgumentException>();
    }

    // --- GenerateSecureToken (sync) ---

    [Fact]
    public void GenerateSecureToken_ShouldReturnNonEmpty()
    {
        _service.GenerateSecureToken().Should().NotBeNullOrEmpty();
    }

    // --- GenerateSecureTokenAsync / ValidateSecureTokenAsync ---

    [Fact]
    public async Task GenerateSecureTokenAsync_ShouldReturnNonEmptyToken()
    {
        var token = await _service.GenerateSecureTokenAsync(32);
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateSecureTokenAsync_WithZeroOrNegative_ShouldThrow()
    {
        await FluentActions.Invoking(() => _service.GenerateSecureTokenAsync(0))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => _service.GenerateSecureTokenAsync(-1))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateSecureTokenAsync_TokensShouldBeDifferent()
    {
        var t1 = await _service.GenerateSecureTokenAsync(32);
        var t2 = await _service.GenerateSecureTokenAsync(32);
        t1.Should().NotBe(t2);
    }

    [Fact]
    public async Task ValidateSecureTokenAsync_WithValidToken_ShouldReturnTrue()
    {
        var token = await _service.GenerateSecureTokenAsync(32);
        (await _service.ValidateSecureTokenAsync(token)).Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSecureTokenAsync_WithNullOrEmpty_ShouldReturnFalse()
    {
        (await _service.ValidateSecureTokenAsync(null!)).Should().BeFalse();
        (await _service.ValidateSecureTokenAsync("")).Should().BeFalse();
        (await _service.ValidateSecureTokenAsync("   ")).Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSecureTokenAsync_WithShortToken_ShouldReturnFalse()
    {
        var shortToken = Convert.ToBase64String(new byte[8])
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        (await _service.ValidateSecureTokenAsync(shortToken)).Should().BeFalse();
    }
}

/// <summary>
///     Key-handling policy for <see cref="EncryptionService" />: the service must fail closed when no
///     key (or a key shorter than 32 bytes) is configured, and it must never fall back to a shared key.
/// </summary>
public sealed class EncryptionServiceKeyPolicyTests
{
    private const string ValidKey = "unit-test-encryption-key-with-at-least-32-bytes";

    private static EncryptionService CreateService(Dictionary<string, string?> settings) =>
        new(NullLogger<EncryptionService>.Instance,
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

    // --- Fail-closed: missing key ---

    [Fact]
    public void Encrypt_WithoutConfiguredKey_ThrowsInvalidOperationException()
    {
        var sut = CreateService(new Dictionary<string, string?>());

        var act = () => sut.Encrypt("sensitive data");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Encryption:EncryptionKey*Encryption:Key*");
    }

    [Fact]
    public async Task EncryptAsync_WithoutConfiguredKey_ThrowsInvalidOperationException()
    {
        var sut = CreateService(new Dictionary<string, string?>());

        var act = async () => await sut.EncryptAsync("sensitive data");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Decrypt_WithoutConfiguredKey_ThrowsInvalidOperationException()
    {
        var sut = CreateService(new Dictionary<string, string?>());

        // Format-valid ciphertext so decryption reaches key resolution before any format check.
        var act = () => sut.Decrypt(Convert.ToBase64String(new byte[40]));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Encrypt_WithWhitespaceKey_ThrowsInvalidOperationException()
    {
        var sut = CreateService(new Dictionary<string, string?>
        {
            ["Encryption:Key"] = "   ",
            ["Encryption:EncryptionKey"] = "   "
        });

        var act = () => sut.Encrypt("sensitive data");
        act.Should().Throw<InvalidOperationException>();
    }

    // --- Fail-closed: key shorter than 32 bytes ---

    [Theory]
    [InlineData("Encryption:Key")]
    [InlineData("Encryption:EncryptionKey")]
    public void Encrypt_WithKeyShorterThan32Bytes_ThrowsInvalidOperationException(string settingName)
    {
        var sut = CreateService(new Dictionary<string, string?>
        {
            [settingName] = new string('x', 31) // 31 bytes — one short of the minimum
        });

        var act = () => sut.Encrypt("sensitive data");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*32*");
    }

    [Fact]
    public void Encrypt_WithExactly32ByteKey_RoundTrips()
    {
        var sut = CreateService(new Dictionary<string, string?>
        {
            ["Encryption:Key"] = "0123456789abcdef0123456789abcdef" // exactly 32 bytes
        });

        var decrypted = sut.Decrypt(sut.Encrypt("boundary key length"));
        decrypted.Should().Be("boundary key length");
    }

    // --- Setting resolution (mirrors the host startup guard) ---

    [Fact]
    public void EncryptionKeyAlias_EncryptionEncryptionKey_IsAccepted()
    {
        var sut = CreateService(new Dictionary<string, string?>
        {
            ["Encryption:EncryptionKey"] = ValidKey
        });

        var decrypted = sut.Decrypt(sut.Encrypt("alias resolution"));
        decrypted.Should().Be("alias resolution");
    }

    [Fact]
    public void WhenBothSettingsPresent_EncryptionEncryptionKey_TakesPrecedence()
    {
        // Same precedence as the host startup guard: Encryption:EncryptionKey first.
        var encrypter = CreateService(new Dictionary<string, string?>
        {
            ["Encryption:EncryptionKey"] = ValidKey,
            ["Encryption:Key"] = "another-encryption-key-with-32-bytes-min!"
        });
        var sameAlias = CreateService(new Dictionary<string, string?>
        {
            ["Encryption:EncryptionKey"] = ValidKey
        });
        var otherKeyOnly = CreateService(new Dictionary<string, string?>
        {
            ["Encryption:Key"] = "another-encryption-key-with-32-bytes-min!"
        });

        var encrypted = encrypter.Encrypt("precedence");

        sameAlias.Decrypt(encrypted).Should().Be("precedence");
        var act = () => otherKeyOnly.Decrypt(encrypted);
        act.Should().Throw<CryptographicException>();
    }

    // --- Unbiased secure random string generation ---

    private static string SampleOne() => CreateService(new Dictionary<string, string?>
    {
        ["Encryption:Key"] = ValidKey
    }).GenerateSecureRandomString(310);

    [Fact]
    public void GenerateSecureRandomString_SelectsEveryAlphabetCharacter()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        var sample = string.Concat(Enumerable.Range(0, 100).Select(_ => SampleOne()));

        foreach (var c in alphabet)
        {
            sample.Should().Contain(c.ToString(), $"alphabet character '{c}' must be selectable");
        }
    }

    [Fact]
    public void GenerateSecureRandomString_IsApproximatelyUniform_NoModuloBias()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        const int samplesPerCharacter = 500;
        var totalSamples = alphabet.Length * samplesPerCharacter; // 31,000 characters
        var sample = string.Concat(Enumerable.Range(0, totalSamples / 310).Select(_ => SampleOne()));

        sample.Should().HaveLength(totalSamples);

        // Each character should appear ~500 times. Bounds are ±5σ (σ ≈ 22.2), which the previous
        // modulo-biased selection (first 8 characters ~25% more likely) would exceed while a
        // correct uniform sampler stays inside with overwhelming probability.
        const int expected = samplesPerCharacter;
        const double sigma = 22.2;
        var lowerBound = (int)(expected - 5 * sigma);
        var upperBound = (int)(expected + 5 * sigma);

        foreach (var c in alphabet)
        {
            var occurrences = sample.Count(ch => ch == c);
            occurrences.Should().BeInRange(lowerBound, upperBound,
                $"character '{c}' must be selected uniformly (expected ~{expected}, ±5σ bounds [{lowerBound}, {upperBound}])");
        }
    }
}
