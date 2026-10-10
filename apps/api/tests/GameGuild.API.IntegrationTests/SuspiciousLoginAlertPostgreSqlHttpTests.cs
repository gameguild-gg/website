using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using NotificationPriority = GameGuild.Notifications.NotificationPriority;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameGuild.API.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SuspiciousLoginAlertPostgreSqlCollection : ICollectionFixture<ApiPostgreSqlFixture>
{
    internal const string Name = "Suspicious-login alert PostgreSQL";
}

[Collection(SuspiciousLoginAlertPostgreSqlCollection.Name)]
public sealed class SuspiciousLoginAlertPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private const string EventName = "identity.authentication.suspicious-login-detected.v1";
    private const string Endpoint = "/v1/auth/sign-in";
    private const string Password = "S3cure-Alert-Password!";

    [Fact]
    public async Task HighRiskStepUpSignInProducesRedactedOwnerAlertsOnceThroughActualInbox()
    {
        var clock = new AdvancingTimeProvider();
        using var factory = CreateFactory(clock);
        var account = await SeedAsync(factory);
        using var client = factory.CreateClient();
        SetStableClientIdentity(client);

        using var response = await client.PostAsJsonAsync(Endpoint,
            new { email = account.Email, password = Password, deviceFingerprint = "current-request-fingerprint" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("requiresStepUp").GetBoolean());
        Assert.False(document.RootElement.GetProperty("success").GetBoolean());

        var message = await AlertEventAsync(factory, account.UserId);
        Assert.Equal(DurableIntegrationEventActors.System, message.ActorId);
        Assert.Equal(DurableIntegrationEventTenants.Platform, message.TenantId);
        Assert.Contains(account.UserId.ToString(), message.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(SecurityAlertKinds.LoginStepUpRequired, message.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(account.Email, message.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("203.0.113.10", message.Payload, StringComparison.Ordinal);

        await FlushAsync(factory);
        await FlushAsync(factory);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = await db.Set<Notification>().AsNoTracking()
            .Where(value => value.ReferenceEntityId == message.EventId && value.Type == NotificationType.Security).ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, value =>
        {
            Assert.Equal(account.UserId, value.RecipientId);
            Assert.Equal(NotificationPriority.Urgent, value.Priority);
            Assert.Equal(nameof(SuspiciousLoginDetectedV1), value.ReferenceEntityType);
            Assert.Null(value.Metadata);
            Assert.Contains("high-risk sign-in attempt", value.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(account.Email, value.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("203.0.113.10", value.Message, StringComparison.Ordinal);
        });
        Assert.Equal(account.Email, Assert.Single(notifications, value => value.Channel == NotificationChannel.Email).RecipientEmail);
        Assert.True(Assert.Single(notifications, value => value.Channel == NotificationChannel.InApp).IsSent);
        Assert.NotNull((await db.Set<OutboxMessage>().SingleAsync(value => value.EventId == message.EventId)).CompletedAtUtc);
        var receipt = Assert.Single(await db.Set<InboxReceipt>().Where(value => value.EventId == message.EventId).ToListAsync());
        Assert.Equal(1, receipt.AttemptCount);
        Assert.NotNull(receipt.CompletedAtUtc);
    }

    [Fact]
    public async Task BruteForceAgainstKnownAccountAlertsOwnerEvenWhenTheAttemptFails()
    {
        var clock = new AdvancingTimeProvider();
        var recorder = new RecordingLoggerProvider();
        using var factory = CreateFactory(clock,
            maxFailedAttemptsPerHour: "10", // above the seeded five failures so the lockout filter lets the request reach the sign-in action
            recorder: recorder);
        var account = await SeedAsync(factory, bruteForceHistory: true);
        using var client = factory.CreateClient();
        SetStableClientIdentity(client);

        using var response = await client.PostAsJsonAsync(Endpoint,
            new { email = account.Email, password = "wrong-password", deviceFingerprint = "current-request-fingerprint" });

        var responseBody = await response.Content.ReadAsStringAsync();
        var message = await AlertEventOrNullAsync(factory, account.UserId);
        if (message is null)
        {
            Assert.Fail(await DescribePipelineStateAsync(factory, account, response.StatusCode, responseBody, recorder));
        }
        Assert.Contains(SecurityAlertKinds.BruteForceDetected, message.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(account.Email, message.Payload, StringComparison.Ordinal);

        await FlushAsync(factory);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = await db.Set<Notification>().AsNoTracking()
            .Where(value => value.ReferenceEntityId == message.EventId && value.Type == NotificationType.Security).ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, value => Assert.Contains("repeated failed sign-in attempts", value.Message, StringComparison.Ordinal));
        Assert.Equal(account.Email, Assert.Single(notifications, value => value.Channel == NotificationChannel.Email).RecipientEmail);
    }

    [Fact]
    public async Task RecordedImpossibleTravelEventAlertsOwnerExactlyOnce()
    {
        var clock = new AdvancingTimeProvider();
        using var factory = CreateFactory(clock);
        var account = await SeedAsync(factory);

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
        var notifications = await db.Set<Notification>().AsNoTracking()
            .Where(value => value.ReferenceEntityId == eventId && value.Type == NotificationType.Security).ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, value => Assert.Contains("successful sign-in", value.Message, StringComparison.Ordinal));
        Assert.Equal(account.Email, Assert.Single(notifications, value => value.Channel == NotificationChannel.Email).RecipientEmail);
        var receipt = Assert.Single(await db.Set<InboxReceipt>().Where(value => value.EventId == eventId).ToListAsync());
        Assert.Equal(1, receipt.AttemptCount);
        Assert.NotNull(receipt.CompletedAtUtc);
    }

    [Fact]
    public async Task SecurityNotificationsDisabledSuppressesTheAlertEvent()
    {
        var clock = new AdvancingTimeProvider();
        using var factory = CreateFactory(clock, enabled: "false");
        var account = await SeedAsync(factory);
        using var client = factory.CreateClient();
        SetStableClientIdentity(client);

        using var response = await client.PostAsJsonAsync(Endpoint,
            new { email = account.Email, password = Password, deviceFingerprint = "current-request-fingerprint" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Set<OutboxMessage>().AnyAsync(value => value.EventName == EventName && value.AggregateId == account.UserId.ToString()));
    }

    [Fact]
    public async Task RaisedMinimumRiskLevelSuppressesHighRiskStepUpAlerts()
    {
        var clock = new AdvancingTimeProvider();
        using var factory = CreateFactory(clock, minimumRiskLevel: "Critical");
        var account = await SeedAsync(factory);
        using var client = factory.CreateClient();
        SetStableClientIdentity(client);

        using var response = await client.PostAsJsonAsync(Endpoint,
            new { email = account.Email, password = Password, deviceFingerprint = "current-request-fingerprint" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("requiresStepUp").GetBoolean());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Set<OutboxMessage>().AnyAsync(value => value.EventName == EventName && value.AggregateId == account.UserId.ToString()));
    }

    private WebApplicationFactory<Program> CreateFactory(AdvancingTimeProvider clock,
        string? enabled = null, string? minimumRiskLevel = null, RecordingLoggerProvider? recorder = null,
        string? maxFailedAttemptsPerHour = null) =>
        fixture.Factory.WithWebHostBuilder(builder =>
        {
            if (enabled is not null)
            {
                builder.UseSetting("Authentication:SecurityNotifications:Enabled", enabled);
            }
            if (minimumRiskLevel is not null)
            {
                builder.UseSetting("Authentication:SecurityNotifications:MinimumRiskLevel", minimumRiskLevel);
            }
            if (maxFailedAttemptsPerHour is not null)
            {
                builder.UseSetting("AuthenticationSecurity:MaxFailedAttemptsPerHour", maxFailedAttemptsPerHour);
            }
            builder.ConfigureTestServices(services =>
            {
                if (recorder is not null)
                {
                    services.AddSingleton<ILoggerProvider>(recorder);
                }
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

    private static void SetStableClientIdentity(HttpClient client)
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SuspiciousLoginAlertClient/1.0");
        client.DefaultRequestHeaders.Add("X-Device-Fingerprint", "current-request-fingerprint");
    }

    private static async Task<Account> SeedAsync(WebApplicationFactory<Program> factory, bool bruteForceHistory = false)
    {
        var marker = Guid.NewGuid().ToString("N");
        var email = $"suspicious-login-{marker}@example.test";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = User.CreateWithPassword(email, "Synthetic suspicious login owner", hasher.HashPassword(Password), $"suspicious-login-{marker}");
        db.Set<User>().Add(user);

        var now = DateTime.UtcNow;
        // Last successful sign-in far outside the 24h window, from a different IP, user agent,
        // and device fingerprint: absence (+10) + IP change (+20) + UA change (+15) + fingerprint
        // change (+15) = 60 => High risk, so the sign-in flow must demand step-up authentication.
        db.Set<AuthenticationAttempt>().Add(new AuthenticationAttempt
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserId = user.Id,
            IpAddress = "203.0.113.10",
            UserAgent = "SeededHistoricAgent/2.0",
            IsSuccessful = true,
            AttemptedAt = now.AddHours(-48),
            Location = "BR-Rio",
            DeviceFingerprint = "seeded-historic-fingerprint"
        });
        if (bruteForceHistory)
        {
            // Seed five committed failures so the brute-force detector (threshold 5 within
            // 15 minutes) fires from stored history alone: the attempt the failing request
            // records shares the request transaction, which the sign-in failure rolls back.
            // The lockout action filter shares the same threshold (MaxFailedAttemptsPerHour),
            // so tests that seed this history raise that limit via factory configuration to
            // keep the request reaching the sign-in action and its owner alert.
            for (var index = 0; index < 5; index++)
            {
                db.Set<AuthenticationAttempt>().Add(new AuthenticationAttempt
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    UserId = user.Id,
                    IpAddress = "198.51.100." + (20 + index),
                    UserAgent = "BruteForceAgent/9.9",
                    IsSuccessful = false,
                    FailureReason = "InvalidCredentials",
                    AttemptedAt = now.AddMinutes(-5)
                });
            }
        }

        await db.SaveChangesAsync();
        return new Account(user.Id, email);
    }

    private static async Task<OutboxMessage> AlertEventAsync(WebApplicationFactory<Program> factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<OutboxMessage>().AsNoTracking()
            .SingleAsync(value => value.EventName == EventName && value.AggregateId == userId.ToString());
    }

    private static async Task<OutboxMessage?> AlertEventOrNullAsync(WebApplicationFactory<Program> factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<OutboxMessage>().AsNoTracking()
            .SingleOrDefaultAsync(value => value.EventName == EventName && value.AggregateId == userId.ToString());
    }

    /// <summary>
    ///     Dumps every observable of the suspicious-login pipeline (response, stored attempts, outbox
    ///     rows, captured host logs) so a missing alert event can be diagnosed from the CI failure
    ///     message alone - the PostgreSQL suites cannot be executed under the local build gate.
    /// </summary>
    private static async Task<string> DescribePipelineStateAsync(
        WebApplicationFactory<Program> factory,
        Account account,
        HttpStatusCode statusCode,
        string responseBody,
        RecordingLoggerProvider recorder)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attempts = await db.Set<AuthenticationAttempt>().AsNoTracking()
            .Where(value => value.Email == account.Email)
            .OrderByDescending(value => value.AttemptedAt).Take(10)
            .Select(value => $"{value.AttemptedAt:HH:mm:ss} success={value.IsSuccessful} reason={value.FailureReason ?? "-"} ip={value.IpAddress}")
            .ToListAsync();
        var outbox = await db.Set<OutboxMessage>().AsNoTracking()
            .Where(value => value.AggregateId == account.UserId.ToString())
            .Select(value => $"{value.EventName} at {value.CreatedAtUtc:HH:mm:ss}")
            .ToListAsync();

        var keywordFilters = new[]
        {
            "brute", "Brute", "lockout", "Lockout", "suspicious", "Suspicious", "throttl", "Throttl",
            "Could not record", "Could not analyze", "Could not forward", "Anomalous", "Invalid password",
            "User not found", "SIEM", "error while", " failed"
        };
        var relevantLogs = recorder.Entries
            .Where(entry => keywordFilters.Any(keyword => entry.Message.Contains(keyword, StringComparison.Ordinal)))
            .Select(entry => $"{entry.Timestamp:HH:mm:ss.fff} {entry.Level} [{entry.Category}] {entry.Message}")
            .TakeLast(60);
        var allLogs = recorder.Entries
            .Select(entry =>
            {
                var category = entry.Category.Length <= 40
                    ? entry.Category
                    : "..." + entry.Category[^37..];
                return $"{entry.Timestamp:HH:mm:ss.fff} {entry.Level} [{category}] {entry.Message}";
            })
            .TakeLast(80);

        return string.Join(Environment.NewLine,
        [
            $"No '{EventName}' outbox event for user {account.UserId}.",
            $"Response {(int)statusCode}: {responseBody}",
            $"Stored attempts for {account.Email}:",
            .. attempts,
            "Outbox rows for the user aggregate:",
            .. outbox,
            $"Relevant host logs (of {recorder.Entries.Count} captured):",
            .. relevantLogs,
            "Last captured host logs, unfiltered:",
            .. allLogs
        ]);
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
        throw new InvalidOperationException("Synthetic suspicious-login outbox did not settle within ten dispatch cycles.");
    }

    private sealed record Account(Guid UserId, string Email);

    /// <summary>Captures host log output so failing assertions can embed the pipeline's own diagnostics.</summary>
    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<RecordedEntry> _entries = new();

        public IReadOnlyList<RecordedEntry> Entries => _entries.ToList();

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(this, categoryName);

        public void Dispose() { }

        public sealed record RecordedEntry(DateTimeOffset Timestamp, string Category, LogLevel Level, string Message);

        private sealed class RecordingLogger(RecordingLoggerProvider owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var text = formatter(state, exception);
                if (exception is not null)
                {
                    text += $" | {exception.GetType().Name}: {exception.Message}";
                }
                owner._entries.Enqueue(new RecordedEntry(DateTimeOffset.UtcNow, category, logLevel, text));
            }
        }
    }

    private sealed class AdvancingTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow.AddMinutes(1);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }
}
