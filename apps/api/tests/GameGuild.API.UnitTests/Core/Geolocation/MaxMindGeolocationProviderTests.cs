using FluentAssertions;
using GameGuild.API.Geolocation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.API.UnitTests.Core.Geolocation;

/// <summary>
///     MaxMind GeoLite2 adapter tests (issue #242): fail-closed behavior when configuration or
///     the database file is absent — every lookup resolves to null (Unknown), never throws.
/// </summary>
public sealed class MaxMindGeolocationProviderTests
{
    [Fact]
    public async Task NoDatabasePathConfigured_ResolvesToNull()
    {
        using var provider = new MaxMindGeolocationProvider((string?)null, NullLogger<MaxMindGeolocationProvider>.Instance);

        var result = await provider.ResolveAsync("203.0.113.25");

        result.Should().BeNull();
    }

    [Fact]
    public async Task MissingDatabaseFile_ResolvesToNull()
    {
        using var provider = new MaxMindGeolocationProvider("Z:/does-not-exist/GeoLite2-City.mmdb", NullLogger<MaxMindGeolocationProvider>.Instance);

        var result = await provider.ResolveAsync("203.0.113.25");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-ip")]
    public async Task MalformedIp_ResolvesToNull(string ipAddress)
    {
        using var provider = new MaxMindGeolocationProvider((string?)null, NullLogger<MaxMindGeolocationProvider>.Instance);

        var result = await provider.ResolveAsync(ipAddress);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("192.168.1.1")]
    [InlineData("172.16.0.9")]
    [InlineData("::1")]
    public async Task LoopbackAndPrivateAddresses_AreNotResolved(string ipAddress)
    {
        using var provider = new MaxMindGeolocationProvider((string?)null, NullLogger<MaxMindGeolocationProvider>.Instance);

        var result = await provider.ResolveAsync(ipAddress);

        result.Should().BeNull();
    }
}
