using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class VersionedUserTokenRevocationTests
{
    public static IEnumerable<object?[]> Boundaries =>
        from distributed in new[] { false, true }
        from scenario in new (int? Version, bool After, bool Bound, bool Revoked)[]
        {
            (1, false, true, true), (2, false, true, false), (3, false, true, false),
            (null, false, true, true), (2, false, false, true), (1, true, true, false),
            (null, true, true, false), (0, false, true, true)
        }
        select new object?[] { distributed, scenario.Version, scenario.After, scenario.Bound, scenario.Revoked };

    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task VersionBoundaryAllowsCurrentSameSecondTokensButPreservesLegacyAndOrdinaryCutoffs(
        bool distributed, int? version, bool after, bool bound, bool expected)
    {
        using var fixture = new StoreFixture(distributed);
        var user = Guid.NewGuid();
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).AddMilliseconds(500);
        SystemClock.SetProvider(new FixedClock(now));
        try
        {
            if (bound)
            {
                await fixture.Versioned.RevokeAllUserTokensAsync(user, 2, "Synthetic revocation", CancellationToken.None);
            }
            else
            {
                await fixture.Ordinary.RevokeAllUserTokensAsync(user, "Synthetic revocation");
            }
            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds() + (after ? 1 : 0)).UtcDateTime;
            Assert.Equal(expected, await fixture.Versioned.IsUserTokenRevokedAsync(user, issuedAt, version, CancellationToken.None));
            Assert.Equal(!after, await fixture.Ordinary.IsUserTokenRevokedAsync(user, issuedAt));
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, -1)]
    [InlineData(true, 0)]
    [InlineData(true, -1)]
    public async Task InvalidMinimumVersionCannotWriteRevocation(bool distributed, int version)
    {
        using var fixture = new StoreFixture(distributed);
        var user = Guid.NewGuid();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Versioned.RevokeAllUserTokensAsync(user, version, null, CancellationToken.None));
        Assert.False(await fixture.Versioned.IsUserTokenRevokedAsync(user, DateTime.UtcNow.AddMinutes(-1), 1, CancellationToken.None));
    }

    [Fact]
    public async Task ExistingDistributedPayloadWithoutVersionStillRevokesVersionedAndLegacyTokens()
    {
        using var fixture = new StoreFixture(true);
        var user = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await fixture.Cache.SetStringAsync("auth:user-token-revoked-at:" + user.ToString("N"),
            JsonSerializer.Serialize(new { userId = user, revokedAt = now, reason = "Existing time-only payload" }));
        Assert.True(await fixture.Versioned.IsUserTokenRevokedAsync(user, now.AddSeconds(-1), 2, CancellationToken.None));
        Assert.True(await fixture.Versioned.IsUserTokenRevokedAsync(user, now.AddSeconds(-1), null, CancellationToken.None));
        Assert.False(await fixture.Versioned.IsUserTokenRevokedAsync(user, now.AddSeconds(1), 2, CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VersionedRevocationCannotAffectAnotherUser(bool distributed)
    {
        using var fixture = new StoreFixture(distributed);
        await fixture.Versioned.RevokeAllUserTokensAsync(Guid.NewGuid(), 2, null, CancellationToken.None);
        Assert.False(await fixture.Versioned.IsUserTokenRevokedAsync(Guid.NewGuid(), DateTime.UtcNow.AddHours(-1), 1, CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledVersionedWriteCannotPublishACutoff(bool distributed)
    {
        using var fixture = new StoreFixture(distributed);
        var user = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Versioned.RevokeAllUserTokensAsync(user, 2, null, cancellation.Token));
        Assert.False(await fixture.Versioned.IsUserTokenRevokedAsync(user, DateTime.UtcNow.AddHours(-1), 1, CancellationToken.None));
    }

    private sealed class StoreFixture : IDisposable
    {
        private readonly ServiceProvider _services = new ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider();
        public IDistributedCache Cache => _services.GetRequiredService<IDistributedCache>();
        public ITokenRevocationService Ordinary { get; }
        public IVersionedUserTokenRevocationService Versioned { get; }

        public StoreFixture(bool distributed)
        {
            Ordinary = distributed
                ? new DistributedCacheTokenRevocationService(Cache, NullLogger<DistributedCacheTokenRevocationService>.Instance)
                : new InMemoryTokenRevocationService(NullLogger<InMemoryTokenRevocationService>.Instance);
            Versioned = (IVersionedUserTokenRevocationService)Ordinary;
        }

        public void Dispose() => _services.Dispose();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
