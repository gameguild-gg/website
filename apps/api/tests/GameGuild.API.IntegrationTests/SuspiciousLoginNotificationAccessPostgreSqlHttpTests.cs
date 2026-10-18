using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace GameGuild.API.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SecurityAlertAccessPostgreSqlCollection : ICollectionFixture<ApiPostgreSqlFixture>
{
    internal const string Name = "Security alert access PostgreSQL";
}

// These scenarios dispatch manually. A separate collection owns one database,
// outside the shared collection's background consumers, for all eight cases.
[Collection(SecurityAlertAccessPostgreSqlCollection.Name)]
public sealed class SuspiciousLoginNotificationAccessPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerCanReadDeliveredSecurityAlertThroughAuthorizedHttp(bool platformEvent)
    {
        using var factory = CreateFactory();
        var scenario = await RecordAndDeliverAsync(factory, platformEvent);
        using var client = CreateAuthenticatedClient(factory, scenario.OwnerId, scenario.TenantId);
        using var list = await client.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var payload = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var alert = Assert.Single(payload.RootElement.EnumerateArray().ToArray(), value => value.GetProperty("id").GetGuid() == scenario.NotificationId);
        Assert.Equal("Security", alert.GetProperty("type").GetString());
        Assert.Equal("InApp", alert.GetProperty("channel").GetString());
        Assert.Equal("Account security alert", alert.GetProperty("title").GetString());
        Assert.Contains("high-risk sign-in attempt", alert.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(scenario.OwnerEmail, alert.GetRawText(), StringComparison.Ordinal);
        using var byId = await client.GetAsync("/api/notifications/" + scenario.NotificationId);
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);
        using var value = JsonDocument.Parse(await byId.Content.ReadAsStringAsync());
        Assert.Equal(scenario.NotificationId, value.RootElement.GetProperty("id").GetGuid());
        using var count = await client.GetAsync("/api/notifications/unread-count");
        Assert.Equal(HttpStatusCode.OK, count.StatusCode);
        using var countValue = JsonDocument.Parse(await count.Content.ReadAsStringAsync());
        Assert.Equal(1, countValue.RootElement.GetProperty("count").GetInt32());
    }

    [Theory]
    [InlineData("list")]
    [InlineData("count")]
    [InlineData("by-id")]
    [InlineData("mark-read")]
    public async Task AnonymousCallerCannotReadOrChangeSecurityAlert(string operation)
    {
        using var factory = CreateFactory();
        var scenario = await RecordAndDeliverAsync(factory, platformEvent: true);
        using var client = factory.CreateClient();
        var path = operation switch
        {
            "list" => "/api/notifications",
            "count" => "/api/notifications/unread-count",
            "by-id" => "/api/notifications/" + scenario.NotificationId,
            _ => "/api/notifications/" + scenario.NotificationId + "/read"
        };
        using var response = operation == "mark-read" ? await client.PostAsync(path, null) : await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertUnreadAsync(factory, scenario.NotificationId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OtherAccountCannotReadOrChangeOwnerAlertEvenWithMatchingTenant(bool sameTenant)
    {
        using var factory = CreateFactory();
        var scenario = await RecordAndDeliverAsync(factory, platformEvent: false);
        using var client = CreateAuthenticatedClient(factory, scenario.OtherId, sameTenant ? scenario.TenantId : scenario.OtherTenantId);
        using var list = await client.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listValue = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.DoesNotContain(listValue.RootElement.EnumerateArray().ToArray(), value => value.GetProperty("id").GetGuid() == scenario.NotificationId);
        using var count = await client.GetAsync("/api/notifications/unread-count");
        Assert.Equal(HttpStatusCode.OK, count.StatusCode);
        using var countValue = JsonDocument.Parse(await count.Content.ReadAsStringAsync());
        Assert.Equal(0, countValue.RootElement.GetProperty("count").GetInt32());
        using var read = await client.GetAsync("/api/notifications/" + scenario.NotificationId);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        using var markRead = await client.PostAsync("/api/notifications/" + scenario.NotificationId + "/read", null);
        Assert.Equal(HttpStatusCode.Forbidden, markRead.StatusCode);
        await AssertUnreadAsync(factory, scenario.NotificationId);
    }

    private WebApplicationFactory<Program> CreateFactory() => fixture.CreateFactory(builder =>
        builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>()));

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, Guid userId, Guid tenantId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(ApiPostgreSqlTestAuthHandler.SchemeName, "authenticated");
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.TenantIdHeader, tenantId.ToString());
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    private static async Task<Scenario> RecordAndDeliverAsync(WebApplicationFactory<Program> factory, bool platformEvent)
    {
        var marker = Guid.NewGuid().ToString("N");
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var email = "alert-owner-" + marker + "@example.test";
        Guid ownerId;
        Guid otherId;
        Guid eventId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var passwordHash = hasher.HashPassword("aA7!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(20)));
            var owner = User.CreateWithPassword(email, "Synthetic alert owner", passwordHash, "alert-owner-" + marker);
            var other = User.CreateWithPassword("alert-other-" + marker + "@example.test", "Synthetic other account", passwordHash, "alert-other-" + marker);
            ownerId = owner.Id;
            otherId = other.Id;
            db.Set<User>().AddRange(owner, other);
            db.Set<Tenant>().AddRange(
                new Tenant { Id = tenantId, Name = "Alert " + marker, Slug = "alert-" + marker, AdminEmail = "admin-" + marker + "@example.test", IsActive = true },
                new Tenant { Id = otherTenantId, Name = "Other alert " + marker, Slug = "alert-other-" + marker, AdminEmail = "admin-other-" + marker + "@example.test", IsActive = true });
            db.Set<TenantMember>().AddRange(
                new TenantMember { Id = Guid.NewGuid(), TenantId = tenantId, UserId = ownerId, Role = "Member", IsActive = true },
                new TenantMember { Id = Guid.NewGuid(), TenantId = tenantId, UserId = otherId, Role = "Member", IsActive = true },
                new TenantMember { Id = Guid.NewGuid(), TenantId = otherTenantId, UserId = otherId, Role = "Member", IsActive = true });
            await db.SaveChangesAsync();
            var signal = new SuspiciousLoginDetectedV1(ownerId, SecurityAlertKinds.LoginStepUpRequired, nameof(RiskLevel.High), 75)
            {
                TenantId = platformEvent ? DurableIntegrationEventTenants.Platform : tenantId,
                ActorId = DurableIntegrationEventActors.System,
                AggregateType = "User",
                AggregateId = ownerId.ToString()
            };
            eventId = signal.EventId;
            await scope.ServiceProvider.GetRequiredService<IDurableEventProducer>().RecordAsync(signal);
        }
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>().DispatchPendingAsync() == 0)
            {
                break;
            }
            Assert.True(attempt < 9, "Synthetic security alert outbox did not settle.");
        }
        await using var verify = factory.Services.CreateAsyncScope();
        var notification = Assert.Single(await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<Notification>().AsNoTracking().Where(value => value.ReferenceEntityId == eventId && value.Channel == NotificationChannel.InApp).ToListAsync());
        Assert.Equal(ownerId, notification.RecipientId);
        Assert.True(notification.IsSent);
        Assert.False(notification.IsRead);
        return new Scenario(ownerId, otherId, tenantId, otherTenantId, notification.Id, email);
    }

    private static async Task AssertUnreadAsync(WebApplicationFactory<Program> factory, Guid notificationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var notification = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<Notification>().AsNoTracking().SingleAsync(value => value.Id == notificationId);
        Assert.False(notification.IsRead);
    }

    private sealed record Scenario(Guid OwnerId, Guid OtherId, Guid TenantId, Guid OtherTenantId, Guid NotificationId, string OwnerEmail);
}
