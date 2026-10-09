using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     A TimeProvider whose clock only moves when the test advances it, so feed reload
///     intervals can be exercised deterministically.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan elapsed) => _utcNow += elapsed;
}

public sealed class LocalFileThreatIntelligenceProviderTests : IDisposable
{
    private readonly string _feedDirectory;
    private readonly ManualTimeProvider _timeProvider = new();
    private readonly Mock<IAuthenticationAuditEventSink> _auditSinkMock = new();
    private readonly IServiceScopeFactory _scopeFactory;

    public LocalFileThreatIntelligenceProviderTests()
    {
        _feedDirectory = Path.Combine(Path.GetTempPath(), $"gg-threat-intel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_feedDirectory);

        var services = new ServiceCollection();
        services.AddSingleton(_auditSinkMock.Object);
        _scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_feedDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Temporary directory cleanup is best-effort.
        }
    }

    // ── CIDR matching ─────────────────────────────────────────────────

    [Fact]
    public async Task CheckIpAddressAsync_IpInsideIpv4Cidr_Matches()
    {
        var feedPath = WriteFeed("""{ "maliciousIpCidrs": [ "203.0.113.0/24" ] }""");
        var sut = CreateProvider(feedPath);

        var result = await sut.CheckIpAddressAsync("203.0.113.42");

        result.IsMatch.Should().BeTrue();
        result.MatchedCidr.Should().Be("203.0.113.0/24");
        result.ProviderAvailable.Should().BeTrue();
        result.ProviderError.Should().BeNull();
    }

    [Fact]
    public async Task CheckIpAddressAsync_IpOutsideCidr_DoesNotMatch()
    {
        var feedPath = WriteFeed("""{ "maliciousIpCidrs": [ "203.0.113.0/24" ] }""");
        var sut = CreateProvider(feedPath);

        var result = await sut.CheckIpAddressAsync("198.51.100.7");

        result.IsMatch.Should().BeFalse();
        result.MatchedCidr.Should().BeNull();
    }

    [Fact]
    public async Task CheckIpAddressAsync_Ipv6Cidr_MatchesWithinPrefix()
    {
        var feedPath = WriteFeed("""{ "maliciousIpCidrs": [ "2001:db8::/32" ] }""");
        var sut = CreateProvider(feedPath);

        (await sut.CheckIpAddressAsync("2001:db8:1234::1")).IsMatch.Should().BeTrue();
        (await sut.CheckIpAddressAsync("2001:db9::1")).IsMatch.Should().BeFalse();
    }

    [Fact]
    public async Task CheckIpAddressAsync_Ipv4MappedIpv6Input_NormalizesToIpv4AndMatches()
    {
        var feedPath = WriteFeed("""{ "maliciousIpCidrs": [ "203.0.113.0/24" ] }""");
        var sut = CreateProvider(feedPath);

        var result = await sut.CheckIpAddressAsync("::ffff:203.0.113.5");

        result.IsMatch.Should().BeTrue();
        result.MatchedCidr.Should().Be("203.0.113.0/24");
    }

    [Fact]
    public async Task CheckIpAddressAsync_CidrWithHostBitsSet_MasksHostBits()
    {
        var feedPath = WriteFeed("""{ "maliciousIpCidrs": [ "203.0.113.37/24" ] }""");
        var sut = CreateProvider(feedPath);

        var result = await sut.CheckIpAddressAsync("203.0.113.200");

        result.IsMatch.Should().BeTrue();
    }

    [Fact]
    public async Task CheckIpAddressAsync_InvalidCidrEntries_AreSkippedButValidEntriesLoad()
    {
        var feedPath = WriteFeed("""{ "maliciousIpCidrs": [ "not-a-cidr", "10.0.0.0/8", "300.0.0.0/8", "10.0.0.0/99" ] }""");
        var sut = CreateProvider(feedPath);

        (await sut.CheckIpAddressAsync("10.1.2.3")).IsMatch.Should().BeTrue();
        (await sut.CheckIpAddressAsync("203.0.113.1")).IsMatch.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unknown")]
    [InlineData("not-an-ip")]
    public async Task CheckIpAddressAsync_UnparseableIp_NeverMatchesAndDoesNotThrow(string? ipAddress)
    {
        var feedPath = WriteFeed("""{ "maliciousIpCidrs": [ "203.0.113.0/24" ] }""");
        var sut = CreateProvider(feedPath);

        var result = await sut.CheckIpAddressAsync(ipAddress);

        result.IsMatch.Should().BeFalse();
    }

    // ── Breached-password hash matching ───────────────────────────────

    [Fact]
    public async Task CheckPasswordHashAsync_KnownBreachedDigest_MatchesCaseInsensitively()
    {
        // SHA-256("password") — a canonical breached-credential entry.
        const string digestLower = "5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8";
        var feedPath = WriteFeed($$"""{ "breachedPasswordSha256": [ "{{digestLower}}" ] }""");
        var sut = CreateProvider(feedPath);

        (await sut.CheckPasswordHashAsync(digestLower)).IsMatch.Should().BeTrue();
        (await sut.CheckPasswordHashAsync(digestLower.ToUpperInvariant())).IsMatch.Should().BeTrue();

        var unknown = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("not-in-corpus-" + Guid.NewGuid())));
        (await sut.CheckPasswordHashAsync(unknown)).IsMatch.Should().BeFalse();
    }

    [Fact]
    public async Task CheckPasswordHashAsync_MalformedDigests_NeverMatchAndInvalidFeedEntriesAreSkipped()
    {
        var feedPath = WriteFeed("""{ "breachedPasswordSha256": [ "not-hex", "5e88", null ] }""");
        var sut = CreateProvider(feedPath);

        (await sut.CheckPasswordHashAsync("not-hex")).IsMatch.Should().BeFalse();
        (await sut.CheckPasswordHashAsync(null)).IsMatch.Should().BeFalse();
        (await sut.CheckPasswordHashAsync("  5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8  ")).IsMatch.Should().BeFalse();
    }

    [Fact]
    public async Task CheckPasswordHashAsync_Sha256DigestIsCaseInsensitiveOnBothSides()
    {
        const string digestUpper = "5E884898DA28047151D0E56F8DC6292773603D0D6AABBDD62A11EF721D1542D8";
        var feedPath = WriteFeed($$"""{ "breachedPasswordSha256": [ "{{digestUpper}}" ] }""");
        var sut = CreateProvider(feedPath);

        (await sut.CheckPasswordHashAsync(digestUpper.ToLowerInvariant())).IsMatch.Should().BeTrue();
    }

    // ── Fail-open behavior ────────────────────────────────────────────

    [Fact]
    public async Task MissingFeedFile_FailsOpen_ReportsUnavailable_AndEmitsOneSecurityEventPerOutage()
    {
        var sut = CreateProvider(Path.Combine(_feedDirectory, "does-not-exist.json"));

        var first = await sut.CheckIpAddressAsync("203.0.113.42");
        var second = await sut.CheckIpAddressAsync("203.0.113.42");
        var password = await sut.CheckPasswordHashAsync("5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8");

        first.IsMatch.Should().BeFalse();
        first.ProviderAvailable.Should().BeFalse();
        first.ProviderError.Should().NotBeNullOrWhiteSpace();
        second.IsMatch.Should().BeFalse();
        second.ProviderAvailable.Should().BeFalse();
        password.IsMatch.Should().BeFalse();
        password.ProviderAvailable.Should().BeFalse();

        // The outage is announced exactly once until the feed recovers.
        _auditSinkMock.Verify(
            sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(e =>
                    e.ActionType == "Authentication.ThreatIntelligenceFeedUnavailable"
                    && e.Success == false
                    && e.ErrorMessage != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CorruptFeedFile_FailsOpen_WithoutThrowing()
    {
        var feedPath = Path.Combine(_feedDirectory, "corrupt.json");
        await File.WriteAllTextAsync(feedPath, "{ this is not json");
        var sut = CreateProvider(feedPath);

        var result = await sut.CheckIpAddressAsync("203.0.113.42");

        result.IsMatch.Should().BeFalse();
        result.ProviderAvailable.Should().BeFalse();
        result.ProviderError.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task FeedRecovers_AfterOutage_AndNextOutageEmitsAnotherEvent()
    {
        var feedPath = Path.Combine(_feedDirectory, "recovering.json");
        var sut = CreateProvider(feedPath);

        // Outage 1: file missing.
        (await sut.CheckIpAddressAsync("203.0.113.42")).ProviderAvailable.Should().BeFalse();

        // Operator drops a valid feed.
        WriteFeed("""{ "maliciousIpCidrs": [ "203.0.113.0/24" ] }""", feedPath, new DateTime(2026, 1, 1, 0, 1, 0, DateTimeKind.Utc));
        var recovered = await sut.CheckIpAddressAsync("203.0.113.42");
        recovered.ProviderAvailable.Should().BeTrue();
        recovered.IsMatch.Should().BeTrue();

        // Outage 2: feed becomes unreadable again after the reload interval; the last-good
        // feed keeps being served (fail-open never blocks), but a fresh security event fires.
        File.Delete(feedPath);
        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        var secondOutage = await sut.CheckIpAddressAsync("203.0.113.42");
        secondOutage.IsMatch.Should().BeTrue("the recovered feed keeps being served");
        secondOutage.ProviderAvailable.Should().BeTrue();
        secondOutage.ProviderError.Should().NotBeNullOrWhiteSpace();

        _auditSinkMock.Verify(
            sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(e => e.ActionType == "Authentication.ThreatIntelligenceFeedUnavailable"),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task FeedBecomesUnreadable_KeepsServingLastGoodData()
    {
        var feedPath = WriteFeed("""{ "maliciousIpCidrs": [ "203.0.113.0/24" ] }""");
        var sut = CreateProvider(feedPath);

        (await sut.CheckIpAddressAsync("203.0.113.42")).IsMatch.Should().BeTrue();

        await File.WriteAllTextAsync(feedPath, "{ broken");
        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        var stale = await sut.CheckIpAddressAsync("203.0.113.42");
        stale.IsMatch.Should().BeTrue("the last-good feed keeps being served while reloads fail");
        stale.ProviderAvailable.Should().BeTrue("data is still usable even though the last load failed");
        stale.ProviderError.Should().NotBeNullOrWhiteSpace("the degradation is still surfaced");
    }

    // ── Reload behavior ───────────────────────────────────────────────

    [Fact]
    public async Task ReloadOnFileChange_PicksUpNewFeedImmediately()
    {
        var feedPath = WriteFeed("""{ "maliciousIpCidrs": [ "203.0.113.0/24" ] }""");
        var sut = CreateProvider(feedPath);

        (await sut.CheckIpAddressAsync("203.0.113.42")).IsMatch.Should().BeTrue();
        (await sut.CheckIpAddressAsync("198.51.100.7")).IsMatch.Should().BeFalse();

        WriteFeed("""{ "maliciousIpCidrs": [ "198.51.100.0/24" ] }""", feedPath, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        var afterChange = await sut.CheckIpAddressAsync("198.51.100.7");
        afterChange.IsMatch.Should().BeTrue("the write-time change triggers a reload before the interval elapses");
        (await sut.CheckIpAddressAsync("203.0.113.42")).IsMatch.Should().BeFalse();
    }

    [Fact]
    public async Task ReloadInterval_FileUnchangedBetweenChecks_DoesNotReload()
    {
        var fixedWriteTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var feedPath = WriteFeed("""{ "maliciousIpCidrs": [ "203.0.113.0/24" ] }""", writeTimeUtc: fixedWriteTime);
        var sut = CreateProvider(feedPath);

        (await sut.CheckIpAddressAsync("203.0.113.42")).IsMatch.Should().BeTrue();

        // Replace the feed but keep the write time identical: no reload signal.
        WriteFeed("""{ "maliciousIpCidrs": [ "198.51.100.0/24" ] }""", feedPath, fixedWriteTime);
        (await sut.CheckIpAddressAsync("198.51.100.7")).IsMatch.Should().BeFalse("no reload happened");

        // After the interval elapses, the periodic reload picks the new content up.
        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        (await sut.CheckIpAddressAsync("198.51.100.7")).IsMatch.Should().BeTrue();
    }

    [Fact]
    public async Task ReloadOnFileChange_Disabled_WaitsForInterval()
    {
        var fixedWriteTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var feedPath = WriteFeed("""{ "maliciousIpCidrs": [ "203.0.113.0/24" ] }""", writeTimeUtc: fixedWriteTime);
        var sut = CreateProvider(feedPath, reloadOnFileChange: false);

        (await sut.CheckIpAddressAsync("203.0.113.42")).IsMatch.Should().BeTrue();

        WriteFeed("""{ "maliciousIpCidrs": [ "198.51.100.0/24" ] }""", feedPath, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        (await sut.CheckIpAddressAsync("198.51.100.7")).IsMatch.Should().BeFalse("file-change reloads are disabled");

        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        (await sut.CheckIpAddressAsync("198.51.100.7")).IsMatch.Should().BeTrue();
    }

    // ── Disabled provider ─────────────────────────────────────────────

    [Fact]
    public async Task NullProvider_IsANoOp()
    {
        var ip = await NullThreatIntelligenceProvider.Instance.CheckIpAddressAsync("203.0.113.42");
        var password = await NullThreatIntelligenceProvider.Instance.CheckPasswordHashAsync("5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8");

        ip.IsMatch.Should().BeFalse();
        ip.ProviderAvailable.Should().BeTrue();
        password.IsMatch.Should().BeFalse();
        password.ProviderAvailable.Should().BeTrue();
        NullThreatIntelligenceProvider.Instance.ProviderName.Should().Be("None");
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private LocalFileThreatIntelligenceProvider CreateProvider(
        string feedPath,
        bool reloadOnFileChange = true,
        TimeSpan? reloadInterval = null)
        => new(
            new ThreatIntelligenceOptions
            {
                Provider = ThreatIntelligenceOptions.LocalFileProvider,
                LocalFile = new LocalFileThreatIntelligenceOptions
                {
                    FilePath = feedPath,
                    ReloadInterval = reloadInterval ?? TimeSpan.FromMinutes(5),
                    ReloadOnFileChange = reloadOnFileChange
                }
            },
            _timeProvider,
            NullLogger<LocalFileThreatIntelligenceProvider>.Instance,
            _scopeFactory);

    private string WriteFeed(string json, string? path = null, DateTime? writeTimeUtc = null)
    {
        var feedPath = path ?? Path.Combine(_feedDirectory, $"feed-{Guid.NewGuid():N}.json");
        File.WriteAllText(feedPath, json);
        File.SetLastWriteTimeUtc(feedPath, writeTimeUtc ?? DateTime.UtcNow);
        return feedPath;
    }
}
