using GameGuild.Identity.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class CredentialWorkTests
{
    private static readonly Lazy<string> Legacy = new(() => BCrypt.Net.BCrypt.HashPassword("Known-Correct-1!", 4));
    private static readonly Lazy<string> Long = new(() => LongPasswordHash.Create(new string('p', 80)));

    [Theory]
    [InlineData("", "wrong")]
    [InlineData("malformed", "wrong")]
    [InlineData("unsupported-bcrypt-cost", "wrong")]
    [InlineData("pbkdf2-sha256$1$bad$bad", "wrong")]
    [InlineData("pbkdf2-sha256$600000$invalid$invalid", "wrong")]
    public void RejectedRecordsNeverClaimCompletedCredentialWork(string stored, string password)
    {
        var hasher = Create();
        if (stored == "unsupported-bcrypt-cost")
        {
            stored = Legacy.Value[..4] + "99" + Legacy.Value[6..];
        }
        Assert.Equal(new PasswordVerificationResult(false, false), hasher.VerifyPasswordWithWork(stored, password));
        Assert.False(hasher.VerifyPassword(stored, password));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("legacy-too-long")]
    public void RejectedLegacyInputsNeverClaimCompletedCredentialWork(string input)
    {
        var password = input == "legacy-too-long" ? new string('p', 73) : input;
        Assert.Equal(new PasswordVerificationResult(false, false), Create().VerifyPasswordWithWork(Legacy.Value, password));
    }

    [Theory]
    [InlineData("Known-Correct-1!", true)]
    [InlineData("Wrong-Password-1!", false)]
    public void SuccessfulAndFailedLegacyVerificationBothReportActualWork(string password, bool valid)
    {
        var hasher = Create();
        Assert.Equal(new PasswordVerificationResult(valid, true), hasher.VerifyPasswordWithWork(Legacy.Value, password));
        Assert.Equal(valid, hasher.VerifyPassword(Legacy.Value, password));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LongPasswordVerificationPreservesFullLengthAndReportsActualDerivation(bool valid)
    {
        var password = new string('p', 79) + (valid ? "p" : "q");
        Assert.Equal(new PasswordVerificationResult(valid, true), Create().VerifyPasswordWithWork(Long.Value, password));
    }

    [Fact]
    public void WorkFactorUsesCurrentPrecedenceAndReloadedValues()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PasswordPolicy:BCryptWorkFactor"] = "10",
            ["Authentication:PasswordPolicy:BCryptWorkFactor"] = "11",
            ["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = "12"
        }).Build();
        Assert.Equal(12, PasswordHasher.ResolveBCryptWorkFactor(configuration));
        configuration["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = "13";
        configuration.Reload();
        Assert.Equal(13, PasswordHasher.ResolveBCryptWorkFactor(configuration));
        configuration["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = null;
        Assert.Equal(11, PasswordHasher.ResolveBCryptWorkFactor(configuration));
        configuration["Authentication:PasswordPolicy:BCryptWorkFactor"] = null;
        Assert.Equal(10, PasswordHasher.ResolveBCryptWorkFactor(configuration));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    [InlineData(17)]
    public void NewHashPolicyRejectsUnsupportedCosts(int cost)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PasswordPolicy:BCryptWorkFactor"] = cost.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }).Build();
        Assert.Throws<InvalidOperationException>(() => PasswordHasher.ResolveBCryptWorkFactor(configuration));
    }

    private static PasswordHasher Create() => new(NullLogger<PasswordHasher>.Instance,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PasswordPolicy:BCryptWorkFactor"] = "10"
        }).Build());
}
