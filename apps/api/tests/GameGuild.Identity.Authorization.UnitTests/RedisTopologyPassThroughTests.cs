using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization.Caching;
using StackExchange.Redis;

namespace GameGuild.Identity.Authorization.UnitTests;

public sealed class RedisTopologyPassThroughTests
{
    [Fact]
    public void ApplyRedisTopology_PassesSentinelServiceNameThrough()
    {
        var configuration = ConfigurationOptions.Parse("sentinel-a:26379,sentinel-b:26379");
        var options = new AuthorizationCacheOptions { RedisServiceName = "mymaster" };

        CachingServiceExtensions.ApplyRedisTopology(configuration, options);

        configuration.ServiceName.Should().Be("mymaster");
    }

    [Theory]
    [InlineData("Twemproxy", Proxy.Twemproxy)]
    [InlineData("twemproxy", Proxy.Twemproxy)]
    [InlineData("Envoyproxy", Proxy.Envoyproxy)]
    [InlineData("None", Proxy.None)]
    public void ApplyRedisTopology_ParsesSupportedProxyModes(string configured, Proxy expected)
    {
        var configuration = ConfigurationOptions.Parse("localhost:6379");
        var options = new AuthorizationCacheOptions { RedisProxy = configured };

        CachingServiceExtensions.ApplyRedisTopology(configuration, options);

        configuration.Proxy.Should().Be(expected);
    }

    [Fact]
    public void ApplyRedisTopology_AppliesFailoverTimeoutAndRetrySettings()
    {
        var configuration = ConfigurationOptions.Parse("localhost:6379");
        var options = new AuthorizationCacheOptions
        {
            RedisConnectTimeoutMilliseconds = 7_500,
            RedisConnectRetry = 4
        };

        CachingServiceExtensions.ApplyRedisTopology(configuration, options);

        configuration.ConnectTimeout.Should().Be(7_500);
        configuration.ConnectRetry.Should().Be(4);
    }

    [Fact]
    public void ApplyRedisTopology_LeavesConnectionStringValuesUntouchedWhenOptionsAreUnset()
    {
        var configuration = ConfigurationOptions.Parse("localhost:6379,connectTimeout=3000,connectRetry=2");
        var defaults = new ConfigurationOptions();
        var options = new AuthorizationCacheOptions();

        CachingServiceExtensions.ApplyRedisTopology(configuration, options);

        configuration.ServiceName.Should().Be(defaults.ServiceName);
        configuration.Proxy.Should().Be(defaults.Proxy);
        configuration.ConnectTimeout.Should().Be(3_000, "the connection-string value must win when the option is unset");
        configuration.ConnectRetry.Should().Be(2);
    }

    [Fact]
    public void ApplyRedisTopology_RejectsUnsupportedProxyModes()
    {
        var configuration = ConfigurationOptions.Parse("localhost:6379");
        var options = new AuthorizationCacheOptions { RedisProxy = "HAProxy" };

        var act = () => CachingServiceExtensions.ApplyRedisTopology(configuration, options);

        act.Should().Throw<InvalidOperationException>().WithMessage("*HAProxy*");
    }

    [Fact]
    public void ApplyRedisTopology_RejectsMissingInputs()
    {
        var configuration = ConfigurationOptions.Parse("localhost:6379");

        var nullConfiguration = () => CachingServiceExtensions.ApplyRedisTopology(null!, new AuthorizationCacheOptions());
        var nullOptions = () => CachingServiceExtensions.ApplyRedisTopology(configuration, null!);

        nullConfiguration.Should().Throw<ArgumentNullException>();
        nullOptions.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_RejectsNegativeCompressionThreshold()
    {
        var options = new AuthorizationCacheOptions
        {
            L2CompressionThresholdBytes = -1
        };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*L2CompressionThresholdBytes*");
    }

    [Fact]
    public void Validate_RejectsInvalidTopologySettings()
    {
        var badProxy = new AuthorizationCacheOptions { RedisProxy = "Corvus" };
        var badTimeout = new AuthorizationCacheOptions { RedisConnectTimeoutMilliseconds = -5 };
        var badRetry = new AuthorizationCacheOptions { RedisConnectRetry = -1 };
        var emptyServiceName = new AuthorizationCacheOptions { RedisServiceName = "  " };

        ((Action)(() => badProxy.Validate())).Should().Throw<InvalidOperationException>().WithMessage("*RedisProxy*");
        ((Action)(() => badTimeout.Validate())).Should().Throw<InvalidOperationException>().WithMessage("*RedisConnectTimeoutMilliseconds*");
        ((Action)(() => badRetry.Validate())).Should().Throw<InvalidOperationException>().WithMessage("*RedisConnectRetry*");
        ((Action)(() => emptyServiceName.Validate())).Should().Throw<InvalidOperationException>().WithMessage("*RedisServiceName*");
    }

    [Fact]
    public void Validate_AcceptsDefaultCompressionAndTopologySettings()
    {
        var options = new AuthorizationCacheOptions
        {
            UseDistributedCache = true,
            RedisConnectionString = "localhost:6379",
            RedisServiceName = "mymaster",
            RedisProxy = "Twemproxy",
            RedisConnectTimeoutMilliseconds = 5_000,
            RedisConnectRetry = 3,
            L2CompressionEnabled = true,
            L2CompressionAlgorithm = L2CompressionAlgorithm.Brotli,
            L2CompressionThresholdBytes = 512
        };

        var act = () => options.Validate();

        act.Should().NotThrow();
    }
}
