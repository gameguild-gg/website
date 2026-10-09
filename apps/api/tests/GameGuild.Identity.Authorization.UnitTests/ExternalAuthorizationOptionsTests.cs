using FluentAssertions;
using GameGuild.Identity.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Validation tests for <see cref="ExternalAuthorizationOptions"/> (issue #146):
///     disabled configuration always passes; enabling requires complete HTTPS-only
///     endpoints and client credentials; timeout and cache-TTL bounds hold.
/// </summary>
public class ExternalAuthorizationOptionsTests
{
    private static ExternalAuthorizationOptions EnabledOptions() => new()
    {
        Enabled = true,
        Endpoint = "https://pdp.example.test/decisions",
        TokenEndpoint = "https://idp.example.test/token",
        ClientId = "client-id",
        ClientSecret = "client-secret"
    };

    [Fact]
    public void Defaults_AreDisabledAndValidate()
    {
        var options = new ExternalAuthorizationOptions();

        options.Enabled.Should().BeFalse("the integration must be opt-in");
        options.FailMode.Should().Be(ExternalAuthorizationFailMode.Enforce, "unavailable decisions fail closed by default");
        options.Timeout.Should().Be(TimeSpan.FromSeconds(5));
        options.CacheTtl.Should().Be(TimeSpan.FromSeconds(30));
        ExternalAuthorizationOptions.SectionName.Should().Be("Authorization:ExternalDecision");

        var act = () => options.Validate();
        act.Should().NotThrow("disabled configuration never fails startup");
    }

    [Fact]
    public void FullyConfigured_EnabledOptions_Validate()
    {
        var act = () => EnabledOptions().Validate();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-uri")]
    [InlineData("http://pdp.example.test/decisions")]
    public void Enabled_WithoutHttpsEndpoint_Throws(string? endpoint)
    {
        var options = EnabledOptions();
        options.Endpoint = endpoint;

        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Endpoint*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ftp://idp.example.test/token")]
    [InlineData("http://idp.example.test/token")]
    public void Enabled_WithoutHttpsTokenEndpoint_Throws(string? tokenEndpoint)
    {
        var options = EnabledOptions();
        options.TokenEndpoint = tokenEndpoint;

        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*TokenEndpoint*");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Enabled_WithoutClientCredentials_Throws(bool hasClientId, bool hasClientSecret)
    {
        var options = EnabledOptions();
        options.ClientId = hasClientId ? "client-id" : null;
        options.ClientSecret = hasClientSecret ? "client-secret" : null;

        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Disabled_MissingEndpoints_DoNotThrow()
    {
        var options = new ExternalAuthorizationOptions { Enabled = false };

        var act = () => options.Validate();
        act.Should().NotThrow("a disabled integration may be entirely unconfigured");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(121)]
    public void Timeout_OutOfBounds_Throws(int seconds)
    {
        var options = EnabledOptions();
        options.Timeout = TimeSpan.FromSeconds(seconds);

        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Timeout*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3601)]
    public void CacheTtl_OutOfBounds_Throws(int seconds)
    {
        var options = EnabledOptions();
        options.CacheTtl = TimeSpan.FromSeconds(seconds);

        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>().WithMessage("*CacheTtl*");
    }

    [Fact]
    public void CacheTtlZero_IsAllowed_DisablesCaching()
    {
        var options = EnabledOptions();
        options.CacheTtl = TimeSpan.Zero;

        var act = () => options.Validate();
        act.Should().NotThrow("zero is the documented way to disable decision caching");
    }
}

/// <summary>
///     DI wiring tests for the external authorization-decision integration (issue #146).
/// </summary>
public class ExternalAuthorizationDiTests
{
    [Fact]
    public void AddUnifiedAuthorizationLayer_RegistersExternalDecisionProvider()
    {
        var services = new ServiceCollection();
        services.AddUnifiedAuthorizationLayer();

        services.Should().Contain(d => d.ServiceType == typeof(IExternalAuthorizationDecisionProvider));
    }

    [Fact]
    public void AddAuthorizationOptions_BindsExternalDecisionSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:ExternalDecision:Enabled"] = "true",
                ["Authorization:ExternalDecision:Endpoint"] = "https://pdp.example.test/decisions",
                ["Authorization:ExternalDecision:TokenEndpoint"] = "https://idp.example.test/token",
                ["Authorization:ExternalDecision:ClientId"] = "client-id",
                ["Authorization:ExternalDecision:ClientSecret"] = "client-secret",
                ["Authorization:ExternalDecision:FailMode"] = "Observe"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddAuthorizationOptions(configuration);

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<ExternalAuthorizationOptions>>()
            .Value;

        options.Enabled.Should().BeTrue();
        options.Endpoint.Should().Be("https://pdp.example.test/decisions");
        options.ClientId.Should().Be("client-id");
        options.FailMode.Should().Be(ExternalAuthorizationFailMode.Observe);
    }

    [Fact]
    public void AddAuthorizationOptions_MissingSection_DefaultsToDisabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var services = new ServiceCollection();
        services.AddAuthorizationOptions(configuration);

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<ExternalAuthorizationOptions>>()
            .Value;

        options.Enabled.Should().BeFalse("the integration must stay off unless explicitly configured");
    }
}
