using System.Security.Cryptography;
using System.Text;
using GameGuild.Configuration.PresentationLayer.Authentication;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class PasswordHashReconciliationTests
{
    [Theory]
    [InlineData("PresentationLayer:Authentication:PasswordPolicy")]
    [InlineData("Authentication:PasswordPolicy")]
    [InlineData("PasswordPolicy")]
    public void ConfiguredWorkFactorIsUsed(string section)
    {
        var hasher = Create(new Dictionary<string, string?> { [$"{section}:BCryptWorkFactor"] = "10" });
        var password = SyntheticPassword();
        var hash = hasher.HashPassword(password);
        Assert.Equal("10", hash.Split('$')[2]);
        Assert.True(BCrypt.Net.BCrypt.Verify(password, hash));
    }

    [Fact]
    public void SharedConfigurationTakesPrecedenceAndLowerLegacyHashNeedsUpgrade()
    {
        var hasher = Create(new Dictionary<string, string?>
        {
            ["PasswordPolicy:BCryptWorkFactor"] = "10",
            ["Authentication:PasswordPolicy:BCryptWorkFactor"] = "11",
            ["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = "12"
        });
        var password = SyntheticPassword();
        Assert.Equal("12", hasher.HashPassword(password).Split('$')[2]);
        var lowerHash = BCrypt.Net.BCrypt.HashPassword(password, 10);
        Assert.True(hasher.VerifyPassword(lowerHash, password));
        Assert.True(hasher.NeedsUpgrade(lowerHash));
    }

    [Fact]
    public void DefaultCostAndRandomSaltArePreserved()
    {
        var hasher = Create();
        var password = SyntheticPassword();
        var first = hasher.HashPassword(password);
        var second = hasher.HashPassword(password);
        Assert.Equal("12", first.Split('$')[2]);
        Assert.NotEqual(first, second);
        Assert.False(hasher.NeedsUpgrade(first));
        Assert.True(hasher.VerifyPassword(first, password));
        Assert.False(hasher.VerifyPassword(first, SyntheticPassword()));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(17)]
    [InlineData(31)]
    public void InvalidGenerationCostsAreRejectedByServiceAndTypedConfiguration(int cost)
    {
        var hasher = Create(new Dictionary<string, string?> { ["PasswordPolicy:BCryptWorkFactor"] = cost.ToString() });
        Assert.Throws<InvalidOperationException>(() => hasher.HashPassword(SyntheticPassword()));
        Assert.Throws<InvalidOperationException>(() => new AuthenticationPasswordPolicySettings { BCryptWorkFactor = cost }.Validate());
    }

    [Theory]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(16)]
    public void TypedConfigurationAcceptsSupportedCostRange(int cost)
    {
        new AuthenticationPasswordPolicySettings { BCryptWorkFactor = cost }.Validate();
    }

    [Theory]
    [InlineData(71)]
    [InlineData(72)]
    [InlineData(73)]
    [InlineData(128)]
    public void ByteBoundarySelectsAnExplicitFormatAndPreservesTheWholePassword(int length)
    {
        var hasher = FastHasher();
        var password = (SyntheticPassword() + new string('x', 128))[..length];
        var hash = hasher.HashPassword(password);
        Assert.Equal(length > 72, hash.StartsWith("pbkdf2-sha256$", StringComparison.Ordinal));
        Assert.True(hasher.VerifyPassword(hash, password));
        Assert.False(hasher.VerifyPassword(hash, password[..^1] + "z"));
        Assert.False(hasher.NeedsUpgrade(hash));
    }

    [Fact]
    public void LongValidPasswordsWithTheSameFirst72BytesAreDistinguished()
    {
        var hasher = FastHasher();
        var prefix = "aA7!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(34));
        var password = prefix + Guid.NewGuid().ToString("N");
        var other = prefix + Guid.NewGuid().ToString("N");
        Assert.True(hasher.ValidatePasswordStrength(password).IsValid);
        Assert.True(hasher.ValidatePasswordStrength(other).IsValid);
        var first = hasher.HashPassword(password);
        var second = hasher.HashPassword(password);
        Assert.NotEqual(first, second);
        Assert.True(hasher.VerifyPassword(first, password));
        Assert.False(hasher.VerifyPassword(first, other));
        AssertFullLengthPbkdf2(first, password);
    }

    [Fact]
    public void MultibytePasswordUsesItsUtf8ByteLengthWithoutTruncation()
    {
        var hasher = FastHasher();
        var prefix = "aA7!" + new string('\u00e9', 35);
        var password = prefix + Guid.NewGuid().ToString("N");
        Assert.True(password.Length < 72);
        Assert.True(Encoding.UTF8.GetByteCount(password) > 72);
        var hash = hasher.HashPassword(password);
        Assert.StartsWith("pbkdf2-sha256$", hash);
        Assert.True(hasher.VerifyPassword(hash, password));
        Assert.False(hasher.VerifyPassword(hash, prefix + Guid.NewGuid().ToString("N")));
        AssertFullLengthPbkdf2(hash, password);
    }

    [Fact]
    public void LongLegacyInputsCannotAuthenticateAgainstAnAmbiguousTruncatedHash()
    {
        var hasher = FastHasher();
        var password = "aA7!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(50));
        var legacyHash = BCrypt.Net.BCrypt.HashPassword(password, 10);
        Assert.False(hasher.VerifyPassword(legacyHash, password));
        Assert.True(hasher.VerifyPassword(legacyHash, password[..72]));
        // Legacy hashes cannot recover the original suffix. Authentication does not certify that suffix.
    }

    [Fact]
    public async Task ResetStillRejectsReuseOfAnAmbiguousLongLegacyPassword()
    {
        var prefix = "aA7!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(34));
        var password = prefix + Guid.NewGuid().ToString("N");
        var candidate = prefix + Guid.NewGuid().ToString("N");
        var user = new User { Id = Guid.NewGuid(), Email = $"history-{Guid.NewGuid():N}@example.test", PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 10) };
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        repository.Setup(service => service.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var token = Guid.NewGuid().ToString("N");
        var verification = new Mock<IEmailVerificationService>(MockBehavior.Strict);
        verification.Setup(service => service.VerifyPasswordResetTokenAsync(token)).ReturnsAsync(new TokenValidationResult(true, user.Id, user.Email));
        var handler = new ResetPasswordCommandHandler(repository.Object, FastHasher(), verification.Object, NullLogger<ResetPasswordCommandHandler>.Instance);
        var result = await handler.Handle(new ResetPasswordCommand { Token = token, NewPassword = candidate, ConfirmPassword = candidate }, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("reuse", result.Message, StringComparison.OrdinalIgnoreCase);
        repository.VerifyAll();
    }

    [Theory]
    [InlineData("$2b$12$not-a-hash")]
    [InlineData("$2b$99$not-a-hash")]
    [InlineData("$2b$12$")]
    [InlineData("unknown$12$not-a-hash")]
    [InlineData("pbkdf2-sha256$1$invalid$invalid")]
    [InlineData("pbkdf2-sha256$600000$invalid$invalid")]
    public void MalformedHashIsRejectedAndNeverConsideredCurrent(string hash)
    {
        var hasher = FastHasher();
        Assert.False(hasher.VerifyPassword(hash, SyntheticPassword()));
        Assert.True(hasher.NeedsUpgrade(hash));
    }

    [Fact]
    public void AlteredPbkdf2ParametersCannotChooseAWeakerOrUnboundedOperation()
    {
        var hasher = FastHasher();
        var password = "aA7!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(50));
        var hash = hasher.HashPassword(password);
        foreach (var iterations in new[] { "1", "600001", "2147483647" })
        {
            var altered = hash.Replace("$600000$", $"${iterations}$", StringComparison.Ordinal);
            Assert.False(hasher.VerifyPassword(altered, password));
            Assert.True(hasher.NeedsUpgrade(altered));
        }
    }

    [Fact]
    public void PolicyScoreAppliesIndependentSequenceAndRepetitionControls()
    {
        static char[] Sample(string alphabet) => alphabet.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).Take(5).ToArray();
        var upper = Sample("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        var lower = Sample("abcdefghijklmnopqrstuvwxyz");
        var digits = Sample("0123456789");
        var punctuation = Sample("!?@#$%&*+=");
        var baseline = string.Concat(Enumerable.Range(0, 5).Select(index => new string([upper[index], lower[index], digits[index], punctuation[index]])));
        var start = RandomNumberGenerator.GetInt32('a', 'w' + 1);
        var sequence = new string([(char)start, (char)(start + 1), (char)(start + 2)]);
        var repetition = new string(upper[0], 3);
        var hasher = FastHasher();
        foreach (var (candidate, expectedScore) in new[] { (baseline, 100), (sequence + baseline, 90), (repetition + baseline, 90), (sequence + repetition + baseline, 80) })
        {
            var result = hasher.ValidatePasswordStrength(candidate);
            Assert.True(result.IsValid);
            Assert.Equal(expectedScore, result.StrengthScore);
            Assert.Equal("Strong", result.StrengthLevel);
        }
        // The score is a documented heuristic; it is not measured entropy or a crack-time guarantee.
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("verify")]
    [InlineData("rehash")]
    [InlineData("policy")]
    public async Task PreCancelledOperationsPropagateCancellation(string operation)
    {
        var hasher = FastHasher();
        var password = SyntheticPassword();
        var hash = hasher.HashPassword(password);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Func<Task> action = operation switch
        {
            "hash" => () => hasher.HashPasswordAsync(password, cancelled.Token),
            "verify" => () => hasher.VerifyPasswordAsync(hash, password, cancelled.Token),
            "rehash" => () => hasher.NeedsRehashAsync(hash, cancelled.Token),
            _ => () => hasher.ValidatePasswordStrengthAsync(password, cancelled.Token)
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(action);
    }

    private static void AssertFullLengthPbkdf2(string hash, string password)
    {
        var parts = hash.Split('$');
        Assert.Equal(4, parts.Length);
        Assert.Equal("600000", parts[1]);
        var salt = Convert.FromBase64String(parts[2]);
        Assert.Equal(16, salt.Length);
        var expected = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 600_000, HashAlgorithmName.SHA256, 32);
        Assert.Equal(expected, Convert.FromBase64String(parts[3]));
    }

    private static string SyntheticPassword() => "aA7!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(20));

    private static PasswordHasher FastHasher() => Create(new Dictionary<string, string?> { ["PasswordPolicy:BCryptWorkFactor"] = "10" });

    private static PasswordHasher Create(Dictionary<string, string?>? values = null) => new(
        NullLogger<PasswordHasher>.Instance,
        new ConfigurationBuilder().AddInMemoryCollection(values ?? []).Build());
}
