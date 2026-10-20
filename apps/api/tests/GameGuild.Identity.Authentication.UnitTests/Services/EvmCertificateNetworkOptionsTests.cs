using GameGuild.Configuration.ApplicationLayer;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class EvmCertificateNetworkOptionsTests
{
    [Fact]
    public void CompleteSepoliaFinalizedConfigurationIsValid()
    {
        Assert.Empty(ValidOptions().Validate());
    }

    [Fact]
    public void DefaultsDoNotImplyAConfiguredDeployedContract()
    {
        var options = new EvmCertificateNetworkOptions();
        Assert.Equal(11155111, options.ChainId);
        Assert.Equal(EvmCertificateFinalityMode.Finalized, options.FinalityMode);
        Assert.NotEmpty(options.Validate());
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void ChainIdMustBePositive(long chainId)
    {
        var options = ValidOptions();
        options.ChainId = chainId;
        Assert.Contains(options.Validate(), error => error.Contains("ChainId", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uri")]
    [InlineData("https://rpc.invalid/#fragment")]
    public void RpcMustUseAbsoluteHttpsWithoutUserInfoOrFragment(string endpoint)
    {
        var options = ValidOptions();
        options.RpcEndpoint = endpoint;
        Assert.Contains(options.Validate(), error => error.Contains("RpcEndpoint", StringComparison.Ordinal));
        Assert.DoesNotContain(options.Validate(), error => error.Contains("private-marker", StringComparison.Ordinal));
    }

    [Fact]
    public void PlainHttpRpcEndpointIsRejected()
    {
        var options = ValidOptions();
        options.RpcEndpoint = new UriBuilder(Uri.UriSchemeHttp, "rpc.invalid").Uri.AbsoluteUri;
        Assert.Contains(options.Validate(), error => error.Contains("RpcEndpoint", StringComparison.Ordinal));
    }

    [Fact]
    public void RpcUserInformationIsRejectedWithoutEchoingTheGeneratedCredential()
    {
        var credential = Guid.NewGuid().ToString("N");
        var options = ValidOptions();
        options.RpcEndpoint = new UriBuilder(Uri.UriSchemeHttps, "rpc.invalid")
        {
            UserName = "fixture-user",
            Password = credential
        }.Uri.AbsoluteUri;
        Assert.Contains(options.Validate(), error => error.Contains("RpcEndpoint", StringComparison.Ordinal));
        Assert.DoesNotContain(options.Validate(), error => error.Contains(credential, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0x123")]
    [InlineData("0x0000000000000000000000000000000000000000")]
    [InlineData("0xgg11111111111111111111111111111111111111")]
    public void ContractAddressMustBeANonzeroEvmAddress(string address)
    {
        var options = ValidOptions();
        options.ContractAddress = address;
        Assert.Contains(options.Validate(), error => error.Contains("ContractAddress", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0x123")]
    [InlineData("0x0000000000000000000000000000000000000000000000000000000000000000")]
    public void IssuerMustBeANonzeroPublicBytes32Identity(string issuerId)
    {
        var options = ValidOptions();
        options.IssuerId = issuerId;
        Assert.Contains(options.Validate(), error => error.Contains("IssuerId", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0x123")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void DeploymentCodeSha256MustBeExactly32Bytes(string digest)
    {
        var options = ValidOptions();
        options.ExpectedRuntimeCodeSha256 = digest;
        Assert.Contains(options.Validate(), error => error.Contains("ExpectedRuntimeCodeSha256", StringComparison.Ordinal));
    }

    [Fact]
    public void SepoliaCannotSilentlyUseAConfirmationCountInsteadOfFinality()
    {
        var options = ValidOptions();
        options.FinalityMode = EvmCertificateFinalityMode.Confirmations;
        options.MinimumConfirmations = 12;
        Assert.Contains(options.Validate(), error => error.Contains("Sepolia", StringComparison.Ordinal));
    }

    [Fact]
    public void OtherNetworksRequireAnExplicitPositiveConfirmationPolicy()
    {
        var options = ValidOptions();
        options.ChainId = 12345;
        options.FinalityMode = EvmCertificateFinalityMode.Confirmations;
        Assert.Contains(options.Validate(), error => error.Contains("MinimumConfirmations", StringComparison.Ordinal));
        options.MinimumConfirmations = 12;
        Assert.Empty(options.Validate());
    }

    [Fact]
    public void UnknownFinalityModeIsRejected()
    {
        var options = ValidOptions();
        options.FinalityMode = (EvmCertificateFinalityMode)999;
        Assert.Contains(options.Validate(), error => error.Contains("FinalityMode", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(120001)]
    public void RequestTimeoutIsBounded(int milliseconds)
    {
        var options = ValidOptions();
        options.RequestTimeout = TimeSpan.FromMilliseconds(milliseconds);
        Assert.Contains(options.Validate(), error => error.Contains("RequestTimeout", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(4194305)]
    public void ResponseBudgetIsBounded(int bytes)
    {
        var options = ValidOptions();
        options.MaximumResponseBytes = bytes;
        Assert.Contains(options.Validate(), error => error.Contains("MaximumResponseBytes", StringComparison.Ordinal));
    }

    internal static EvmCertificateNetworkOptions ValidOptions() => new()
    {
        RpcEndpoint = "https://rpc.invalid/",
        ContractAddress = "0x1111111111111111111111111111111111111111",
        IssuerId = "0x2222222222222222222222222222222222222222222222222222222222222222",
        ExpectedRuntimeCodeSha256 = new string('a', 64)
    };
}
