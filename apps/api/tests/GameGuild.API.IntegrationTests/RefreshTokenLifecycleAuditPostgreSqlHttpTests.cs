using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class RefreshTokenLifecycleAuditPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private const string RefreshEndpoint = "/v1/auth/tokens:refresh";
    private const string RevokeEndpoint = "/v1/auth/tokens:revoke";

    [Fact]
    public async Task RotationCommitsRedactedAuditAndOneMetricWithPersistedLineage()
    {
        using var factory = CreateFactory();
        var account = await SeedAsync(factory);
        using var measurements = new Measurements();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = account.RawToken, account.TenantId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var replacement = payload.RootElement.GetProperty("refreshToken").GetString()!;
        var audit = Assert.Single(await AuditsAsync(factory, account.User.Id), value => value.ActionType == "Authentication.RefreshTokenRotated");
        Assert.Equal(account.Session.Id, audit.SessionId);
        Assert.Equal(account.TenantId, audit.TenantId);
        Assert.True(audit.Success);
        Assert.Equal(AuditCategory.Authentication, audit.Category);
        Assert.Contains(account.Token.Id.ToString(), audit.Metadata!, StringComparison.Ordinal);
        Assert.DoesNotContain(account.RawToken, audit.Metadata!, StringComparison.Ordinal);
        Assert.DoesNotContain(account.Token.Token, audit.Metadata!, StringComparison.Ordinal);
        Assert.DoesNotContain(replacement, audit.Metadata!, StringComparison.Ordinal);
        Assert.DoesNotContain(account.User.Email, audit.Metadata!, StringComparison.Ordinal);
        Assert.Equal(1, measurements.Count("rotated", "committed"));
        measurements.AssertBoundedTags();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectionSurvivesCommandRollbackWithoutCredentialsOrCredentialValues(bool expired)
    {
        using var factory = CreateFactory();
        var account = await SeedAsync(factory);
        if (expired)
        {
            using var seed = factory.Services.CreateScope();
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Set<RefreshToken>().SingleAsync(value => value.Id == account.Token.Id)).ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        var raw = expired ? account.RawToken : Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var reason = expired ? "Expired" : "Unknown";
        var ownerId = expired ? account.User.Id : (Guid?)null;
        using var verification = factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // The collection database retains earlier requests, including anonymous denials.
        // Seed one explicitly so this check proves exactly one NEW durable rejection.
        var earlierRejection = new AuditLog
        {
            ActionType = "Authentication.RefreshTokenRejected", ErrorMessage = reason, UserId = ownerId,
            Category = AuditCategory.Authentication, Success = false, Description = "Earlier independent rejection"
        };
        context.Set<AuditLog>().Add(earlierRejection);
        await context.SaveChangesAsync();
        var previousRejectionIds = await context.Set<AuditLog>().AsNoTracking()
            .Where(value => value.ActionType == "Authentication.RefreshTokenRejected" && value.ErrorMessage == reason && value.UserId == ownerId)
            .Select(value => value.Id).ToListAsync();
        using var measurements = new Measurements();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = raw, account.TenantId });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(raw, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var audit = Assert.Single(await context.Set<AuditLog>().AsNoTracking()
            .Where(value => value.ActionType == "Authentication.RefreshTokenRejected" && value.ErrorMessage == reason && value.UserId == ownerId &&
                !previousRejectionIds.Contains(value.Id)).ToListAsync());
        Assert.Equal(previousRejectionIds.Count, await context.Set<AuditLog>().CountAsync(value => previousRejectionIds.Contains(value.Id)));
        Assert.Equal(expired ? account.User.Id : (Guid?)null, audit.UserId);
        Assert.False(audit.Success);
        Assert.Equal(expired ? "Expired" : "Unknown", audit.ErrorMessage);
        var auditId = audit.Id.ToString();
        Assert.False(await context.Set<OutboxMessage>().AnyAsync(value => value.AggregateType == nameof(AuditLog) && value.AggregateId == auditId));
        Assert.DoesNotContain(raw, audit.Metadata!, StringComparison.Ordinal);
        Assert.DoesNotContain(account.Token.Token, audit.Metadata!, StringComparison.Ordinal);
        Assert.Equal(1, measurements.Count("rejected", "rejected"));
        Assert.Equal(0, measurements.Count("rotated", "committed"));
        measurements.AssertBoundedTags();
    }

    [Fact]
    public async Task ReplayCommitsContainmentAuditAndMetricWhileReturningNoCredentials()
    {
        using var factory = CreateFactory();
        var account = await SeedAsync(factory);
        using var initial = factory.CreateClient();
        using var rotation = await initial.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = account.RawToken, account.TenantId });
        Assert.Equal(HttpStatusCode.OK, rotation.StatusCode);
        using var measurements = new Measurements();
        using var response = await initial.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = account.RawToken, account.TenantId });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(account.RawToken, body, StringComparison.Ordinal);
        var audit = Assert.Single(await AuditsAsync(factory, account.User.Id), value => value.ActionType == "Authentication.RefreshTokenReplayContained");
        Assert.Equal(account.Token.Id.ToString(), audit.ResourceId);
        Assert.False(audit.Success);
        Assert.Equal(AuditRiskLevel.Critical, audit.RiskLevel);
        Assert.Equal(1, measurements.Count("replaycontained", "contained"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.All(await db.Set<RefreshToken>().Where(value => value.UserId == account.User.Id).ToListAsync(), value => Assert.True(value.IsRevoked));
        Assert.False((await db.Set<UserSession>().SingleAsync(value => value.Id == account.Session.Id)).IsActive);
        measurements.AssertBoundedTags();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitRevocationCommitsAuditAndMetricForOwnedTokenOrWholeAccount(bool all)
    {
        using var factory = CreateFactory();
        var account = await SeedAsync(factory);
        using var measurements = new Measurements();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var jwt = await scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessTokenAsync(
                account.User.Id, account.User.Email, ["Member"], account.TenantId, account.User.TokenVersion, account.Session.Id);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        }
        using var response = await client.PostAsJsonAsync(all ? "/v1/auth/sessions:terminate-all" : RevokeEndpoint, new { token = account.RawToken });
        Assert.Equal(all ? HttpStatusCode.OK : HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(await AuditsAsync(factory, account.User.Id), value => value.ActionType ==
            (all ? "Authentication.RefreshTokenAllRevoked" : "Authentication.RefreshTokenRevoked"));
        Assert.Equal(1, measurements.Count(all ? "allrevoked" : "revoked", "committed"));
        measurements.AssertBoundedTags();
    }

    [Fact]
    public async Task CommitFailureRollsBackAuditAndRotationWithoutEmittingCommittedMetric()
    {
        var fault = new CommitFailure();
        using var factory = CreateFactory(fault);
        var account = await SeedAsync(factory);
        fault.UserId = account.User.Id;
        using var measurements = new Measurements();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = account.RawToken, account.TenantId });
        Assert.True(fault.Reached);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(await AuditsAsync(factory, account.User.Id));
        Assert.Equal(0, measurements.Count("rotated", "committed"));
        Assert.Equal(0, measurements.Count("issued", "committed"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var token = Assert.Single(await db.Set<RefreshToken>().AsNoTracking().Where(value => value.UserId == account.User.Id).ToListAsync());
        Assert.False(token.IsRevoked);
        Assert.Null(token.ReplacedByToken);
        Assert.Equal(account.Token.Token, (await db.Set<UserSession>().AsNoTracking().SingleAsync(value => value.Id == account.Session.Id)).RefreshToken);
    }

    [Fact]
    public async Task FailedBestEffortAuditTransportDoesNotUndoReplayContainmentOrItsTransactionalAudit()
    {
        using var factory = CreateFactory(auditTransportFails: true);
        var account = await SeedAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var root = await db.Set<RefreshToken>().SingleAsync(value => value.Id == account.Token.Id);
        root.IsRevoked = true;
        root.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = account.RawToken, account.TenantId });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(await AuditsAsync(factory, account.User.Id), value => value.ActionType == "Authentication.RefreshTokenReplayContained");
        db.ChangeTracker.Clear();
        Assert.False((await db.Set<UserSession>().SingleAsync(value => value.Id == account.Session.Id)).IsActive);
        Assert.Equal(account.User.TokenVersion + 1, (await db.Set<User>().SingleAsync(value => value.Id == account.User.Id)).TokenVersion);
    }

    [Fact]
    public async Task ForeignTenantDenialAuditsTheKnownOwnerWithoutTrustingTheRequestedTenant()
    {
        using var factory = CreateFactory();
        var account = await SeedAsync(factory);
        var foreign = await SeedAsync(factory);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = account.RawToken, tenantId = foreign.TenantId });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var audit = Assert.Single(await AuditsAsync(factory, account.User.Id));
        Assert.Equal("Authentication.RefreshTokenRejected", audit.ActionType);
        Assert.Equal("TenantDenied", audit.ErrorMessage);
        Assert.Null(audit.TenantId);
        Assert.False(audit.Success);
        using var scope = factory.Services.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<RefreshToken>()
            .AsNoTracking().SingleAsync(value => value.Id == account.Token.Id)).IsRevoked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransactionalAuditStorageFailureRollsBackIssuanceSessionAndClaim(bool all)
    {
        var fault = new AuditStorageFailure { ActionType = all ? "Authentication.RefreshTokenAllRevoked" : "Authentication.RefreshTokenRotated" };
        using var factory = CreateFactory(auditStorageFailure: fault);
        var account = await SeedAsync(factory);
        fault.UserId = account.User.Id;
        using var measurements = new Measurements();
        using var client = factory.CreateClient();
        if (all)
        {
            using var authentication = factory.Services.CreateScope();
            var access = await authentication.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessTokenAsync(
                account.User.Id, account.User.Email, ["Member"], account.TenantId, account.User.TokenVersion, account.Session.Id);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        }
        using var response = await client.PostAsJsonAsync(all ? "/v1/auth/sessions:terminate-all" : RefreshEndpoint,
            new { refreshToken = account.RawToken, account.TenantId });
        Assert.True(fault.Reached, $"The transactional audit fault was not reached; HTTP status was {response.StatusCode}.");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(await AuditsAsync(factory, account.User.Id));
        Assert.Equal(0, measurements.Count("rotated", "committed"));
        Assert.Equal(0, measurements.Count("issued", "committed"));
        Assert.Equal(0, measurements.Count("allrevoked", "committed"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var root = Assert.Single(await db.Set<RefreshToken>().AsNoTracking().Where(value => value.UserId == account.User.Id).ToListAsync());
        Assert.False(root.IsRevoked);
        Assert.Null(root.ReplacedByToken);
        Assert.Equal(account.User.TokenVersion, (await db.Set<User>().AsNoTracking().SingleAsync(value => value.Id == account.User.Id)).TokenVersion);
        Assert.Equal(account.Token.Token, (await db.Set<UserSession>().AsNoTracking().SingleAsync(value => value.Id == account.Session.Id)).RefreshToken);
    }

    private WebApplicationFactory<Program> CreateFactory(CommitFailure? fault = null, bool auditTransportFails = false,
        AuditStorageFailure? auditStorageFailure = null) =>
        fixture.CreateFactory(builder => builder.ConfigureTestServices(services =>
        {
            // These existing cases exercise account containment and persisted version invalidation.
            services.PostConfigure<JwtOptions>(options => options.RefreshTokenReplayContainmentScope = RefreshTokenReplayScope.Account);
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            });
            if (fault is not null)
            {
                services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(fault));
            }
            if (auditStorageFailure is not null)
            {
                services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(auditStorageFailure));
            }
            if (auditTransportFails)
            {
                services.RemoveAll<IAuthenticationAuditEventSink>();
                services.AddScoped<IAuthenticationAuditEventSink, UnavailableAuditTransport>();
            }
        }));

    private static async Task<Account> SeedAsync(WebApplicationFactory<Program> factory)
    {
        var marker = Guid.NewGuid().ToString("N");
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = User.Create($"refresh-audit-{marker}@example.test", "Synthetic refresh audit account");
        user.Username = $"refresh-audit-{marker}";
        var tenant = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var sessionId = Guid.NewGuid();
        var token = new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id,
            Token = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().HashToken(raw), SessionId = sessionId,
            CreatedAt = now.AddHours(-1), UpdatedAt = now, ExpiresAt = now.AddHours(6), CreatedByIp = "127.0.0.1" };
        var session = new UserSession { Id = sessionId, UserId = user.Id, RefreshToken = token.Token, IpAddress = "127.0.0.1",
            CreatedAt = now.AddHours(-1), UpdatedAt = now, LastUsedAt = now, ExpiresAt = now.AddHours(6), IsActive = true };
        db.Set<User>().Add(user);
        db.Set<Tenant>().Add(new Tenant { Id = tenant, Name = $"Refresh audit {marker}", Slug = $"refresh-audit-{marker}",
            AdminEmail = $"admin-{marker}@example.test", IsActive = true });
        db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), TenantId = tenant, UserId = user.Id, Role = "Member", IsActive = true });
        db.Set<RefreshToken>().Add(token);
        db.Set<UserSession>().Add(session);
        await db.SaveChangesAsync();
        return new Account(user, tenant, token, session, raw);
    }

    private static async Task<List<AuditLog>> AuditsAsync(WebApplicationFactory<Program> factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AuditLog>().AsNoTracking()
            .Where(value => value.UserId == userId && value.ActionType.StartsWith("Authentication.RefreshToken")).ToListAsync();
    }

    private sealed record Account(User User, Guid TenantId, RefreshToken Token, UserSession Session, string RawToken);

    private sealed class UnavailableAuditTransport : IAuthenticationAuditEventSink
    {
        public Task RecordAsync(AuthenticationAuditEvent auditEvent, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(auditEvent);
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Synthetic unavailable best-effort audit transport");
        }
    }

    private sealed class CommitFailure : DbTransactionInterceptor
    {
        public Guid? UserId { get; set; }
        public bool Reached { get; private set; }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (UserId.HasValue && eventData.Context!.ChangeTracker.Entries<RefreshToken>()
                    .Any(value => value.Entity.UserId == UserId))
            {
                Reached = true;
                throw new InvalidOperationException("Synthetic failure before transaction commit");
            }
            return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
        }
    }

    private sealed class AuditStorageFailure : SaveChangesInterceptor
    {
        public Guid? UserId { get; set; }
        public string ActionType { get; set; } = "Authentication.RefreshTokenRotated";
        public bool Reached { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (UserId.HasValue && eventData.Context!.ChangeTracker.Entries<AuditLog>().Any(value =>
                    value.State == EntityState.Added && value.Entity.UserId == UserId && value.Entity.ActionType == ActionType))
            {
                Reached = true;
                throw new InvalidOperationException("Synthetic lifecycle audit storage failure");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class Measurements : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly ConcurrentQueue<(long Value, Dictionary<string, string> Tags)> _values = new();

        public Measurements()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "GameGuild.Identity.Authentication.RefreshTokens" && instrument.Name == "authentication.refresh_token.operations")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
                _values.Enqueue((value, tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value?.ToString() ?? string.Empty))));
            _listener.Start();
        }

        public long Count(string operation, string outcome) => _values.Where(value =>
            value.Tags.GetValueOrDefault("operation") == operation && value.Tags.GetValueOrDefault("outcome") == outcome).Sum(value => value.Value);

        public void AssertBoundedTags() => Assert.All(_values, value =>
        {
            Assert.Equal(new[] { "operation", "outcome", "reason" }, value.Tags.Keys.Order(StringComparer.Ordinal).ToArray());
            Assert.All(value.Tags.Values, tag => Assert.False(Guid.TryParse(tag, out _)));
        });

        public void Dispose() => _listener.Dispose();
    }
}
