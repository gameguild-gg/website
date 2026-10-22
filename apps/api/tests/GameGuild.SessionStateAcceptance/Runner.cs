using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GameGuild.SessionStateAcceptance;

// Hosted-only acceptance entry point. Uses the API's actual JWT handler, route,
// Kestrel transport and migrated PostgreSQL; no auth/session collaborators replaced.
internal static class Runner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<int> Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" || args.Length != 2)
        {
            throw new InvalidOperationException("Run this acceptance harness only in its hosted workflow, with repository and evidence paths.");
        }
        var repository = Path.GetFullPath(args[0]);
        var evidence = Path.GetFullPath(args[1]);
        var apiContentRoot = Path.Combine(repository, "apps", "api", "Source", "GameGuild.API");
        Assert.True(File.Exists(Path.Combine(apiContentRoot, "GameGuild.API.csproj")));
        Directory.CreateDirectory(evidence);
        var fixture = new ApiPostgreSqlFixture();
        try
        {
            await fixture.InitializeAsync();
            // The standalone console has no test-runner content-root manifest. Bind the
            // existing real-authentication fixture to the API's actual source directory.
            using var factory = fixture.RealAuthenticationFactory.WithWebHostBuilder(
                builder => builder.UseContentRoot(apiContentRoot));
            factory.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
            Assert.Equal(apiContentRoot, factory.Services.GetRequiredService<IHostEnvironment>().ContentRootPath);
            var account = await SeedAsync(factory.Services);
            var server = factory.Services.GetRequiredService<IServer>();
            Assert.StartsWith("Microsoft.AspNetCore.Server.Kestrel", server.GetType().Namespace!);
            var address = new Uri(Assert.Single(server.Features.Get<IServerAddressesFeature>()!.Addresses));
            Assert.True(address.IsLoopback && address.Scheme == "http" && address.Port > 0);
            string accessToken;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var schemes = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();
                Assert.Equal(typeof(JwtBearerHandler), (await schemes.GetSchemeAsync("Bearer"))!.HandlerType);
                Assert.Equal("Bearer", (await schemes.GetDefaultAuthenticateSchemeAsync())!.Name);
                accessToken = await scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
                    .GenerateAccessTokenAsync(account.UserId, account.Email, [], account.TenantId,
                        account.TokenVersion, DateTimeOffset.UtcNow, account.SessionId);
            }
            var validTo = new JwtSecurityTokenHandler().ReadJwtToken(accessToken).ValidTo;
            var input = new
            {
                apiUrl = address.GetLeftPart(UriPartial.Authority),
                account,
                accessToken,
                accessTokenExpires = new DateTimeOffset(validTo).ToUnixTimeMilliseconds(),
                authSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            };
            using var child = new Process
            {
                StartInfo = new ProcessStartInfo("node")
                {
                    WorkingDirectory = Path.Combine(repository, "apps", "web"),
                    UseShellExecute = false,
                    RedirectStandardInput = true
                }
            };
            child.StartInfo.ArgumentList.Add("--max-old-space-size=128");
            child.StartInfo.ArgumentList.Add("scripts/session-state-native-browser-e2e.mjs");
            child.StartInfo.ArgumentList.Add(evidence);
            if (!child.Start())
            {
                throw new InvalidOperationException("Browser acceptance runner failed to start.");
            }
            try
            {
                // Synthetic credentials stay in an input pipe; never command arguments or evidence.
                await child.StandardInput.WriteAsync(JsonSerializer.Serialize(input, JsonOptions));
                child.StandardInput.Close();
                using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(15));
                await child.WaitForExitAsync(deadline.Token);
                Assert.Equal(0, child.ExitCode);
            }
            finally
            {
                if (!child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                    await child.WaitForExitAsync();
                }
            }
            using var browser = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(evidence, "browser.json")));
            Assert.True(browser.RootElement.GetProperty("passed").GetBoolean());
            Assert.Equal(account.SessionId.ToString(), browser.RootElement.GetProperty("sessionId").GetString());
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var rows = await db.Set<UserSession>().AsNoTracking()
                    .Where(row => row.UserId == account.UserId || row.UserId == account.ForeignUserId).ToListAsync();
                Assert.Equal(3, rows.Count);
                Assert.Equal(account.Fingerprint, Assert.Single(rows, row => row.Id == account.SessionId).DeviceFingerprint);
                var trusted = await db.Set<TrustedDevice>().AsNoTracking()
                    .Where(row => row.UserId == account.UserId || row.UserId == account.ForeignUserId).ToListAsync();
                Assert.Equal(2, trusted.Count);
                var ownTrust = Assert.Single(trusted, row => row.UserId == account.UserId);
                Assert.Equal(account.TrustedDeviceId, ownTrust.Id);
                Assert.Equal(account.Fingerprint, ownTrust.DeviceFingerprint);
                Assert.Equal(string.Empty, ownTrust.DeviceName);
                Assert.False(ownTrust.IsActive);
                Assert.True(ownTrust.ExpiresAt > DateTime.UtcNow);
                var foreignTrust = Assert.Single(trusted, row => row.UserId == account.ForeignUserId);
                Assert.Equal(account.ForeignTrustedDeviceId, foreignTrust.Id);
                Assert.Equal(account.Fingerprint, foreignTrust.DeviceFingerprint);
                Assert.True(foreignTrust.IsActive);
                Assert.Equal("Foreign browser", foreignTrust.DeviceName);
                Assert.Equal(account.LastUsedAt, foreignTrust.UpdatedAt);
                Assert.Equal(account.LastUsedAt, foreignTrust.LastUsedAt);
                Assert.Equal(account.ExpiresAt, foreignTrust.ExpiresAt);
                await File.WriteAllTextAsync(Path.Combine(evidence, "trust-persistence.json"),
                    JsonSerializer.Serialize(new { passed = true, account.SessionId, account.TrustedDeviceId,
                        account.ForeignTrustedDeviceId, currentSessionFingerprint = account.Fingerprint,
                        renewedSameIdentity = true, ownTrustRevoked = true, foreignTrustUnchanged = true,
                        database = "Fresh PostgreSQL context after actual browser server actions" }, JsonOptions));
                var revoked = Assert.Single(rows, row => row.Id == account.SessionId);
                Assert.False(revoked.IsActive);
                Assert.NotNull(revoked.TerminatedAt);
                Assert.False(string.IsNullOrWhiteSpace(revoked.TerminationReason));
                foreach (var id in new[] { account.OtherSessionId, account.ForeignSessionId })
                {
                    var unchanged = Assert.Single(rows, row => row.Id == id);
                    Assert.True(unchanged.IsActive);
                    Assert.Null(unchanged.TerminatedAt);
                    Assert.Equal(account.LastUsedAt, unchanged.LastUsedAt);
                    Assert.Equal(account.LastUsedAt, unchanged.UpdatedAt);
                    Assert.Equal(account.ExpiresAt, unchanged.ExpiresAt);
                }
            }
            await File.WriteAllTextAsync(Path.Combine(evidence, "persistence.json"),
                JsonSerializer.Serialize(new { passed = true, account.UserId, account.SessionId,
                    ownSessionTerminated = true, unrelatedSessionsUnchanged = 2,
                    transport = "Actual Kestrel HTTP", database = "Migrated PostgreSQL" }, JsonOptions));
            Console.WriteLine("Session-state browser/API/PostgreSQL acceptance passed.");
            return 0;
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    private static async Task<Account> SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marker = Guid.NewGuid().ToString("N");
        var owner = User.Create(marker + "@example.test", "Synthetic session-state browser account");
        owner.Username = marker;
        var foreign = User.Create("foreign-" + marker + "@example.test", "Synthetic unrelated account");
        foreign.Username = "foreign-" + marker;
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Session-state browser tenant",
            Slug = "browser-" + marker, AdminEmail = owner.Email, IsActive = true };
        db.AddRange(owner, foreign, tenant, new TenantMember { Id = Guid.NewGuid(), UserId = owner.Id,
            TenantId = tenant.Id, Role = "Member", IsActive = true },
            new TenantMember { Id = Guid.NewGuid(), UserId = foreign.Id,
                TenantId = tenant.Id, Role = "Member", IsActive = true });
        var now = new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);
        var fingerprint = Guid.NewGuid().ToString("N");
        UserSession Session(Guid userId) => new()
        {
            Id = Guid.NewGuid(), UserId = userId, IsActive = true,
            RefreshToken = new RefreshTokenHasher().HashToken(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))),
            IpAddress = "127.0.0.1", UserAgent = "Hosted session-state acceptance",
            CreatedAt = now.AddHours(-1), UpdatedAt = now.AddMinutes(-5),
            LastUsedAt = now.AddMinutes(-5), ExpiresAt = now.AddHours(6)
        };
        var current = Session(owner.Id);
        var other = Session(owner.Id);
        var foreignSession = Session(foreign.Id);
        current.DeviceFingerprint = fingerprint;
        current.IsTrustedDevice = true; // Deliberately stale legacy flag: the real trust record is expired.
        current.DeviceInfo = JsonSerializer.Serialize(new DeviceInfo { DeviceName = "Current browser", Fingerprint = fingerprint });
        other.DeviceFingerprint = Guid.NewGuid().ToString("N");
        other.DeviceInfo = JsonSerializer.Serialize(new DeviceInfo { DeviceName = "Other browser", Fingerprint = other.DeviceFingerprint });
        foreignSession.DeviceFingerprint = fingerprint;
        var ownTrust = new TrustedDevice
        {
            Id = Guid.NewGuid(), UserId = owner.Id, DeviceFingerprint = fingerprint, DeviceName = "Expired browser",
            IsActive = true, DeviceInfo = "{}", CreatedAt = current.LastUsedAt, UpdatedAt = current.LastUsedAt,
            TrustedAt = current.LastUsedAt, LastUsedAt = current.LastUsedAt, ExpiresAt = now.AddMinutes(-1)
        };
        var foreignTrust = new TrustedDevice
        {
            Id = Guid.NewGuid(), UserId = foreign.Id, DeviceFingerprint = fingerprint, DeviceName = "Foreign browser",
            IsActive = true, DeviceInfo = "{}", CreatedAt = current.LastUsedAt, UpdatedAt = current.LastUsedAt,
            TrustedAt = current.LastUsedAt, LastUsedAt = current.LastUsedAt, ExpiresAt = current.ExpiresAt
        };
        db.AddRange(current, other, foreignSession, ownTrust, foreignTrust);
        await db.SaveChangesAsync();
        return new(owner.Id, owner.Email, tenant.Id, owner.TokenVersion, current.Id, other.Id,
            foreign.Id, foreignSession.Id, current.ExpiresAt, current.LastUsedAt, fingerprint, ownTrust.Id, foreignTrust.Id);
    }

    private sealed record Account(Guid UserId, string Email, Guid TenantId, int TokenVersion,
        Guid SessionId, Guid OtherSessionId, Guid ForeignUserId, Guid ForeignSessionId,
        DateTime ExpiresAt, DateTime LastUsedAt, string Fingerprint, Guid TrustedDeviceId, Guid ForeignTrustedDeviceId);
}
