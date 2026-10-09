using GameGuild.Configuration.ApplicationLayer;
using Microsoft.Extensions.Configuration;

namespace GameGuild.SharedKernel.UnitTests.Configuration;

public sealed class RefreshTokenReplayScopeOptionsTests
{
    [Fact]
    public void DefaultReplayPolicyMatchesTheFamilyCriterion()
    {
        Assert.Equal(RefreshTokenReplayScope.Family, new JwtOptions().RefreshTokenReplayContainmentScope);
        Assert.Equal(RefreshTokenReplayScope.Family, JwtOptionsResolver.ResolveReplayScope(new ConfigurationBuilder().Build()));
    }

    [Theory]
    [InlineData("Family", RefreshTokenReplayScope.Family)]
    [InlineData("Account", RefreshTokenReplayScope.Account)]
    [InlineData("account", RefreshTokenReplayScope.Account)]
    public void ValidatedTypedOptionsKeepTheSelectedScope(string value, RefreshTokenReplayScope expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = "SyntheticConfigurationSecretForTestsOnly123",
            ["Jwt:RefreshTokenReplayContainmentScope"] = value,
            ["Jwt:RefreshTokenSlidingExpiration"] = "false"
        }).Build();
        var options = JwtOptionsResolver.CreateValidated(configuration);
        Assert.Equal(expected, options.RefreshTokenReplayContainmentScope);
        Assert.False(options.RefreshTokenSlidingExpiration);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("-1")]
    [InlineData("9")]
    public void UnknownPolicyFailsRatherThanSilentlySelectingAnotherScope(string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Jwt:RefreshTokenReplayContainmentScope"] = value }).Build();
        Assert.Throws<InvalidOperationException>(() => JwtOptionsResolver.ResolveReplayScope(configuration));
    }

    [Fact]
    public void DirectTypedOptionsRejectUndefinedEnumValues()
    {
        var options = new JwtOptions { RefreshTokenReplayContainmentScope = (RefreshTokenReplayScope)9 };
        Assert.Contains("JWT RefreshTokenReplayContainmentScope must be Family or Account", options.Validate());
    }
}
