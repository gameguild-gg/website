using System.Net;
using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests;

/// <summary>
///     FX rate sync foundation (issue #412 spec B): config-gated providers, retry with
///     exponential backoff, fallback, snapshot persistence with rate-change audit, consecutive
///     failure alerting, staleness auditing, the runtime rate source (override &gt; grid,
///     fail-closed derivation inside a single grid), the TTL cache in front of the conversion
///     seam, the audited manual override command, the fail-closed manual sync guard and the
///     Stripe minor-unit scaling fix for three-decimal currencies.
/// </summary>
public class FxRateSyncTests
{
    private readonly Mock<IFxRateSyncAlerter> _alerter = new();
    private readonly FxSyncOptions _options = new()
    {
        Provider = FxSyncOptions.ProviderOpenExchangeRates,
        RetryMaxAttempts = 2,
        RetryBaseDelaySeconds = 0,
        AlertAfterConsecutiveFailures = 1,
        StalenessAlertDays = 2,
    };

    // ────────────────────────────── Sync service ──────────────────────────────

    [Fact]
    public async Task Sync_Success_PersistsSnapshotsAndRun_AndRefreshesRuntimeSource()
    {
        var (service, db, runtime) = BuildSyncService(new StubFxRateProvider("openexchangerates",
            _ => NewRateSet(new Dictionary<string, decimal> { ["EUR"] = 0.9m, ["JPY"] = 150m })));

        var result = await service.SyncAsync();

        result.Status.Should().Be(FxRateSyncRunStatus.Succeeded);
        result.RatesApplied.Should().Be(2);
        result.RatesChanged.Should().Be(0);
        result.ConsecutiveFailures.Should().Be(0);
        result.ProviderName.Should().Be("openexchangerates");

        var snapshots = db.Set<FxRateSnapshot>().ToList();
        snapshots.Should().OnlyContain(snapshot => !snapshot.IsOverride);
        snapshots.Single(row => row.QuoteCurrency == "EUR").Rate.Should().Be(0.9m);
        snapshots.Single(row => row.QuoteCurrency == "JPY").Rate.Should().Be(150m);

        var run = db.Set<FxRateSyncRun>().Single();
        run.Status.Should().Be(FxRateSyncRunStatus.Succeeded);
        run.RawResponse.Should().Contain("raw-openexchangerates");
        run.RawResponseContentType.Should().Be("application/json");
        snapshots.Should().OnlyContain(row => row.SyncRunId == run.Id);

        // Run summary audit entry (rate-change audit log).
        db.Set<AuditTrail>()
            .Should().Contain(audit => audit.EntityType == nameof(FxRateSyncRun) && audit.Action == AuditAction.Created);

        runtime.CurrentProviderSet.Should().NotBeNull();
        runtime.CurrentProviderSet!.Rates["EUR"].Should().Be(0.9m);
    }

    [Fact]
    public async Task Sync_ChangedRate_UpdatesInPlace_AndWritesRateChangeAudit()
    {
        var grid = new Dictionary<string, decimal> { ["EUR"] = 0.95m };
        var (service, db, _) = BuildSyncService(new StubFxRateProvider("openexchangerates", _ => NewRateSet(grid)));

        // Pre-existing quotation for the same provider/date/source — the re-sync target.
        var asOf = SystemClock.UtcNow;
        db.Set<FxRateSnapshot>().Add(new FxRateSnapshot
        {
            BaseCurrency = "USD",
            QuoteCurrency = "EUR",
            Rate = 0.9m,
            EffectiveDateUtc = asOf.Date,
            AsOfUtc = asOf,
            ProviderName = "openexchangerates",
            Source = "latest",
            IsOverride = false,
        });
        await db.SaveChangesAsync();

        var result = await service.SyncAsync();

        result.RatesApplied.Should().Be(1);
        result.RatesChanged.Should().Be(1);
        db.Set<FxRateSnapshot>().Should().HaveCount(1);
        db.Set<FxRateSnapshot>().Single().Rate.Should().Be(0.95m);

        var changeAudit = db.Set<AuditTrail>().Single(audit =>
            audit.EntityType == nameof(FxRateSnapshot) && audit.Action == AuditAction.Updated);
        changeAudit.OldValue.Should().Contain("0.9");
        changeAudit.NewValue.Should().Contain("0.95");
    }

    [Fact]
    public async Task Sync_ProviderFails_PersistsFailedRun_CountsConsecutiveFailures_Alerts()
    {
        var failing = new StubFxRateProvider("openexchangerates", _ => throw new HttpRequestException("boom"));
        var (service, db, _) = BuildSyncService(failing);

        var first = await service.SyncAsync();
        var second = await service.SyncAsync();

        first.Status.Should().Be(FxRateSyncRunStatus.Failed);
        first.ConsecutiveFailures.Should().Be(1);
        second.ConsecutiveFailures.Should().Be(2);

        // Retry with backoff was applied before giving up: 2 attempts per sync pass x 2 passes.
        failing.Attempts.Should().Be(4);

        var runs = db.Set<FxRateSyncRun>().ToList();
        runs.Should().OnlyContain(run => run.Status == FxRateSyncRunStatus.Failed);
        runs.OrderByDescending(run => run.StartedAtUtc).First().ConsecutiveFailureCount.Should().Be(2);

        // Fail-closed: nothing persisted except the failed runs, and operators were alerted.
        db.Set<FxRateSnapshot>().Should().BeEmpty();
        _alerter.Verify(
            alerter => alerter.RaiseSyncFailureAlertAsync(
                It.IsAny<string>(), It.Is<int>(failures => failures >= 1), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Sync_PrimaryUnavailable_FailsOverToFallbackProvider()
    {
        var (service, db, _) = BuildSyncService(
            new StubFxRateProvider("openexchangerates", _ => throw new HttpRequestException("primary down")),
            new StubFxRateProvider("ecb", _ => new FxRateSet(
                "ecb", "EUR",
                new Dictionary<string, decimal> { ["USD"] = 1.1m },
                new DateTimeOffset(SystemClock.UtcNow, TimeSpan.Zero),
                SystemClock.UtcNow.Date,
                "<xml/>ecb",
                "application/xml")),
            fallbackProviderName: FxSyncOptions.ProviderEcb);

        var result = await service.SyncAsync();

        result.Status.Should().Be(FxRateSyncRunStatus.Succeeded);
        result.ProviderName.Should().Be("ecb");
        db.Set<FxRateSnapshot>().Single(row => row.QuoteCurrency == "USD").BaseCurrency.Should().Be("EUR");
        db.Set<FxRateSyncRun>().Single().ProviderName.Should().Be("ecb");
    }

    [Fact]
    public async Task Sync_TransientFailureOnFirstAttempt_SucceedsAfterRetry()
    {
        var provider = new StubFxRateProvider("openexchangerates", attempts => attempts == 1
            ? throw new HttpRequestException("transient")
            : NewRateSet(new Dictionary<string, decimal> { ["EUR"] = 0.9m }));
        var (service, db, _) = BuildSyncService(provider);

        var result = await service.SyncAsync();

        result.Status.Should().Be(FxRateSyncRunStatus.Succeeded);
        provider.Attempts.Should().Be(2);
        db.Set<FxRateSyncRun>().Single().Status.Should().Be(FxRateSyncRunStatus.Succeeded);
    }

    [Fact]
    public async Task Sync_StaleEffectiveDate_MarksRunStale_AndRaisesStalenessAlert()
    {
        var staleDate = SystemClock.UtcNow.Date.AddDays(-5); // threshold is 2 days
        var (service, db, _) = BuildSyncService(new StubFxRateProvider("openexchangerates", _ => new FxRateSet(
            "openexchangerates", "USD",
            new Dictionary<string, decimal> { ["EUR"] = 0.9m },
            new DateTimeOffset(staleDate, TimeSpan.Zero),
            staleDate,
            "raw", "application/json")));

        var result = await service.SyncAsync();

        result.Status.Should().Be(FxRateSyncRunStatus.SucceededStale);
        db.Set<FxRateSyncRun>().Single().Status.Should().Be(FxRateSyncRunStatus.SucceededStale);
        _alerter.Verify(
            alerter => alerter.RaiseStalenessAlertAsync(staleDate, 2, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ────────────────────────────── Runtime rate source ──────────────────────────────

    [Fact]
    public void RuntimeSource_DerivesDirectInverseAndCrossInsideOneGrid_FailsClosedOutsideIt()
    {
        var source = NewRuntimeSource();
        source.ApplyProviderSet(Grid(("EUR", 0.8m), ("JPY", 160m)));
        var now = SystemClock.UtcNow;

        source.TryGetRate("USD", "EUR", now)!.Rate.Should().Be(0.8m); // direct as published
        source.TryGetRate("EUR", "USD", now)!.Rate.Should().Be(1.25m); // inverse: 1/0.8
        source.TryGetRate("EUR", "JPY", now)!.Rate.Should().Be(200m); // cross via grid base: 160/0.8

        source.TryGetRate("USD", "BRL", now).Should().BeNull(); // leg outside the grid: fail closed
        source.TryGetRate("USD", "USD", now).Should().BeNull(); // same-code pair is not an FX pair
    }

    [Fact]
    public void RuntimeSource_OverrideAlwaysWinsOverTheProviderGrid()
    {
        var source = NewRuntimeSource();
        source.ApplyProviderSet(Grid(("EUR", 0.8m)));
        source.ApplyOverride(new ExchangeRate(
            "USD", "EUR", 0.75m, new DateTimeOffset(SystemClock.UtcNow, TimeSpan.Zero), "manual-override"));

        var rate = source.TryGetRate("USD", "EUR", SystemClock.UtcNow);

        rate!.Rate.Should().Be(0.75m);
        rate.Source.Should().Be("manual-override");
    }

    [Fact]
    public async Task RuntimeSource_SeedsFromPersistedRows_IgnoringExpiredOverrides()
    {
        var (db, serviceProvider) = NewDbAndProvider();
        var today = SystemClock.UtcNow.Date;
        var activeOverride = new FxRateSnapshot
        {
            BaseCurrency = "USD", QuoteCurrency = "EUR", Rate = 0.7m,
            EffectiveDateUtc = today.AddDays(-3), AsOfUtc = SystemClock.UtcNow,
            ProviderName = "manual", Source = "manual-override", IsOverride = true, ExpiresAtUtc = null,
        };
        var expiredOverride = new FxRateSnapshot
        {
            BaseCurrency = "USD", QuoteCurrency = "JPY", Rate = 100m,
            EffectiveDateUtc = today.AddDays(-3), AsOfUtc = SystemClock.UtcNow,
            ProviderName = "manual", Source = "manual-override", IsOverride = true, ExpiresAtUtc = SystemClock.UtcNow.AddHours(-1),
        };
        var gridRow = new FxRateSnapshot
        {
            BaseCurrency = "USD", QuoteCurrency = "GBP", Rate = 0.8m,
            EffectiveDateUtc = today, AsOfUtc = SystemClock.UtcNow,
            ProviderName = "openexchangerates", Source = "latest", IsOverride = false,
        };
        db.AddRange(activeOverride, expiredOverride, gridRow);
        await db.SaveChangesAsync();

        var source = new FxRuntimeRateSource(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<FxRuntimeRateSource>.Instance);
        await source.EnsureSeededAsync();

        var now = SystemClock.UtcNow;
        source.TryGetRate("USD", "EUR", now)!.Rate.Should().Be(0.7m); // active override
        source.TryGetRate("USD", "JPY", now).Should().BeNull(); // expired override is gone, JPY not in the grid
        source.TryGetRate("USD", "GBP", now)!.Rate.Should().Be(0.8m); // grid row seeded the provider set
    }

    // ────────────────────────────── Conversion path: provider + cache ──────────────────────────────

    [Fact]
    public async Task PersistedFxRateProvider_ServesFromCacheUntilInvalidated()
    {
        var source = NewRuntimeSource();
        source.ApplyProviderSet(Grid(("EUR", 0.9m)));
        var cache = NewCache(ttlMinutes: 60);
        var provider = new PersistedFxRateProvider(cache, source);

        (await provider.GetRateAsync("usd", "eur")).Should().NotBeNull();

        // Fresh grid lands (e.g. a sync just ran) — the cached quotation must still win until invalidated.
        source.ApplyProviderSet(Grid(("EUR", 0.95m)));
        (await provider.GetRateAsync("USD", "EUR"))!.Rate.Should().Be(0.9m);

        // After invalidation the new grid value becomes visible.
        await cache.InvalidateAsync("USD", "EUR");
        (await provider.GetRateAsync("USD", "EUR"))!.Rate.Should().Be(0.95m);

        // Unknown legs fail closed.
        (await provider.GetRateAsync("USD", "BRL")).Should().BeNull();
    }

    [Fact]
    public async Task FxRateCache_ZeroTtl_ExpiresImmediately()
    {
        var cache = NewCache(ttlMinutes: 0);
        var rate = new ExchangeRate("USD", "EUR", 0.9m, new DateTimeOffset(SystemClock.UtcNow, TimeSpan.Zero), "test");

        await cache.SetAsync(rate);

        (await cache.GetAsync("USD", "EUR")).Should().BeNull();
    }

    [Fact]
    public async Task FxRateCache_DistributedBacking_UsedWhenRegistered()
    {
        var distributed = new StubDistributedCache();
        var cache = new FxRateCache(
            distributed,
            Options.Create(new FxSyncOptions { CacheTtlMinutes = 60 }),
            NullLogger<FxRateCache>.Instance);
        var rate = new ExchangeRate("USD", "EUR", 0.91m, new DateTimeOffset(SystemClock.UtcNow, TimeSpan.Zero), "test");

        await cache.SetAsync(rate);
        (await cache.GetAsync("USD", "EUR")).Should().NotBeNull();
        distributed.Store.Keys.Should().Contain("fx:rate:USD:EUR");

        await cache.InvalidateAsync("USD", "EUR");
        distributed.Store.Should().BeEmpty();
        (await cache.GetAsync("USD", "EUR")).Should().BeNull();
    }

    // ────────────────────────────── Admin commands ──────────────────────────────

    [Fact]
    public async Task SetManualFxRateOverride_PersistsAuditsAppliesAndInvalidates()
    {
        var (db, _) = NewDbAndProvider();
        var runtime = NewRuntimeSource();
        var cache = new Mock<IFxRateCache>();
        var handler = new SetManualFxRateOverrideCommandHandler(db, ActorAccessor(), runtime, cache.Object);

        var id = await handler.Handle(
            new SetManualFxRateOverrideCommand("usd", "eur", 0.75m, "market outage", null),
            CancellationToken.None);

        var row = db.Set<FxRateSnapshot>().Single();
        row.Id.Should().Be(id);
        row.IsOverride.Should().BeTrue();
        row.Rate.Should().Be(0.75m);
        row.ProviderName.Should().Be("manual");

        var audit = db.Set<AuditTrail>().Single();
        audit.Action.Should().Be(AuditAction.ConfigurationChanged);
        audit.Metadata.Should().Contain("market outage");

        runtime.CurrentOverrides.Should().Contain(rate => rate.BaseCurrency == "USD" && rate.QuoteCurrency == "EUR");
        cache.Verify(item => item.InvalidateAsync("USD", "EUR", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetManualFxRateOverride_UnknownCurrency_FailsClosed()
    {
        var (db, _) = NewDbAndProvider();
        var handler = new SetManualFxRateOverrideCommandHandler(db, ActorAccessor(), NewRuntimeSource(), NewCache());

        var act = () => handler.Handle(
            new SetManualFxRateOverrideCommand("USD", "XXY", 0.75m, "reason", null),
            CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .Which.Rule.Should().Be(CurrencyConfigurationRules.UnknownCurrency);
    }

    [Fact]
    public async Task RunFxRateSyncCommand_WithNoProviderConfigured_IsRejected()
    {
        var handler = new RunFxRateSyncCommandHandler(
            Mock.Of<IFxRateSyncService>(), Options.Create(new FxSyncOptions()), ActorAccessor());

        var act = () => handler.Handle(new RunFxRateSyncCommand(), CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .Which.Rule.Should().Be("FxProviderNotConfigured");
    }

    // ────────────────────────────── Options validator (fail-closed startup) ──────────────────────────────

    [Fact]
    public void FxSyncOptions_Defaults_Validate()
    {
        var validator = new FxSyncOptionsValidator();

        var result = validator.Validate(null, new FxSyncOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void FxSyncOptions_FailClosedMisconfigurations_AreRejected()
    {
        var validator = new FxSyncOptionsValidator();

        validator.Validate(null, new FxSyncOptions { Provider = "made-up" }).Failed.Should().BeTrue();
        validator.Validate(null, new FxSyncOptions { Provider = "none", FallbackProvider = "ecb" }).Failed.Should().BeTrue();
        validator.Validate(null, new FxSyncOptions
        {
            Provider = FxSyncOptions.ProviderOpenExchangeRates,
            OpenExchangeRates = new FxSyncOptions.OpenExchangeRatesOptions { AppId = "" },
        }).Failed.Should().BeTrue();
        validator.Validate(null, new FxSyncOptions { SyncTimeUtc = "25:99" }).Failed.Should().BeTrue();
        validator.Validate(null, new FxSyncOptions { RetryMaxAttempts = 0 }).Failed.Should().BeTrue();
    }

    // ────────────────────────────── Provider adapters (config-gated HTTP) ──────────────────────────────

    [Fact]
    public async Task OpenExchangeRatesProvider_ParsesLatestGrid_AndCarriesTheRawResponse()
    {
        var (provider, _) = BuildOpenExchangeRatesProvider(
            """{"disclaimer":"x","license":"y","timestamp":1730000000,"base":"USD","date":"2026-10-20","rates":{"EUR":0.9025,"JPY":149.5,"BROKEN":"nope"}}""",
            HttpStatusCode.OK);

        var set = await provider.GetRatesAsync();

        set.ProviderName.Should().Be("openexchangerates");
        set.BaseCurrency.Should().Be("USD");
        set.Rates["EUR"].Should().Be(0.9025m);
        set.Rates.Should().NotContainKey("BROKEN");
        set.EffectiveDateUtc.Should().Be(new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc));
        set.RawResponse.Should().Contain("0.9025");
    }

    [Fact]
    public async Task OpenExchangeRatesProvider_HttpFailure_FailsClosed()
    {
        var (provider, _) = BuildOpenExchangeRatesProvider("quota exceeded", HttpStatusCode.Forbidden);

        var act = () => provider.GetRatesAsync();

        (await act.Should().ThrowAsync<FxRateProviderUnavailableException>())
            .Which.Message.Should().Contain("403");
    }

    [Fact]
    public async Task OpenExchangeRatesProvider_WithoutAppId_NeverDialsOut()
    {
        var (provider, handler) = BuildOpenExchangeRatesProvider("unused", HttpStatusCode.OK, appId: "");

        var act = () => provider.GetRatesAsync();

        await act.Should().ThrowAsync<FxRateProviderUnavailableException>();
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task EcbProvider_ParsesEuroReferenceRates()
    {
        var provider = BuildEcbProvider(
            """
            <gesmes:Envelope xmlns:gesmes="http://www.gesmes.org/xml/2002-08-01" xmlns="http://www.ecb.int/vocabulary/2002-08-01/eurofxref">
              <gesmes:subject>Reference rates</gesmes:subject>
              <Cube>
                <Cube time="2026-10-20">
                  <Cube currency="USD" rate="1.0810"/>
                  <Cube currency="JPY" rate="163.25"/>
                </Cube>
              </Cube>
            </gesmes:Envelope>
            """,
            HttpStatusCode.OK);

        var set = await provider.GetRatesAsync();

        set.ProviderName.Should().Be("ecb");
        set.BaseCurrency.Should().Be("EUR");
        set.Rates["USD"].Should().Be(1.0810m);
        set.Rates["JPY"].Should().Be(163.25m);
        set.EffectiveDateUtc.Should().Be(new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task EcbProvider_HistoricalRequest_SelectsLatestDateAtOrBeforeIt()
    {
        var provider = BuildEcbProvider(
            """
            <gesmes:Envelope xmlns:gesmes="http://www.gesmes.org/xml/2002-08-01" xmlns="http://www.ecb.int/vocabulary/2002-08-01/eurofxref">
              <Cube>
                <Cube time="2026-10-20"><Cube currency="USD" rate="1.0810"/></Cube>
                <Cube time="2026-10-17"><Cube currency="USD" rate="1.0750"/></Cube>
              </Cube>
            </gesmes:Envelope>
            """,
            HttpStatusCode.OK);

        var set = await provider.GetRatesAsync(asOfUtc: new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));

        set.EffectiveDateUtc.Should().Be(new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc));
        set.Rates["USD"].Should().Be(1.0750m);
    }

    // ────────────────────────────── Stripe minor-unit scaling (3-decimal fix) ──────────────────────────────

    [Theory]
    [InlineData(25.431, "KWD", 25431L)]
    [InlineData(12.345, "BHD", 12345L)]
    [InlineData(3.456, "OMR", 3456L)]
    public void ToStripeAmount_ThreeDecimalCurrency_ScalesBy1000(decimal amount, string currency, long expected)
    {
        StripeAmountConverter.ToStripeAmount(amount, currency).Should().Be(expected);
    }

    [Theory]
    [InlineData(25431L, "KWD", 25.431)]
    [InlineData(12345L, "BHD", 12.345)]
    public void FromStripeAmount_ThreeDecimalCurrency_DividesBy1000(long amount, string currency, decimal expected)
    {
        StripeAmountConverter.FromStripeAmount(amount, currency).Should().Be(expected);
    }

    // ────────────────────────────── Helpers ──────────────────────────────

    private (FxRateSyncService Service, CurrencyFxTestDbContext Db, FxRuntimeRateSource Runtime) BuildSyncService(
        StubFxRateProvider primary,
        StubFxRateProvider? fallbackProvider = null,
        string? fallbackProviderName = null)
    {
        var (db, serviceProvider) = NewDbAndProvider();
        if (fallbackProviderName is not null)
        {
            _options.FallbackProvider = fallbackProviderName;
        }

        var runtime = new FxRuntimeRateSource(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<FxRuntimeRateSource>.Instance);
        var providers = fallbackProvider is null
            ? new IFxRateProvider[] { primary }
            : new IFxRateProvider[] { primary, fallbackProvider };
        var service = new FxRateSyncService(
            providers, serviceProvider, Options.Create(_options), runtime, _alerter.Object,
            NullLogger<FxRateSyncService>.Instance);
        return (service, db, runtime);
    }

    private static FxRateSet NewRateSet(IReadOnlyDictionary<string, decimal> rates)
    {
        var asOf = SystemClock.UtcNow;
        return new FxRateSet(
            "openexchangerates", "USD",
            new Dictionary<string, decimal>(rates),
            new DateTimeOffset(asOf, TimeSpan.Zero),
            asOf.Date,
            "raw-openexchangerates",
            "application/json");
    }

    private static FxRateSet Grid(params (string Code, decimal Rate)[] quotes)
    {
        return new FxRateSet(
            "openexchangerates", "USD",
            quotes.ToDictionary(quote => quote.Code, quote => quote.Rate),
            new DateTimeOffset(SystemClock.UtcNow, TimeSpan.Zero),
            SystemClock.UtcNow.Date,
            string.Empty,
            "application/json");
    }

    private static FxRuntimeRateSource NewRuntimeSource()
    {
        var (_, serviceProvider) = NewDbAndProvider();
        return new FxRuntimeRateSource(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<FxRuntimeRateSource>.Instance);
    }

    private static FxRateCache NewCache(int ttlMinutes = 60)
    {
        return new FxRateCache(
            null, Options.Create(new FxSyncOptions { CacheTtlMinutes = ttlMinutes }),
            NullLogger<FxRateCache>.Instance);
    }

    private static (CurrencyFxTestDbContext Db, ServiceProvider Provider) NewDbAndProvider()
    {
        var options = new DbContextOptionsBuilder<CurrencyFxTestDbContext>()
            .UseInMemoryDatabase($"fx-sync-{Guid.NewGuid():N}")
            .Options;
        var db = new CurrencyFxTestDbContext(options);
        var services = new ServiceCollection();
        services.AddSingleton<IApplicationDbContext>(db);
        var provider = services.BuildServiceProvider();
        return (db, provider);
    }

    private static IActorContextAccessor ActorAccessor()
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(item => item.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string> { "SystemAdmin" },
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        });
        return accessor.Object;
    }

    private static (OpenExchangeRatesFxRateProvider Provider, RecordingHandler Handler) BuildOpenExchangeRatesProvider(
        string payload, HttpStatusCode status, string appId = "test-app-id")
    {
        var handler = new RecordingHandler(payload, status);
        return (new OpenExchangeRatesFxRateProvider(
            new HttpClient(handler),
            Options.Create(new FxSyncOptions
            {
                Provider = FxSyncOptions.ProviderOpenExchangeRates,
                OpenExchangeRates = new FxSyncOptions.OpenExchangeRatesOptions { AppId = appId },
            }),
            NullLogger<OpenExchangeRatesFxRateProvider>.Instance), handler);
    }

    private static EcbFxRateProvider BuildEcbProvider(string payload, HttpStatusCode status)
    {
        return new EcbFxRateProvider(
            new HttpClient(new RecordingHandler(payload, status)),
            Options.Create(new FxSyncOptions { Provider = FxSyncOptions.ProviderEcb }),
            NullLogger<EcbFxRateProvider>.Instance);
    }

    /// <summary>Configurable provider stub with an attempt counter (retry/backoff evidence).</summary>
    private sealed class StubFxRateProvider(string name, Func<int, FxRateSet> behavior) : IFxRateProvider
    {
        public string Name { get; } = name;

        public int Attempts { get; private set; }

        public Task<FxRateSet> GetRatesAsync(DateTimeOffset? asOfUtc = null, CancellationToken cancellationToken = default)
        {
            Attempts++;
            return Task.FromResult(behavior(Attempts));
        }
    }

    /// <summary>Serves canned payloads without a network.</summary>
    private sealed class RecordingHandler(string payload, HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(payload),
            });
        }
    }

    /// <summary>In-memory IDistributedCache backing for the distributed cache path.</summary>
    private sealed class StubDistributedCache : IDistributedCache
    {
        public Dictionary<string, string> Store { get; } = new();

        public byte[]? Get(string key)
        {
            return Store.TryGetValue(key, out var value) ? System.Text.Encoding.UTF8.GetBytes(value) : null;
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            return Task.FromResult(Get(key));
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            Store[key] = System.Text.Encoding.UTF8.GetString(value);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            Store.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Refresh(string key) { }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }
    }
}
