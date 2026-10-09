using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Compliance.Audit;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace GameGuild.API.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RefreshTokenReplayAlertPostgreSqlCollection : ICollectionFixture<ApiPostgreSqlFixture>
{
    internal const string Name = "Refresh-token replay alert PostgreSQL";
}

[Collection(RefreshTokenReplayAlertPostgreSqlCollection.Name)]
public sealed class RefreshTokenReplayAlertPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private const string EventName = "identity.authentication.refresh-token.replay-contained.v1";
    private const string Endpoint = "/v1/auth/tokens:refresh";

    [Theory]
    [InlineData(RefreshTokenReplayScope.Family)]
    [InlineData(RefreshTokenReplayScope.Account)]
    public async Task CommittedReplayProducesRedactedOwnerAlertsOnceThroughActualInbox(RefreshTokenReplayScope policy)
    {
        var clock = new AdvancingTimeProvider();
        using var factory = CreateFactory(clock, policy: policy);
        var account = await SeedAsync(factory);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(Endpoint,
            new { refreshToken = account.RawRootToken, tenantId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(account.RawRootToken, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var message = await AlertEventAsync(factory, account.Root.Id);
        Assert.Equal(DurableIntegrationEventActors.System, message.ActorId);
        Assert.Equal(DurableIntegrationEventTenants.Platform, message.TenantId);
        Assert.DoesNotContain(account.RawRootToken, message.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(account.Root.Token, message.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(account.User.Email, message.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", message.Payload, StringComparison.Ordinal);
        Assert.Contains(account.User.Id.ToString(), message.Payload, StringComparison.OrdinalIgnoreCase);

        await FlushAsync(factory);
        await FlushAsync(factory);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = await db.Set<Notification>().AsNoTracking()
            .Where(value => value.ReferenceEntityId == message.EventId && value.Type == NotificationType.Security).ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, value =>
        {
            Assert.Equal(account.User.Id, value.RecipientId);
            Assert.Equal(NotificationPriority.Urgent, value.Priority);
            Assert.Null(value.Metadata);
            Assert.DoesNotContain(account.RawRootToken, value.Message, StringComparison.Ordinal);
        });
        Assert.Equal(account.User.Email, Assert.Single(notifications, value => value.Channel == NotificationChannel.Email).RecipientEmail);
        Assert.True(Assert.Single(notifications, value => value.Channel == NotificationChannel.InApp).IsSent);
        Assert.NotNull((await db.Set<OutboxMessage>().SingleAsync(value => value.EventId == message.EventId)).CompletedAtUtc);
        var receipt = Assert.Single(await db.Set<InboxReceipt>().Where(value => value.EventId == message.EventId).ToListAsync());
        Assert.Equal(1, receipt.AttemptCount);
        Assert.NotNull(receipt.CompletedAtUtc);
        Assert.False((await db.Set<UserSession>().SingleAsync(value => value.Id == account.Session.Id)).IsActive);
        Assert.True((await db.Set<RefreshToken>().SingleAsync(value => value.Id == account.Child.Id)).IsRevoked);
    }

    [Theory]
    [InlineData(RefreshTokenReplayScope.Family)]
    [InlineData(RefreshTokenReplayScope.Account)]
    public async Task QueueWriteFailureRollsBackBothAlertsAndRetriesWithoutUndoingCommittedContainment(RefreshTokenReplayScope policy)
    {
        var clock = new AdvancingTimeProvider();
        var fault = new QueueWriteFailure();
        using var factory = CreateFactory(clock, queueFailure: fault, policy: policy);
        var account = await SeedAsync(factory);
        fault.UserId = account.User.Id;
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Endpoint,
            new { refreshToken = account.RawRootToken, tenantId = account.TenantId });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var message = await AlertEventAsync(factory, account.Root.Id);

        await FlushAsync(factory);

        Assert.True(fault.Reached);
        using (var verification = factory.Services.CreateScope())
        {
            var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Set<Notification>().AnyAsync(value => value.ReferenceEntityId == message.EventId));
            var receipt = Assert.Single(await db.Set<InboxReceipt>().Where(value => value.EventId == message.EventId).ToListAsync());
            Assert.Equal(1, receipt.AttemptCount);
            Assert.Null(receipt.CompletedAtUtc);
            Assert.Null(receipt.DeadLetteredAtUtc);
            Assert.True(receipt.NextAttemptAtUtc > clock.GetUtcNow());
            Assert.False((await db.Set<UserSession>().SingleAsync(value => value.Id == account.Session.Id)).IsActive);
            Assert.True((await db.Set<RefreshToken>().SingleAsync(value => value.Id == account.Child.Id)).IsRevoked);
            Assert.Equal(account.User.TokenVersion + (policy == RefreshTokenReplayScope.Account ? 1 : 0),
                (await db.Set<User>().SingleAsync(value => value.Id == account.User.Id)).TokenVersion);
        }

        fault.Enabled = false;
        clock.Advance(TimeSpan.FromMinutes(1));
        await FlushAsync(factory);
        await FlushAsync(factory);

        using var completed = factory.Services.CreateScope();
        var context = completed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await context.Set<Notification>().CountAsync(value => value.ReferenceEntityId == message.EventId));
        var completedReceipt = Assert.Single(await context.Set<InboxReceipt>().Where(value => value.EventId == message.EventId).ToListAsync());
        Assert.Equal(2, completedReceipt.AttemptCount);
        Assert.NotNull(completedReceipt.CompletedAtUtc);
        Assert.Null(completedReceipt.LastError);
    }

    [Theory]
    [InlineData(RefreshTokenReplayScope.Family)]
    [InlineData(RefreshTokenReplayScope.Account)]
    public async Task FailedOutboxWriteRollsBackContainmentAuditAndAlertTogether(RefreshTokenReplayScope policy)
    {
        var fault = new AlertOutboxWriteFailure();
        using var factory = CreateFactory(new AdvancingTimeProvider(), outboxFailure: fault, policy: policy);
        var account = await SeedAsync(factory);
        fault.TokenId = account.Root.Id;
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(Endpoint,
            new { refreshToken = account.RawRootToken, tenantId = account.TenantId });

        Assert.True(fault.Reached);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Set<OutboxMessage>().AnyAsync(value => value.EventName == EventName && value.AggregateId == account.Root.Id.ToString()));
        Assert.False(await db.Set<AuditLog>().AnyAsync(value => value.UserId == account.User.Id && value.ActionType == "Authentication.RefreshTokenReplayContained"));
        Assert.True((await db.Set<UserSession>().SingleAsync(value => value.Id == account.Session.Id)).IsActive);
        Assert.False((await db.Set<RefreshToken>().SingleAsync(value => value.Id == account.Child.Id)).IsRevoked);
        Assert.Equal(account.User.TokenVersion, (await db.Set<User>().SingleAsync(value => value.Id == account.User.Id)).TokenVersion);
    }

    private WebApplicationFactory<Program> CreateFactory(AdvancingTimeProvider clock,
        QueueWriteFailure? queueFailure = null, AlertOutboxWriteFailure? outboxFailure = null,
        RefreshTokenReplayScope policy = RefreshTokenReplayScope.Account) =>
        fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtOptions>(options => options.RefreshTokenReplayContainmentScope = policy);
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            });
            // The test controls the actual dispatcher explicitly; no competing background sweep.
            services.RemoveAll<IHostedService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
            if (queueFailure is not null)
            {
                services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(queueFailure));
            }
            if (outboxFailure is not null)
            {
                services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(outboxFailure));
            }
        }));

    private static async Task<Account> SeedAsync(WebApplicationFactory<Program> factory)
    {
        var marker = Guid.NewGuid().ToString("N");
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>();
        var user = User.Create($"replay-alert-{marker}@example.test", "Synthetic replay alert owner");
        user.Username = "replay-alert-" + marker;
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var sessionId = Guid.NewGuid();
        var root = new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, Token = hasher.HashToken(raw),
            SessionId = sessionId, CreatedAt = now.AddHours(-1), UpdatedAt = now, ExpiresAt = now.AddHours(6),
            IsRevoked = true, RevokedAt = now.AddMinutes(-1), CreatedByIp = "127.0.0.1" };
        var child = new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id,
            Token = hasher.HashToken(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))), ParentTokenId = root.Id,
            SessionId = sessionId, CreatedAt = now.AddMinutes(-1), UpdatedAt = now, ExpiresAt = now.AddHours(6), CreatedByIp = "127.0.0.1" };
        root.ReplacedByToken = child.Token;
        var session = new UserSession { Id = sessionId, UserId = user.Id, RefreshToken = child.Token,
            CreatedAt = now.AddHours(-1), UpdatedAt = now, LastUsedAt = now, ExpiresAt = now.AddHours(6),
            IsActive = true, IpAddress = "127.0.0.1" };
        db.Set<User>().Add(user);
        db.Set<Tenant>().Add(new Tenant { Id = tenantId, Name = "Replay alert " + marker, Slug = "replay-alert-" + marker,
            AdminEmail = "synthetic-admin-" + marker + "@example.test", IsActive = true });
        db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), UserId = user.Id, TenantId = tenantId, Role = "Member", IsActive = true });
        db.Set<UserSession>().Add(session);
        db.Set<RefreshToken>().AddRange(root, child);
        await db.SaveChangesAsync();
        return new Account(user, tenantId, root, child, session, raw);
    }

    private static async Task<OutboxMessage> AlertEventAsync(WebApplicationFactory<Program> factory, Guid tokenId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<OutboxMessage>().AsNoTracking()
            .SingleAsync(value => value.EventName == EventName && value.AggregateId == tokenId.ToString());
    }

    private static async Task FlushAsync(WebApplicationFactory<Program> factory)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var scope = factory.Services.CreateScope();
            if (await scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>().DispatchPendingAsync() == 0)
            {
                return;
            }
        }
        throw new InvalidOperationException("Synthetic owned outbox did not settle within ten dispatch cycles.");
    }

    private sealed record Account(User User, Guid TenantId, RefreshToken Root, RefreshToken Child, UserSession Session, string RawRootToken);

    private sealed class AdvancingTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow.AddMinutes(1);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }

    private sealed class QueueWriteFailure : SaveChangesInterceptor
    {
        public Guid? UserId { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Reached { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && UserId.HasValue && eventData.Context!.ChangeTracker.Entries<Notification>()
                .Any(value => value.State == EntityState.Added && value.Entity.RecipientId == UserId && value.Entity.Channel == NotificationChannel.Email))
            {
                Reached = true;
                throw new InvalidOperationException("Synthetic security email queue-write failure");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class AlertOutboxWriteFailure : SaveChangesInterceptor
    {
        public Guid? TokenId { get; set; }
        public bool Reached { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (TokenId.HasValue && eventData.Context!.ChangeTracker.Entries<OutboxMessage>().Any(value =>
                value.State == EntityState.Added && value.Entity.EventName == EventName && value.Entity.AggregateId == TokenId.Value.ToString()))
            {
                Reached = true;
                throw new InvalidOperationException("Synthetic replay alert outbox-write failure");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
