using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Core.Security;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class RefreshTokenCleanupPostgreSqlTests(ApiPostgreSqlFixture fixture) : IAsyncLifetime
{
    private readonly List<SeededChain> seededChains = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Each test owns its seeded IDs. Rollback tests deliberately retain
        // expired rows; remove them after assertions so they cannot consume a
        // later test's one-row batch or race the hosted worker's deadline.
        var tokenIds = seededChains.SelectMany(chain => chain.AllTokens).Select(token => token.Id).ToArray();
        var sessionIds = seededChains.SelectMany(chain => chain.LiveSession is null
            ? new[] { chain.ExpiredSession.Id }
            : new[] { chain.ExpiredSession.Id, chain.LiveSession.Id }).ToArray();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        while (await db.Set<RefreshToken>().AnyAsync(token => tokenIds.Contains(token.Id)))
        {
            var removed = await db.Set<RefreshToken>()
                .Where(token => tokenIds.Contains(token.Id))
                .Where(token => !db.Set<RefreshToken>().Any(child => child.ParentTokenId == token.Id))
                .ExecuteDeleteAsync();
            Assert.True(removed > 0, "Cleanup fixture tokens have an unexpected surviving descendant.");
        }
        await db.Set<UserSession>().Where(session => sessionIds.Contains(session.Id)).ExecuteDeleteAsync();
        Assert.False(await db.Set<UserSession>().AnyAsync(session => sessionIds.Contains(session.Id)));
    }
    [Fact]
    public async Task CommittedCleanupRetainsActiveDescendantsAndReplayEvidenceAndAuditsRealCounts()
    {
        var seeded = await SeedAsync(includeRetained: true);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var before = await db.Set<AuditLog>().CountAsync(row => row.ActionType == "Authentication.RefreshTokenCleanup");
        var result = await Operation(db).RunAsync(CancellationToken.None);
        Assert.True(result.TokensDeleted >= 2);
        Assert.True(result.SessionsDeleted >= 1);
        await AssertExpiredStateAsync(seeded, retained: false);
        using var verification = fixture.Factory.Services.CreateScope();
        var stored = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (var token in seeded.RetainedTokens)
        {
            Assert.True(await stored.Set<RefreshToken>().AnyAsync(row => row.Id == token.Id));
        }
        Assert.True(await stored.Set<UserSession>().AnyAsync(row => row.Id == seeded.LiveSession!.Id));
        Assert.Equal(before + 1, await stored.Set<AuditLog>().CountAsync(row => row.ActionType == "Authentication.RefreshTokenCleanup"));
        var audit = await stored.Set<AuditLog>().Where(row => row.ActionType == "Authentication.RefreshTokenCleanup")
            .OrderByDescending(row => row.CreatedAt).FirstAsync();
        using var metadata = JsonDocument.Parse(audit.Metadata!);
        Assert.Equal(result.TokensDeleted, metadata.RootElement.GetProperty("TokensDeleted").GetInt32());
        Assert.Equal(result.SessionsDeleted, metadata.RootElement.GetProperty("SessionsDeleted").GetInt32());
        Assert.Equal("System", metadata.RootElement.GetProperty("ActorKind").GetString());
        Assert.Null(audit.UserId);
        Assert.Null(audit.TenantId);
        foreach (var token in seeded.AllTokens) { Assert.DoesNotContain(token.Token, audit.Metadata!); }
    }

    [Fact]
    public async Task RequiredAuditFailureRollsBackDeletionAndEmitsNoCommittedRows()
    {
        var seeded = await SeedAsync(includeRetained: false);
        long committedRows = 0;
        using var listener = RowListener(value => committedRows += value);
        using var scope = fixture.Factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>();
        await using var db = new FailingCleanupAuditContext(options);
        await Assert.ThrowsAsync<IOException>(() => Operation(db).RunAsync(CancellationToken.None));
        Assert.Equal(0, committedRows);
        await AssertExpiredStateAsync(seeded, retained: true);
    }

    [Fact]
    public async Task CancellationAfterStorageWriteRollsBackTheWholeCycle()
    {
        var seeded = await SeedAsync(includeRetained: false);
        using var cancellation = new CancellationTokenSource();
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tokenStore = new CancelAfterDeletionStore(new RefreshTokenRepository(db), cancellation);
        var operation = new RefreshTokenCleanupOperation(db, tokenStore, new UserSessionRepository(db),
            Options.Create(Policy()), TimeProvider.System);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.RunAsync(cancellation.Token));
        await AssertExpiredStateAsync(seeded, retained: true);
    }

    [Fact]
    public async Task BatchBoundLeavesTheAncestorAndSessionUntilTheNextCommittedCycle()
    {
        var seeded = await SeedAsync(includeRetained: false);
        var policy = Policy();
        policy.BatchSize = policy.MaxBatchesPerCycle = 1;
        using (var first = fixture.Factory.Services.CreateScope())
        {
            var result = await Operation(first.ServiceProvider.GetRequiredService<ApplicationDbContext>(), policy).RunAsync(CancellationToken.None);
            Assert.Equal(new RefreshTokenCleanupResult(1, 0), result);
        }
        using (var verification = fixture.Factory.Services.CreateScope())
        {
            var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await db.Set<RefreshToken>().AnyAsync(row => row.Id == seeded.ExpiredRoot.Id));
            Assert.False(await db.Set<RefreshToken>().AnyAsync(row => row.Id == seeded.ExpiredLeaf.Id));
            Assert.True(await db.Set<UserSession>().AnyAsync(row => row.Id == seeded.ExpiredSession.Id));
        }
        using (var second = fixture.Factory.Services.CreateScope())
        {
            var result = await Operation(second.ServiceProvider.GetRequiredService<ApplicationDbContext>(), policy).RunAsync(CancellationToken.None);
            Assert.Equal(new RefreshTokenCleanupResult(1, 1), result);
        }
        await AssertExpiredStateAsync(seeded, retained: false);
    }

    [Fact]
    public async Task RegisteredHostedWorkerActuallySchedulesMigratedPostgreSqlCleanup()
    {
        var seeded = await SeedAsync(includeRetained: false);
        await using var scheduled = fixture.CreateFactory(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:RefreshTokenCleanup:Enabled"] = "true",
                ["Authentication:RefreshTokenCleanup:InitialDelay"] = "00:00:00",
                ["Authentication:RefreshTokenCleanup:Interval"] = "00:00:01",
                ["Authentication:RefreshTokenCleanup:RetentionDays"] = "3650",
                ["Authentication:RefreshTokenCleanup:BatchSize"] = "1",
                ["Authentication:RefreshTokenCleanup:MaxBatchesPerCycle"] = "1"
            })));
        using var client = scheduled.CreateClient();
        Assert.Contains(scheduled.Services.GetServices<IHostedService>(), worker => worker is RefreshTokenCleanupWorker);
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(15))
        {
            using var verification = fixture.Factory.Services.CreateScope();
            var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!await db.Set<UserSession>().AnyAsync(row => row.Id == seeded.ExpiredSession.Id))
            {
                await AssertExpiredStateAsync(seeded, retained: false);
                return;
            }
            await Task.Delay(100);
        }
        Assert.Fail("The registered cleanup worker did not complete the migrated PostgreSQL chain within its configured cycles.");
    }

    private static RefreshTokenCleanupOptions Policy() => new() { RetentionDays = 3650, BatchSize = 1000, MaxBatchesPerCycle = 10 };

    private static RefreshTokenCleanupOperation Operation(ApplicationDbContext db, RefreshTokenCleanupOptions? policy = null) => new(
        db, new RefreshTokenRepository(db), new UserSessionRepository(db), Options.Create(policy ?? Policy()), TimeProvider.System);

    private async Task<SeededChain> SeedAsync(bool includeRetained)
    {
        var owner = Guid.NewGuid();
        var old = DateTime.UnixEpoch.AddDays(10);
        var root = Token(owner, old);
        var leaf = Token(owner, old);
        var expiredSession = Session(owner, leaf.Token, old);
        expiredSession.IsActive = false;
        expiredSession.TerminatedAt = old;
        root.IsRevoked = true;
        root.RevokedAt = old;
        root.SessionId = leaf.SessionId = expiredSession.Id;
        leaf.ParentTokenId = root.Id;
        var retained = new List<RefreshToken>();
        UserSession? liveSession = null;
        if (includeRetained)
        {
            var active = Token(owner, DateTime.UtcNow.AddDays(7));
            liveSession = Session(owner, active.Token, DateTime.UtcNow.AddHours(1));
            var predecessor = Token(owner, old);
            predecessor.IsRevoked = true;
            predecessor.RevokedAt = old;
            active.ParentTokenId = predecessor.Id;
            var unexpiredRevoked = Token(owner, DateTime.UtcNow.AddDays(7));
            unexpiredRevoked.IsRevoked = true;
            unexpiredRevoked.RevokedAt = old;
            var recentRevoked = Token(owner, old);
            recentRevoked.IsRevoked = true;
            recentRevoked.RevokedAt = DateTime.UtcNow.AddDays(-1);
            retained.AddRange([predecessor, active, unexpiredRevoked, recentRevoked]);
            foreach (var token in retained) { token.SessionId = liveSession.Id; }
        }
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<UserSession>().Add(expiredSession);
        if (liveSession is not null) { db.Set<UserSession>().Add(liveSession); }
        db.Set<RefreshToken>().AddRange([root, leaf, .. retained]);
        await db.SaveChangesAsync();
        var seeded = new SeededChain(root, leaf, expiredSession, liveSession, retained);
        seededChains.Add(seeded);
        return seeded;
    }

    private async Task AssertExpiredStateAsync(SeededChain seeded, bool retained)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(retained, await db.Set<RefreshToken>().AnyAsync(row => row.Id == seeded.ExpiredRoot.Id));
        Assert.Equal(retained, await db.Set<RefreshToken>().AnyAsync(row => row.Id == seeded.ExpiredLeaf.Id));
        Assert.Equal(retained, await db.Set<UserSession>().AnyAsync(row => row.Id == seeded.ExpiredSession.Id));
    }

    private static RefreshToken Token(Guid owner, DateTime expiry) => new()
    {
        Id = Guid.NewGuid(), UserId = owner, Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
        CreatedAt = DateTime.UnixEpoch, UpdatedAt = DateTime.UtcNow, ExpiresAt = expiry
    };

    private static UserSession Session(Guid owner, string hash, DateTime expiry) => new()
    {
        Id = Guid.NewGuid(), UserId = owner, RefreshToken = hash, CreatedAt = DateTime.UnixEpoch,
        UpdatedAt = DateTime.UtcNow, LastUsedAt = DateTime.UtcNow, ExpiresAt = expiry, IsActive = true
    };

    private sealed record SeededChain(RefreshToken ExpiredRoot, RefreshToken ExpiredLeaf, UserSession ExpiredSession,
        UserSession? LiveSession, List<RefreshToken> RetainedTokens)
    {
        public IEnumerable<RefreshToken> AllTokens => new[] { ExpiredRoot, ExpiredLeaf }.Concat(RetainedTokens);
    }

    private sealed class FailingCleanupAuditContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ChangeTracker.Entries<AuditLog>().Any(entry => entry.State == EntityState.Added && entry.Entity.ActionType == "Authentication.RefreshTokenCleanup"))
            {
                throw new IOException("Synthetic required cleanup audit persistence failure");
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class CancelAfterDeletionStore(IRefreshTokenCleanupRepository inner, CancellationTokenSource cancellation) : IRefreshTokenCleanupRepository
    {
        public async Task<int> DeleteExpiredAndRevokedBatchAsync(DateTime cutoffUtc, int batchSize, CancellationToken cancellationToken)
        {
            var result = await inner.DeleteExpiredAndRevokedBatchAsync(cutoffUtc, batchSize, cancellationToken);
            cancellation.Cancel();
            return result;
        }
    }

    private static MeterListener RowListener(Action<long> record)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == RefreshTokenLifecycleMetrics.MeterName && instrument.Name == "authentication.refresh_token.cleanup_rows")
            {
                current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => record(value));
        listener.Start();
        return listener;
    }
}
