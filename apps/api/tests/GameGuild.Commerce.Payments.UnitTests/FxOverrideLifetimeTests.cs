using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests;

public class FxOverrideLifetimeTests : IDisposable
{
    private readonly MutableClock _clock = new();

    public void Dispose() => SystemClock.Reset();

    [Fact]
    public void RuntimeOverride_ExpiresExactlyAtDeadlineAndFallsBackToMarket()
    {
        SystemClock.SetProvider(_clock);
        using var services = Services(NewDatabase());
        var source = Runtime(services);
        source.ApplyProviderSet(Grid());
        source.ApplyOverride(Rate(), _clock.GetUtcNow().UtcDateTime.AddMinutes(1));
        source.TryGetRate("USD", "EUR", _clock.GetUtcNow().UtcDateTime)!.Rate.Should().Be(0.75m);

        _clock.Advance(TimeSpan.FromMinutes(1));

        source.TryGetRate("USD", "EUR", _clock.GetUtcNow().UtcDateTime)!.Rate.Should().Be(0.9m);
        source.CurrentOverrides.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConversionCache_CannotExtendOverrideLifetimeOrHideANewOverride(bool distributed)
    {
        SystemClock.SetProvider(_clock);
        using var services = Services(NewDatabase());
        var source = Runtime(services);
        source.ApplyProviderSet(Grid());
        var backing = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var cache = new FxRateCache(distributed ? backing : null, Options.Create(new FxSyncOptions()), NullLogger<FxRateCache>.Instance);
        var provider = new PersistedFxRateProvider(cache, source);
        (await provider.GetRateAsync("USD", "EUR"))!.Rate.Should().Be(0.9m);

        source.ApplyOverride(Rate(), _clock.GetUtcNow().UtcDateTime.AddMinutes(1));
        (await provider.GetRateAsync("USD", "EUR"))!.Rate.Should().Be(0.75m);
        _clock.Advance(TimeSpan.FromMinutes(1));
        (await provider.GetRateAsync("USD", "EUR"))!.Rate.Should().Be(0.9m);

        // Ignore a value written by the old implementation, which had no expiry metadata.
        await cache.SetAsync(Rate());
        (await provider.GetRateAsync("USD", "EUR"))!.Rate.Should().Be(0.9m);
    }

    [Fact]
    public async Task SeededOverride_ExpiresAfterLoadingWithoutRestartOrResurrectingOlderValue()
    {
        SystemClock.SetProvider(_clock);
        await using var db = NewDatabase();
        using var services = Services(db);
        var now = _clock.GetUtcNow().UtcDateTime;
        db.AddRange(Override(0.7m, now.AddDays(-1)), Override(0.75m, now, now.AddMinutes(1)));
        await db.SaveChangesAsync();
        var source = Runtime(services);
        await source.EnsureSeededAsync();
        source.TryGetRate("USD", "EUR", now)!.Rate.Should().Be(0.75m);

        _clock.Advance(TimeSpan.FromMinutes(1));
        source.TryGetRate("USD", "EUR", _clock.GetUtcNow().UtcDateTime).Should().BeNull();
        source.CurrentOverrides.Should().BeEmpty();
        var restarted = Runtime(services);
        await restarted.EnsureSeededAsync();
        restarted.TryGetRate("USD", "EUR", _clock.GetUtcNow().UtcDateTime).Should().BeNull();
    }

    [Fact]
    public async Task RuntimeSource_RefreshesAnotherProcessesPersistedOverrideAfterCacheTtl()
    {
        SystemClock.SetProvider(_clock);
        await using var db = NewDatabase();
        using var services = Services(db);
        var source = Runtime(services, 1);
        await source.EnsureSeededAsync();
        source.TryGetRate("USD", "EUR", _clock.GetUtcNow().UtcDateTime).Should().BeNull();
        db.Add(Override(0.81m, _clock.GetUtcNow().UtcDateTime));
        await db.SaveChangesAsync();
        _clock.Advance(TimeSpan.FromMinutes(1));

        await source.EnsureSeededAsync();

        source.TryGetRate("USD", "EUR", _clock.GetUtcNow().UtcDateTime)!.Rate.Should().Be(0.81m);
    }

    [Fact]
    public async Task ReplacingAnExpiringOverride_UpdatesTheSameRowAndKeepsItsExpiry()
    {
        SystemClock.SetProvider(_clock);
        await using var db = NewDatabase();
        using var services = Services(db);
        var source = Runtime(services);
        var handler = new SetManualFxRateOverrideCommandHandler(db, Administrator(), source, Mock.Of<IFxRateCache>());
        var expiry = _clock.GetUtcNow().UtcDateTime.AddMinutes(1);
        var id = await handler.Handle(new SetManualFxRateOverrideCommand("USD", "EUR", 0.75m, "first", expiry), default);

        var replacement = await handler.Handle(new SetManualFxRateOverrideCommand("USD", "EUR", 0.76m, "replace", expiry), default);

        replacement.Should().Be(id);
        db.Set<FxRateSnapshot>().Should().ContainSingle().Which.ExpiresAtUtc.Should().Be(expiry);
        source.TryGetRate("USD", "EUR", _clock.GetUtcNow().UtcDateTime)!.Rate.Should().Be(0.76m);
        _clock.Advance(TimeSpan.FromMinutes(1));
        source.TryGetRate("USD", "EUR", _clock.GetUtcNow().UtcDateTime).Should().BeNull();
    }

    [Fact]
    public async Task FirstOverrideCommand_DoesNotPreventLoadingOtherPersistedRates()
    {
        SystemClock.SetProvider(_clock);
        await using var db = NewDatabase();
        using var services = Services(db);
        var existing = Override(0.8m, _clock.GetUtcNow().UtcDateTime);
        existing.QuoteCurrency = "GBP";
        db.Add(existing);
        await db.SaveChangesAsync();
        var source = Runtime(services);
        var handler = new SetManualFxRateOverrideCommandHandler(db, Administrator(), source, Mock.Of<IFxRateCache>());

        await handler.Handle(new SetManualFxRateOverrideCommand("USD", "EUR", 0.75m, "first"), default);

        source.TryGetRate("USD", "GBP", _clock.GetUtcNow().UtcDateTime)!.Rate.Should().Be(0.8m);
        source.TryGetRate("USD", "EUR", _clock.GetUtcNow().UtcDateTime)!.Rate.Should().Be(0.75m);
    }

    [Fact]
    public async Task FailedInitialSeed_IsRetriedOnNextLookup()
    {
        await using var db = NewDatabase();
        db.Add(Override(0.75m, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var attempts = 0;
        using var services = new ServiceCollection().AddSingleton<IApplicationDbContext>(_ =>
        {
            if (Interlocked.Increment(ref attempts) == 1) throw new InvalidOperationException("Transient database acquisition failure");
            return db;
        }).BuildServiceProvider();
        var source = Runtime(services);

        await source.EnsureSeededAsync();
        source.TryGetRate("USD", "EUR", DateTime.UtcNow).Should().BeNull();
        await source.EnsureSeededAsync();

        source.TryGetRate("USD", "EUR", DateTime.UtcNow)!.Rate.Should().Be(0.75m);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task ExpiredOverrideRequest_IsRejectedBeforePersistence(int seconds)
    {
        SystemClock.SetProvider(_clock);
        await using var db = NewDatabase();
        using var services = Services(db);
        var handler = new SetManualFxRateOverrideCommandHandler(db, Administrator(), Runtime(services), Mock.Of<IFxRateCache>());
        var act = () => handler.Handle(new SetManualFxRateOverrideCommand("USD", "EUR", 0.75m, "expired", _clock.GetUtcNow().UtcDateTime.AddSeconds(seconds)), default);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>()).Which.Rule.Should().Be("InvalidFxOverrideExpiry");
        db.Set<FxRateSnapshot>().Should().BeEmpty();
    }

    [Fact]
    public async Task FreshManualOverride_DoesNotHideStaleMarketDataInSyncStatus()
    {
        SystemClock.SetProvider(_clock);
        await using var db = NewDatabase();
        var now = _clock.GetUtcNow().UtcDateTime;
        var market = Override(0.9m, now.AddDays(-10));
        market.IsOverride = false;
        market.ProviderName = "openexchangerates";
        market.Source = "latest";
        db.AddRange(market, Override(0.75m, now));
        await db.SaveChangesAsync();

        var status = await new GetFxSyncStatusQueryHandler(db, Options.Create(new FxSyncOptions()), Administrator())
            .Handle(new GetFxSyncStatusQuery(), default);

        status.IsStale.Should().BeTrue();
        status.NewestSnapshotEffectiveDateUtc.Should().Be(now.AddDays(-10).Date);
    }

    private static CurrencyFxTestDbContext NewDatabase() => new(
        new DbContextOptionsBuilder<CurrencyFxTestDbContext>().UseInMemoryDatabase($"fx-expiry-{Guid.NewGuid():N}").Options);

    private static ServiceProvider Services(CurrencyFxTestDbContext db) => new ServiceCollection().AddSingleton<IApplicationDbContext>(db).BuildServiceProvider();

    private static FxRuntimeRateSource Runtime(ServiceProvider services, int ttl = 60) => new(
        services.GetRequiredService<IServiceScopeFactory>(), NullLogger<FxRuntimeRateSource>.Instance,
        Options.Create(new FxSyncOptions { CacheTtlMinutes = ttl }));

    private static ExchangeRate Rate() => new("USD", "EUR", 0.75m, new DateTimeOffset(SystemClock.UtcNow, TimeSpan.Zero), "manual-override");

    private static FxRateSet Grid() => new("openexchangerates", "USD", new Dictionary<string, decimal> { ["EUR"] = 0.9m },
        new DateTimeOffset(SystemClock.UtcNow, TimeSpan.Zero), SystemClock.UtcNow.Date, "", "application/json");

    private static FxRateSnapshot Override(decimal rate, DateTime asOf, DateTime? expiry = null) => new()
    {
        BaseCurrency = "USD", QuoteCurrency = "EUR", Rate = rate, IsOverride = true,
        AsOfUtc = asOf, EffectiveDateUtc = asOf.Date, ProviderName = "manual", Source = "manual-override", ExpiresAtUtc = expiry,
    };

    private static IActorContextAccessor Administrator()
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(new ActorContext
        {
            SubjectId = Guid.NewGuid().ToString(), IsAuthenticated = true,
            Roles = new HashSet<string> { "SystemAdmin" }, ActorKind = ActorKind.User,
            Permissions = new HashSet<string>(), TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
        });
        return accessor.Object;
    }

    private sealed class MutableClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan interval) => _now = _now.Add(interval);
    }
}
