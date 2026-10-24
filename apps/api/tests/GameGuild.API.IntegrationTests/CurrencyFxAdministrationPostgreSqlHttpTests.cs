using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Commerce.Payments;
using GameGuild.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class CurrencyFxAdministrationPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    public static IEnumerable<object[]> ProtectedOperations =>
        from route in new[] { "currencies/USD/enable", "currencies/USD/disable", "currencies/USD/set-base", "fx-rates/overrides", "fx-rates/sync", "fx-rates", "fx-rates/sync-status" }
        from authenticated in new[] { false, true }
        select new object[] { route, authenticated };

    [Theory]
    [MemberData(nameof(ProtectedOperations))]
    public async Task AdministrationRoutes_DenyAnonymousAndOrdinaryMembers(string route, bool authenticated)
    {
        var tenant = Guid.NewGuid();
        using var client = authenticated
            ? fixture.CreateAuthenticatedClient(Guid.NewGuid(), tenant)
            : fixture.Factory.CreateClient();
        using var response = route is "fx-rates" or "fx-rates/sync-status"
            ? await client.GetAsync("/api/v1/" + route)
            : await client.PostAsJsonAsync("/api/v1/" + route, new
            {
                baseCurrency = "USD", quoteCurrency = "EUR", rate = 0.75m,
                reason = "unauthorized override", confirmActiveSubscriptionImpact = true,
            });

        Assert.Equal(authenticated ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, response.StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Set<CurrencyConfiguration>().AnyAsync(row => row.TenantId == tenant));
    }

    [Fact]
    public async Task CurrencyAdministration_PersistsInMigratedSchemaWithTenantIsolationAndAudit()
    {
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var foreignTenant = Guid.NewGuid();
        using var admin = fixture.CreateAuthenticatedClient(user, tenant, isSystemAdmin: true);
        using var foreignAdmin = fixture.CreateAuthenticatedClient(Guid.NewGuid(), foreignTenant, isSystemAdmin: true);
        var enabled = await foreignAdmin.PostAsJsonAsync("/api/v1/currencies/EUR/enable", new { displaySymbol = "foreign" });
        Assert.Equal(HttpStatusCode.NoContent, enabled.StatusCode);
        var configured = await admin.PostAsJsonAsync("/api/v1/currencies/USD/set-base", new { tenantId = foreignTenant, actorId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NoContent, configured.StatusCode);

        using var member = fixture.CreateAuthenticatedClient(Guid.NewGuid(), tenant);
        var currencies = await member.GetFromJsonAsync<List<TenantCurrencyDto>>("/api/v1/currencies")
            ?? throw new InvalidOperationException("Currency listing must return a JSON array.");
        Assert.True(currencies.Single(row => row.CurrencyCode == "USD").IsBaseCurrency);
        Assert.False(currencies.Single(row => row.CurrencyCode == "EUR").IsEnabled);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Contains("20261022132706_AddCurrencyConfigurationAndFxRateSync", await db.Database.GetAppliedMigrationsAsync());
        var own = await db.Set<CurrencyConfiguration>().SingleAsync(row => row.TenantId == tenant);
        Assert.Equal("USD", own.CurrencyCode);
        Assert.Equal("foreign", (await db.Set<CurrencyConfiguration>().SingleAsync(row => row.TenantId == foreignTenant)).DisplaySymbol);
        Assert.Contains(await db.Set<AuditTrail>().Where(row => row.EntityId == own.Id).ToListAsync(), row => row.ChangedBy == user);
    }

    [Fact]
    public async Task GlobalOverride_RequiresSystemAdministratorAndAuditsTheAuthenticatedActor()
    {
        var user = Guid.NewGuid();
        using var admin = fixture.CreateAuthenticatedClient(user, Guid.NewGuid(), isSystemAdmin: true);
        var response = await admin.PostAsJsonAsync("/api/v1/fx-rates/overrides", new
        {
            baseCurrency = "XCD", quoteCurrency = "XOF", rate = 205.75m,
            reason = "HTTP administration regression", actorId = Guid.NewGuid(), tenantId = Guid.NewGuid(),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.Set<FxRateSnapshot>().SingleAsync(item => item.IsOverride && item.BaseCurrency == "XCD" && item.QuoteCurrency == "XOF");
        Assert.Null(row.TenantId);
        Assert.Equal(205.75m, row.Rate);
        Assert.Contains(await db.Set<AuditTrail>().Where(entry => entry.EntityId == row.Id).ToListAsync(), entry => entry.ChangedBy == user);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/fx-rates")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/fx-rates/sync-status")).StatusCode);
    }

    [Fact]
    public async Task FxSync_RetriesTransientProviderFailureAndPersistsGridWithoutIdentityPair()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = JsonSerializer.Serialize(new
        {
            @base = "USD", timestamp,
            rates = new Dictionary<string, decimal> { ["USD"] = 1m, ["EUR"] = 0.9m, ["JPY"] = 150m },
        });
        var transport = new TransientRateHandler(payload);
        using var client = new HttpClient(transport);
        var settings = Options.Create(new FxSyncOptions
        {
            Provider = "openexchangerates", RetryMaxAttempts = 2, RetryBaseDelaySeconds = 0,
            OpenExchangeRates = new() { AppId = "fixture-app-id" },
        });
        var adapter = new OpenExchangeRatesFxRateProvider(client, settings, NullLogger<OpenExchangeRatesFxRateProvider>.Instance);
        var service = new FxRateSyncService([adapter], scope.ServiceProvider, settings,
            scope.ServiceProvider.GetRequiredService<FxRuntimeRateSource>(), Mock.Of<IFxRateSyncAlerter>(), NullLogger<FxRateSyncService>.Instance);

        var result = await service.SyncAsync();

        Assert.Equal(FxRateSyncRunStatus.Succeeded, result.Status);
        Assert.Equal(2, result.RatesApplied);
        Assert.Equal(2, transport.Calls);
        Assert.False(await db.Set<FxRateSnapshot>().AnyAsync(row => row.BaseCurrency == row.QuoteCurrency));
        Assert.Contains(await db.Set<FxRateSyncRun>().Where(row => row.RawResponse == payload).ToListAsync(), row => row.RatesApplied == 2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OperatorAlerts_ArePersistedAndVisibleOnlyInRecipientInboxes(bool stale)
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var alerter = new NotificationFxRateSyncAlerter(
            fixture.Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NotificationFxRateSyncAlerter>.Instance,
            Options.Create(new FxSyncOptions { AlertRecipientIds = [first, second] }));

        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (stale) await alerter.RaiseStalenessAlertAsync(DateTime.UtcNow.AddDays(-3), 2);
            else await alerter.RaiseSyncFailureAlertAsync("ecb", 3, new string('x', 5000));
        }

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.Set<Notification>().AsNoTracking()
            .Where(row => row.RecipientId == first || row.RecipientId == second).ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.All(stored, row =>
        {
            Assert.True(row.IsSent);
            Assert.True(row.Message.Length <= 2000);
            Assert.Equal(NotificationChannel.InApp, row.Channel);
            Assert.Equal(stale ? "FxRateSnapshots" : "FxRateSyncRuns", row.ReferenceEntityType);
        });

        foreach (var recipient in new[] { first, second, outsider })
        {
            using var client = fixture.CreateAuthenticatedClient(recipient, tenant);
            using var response = await client.GetAsync("/api/notifications");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var visible = document.RootElement.EnumerateArray().ToArray();
            foreach (var notification in stored)
            {
                Assert.Equal(notification.RecipientId == recipient,
                    visible.Any(value => value.GetProperty("id").GetGuid() == notification.Id));
            }
        }
    }

    private sealed class TransientRateHandler(string payload) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(Calls == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            {
                Content = new StringContent(Calls == 1 ? "temporary provider failure" : payload),
            });
        }
    }
}
