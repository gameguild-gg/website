using System.IdentityModel.Tokens.Jwt;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class RefreshTokenLifecyclePostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private static readonly string RefreshEndpoint = "/" + typeof(AuthController).GetMethod(nameof(AuthController.RefreshToken))!
        .GetCustomAttribute<HttpPostAttribute>()!.Template!.Replace("v{version:apiVersion}", "v1", StringComparison.Ordinal);
    private static readonly string SessionsEndpoint = "/" + typeof(SessionController).GetMethod(nameof(SessionController.GetSessions))!
        .GetCustomAttribute<HttpGetAttribute>()!.Template!.Replace("v{version:apiVersion}", "v1", StringComparison.Ordinal);

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    public async Task ActualSequentialRotationPreservesSessionAuthTimeHashedReplacementAndConfiguredExpiry(int days)
    {
        // Exercise each configured TTL below an explicit session ceiling. The
        // expiration suite separately verifies shorter absolute session limits.
        const int absoluteTimeoutMinutes = 60 * 24 * 60;
        using var factory = CreateFactory(days, absoluteTimeoutMinutes: absoluteTimeoutMinutes);
        var account = await SeedAsync(factory, days);
        using var client = factory.CreateClient();
        var current = account.RawRefreshToken;
        var precedingId = account.Token.Id;
        string? latestAccess = null;
        for (var rotation = 0; rotation < 2; rotation++)
        {
            var before = DateTime.UtcNow;
            using var response = await client.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = current, account.TenantId });
            var after = DateTime.UtcNow;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var result = payload.RootElement;
            Assert.True(result.GetProperty("success").GetBoolean());
            Assert.Equal(account.User.Id, result.GetProperty("userId").GetGuid());
            Assert.Equal(account.Session.Id, result.GetProperty("sessionId").GetGuid());
            var replacement = result.GetProperty("refreshToken").GetString()!;
            Assert.NotEqual(current, replacement);
            Assert.True(Convert.FromBase64String(replacement).Length >= 64);
            latestAccess = result.GetProperty("accessToken").GetString()!;
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(latestAccess);
            Assert.Equal("HS256", jwt.Header.Alg);
            Assert.Equal(account.User.Id.ToString(), jwt.Subject);
            Assert.Equal(new DateTimeOffset(account.Token.CreatedAt).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                jwt.Claims.Single(value => value.Type == "auth_time").Value);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hash = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().HashToken(replacement);
            var predecessor = await db.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == precedingId);
            var successor = await db.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Token == hash);
            Assert.True(predecessor.IsRevoked);
            Assert.Equal(hash, predecessor.ReplacedByToken);
            Assert.False(successor.IsRevoked);
            Assert.Equal(account.User.Id, successor.UserId);
            Assert.Equal(precedingId, successor.ParentTokenId);
            Assert.Equal(account.Session.Id, predecessor.SessionId);
            Assert.Equal(account.Session.Id, successor.SessionId);
            Assert.Equal(account.Token.CreatedAt, successor.CreatedAt);
            // Rotation renews the credential's originating lifetime (remember-me semantics), so
            // each renewal lands one full window after the rotation; the second rotation also
            // carries the sub-second elapsed-since-issuance observed between seed and rotation,
            // hence the ±5s tolerance instead of the previous ±1s.
            Assert.InRange(successor.ExpiresAt, before.AddDays(days).AddSeconds(-5), after.AddDays(days).AddSeconds(5));
            Assert.True(successor.ExpiresAt <= account.Session.CreatedAt.AddMinutes(absoluteTimeoutMinutes));
            Assert.InRange(result.GetProperty("refreshTokenExpiresAt").GetDateTime(), before.AddDays(days).AddSeconds(-5), after.AddDays(days).AddSeconds(5));
            Assert.DoesNotContain(replacement, (await db.Set<RefreshToken>().Where(value => value.UserId == account.User.Id).Select(value => value.Token).ToListAsync()));
            var storedSession = await db.Set<UserSession>().AsNoTracking().SingleAsync(value => value.Id == account.Session.Id);
            Assert.True(storedSession.IsActive);
            Assert.Equal(hash, storedSession.RefreshToken);
            Assert.Equal(1, await db.Set<UserSession>().CountAsync(value => value.UserId == account.User.Id));
            Assert.Equal(rotation + 2, await db.Set<RefreshToken>().CountAsync(value => value.UserId == account.User.Id));
            current = replacement;
            precedingId = successor.Id;
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, latestAccess);
        using var accepted = await client.GetAsync(SessionsEndpoint);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using (var anonymous = factory.CreateClient())
        using (var replay = await anonymous.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = account.RawRefreshToken, account.TenantId }))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        }
        using var rejected = await client.GetAsync(SessionsEndpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        using var verification = factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.All(await context.Set<RefreshToken>().Where(value => value.UserId == account.User.Id).ToListAsync(), value => Assert.True(value.IsRevoked));
        Assert.False((await context.Set<UserSession>().AsNoTracking().SingleAsync(value => value.Id == account.Session.Id)).IsActive);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(30)]
    public async Task RotationRenewsShortLivedSeededRowsAtTheConfiguredLifetime(int days)
    {
        // Regression guard (develop breakage 2026-10-10, PR #758 follow-up): rotation must
        // mint the replacement with the configured lifetime and must never copy the stored
        // seeded window (8 hours here) into the new token or the reported deadline.
        const int absoluteTimeoutMinutes = 60 * 24 * 60;
        using var factory = CreateFactory(days, absoluteTimeoutMinutes: absoluteTimeoutMinutes);
        var account = await SeedAsync(factory);
        using var client = factory.CreateClient();
        var before = DateTime.UtcNow;
        using var response = await client.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = account.RawRefreshToken, account.TenantId });
        var after = DateTime.UtcNow;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var replacement = payload.RootElement.GetProperty("refreshToken").GetString()!;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hash = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().HashToken(replacement);
        var successor = await db.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Token == hash);
        Assert.InRange(successor.ExpiresAt, before.AddDays(days).AddSeconds(-1), after.AddDays(days).AddSeconds(1));
        Assert.InRange(payload.RootElement.GetProperty("refreshTokenExpiresAt").GetDateTime(), before.AddDays(days).AddSeconds(-1), after.AddDays(days).AddSeconds(1));
        // The seeded 8-hour window must not leak into the replacement lifetime.
        Assert.True(successor.ExpiresAt - after > TimeSpan.FromHours(12), $"replacement renewed at the seeded short window instead of the configured {days} day(s)");
    }

    [Fact]
    public async Task UnjoinedTenantRequestDeniesBeforeIssuanceAndPreservesOldTokenAndSession()
    {
        using var factory = CreateFactory(7);
        var account = await SeedAsync(factory);
        var unjoined = await SeedAsync(factory);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = account.RawRefreshToken, tenantId = unjoined.TenantId });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertUnchangedAsync(factory, account);
    }

    [Theory]
    [InlineData("refresh")]
    [InlineData("bearer")]
    public async Task DeletedUserCannotUsePreviouslyIssuedToken(string kind)
    {
        using var factory = CreateFactory(7);
        var account = await SeedAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var access = await scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessTokenAsync(
            account.User.Id, account.User.Email, ["Member"], account.TenantId, account.User.TokenVersion, account.Session.Id);
        (await db.Set<User>().SingleAsync(value => value.Id == account.User.Id)).SoftDelete();
        await db.SaveChangesAsync();
        using var client = factory.CreateClient();
        if (kind == "bearer")
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, access);
        }
        using var response = kind == "refresh"
            ? await client.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = account.RawRefreshToken, account.TenantId })
            : await client.GetAsync(SessionsEndpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertUnchangedAsync(factory, account);
    }

    [Theory]
    [InlineData(RefreshTokenReplayScope.Family)]
    [InlineData(RefreshTokenReplayScope.Account)]
    public async Task TwoActualRequestsThatReadSameActiveTokenProduceOneRotationAndCommittedContainment(RefreshTokenReplayScope policy)
    {
        var rendezvous = new RefreshReadRendezvous();
        using var factory = CreateFactory(7, rendezvous, replayScope: policy);
        var account = await SeedAsync(factory);
        var unrelated = await SeedAsync(factory);
        rendezvous.TokenHash = account.Token.Token;
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        var responses = await Task.WhenAll(
            first.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = account.RawRefreshToken, account.TenantId }),
            second.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = account.RawRefreshToken, account.TenantId }));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        Assert.Equal(2, rendezvous.Arrivals);
        Assert.Single(responses, value => value.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, value => value.StatusCode == HttpStatusCode.Unauthorized);
        using var winner = JsonDocument.Parse(await responses.Single(value => value.StatusCode == HttpStatusCode.OK).Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tokens = await db.Set<RefreshToken>().AsNoTracking().Where(value => value.UserId == account.User.Id).ToListAsync();
        Assert.All(tokens, value => Assert.True(value.IsRevoked));
        var root = Assert.Single(tokens, value => value.Id == account.Token.Id);
        Assert.NotNull(root.ReplacedByToken);
        var successor = Assert.Single(tokens, value => value.Token == root.ReplacedByToken);
        Assert.Equal(root.Id, successor.ParentTokenId);
        Assert.Equal(account.Session.Id, root.SessionId);
        Assert.Equal(account.Session.Id, successor.SessionId);
        Assert.Single(tokens, value => value.ParentTokenId == root.Id);
        Assert.All(await db.Set<UserSession>().AsNoTracking().Where(value => value.UserId == account.User.Id).ToListAsync(),
            value => Assert.False(value.IsActive));
        var user = await db.Set<User>().AsNoTracking().SingleAsync(value => value.Id == account.User.Id);
        Assert.Equal(account.User.TokenVersion + (policy == RefreshTokenReplayScope.Account ? 1 : 0), user.TokenVersion);
        using var bearer = factory.CreateClient();
        bearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme,
            winner.RootElement.GetProperty("accessToken").GetString());
        using var denied = await bearer.GetAsync(SessionsEndpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var unrelatedAccess = await scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessTokenAsync(
            unrelated.User.Id, unrelated.User.Email, ["Member"], unrelated.TenantId, unrelated.User.TokenVersion, unrelated.Session.Id);
        bearer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, unrelatedAccess);
        using var allowed = await bearer.GetAsync(SessionsEndpoint);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        await AssertUnchangedAsync(factory, unrelated);
    }

    private WebApplicationFactory<Program> CreateFactory(int days, RefreshReadRendezvous? rendezvous = null, int? absoluteTimeoutMinutes = null,
        RefreshTokenReplayScope replayScope = RefreshTokenReplayScope.Family) => fixture.CreateFactory(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            });
            services.PostConfigure<JwtOptions>(options => options.RefreshTokenExpirationDays = days);
            services.PostConfigure<JwtOptions>(options => options.RefreshTokenReplayContainmentScope = replayScope);
            if (absoluteTimeoutMinutes.HasValue)
            {
                // SessionManagementService consumes the validated singleton, not IOptions<SessionOptions>.
                services.AddSingleton(new SessionOptions { AbsoluteTimeoutMinutes = absoluteTimeoutMinutes.Value });
            }
            if (rendezvous is not null)
            {
                services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(rendezvous));
            }
        }));

    private static async Task<Account> SeedAsync(WebApplicationFactory<Program> factory, int? days = null)
    {
        var marker = Guid.NewGuid().ToString("N");
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).UtcDateTime;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = User.Create($"lifecycle-{marker}@example.test", "Synthetic lifecycle account");
        user.Username = $"lifecycle-{marker}";
        var tenant = Guid.NewGuid();
        // Rotation renews the credential's originating lifetime, so a rotation test that asserts
        // the configured window must seed a credential that already carries that window; the
        // default seed keeps the legacy short-lived (-2h..+6h) credential for containment tests.
        var seededCreatedAt = days is null ? now.AddHours(-2) : now;
        var seededExpiresAt = days is null ? now.AddHours(6) : now.AddDays(days.Value);
        var token = new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, Token = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().HashToken(raw),
            CreatedAt = seededCreatedAt, UpdatedAt = now, ExpiresAt = seededExpiresAt, CreatedByIp = "127.0.0.1" };
        var session = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, RefreshToken = token.Token, IpAddress = "127.0.0.1",
            CreatedAt = seededCreatedAt, UpdatedAt = now, LastUsedAt = now, ExpiresAt = seededExpiresAt, IsActive = true };
        db.Set<User>().Add(user);
        db.Set<Tenant>().Add(new Tenant { Id = tenant, Name = $"Lifecycle {marker}", Slug = $"lifecycle-{marker}", AdminEmail = $"admin-{marker}@example.test", IsActive = true });
        db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), TenantId = tenant, UserId = user.Id, Role = "Member", IsActive = true });
        db.Set<RefreshToken>().Add(token);
        db.Set<UserSession>().Add(session);
        await db.SaveChangesAsync();
        return new Account(user, tenant, token, session, raw);
    }

    private static async Task AssertUnchangedAsync(WebApplicationFactory<Program> factory, Account account)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var token = await db.Set<RefreshToken>().AsNoTracking().SingleAsync(value => value.Id == account.Token.Id);
        Assert.False(token.IsRevoked);
        Assert.Null(token.ReplacedByToken);
        Assert.Equal(1, await db.Set<RefreshToken>().CountAsync(value => value.UserId == account.User.Id));
        var session = await db.Set<UserSession>().AsNoTracking().SingleAsync(value => value.Id == account.Session.Id);
        Assert.True(session.IsActive);
        Assert.Equal(account.Token.Token, session.RefreshToken);
        Assert.Equal(account.Session.ExpiresAt, session.ExpiresAt);
    }

    private sealed record Account(User User, Guid TenantId, RefreshToken Token, UserSession Session, string RawRefreshToken);

    private sealed class RefreshReadRendezvous : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _bothRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public string? TokenHash { get; set; }
        public int Arrivals => Volatile.Read(ref _arrivals);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            // Only delay the two real SELECT results; never substitute storage, claims or rotation.
            if (eventData.CommandSource == CommandSource.LinqQuery && TokenHash is not null
                && command.CommandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains(".refreshtoken AS ", StringComparison.Ordinal)
                && command.Parameters.Cast<DbParameter>().Any(value => Equals(value.Value, TokenHash)))
            {
                var arrival = Interlocked.Increment(ref _arrivals);
                if (arrival == 2)
                {
                    _bothRead.TrySetResult();
                }
                if (arrival <= 2)
                {
                    await _bothRead.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                }
            }
            return result;
        }
    }
}
