using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

// Issue278: real signed principals, public trust/session routes and migrated PostgreSQL.
// No authentication handler, command, trust service or repository replacements.
[Collection(ApiPostgreSqlCollection.Name)]
public sealed class TrustedDeviceSessionPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private const string TrustRoute = "/v1/auth/trusted-devices";
    private const string SessionsRoute = "/v1/auth/sessions";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TrustCurrentUsesThePersistedSignedSessionFingerprintDespiteConflictingTransportAndIdentity(bool named)
    {
        var account = await SeedAsync();
        using var client = await SignedClientAsync(account, account.CurrentSessionId);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DifferentTransport/9.0");
        client.DefaultRequestHeaders.Add("X-Device-Fingerprint", "untrusted-header-selection");
        object body = named
            ? new { deviceName = "Current browser", deviceFingerprint = "untrusted-body-selection",
                userId = account.ForeignUserId, sessionId = account.ForeignSessionId }
            : new { deviceFingerprint = "untrusted-body-selection", userId = account.ForeignUserId,
                sessionId = account.ForeignSessionId };
        using var response = await client.PostAsJsonAsync(
            TrustRoute + "?userId=" + account.ForeignUserId + "&sessionId=" + account.ForeignSessionId, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using (var read = fixture.RealAuthenticationFactory.Services.CreateAsyncScope())
        {
            var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rows = await db.Set<TrustedDevice>().AsNoTracking()
                .Where(row => row.UserId == account.UserId || row.UserId == account.ForeignUserId).ToListAsync();
            var trusted = Assert.Single(rows);
            Assert.Equal(account.UserId, trusted.UserId);
            Assert.Equal(account.Fingerprint, trusted.DeviceFingerprint);
            Assert.Equal(named ? "Current browser" : string.Empty, trusted.DeviceName);
            Assert.True(trusted.IsActive);
            Assert.True(trusted.ExpiresAt > DateTime.UtcNow);
        }
        var sessions = await ReadSessionsAsync(client);
        Assert.Equal(2, sessions.Count);
        Assert.True(Assert.Single(sessions, row => row.Id == account.CurrentSessionId).IsTrustedDevice);
        Assert.False(Assert.Single(sessions, row => row.Id == account.OtherSessionId).IsTrustedDevice);
        using var foreign = await SignedClientAsync(account, account.ForeignSessionId, foreign: true);
        Assert.False(Assert.Single(await ReadSessionsAsync(foreign)).IsTrustedDevice);
        await AssertSessionsUnchangedAsync(account);
    }

    [Theory]
    [InlineData("active", true)]
    [InlineData("expired", false)]
    [InlineData("revoked", false)]
    [InlineData("foreign-owner", false)]
    public async Task ListingProjectsCurrentOwnerTrustInsteadOfTheLegacySessionFlag(string boundary, bool expected)
    {
        // The deliberately opposite legacy flag cannot satisfy the required projection.
        var account = await SeedAsync(legacyTrusted: !expected);
        await SeedTrustAsync(account, foreign: boundary == "foreign-owner",
            active: boundary != "revoked", expired: boundary == "expired");
        using var client = await SignedClientAsync(account, account.CurrentSessionId);
        var sessions = await ReadSessionsAsync(client);
        Assert.Equal(2, sessions.Count);
        var current = Assert.Single(sessions, row => row.Id == account.CurrentSessionId);
        Assert.Equal(expected, current.IsTrustedDevice);
        Assert.True(current.IsCurrent);
        Assert.False(Assert.Single(sessions, row => row.Id == account.OtherSessionId).IsTrustedDevice);
        await AssertSessionsUnchangedAsync(account);
    }

    [Fact]
    public async Task RevokingTrustChangesTheProjectionWithoutRevokingSessionsOrAnotherOwnersSameFingerprint()
    {
        var account = await SeedAsync(legacyTrusted: true);
        var ownTrust = await SeedTrustAsync(account);
        var foreignTrust = await SeedTrustAsync(account, foreign: true);
        using var client = await SignedClientAsync(account, account.CurrentSessionId);
        Assert.True(Assert.Single(await ReadSessionsAsync(client),
            row => row.Id == account.CurrentSessionId).IsTrustedDevice);
        using var revoked = await client.DeleteAsync(TrustRoute + "/" + ownTrust);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.False(Assert.Single(await ReadSessionsAsync(client),
            row => row.Id == account.CurrentSessionId).IsTrustedDevice);
        using var state = await client.GetAsync("/v1/auth/session-state");
        Assert.Equal(HttpStatusCode.OK, state.StatusCode);
        using var foreign = await SignedClientAsync(account, account.ForeignSessionId, foreign: true);
        Assert.True(Assert.Single(await ReadSessionsAsync(foreign)).IsTrustedDevice);
        await using (var read = fixture.RealAuthenticationFactory.Services.CreateAsyncScope())
        {
            var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False((await db.Set<TrustedDevice>().AsNoTracking().SingleAsync(row => row.Id == ownTrust)).IsActive);
            var unrelated = await db.Set<TrustedDevice>().AsNoTracking().SingleAsync(row => row.Id == foreignTrust);
            Assert.True(unrelated.IsActive);
            Assert.Equal(account.Timestamp, unrelated.UpdatedAt);
        }
        await AssertSessionsUnchangedAsync(account);
    }

    [Fact]
    public async Task SessionlessBearerCannotTrustARequestDerivedDevice()
    {
        var account = await SeedAsync();
        using var client = await SignedClientAsync(account, null);
        using var response = await client.PostAsJsonAsync(TrustRoute, new { deviceName = "Unbound request" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoTrustAsync(account);
        await AssertSessionsUnchangedAsync(account);
    }

    [Fact]
    public async Task AnonymousCallerCannotCreateTrust()
    {
        var account = await SeedAsync();
        using var client = fixture.RealAuthenticationFactory.CreateClient();
        using var response = await client.PostAsJsonAsync(TrustRoute, new { deviceName = "Anonymous request" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoTrustAsync(account);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign-owner")]
    [InlineData("expired")]
    [InlineData("terminated")]
    public async Task InvalidSignedSessionBindingsCannotCreateTrust(string boundary)
    {
        var account = await SeedAsync();
        var selected = boundary switch
        {
            "missing" => Guid.NewGuid(),
            "foreign-owner" => account.ForeignSessionId,
            _ => account.CurrentSessionId
        };
        if (boundary is "expired" or "terminated")
        {
            await using var scope = fixture.RealAuthenticationFactory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var session = await db.Set<UserSession>().SingleAsync(row => row.Id == selected);
            if (boundary == "expired")
            {
                session.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            }
            else
            {
                session.IsActive = false;
                session.TerminatedAt = DateTime.UtcNow;
                session.TerminationReason = "Synthetic trust boundary";
            }
            await db.SaveChangesAsync();
        }
        using var client = await SignedClientAsync(account, selected);
        using var response = await client.PostAsJsonAsync(TrustRoute, new { deviceName = "Invalid binding" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoTrustAsync(account);
    }

    [Fact]
    public async Task BoundSessionWithoutFingerprintCannotInventTrustFromTransport()
    {
        var account = await SeedAsync(withoutFingerprint: true);
        using var client = await SignedClientAsync(account, account.CurrentSessionId);
        using var response = await client.PostAsJsonAsync(TrustRoute, new { deviceName = "No device identity" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoTrustAsync(account);
        await AssertSessionsUnchangedAsync(account);
    }

    [Fact]
    public async Task DisabledFingerprintingDeniesNewTrustAndDoesNotProjectPreviouslyStoredTrust()
    {
        var account = await SeedAsync(legacyTrusted: true);
        var trustedId = await SeedTrustAsync(account);
        using var factory = fixture.RealAuthenticationFactory.WithWebHostBuilder(
            builder => builder.UseSetting("Session:EnableDeviceFingerprinting", "false"));
        Assert.False(factory.Services.GetRequiredService<SessionOptions>().EnableDeviceFingerprinting);
        using var client = await SignedClientAsync(account, account.CurrentSessionId, factory: factory);
        using var response = await client.PostAsJsonAsync(TrustRoute, new { deviceName = "Disabled device" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(Assert.Single(await ReadSessionsAsync(client),
            row => row.Id == account.CurrentSessionId).IsTrustedDevice);
        await using (var read = factory.Services.CreateAsyncScope())
        {
            var rows = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<TrustedDevice>()
                .AsNoTracking().Where(row => row.UserId == account.UserId).ToListAsync();
            var unchanged = Assert.Single(rows);
            Assert.Equal(trustedId, unchanged.Id);
            Assert.Equal(account.Timestamp, unchanged.UpdatedAt);
        }
        await AssertSessionsUnchangedAsync(account);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TrustRenewsTheExistingExpiredOrRevokedSessionDeviceWithoutCreatingAnotherIdentity(bool expired)
    {
        var account = await SeedAsync();
        var trustedId = await SeedTrustAsync(account, active: expired, expired: expired);
        using var client = await SignedClientAsync(account, account.CurrentSessionId);
        using var response = await client.PostAsJsonAsync(TrustRoute, new { deviceName = "Renewed browser" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var read = fixture.RealAuthenticationFactory.Services.CreateAsyncScope();
        var rows = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<TrustedDevice>()
            .AsNoTracking().Where(row => row.UserId == account.UserId).ToListAsync();
        var renewed = Assert.Single(rows);
        Assert.Equal(trustedId, renewed.Id);
        Assert.Equal(account.Fingerprint, renewed.DeviceFingerprint);
        Assert.Equal("Renewed browser", renewed.DeviceName);
        Assert.True(renewed.IsActive);
        Assert.True(renewed.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task CallerCannotRevokeAnotherOwnersTrustEvenForTheSameFingerprint()
    {
        var account = await SeedAsync();
        await SeedTrustAsync(account);
        var foreignTrust = await SeedTrustAsync(account, foreign: true);
        using var client = await SignedClientAsync(account, account.CurrentSessionId);
        using var response = await client.DeleteAsync(TrustRoute + "/" + foreignTrust);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var read = fixture.RealAuthenticationFactory.Services.CreateAsyncScope();
        var rows = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<TrustedDevice>()
            .AsNoTracking().Where(row => row.UserId == account.UserId || row.UserId == account.ForeignUserId).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => { Assert.True(row.IsActive); Assert.Equal(account.Timestamp, row.UpdatedAt); });
    }

    private static async Task<List<SessionResponse>> ReadSessionsAsync(HttpClient client)
    {
        using var response = await client.GetAsync(SessionsRoute);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<List<SessionResponse>>(await response.Content.ReadFromJsonAsync<List<SessionResponse>>());
    }

    private async Task<HttpClient> SignedClientAsync(Account account, Guid? sessionId, bool foreign = false,
        WebApplicationFactory<Program>? factory = null)
    {
        factory ??= fixture.RealAuthenticationFactory;
        await using var scope = factory.Services.CreateAsyncScope();
        var schemes = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();
        Assert.Equal(typeof(JwtBearerHandler), (await schemes.GetSchemeAsync("Bearer"))!.HandlerType);
        Assert.Equal("Bearer", (await schemes.GetDefaultAuthenticateSchemeAsync())!.Name);
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var userId = foreign ? account.ForeignUserId : account.UserId;
        var email = foreign ? account.ForeignEmail : account.Email;
        var version = foreign ? account.ForeignTokenVersion : account.TokenVersion;
        var token = sessionId.HasValue
            ? await jwt.GenerateAccessTokenAsync(userId, email, [], account.TenantId, version,
                DateTimeOffset.UtcNow, sessionId.Value)
            : await jwt.GenerateAccessTokenAsync(userId, email, [], account.TenantId, version, DateTimeOffset.UtcNow);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", account.TenantId.ToString());
        return client;
    }

    private async Task<Account> SeedAsync(bool legacyTrusted = false, bool withoutFingerprint = false)
    {
        await using var scope = fixture.RealAuthenticationFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(scope.ServiceProvider.GetRequiredService<SessionOptions>().EnableDeviceFingerprinting);
        var marker = Guid.NewGuid().ToString("N");
        var owner = User.Create(marker + "@example.test", "Synthetic trust owner");
        owner.Username = marker;
        var foreign = User.Create("foreign-" + marker + "@example.test", "Synthetic other owner");
        foreign.Username = "foreign-" + marker;
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Device trust acceptance", Slug = "trust-" + marker,
            AdminEmail = owner.Email, IsActive = true };
        db.AddRange(owner, foreign, tenant);
        foreach (var user in new[] { owner, foreign })
        {
            db.Add(new TenantMember { Id = Guid.NewGuid(), UserId = user.Id, TenantId = tenant.Id, Role = "Member", IsActive = true });
        }
        var now = new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);
        var fingerprint = withoutFingerprint ? string.Empty : Guid.NewGuid().ToString("N");
        var metadata = JsonSerializer.Serialize(new DeviceInfo { DeviceName = "Known browser", Fingerprint = fingerprint });
        UserSession Session(Guid userId, string deviceFingerprint, bool trusted) => new()
        {
            Id = Guid.NewGuid(), UserId = userId, DeviceFingerprint = deviceFingerprint, DeviceInfo = metadata,
            RefreshToken = new RefreshTokenHasher().HashToken(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))),
            IpAddress = "203.0.113.10", UserAgent = "OriginalBrowser/1.0", IsActive = true, IsTrustedDevice = trusted,
            CreatedAt = now.AddMinutes(-10), LastUsedAt = now.AddMinutes(-5), UpdatedAt = now,
            ExpiresAt = now.AddHours(6)
        };
        var current = Session(owner.Id, fingerprint, legacyTrusted);
        var other = Session(owner.Id, Guid.NewGuid().ToString("N"), false);
        var foreignSession = Session(foreign.Id, fingerprint, false);
        db.AddRange(current, other, foreignSession);
        await db.SaveChangesAsync();
        return new(owner.Id, owner.Email, owner.TokenVersion, foreign.Id, foreign.Email, foreign.TokenVersion,
            tenant.Id, current.Id, other.Id, foreignSession.Id, fingerprint, now, metadata);
    }

    private async Task<Guid> SeedTrustAsync(Account account, bool foreign = false, bool active = true, bool expired = false)
    {
        await using var scope = fixture.RealAuthenticationFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = new TrustedDevice
        {
            Id = Guid.NewGuid(), UserId = foreign ? account.ForeignUserId : account.UserId,
            DeviceFingerprint = account.Fingerprint, DeviceName = "Previously trusted", DeviceInfo = "{}",
            CreatedAt = account.Timestamp, UpdatedAt = account.Timestamp, TrustedAt = account.Timestamp,
            LastUsedAt = account.Timestamp, IsActive = active,
            ExpiresAt = expired ? account.Timestamp.AddMinutes(-1) : account.Timestamp.AddDays(5)
        };
        db.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private async Task AssertNoTrustAsync(Account account)
    {
        await using var read = fixture.RealAuthenticationFactory.Services.CreateAsyncScope();
        Assert.False(await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<TrustedDevice>()
            .AnyAsync(row => row.UserId == account.UserId || row.UserId == account.ForeignUserId));
    }

    private async Task AssertSessionsUnchangedAsync(Account account)
    {
        await using var read = fixture.RealAuthenticationFactory.Services.CreateAsyncScope();
        var rows = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<UserSession>()
            .AsNoTracking().Where(row => row.UserId == account.UserId || row.UserId == account.ForeignUserId).ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.True(row.IsActive);
            Assert.Null(row.TerminatedAt);
            Assert.Equal(account.Timestamp, row.UpdatedAt);
            Assert.Equal(account.Timestamp.AddMinutes(-5), row.LastUsedAt);
            Assert.Equal(account.Timestamp.AddHours(6), row.ExpiresAt);
            Assert.Equal(account.Metadata, row.DeviceInfo);
        });
    }

    private sealed record Account(Guid UserId, string Email, int TokenVersion, Guid ForeignUserId,
        string ForeignEmail, int ForeignTokenVersion, Guid TenantId, Guid CurrentSessionId, Guid OtherSessionId,
        Guid ForeignSessionId, string Fingerprint, DateTime Timestamp, string Metadata);
}
