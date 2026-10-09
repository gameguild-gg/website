using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests;

public sealed class PermissionCacheL2CompressionTests
{
    private static byte[] SampleJson(int approxBytes)
    {
        var payload = new { Roles = Enumerable.Range(0, Math.Max(1, approxBytes / 12)).Select(i => $"role-{i}").ToArray() };
        return JsonSerializer.SerializeToUtf8Bytes(payload);
    }

    [Fact]
    public void Wrap_Unwrap_RoundTripsGZipPayloads()
    {
        var payload = SampleJson(4_096);

        var wrapped = PermissionCacheL2Payload.Wrap(payload, L2CompressionAlgorithm.GZip);

        wrapped.Should().NotEqual(payload);
        wrapped.AsSpan(0, 4).ToArray().Should().Equal("GGC1"u8.ToArray());
        wrapped.Length.Should().BeLessThan(payload.Length);
        PermissionCacheL2Payload.IsEnveloped(wrapped).Should().BeTrue();

        PermissionCacheL2Payload.TryUnwrap(wrapped, out var plain).Should().BeTrue();
        plain.Should().Equal(payload);
    }

    [Fact]
    public void Wrap_Unwrap_RoundTripsBrotliPayloads()
    {
        var payload = SampleJson(4_096);

        var wrapped = PermissionCacheL2Payload.Wrap(payload, L2CompressionAlgorithm.Brotli);

        wrapped.Should().NotEqual(payload);
        wrapped[AlgorithmByteOffset].Should().Be(BrotliAlgorithmId);
        PermissionCacheL2Payload.TryUnwrap(wrapped, out var plain).Should().BeTrue();
        plain.Should().Equal(payload);
    }

    [Fact]
    public void TryUnwrap_PassesRawJsonThroughWithoutEnvelope()
    {
        var raw = Encoding.UTF8.GetBytes("""{"level":2}""");

        PermissionCacheL2Payload.IsEnveloped(raw).Should().BeFalse();
        PermissionCacheL2Payload.TryUnwrap(raw, out var plain).Should().BeTrue();

        plain.Should().Equal(raw);
    }

    [Fact]
    public void TryUnwrap_RejectsUnknownAlgorithmBytes()
    {
        var wrapped = PermissionCacheL2Payload.Wrap(SampleJson(2_048), L2CompressionAlgorithm.GZip);
        wrapped[AlgorithmByteOffset] = 0x7F;

        PermissionCacheL2Payload.TryUnwrap(wrapped, out var plain).Should().BeFalse();
        plain.Should().BeEmpty();
    }

    [Fact]
    public void TryUnwrap_RejectsCorruptedCompressedStreams()
    {
        var wrapped = PermissionCacheL2Payload.Wrap(SampleJson(2_048), L2CompressionAlgorithm.GZip);
        var corrupted = new byte[wrapped.Length];
        wrapped.CopyTo(corrupted, 0);
        for (var i = MagicLength + 1; i < corrupted.Length; i++)
        {
            corrupted[i] = 0x00;
        }

        PermissionCacheL2Payload.TryUnwrap(corrupted, out var plain).Should().BeFalse();
        plain.Should().BeEmpty();
    }

    [Fact]
    public void Wrap_WithNoneAlgorithm_Throws()
    {
        var act = () => PermissionCacheL2Payload.Wrap(SampleJson(32), L2CompressionAlgorithm.None);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SetValueAsync_CompressesLargeL2PayloadsAndRoundTripsReads()
    {
        var distributed = new Mock<IDistributedCache>(MockBehavior.Strict);
        var stored = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        distributed
            .Setup(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((key, value, _, _) => stored[key] = value)
            .Returns(Task.CompletedTask);
        distributed
            .Setup(cache => cache.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string key, CancellationToken _) => Task.FromResult(stored.TryGetValue(key, out var value) ? value : null));

        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateCache(memory, distributed.Object, new AuthorizationCacheOptions
        {
            UseDistributedCache = true,
            L2CompressionEnabled = true,
            L2CompressionAlgorithm = L2CompressionAlgorithm.GZip,
            L2CompressionThresholdBytes = 128
        });

        var largeValue = string.Join(",", Enumerable.Range(0, 200).Select(i => $"entry-{i}"));
        var first = await cache.GetAsync<string>("policy:large", "policy");
        first.Should().BeNull();
        await cache.SetAsync("policy:large", largeValue, "policy");

        var storedBytes = stored["policy:large"];
        PermissionCacheL2Payload.IsEnveloped(storedBytes).Should().BeTrue();
        storedBytes.Length.Should().BeLessThan(Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(largeValue)));

        // Remove from L1 so the read exercises the L2 decompression path.
        memory.Remove("policy:large");
        var roundTripped = await cache.GetAsync<string>("policy:large", "policy");
        roundTripped.Should().Be(largeValue);
    }

    [Fact]
    public async Task SetValueAsync_StoresRawJsonBelowTheThresholdAndRoundTripsReads()
    {
        var distributed = new Mock<IDistributedCache>(MockBehavior.Strict);
        var stored = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        distributed
            .Setup(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((key, value, _, _) => stored[key] = value)
            .Returns(Task.CompletedTask);

        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateCache(memory, distributed.Object, new AuthorizationCacheOptions
        {
            UseDistributedCache = true,
            L2CompressionEnabled = true,
            L2CompressionAlgorithm = L2CompressionAlgorithm.GZip,
            L2CompressionThresholdBytes = 8_192
        });

        await cache.SetAsync("policy:small", "tiny", "policy");

        var storedBytes = stored["policy:small"];
        PermissionCacheL2Payload.IsEnveloped(storedBytes).Should().BeFalse();
        Encoding.UTF8.GetString(storedBytes).Should().Be("\"tiny\"");
    }

    [Fact]
    public async Task SetValueAsync_StoresRawJsonWhenCompressionIsDisabled()
    {
        var distributed = new Mock<IDistributedCache>(MockBehavior.Strict);
        var stored = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        distributed
            .Setup(cache => cache.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((key, value, _, _) => stored[key] = value)
            .Returns(Task.CompletedTask);

        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateCache(memory, distributed.Object, new AuthorizationCacheOptions
        {
            UseDistributedCache = true,
            L2CompressionEnabled = false,
            L2CompressionThresholdBytes = 1
        });

        await cache.SetValueAsync("acl:raw", AccessLevel.Read, "acl");

        var storedBytes = stored["acl:raw"];
        PermissionCacheL2Payload.IsEnveloped(storedBytes).Should().BeFalse();
        storedBytes.Should().Equal(JsonSerializer.SerializeToUtf8Bytes(AccessLevel.Read));
    }

    [Fact]
    public async Task GetValueAsync_ReadsLegacyUncompressedEntries()
    {
        var distributed = new Mock<IDistributedCache>(MockBehavior.Strict);
        var raw = JsonSerializer.SerializeToUtf8Bytes(AccessLevel.Write);
        distributed
            .Setup(cache => cache.GetAsync("acl:legacy", It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<byte[]?>(raw));

        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = CreateCache(memory, distributed.Object, new AuthorizationCacheOptions
        {
            UseDistributedCache = true,
            L2CompressionEnabled = true,
            L2CompressionThresholdBytes = 1
        });

        var result = await cache.GetValueAsync<AccessLevel>("acl:legacy", "acl");

        result.Should().Be(CacheResult<AccessLevel>.Hit(AccessLevel.Write));
    }

    [Fact]
    public async Task GetValueAsync_TreatsUndecodableEnvelopedEntriesAsMisses()
    {
        var distributed = new Mock<IDistributedCache>(MockBehavior.Strict);
        var wrapped = PermissionCacheL2Payload.Wrap(SampleJson(2_048), L2CompressionAlgorithm.GZip);
        wrapped[AlgorithmByteOffset] = 0x7F;
        distributed
            .Setup(cache => cache.GetAsync("acl:bad", It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<byte[]?>(wrapped));

        using var memory = new MemoryCache(new MemoryCacheOptions());
        var metrics = new CacheMetricsService();
        var cache = CreateCache(memory, distributed.Object, new AuthorizationCacheOptions
        {
            UseDistributedCache = true,
            L2CompressionEnabled = true,
            L2CompressionThresholdBytes = 1
        }, metrics);

        var result = await cache.GetValueAsync<AccessLevel>("acl:bad", "acl");

        result.Found.Should().BeFalse();
        metrics.GetStatistics().Misses.Should().BeGreaterThan(0);
    }

    private const int MagicLength = 4;

    private const int AlgorithmByteOffset = 4;

    private const byte BrotliAlgorithmId = 2;

    private static HybridPermissionCache CreateCache(
        IMemoryCache memory,
        IDistributedCache distributedCache,
        AuthorizationCacheOptions options,
        CacheMetricsService? metrics = null)
    {
        options.AccessControlListTtlSeconds = 60;
        options.DistributedCacheTtlSeconds = Math.Max(options.DistributedCacheTtlSeconds, 600);
        metrics ??= new CacheMetricsService();
        return new HybridPermissionCache(
            memory,
            Options.Create(options),
            metrics,
            NullLogger<HybridPermissionCache>.Instance,
            distributedCache,
            new PermissionCacheKeyTracker(memory, metrics, 1_000));
    }
}
