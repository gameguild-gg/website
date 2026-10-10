using System.Security.Cryptography;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace GameGuild.API.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RiskEventSessionRevocationPostgreSqlCollection : ICollectionFixture<ApiPostgreSqlFixture>
{
    internal const string Name = "Risk-event session revocation PostgreSQL";
}

/// <summary>
///     Closes the loop of issue #279: a <see cref="SuspiciousLoginDetectedV1" /> with a
///     confirmed-compromise kind must not only alert the owner, it must contain the account -
///     sessions terminated as security violations, refresh tokens revoked, and the token
///     version bumped so issued access tokens stop validating.
/// </summary>
[Collection(RiskEventSessionRevocationPostgreSqlCollection.Name)]
public sealed class RiskEventSessionRevocationPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private const string EventName = "identity.authentication.suspicious-login-detected.v1";

    [Fact]
    public async Task ImpossibleTravelEventRevokesSessionsRefreshTokensAndBumpsTokenVersion()
    {
        var clock = new AdvancingTimeProvider();
        using var factory = CreateFactory(clock);
        var account = await SeedAccountAsync(factory);

        Guid eventId;
        await using (var recordScope = factory.Services.CreateAsyncScope())
        {
            var producer = recordScope.ServiceProvider.GetRequiredService<IDurableEventProducer>();
            var @event = new SuspiciousLoginDetectedV1(
                account.UserId, SecurityAlertKinds.ImpossibleTravel, nameof(RiskLevel.High), 75)
            {
                TenantId = DurableIntegrationEventTenants.Platform,
                ActorId = DurableIntegrationEventActors.System,
                AggregateType = "User",
                AggregateId = account.UserId.ToString()
            };
            eventId = @event.EventId;
            await producer.RecordAsync(@event);
        }

        await FlushAsync(factory);
        await FlushAsync(factory);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sessions = await db.Set<UserSession>().AsNoTracking()
            .Where(value => value.UserId == account.UserId).ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, value =>
        {
            Assert.False(value.IsActive);
            Assert.NotNull(value.TerminatedAt);
            Assert.Equal(SessionTerminationReason.SecurityViolation.ToString(), value.TerminationReason);
        });
        var tokens = await db.Set<RefreshToken>().AsNoTracking()
            .Where(value => value.UserId == account.UserId).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, value =>
        {
            Assert.True(value.IsRevoked);
            Assert.NotNull(value.RevokedAt);
        });
        Assert.Equal(account.InitialTokenVersion + 1,
            (await db.Set<User>().AsNoTracking().SingleAsync(value => value.Id == account.UserId)).TokenVersion);

        // Exactly-once delivery per consumer: the revocation consumer completed the event once
        // alongside the notify-only owner-alert consumer.
        var receipts = await db.Set<InboxReceipt>().AsNoTracking()
            .Where(value => value.EventId == eventId).ToListAsync();
        Assert.Equal(2, receipts.Count);
        Assert.All(receipts, value =>
        {
            Assert.Equal(1, value.AttemptCount);
            Assert.NotNull(value.CompletedAtUtc);
        });
        Assert.Contains(receipts, value => value.ConsumerName.Contains("RiskEventSessionRevocationHandler", StringComparison.Ordinal));
        Assert.True((await db.Set<OutboxMessage>().SingleAsync(value => value.EventId == eventId)).CompletedAtUtc is not null);
    }

    [Fact]
    public async Task StepUpEventLeavesSessionsAndTokensIntact()
    {
        var clock = new AdvancingTimeProvider();
        using var factory = CreateFactory(clock);
        var account = await SeedAccountAsync(factory);

        Guid eventId;
        await using (var recordScope = factory.Services.CreateAsyncScope())
        {
            var producer = recordScope.ServiceProvider.GetRequiredService<IDurableEventProducer>();
            var @event = new SuspiciousLoginDetectedV1(
                account.UserId, SecurityAlertKinds.LoginStepUpRequired, nameof(RiskLevel.High), 65)
            {
                TenantId = DurableIntegrationEventTenants.Platform,
                ActorId = DurableIntegrationEventActors.System,
                AggregateType = "User",
                AggregateId = account.UserId.ToString()
            };
            eventId = @event.EventId;
            await producer.RecordAsync(@event);
        }

        await FlushAsync(factory);
        await FlushAsync(factory);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.All(await db.Set<UserSession>().AsNoTracking()
            .Where(value => value.UserId == account.UserId).ToListAsync(), value => Assert.True(value.IsActive));
        Assert.All(await db.Set<RefreshToken>().AsNoTracking()
            .Where(value => value.UserId == account.UserId).ToListAsync(), value => Assert.False(value.IsRevoked));
        Assert.Equal(account.InitialTokenVersion,
            (await db.Set<User>().AsNoTracking().SingleAsync(value => value.Id == account.UserId)).TokenVersion);
    }

    [Fact]
    public async Task DisabledRevocationSwitchKeepsSessionsAndTokensIntact()
    {
        var clock = new AdvancingTimeProvider();
        using var factory = CreateFactory(clock, enabled: "false");
        var account = await SeedAccountAsync(factory);

        Guid eventId;
        await using (var recordScope = factory.Services.CreateAsyncScope())
        {
            var producer = recordScope.ServiceProvider.GetRequiredService<IDurableEventProducer>();
            var @event = new SuspiciousLoginDetectedV1(
                account.UserId, SecurityAlertKinds.ImpossibleTravel, nameof(RiskLevel.High), 75)
            {
                TenantId = DurableIntegrationEventTenants.Platform,
                ActorId = DurableIntegrationEventActors.System,
                AggregateType = "User",
                AggregateId = account.UserId.ToString()
            };
            eventId = @event.EventId;
            await producer.RecordAsync(@event);
        }

        await FlushAsync(factory);
        await FlushAsync(factory);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.All(await db.Set<UserSession>().AsNoTracking()
            .Where(value => value.UserId == account.UserId).ToListAsync(), value => Assert.True(value.IsActive));
        Assert.All(await db.Set<RefreshToken>().AsNoTracking()
            .Where(value => value.UserId == account.UserId).ToListAsync(), value => Assert.False(value.IsRevoked));
        Assert.Equal(account.InitialTokenVersion,
            (await db.Set<User>().AsNoTracking().SingleAsync(value => value.Id == account.UserId)).TokenVersion);
        // The revocation consumer still completes its inbox receipt (idempotent no-op), so the
        // event settles: only the notify-only consumer remains as a receipt for the alert.
        var receipt = await db.Set<InboxReceipt>().AsNoTracking()
            .SingleAsync(value => value.EventId == eventId && value.ConsumerName.Contains("RiskEventSessionRevocationHandler", StringComparison.Ordinal));
        Assert.Equal(1, receipt.AttemptCount);
        Assert.NotNull(receipt.CompletedAtUtc);
    }

    private WebApplicationFactory<Program> CreateFactory(AdvancingTimeProvider clock, string? enabled = null) =>
        fixture.Factory.WithWebHostBuilder(builder =>
        {
            if (enabled is not null)
            {
                builder.UseSetting("Authentication:RiskEventRevocation:Enabled", enabled);
            }
            builder.ConfigureTestServices(services =>
            {
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
            });
        });

    private static async Task<Account> SeedAccountAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>();
        var marker = Guid.NewGuid().ToString("N");
        var email = $"risk-revocation-{marker}@example.test";
        var user = User.CreateWithPassword(email, "Synthetic risk revocation owner",
            scope.ServiceProvider.GetRequiredService<IPasswordHasher>().HashPassword("S3cure-Risk-Password!"),
            $"risk-revocation-{marker}");
        db.Set<User>().Add(user);
        var now = new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);
        for (var index = 0; index < 2; index++)
        {
            var hash = hasher.HashToken(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));
            var sessionId = Guid.NewGuid();
            db.Set<UserSession>().Add(new UserSession
            {
                Id = sessionId,
                UserId = user.Id,
                RefreshToken = hash,
                CreatedAt = now.AddMinutes(-10),
                UpdatedAt = now,
                LastUsedAt = now,
                ExpiresAt = now.AddHours(6),
                IpAddress = "127.0.0.1",
                IsActive = true
            });
            db.Set<RefreshToken>().Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = hash,
                SessionId = sessionId,
                CreatedAt = now.AddMinutes(-10),
                UpdatedAt = now,
                ExpiresAt = now.AddHours(6),
                CreatedByIp = "127.0.0.1"
            });
        }

        await db.SaveChangesAsync();
        return new Account(user.Id, email, user.TokenVersion);
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
        throw new InvalidOperationException("The risk-event revocation outbox did not settle within ten dispatch cycles.");
    }

    private sealed record Account(Guid UserId, string Email, int InitialTokenVersion);

    private sealed class AdvancingTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow.AddMinutes(1);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }
}
