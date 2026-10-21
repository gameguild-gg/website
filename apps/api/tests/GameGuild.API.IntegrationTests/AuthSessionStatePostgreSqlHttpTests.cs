using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

// Issue #221: real bearer validation, API composition and PostgreSQL.
// No authentication, query handler, session repository or MFA repository replacements.
[Collection(ApiPostgreSqlCollection.Name)]
public sealed class AuthSessionStatePostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private const string Route = "/v1/auth/session-state";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualStateReadsOnlyTheSignedOwnersPersistedSessionAndMfaEnrollment(bool enrolled)
    {
        var account = await SeedAsync(enrolled);
        using var client = await CreateClientAsync(account, account.SessionId);
        using var response = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<AuthSessionStateDto>();
        Assert.NotNull(state);
        Assert.Equal(account.UserId, state.UserId);
        Assert.Equal(account.SessionId, state.SessionId);
        Assert.True(state.SessionValid);
        Assert.Equal(account.ExpiresAt, state.SessionExpiresAtUtc);
        Assert.Equal(account.LastUsedAt, state.SessionLastUsedAtUtc);
        Assert.Null(state.SessionTerminationReason);
        Assert.Equal(account.TokenVersion, state.TokenVersion);
        Assert.False(state.TokenRevoked);
        Assert.Equal(2, state.ActiveSessionCount);
        Assert.Equal(enrolled, state.MfaEnrolled);
        // Enrollment does not prove that this bearer completed an MFA challenge.
        Assert.False(state.MfaVerifiedInCurrentToken);

        await using var read = fixture.RealAuthenticationFactory.Services.CreateAsyncScope();
        var db = read.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var session = await db.Set<UserSession>().AsNoTracking().SingleAsync(row => row.Id == account.SessionId);
        Assert.Equal(account.LastUsedAt, session.LastUsedAt);
        Assert.Equal(account.UpdatedAt, session.UpdatedAt);
        Assert.Equal(account.ExpiresAt, session.ExpiresAt);
        Assert.True(session.IsActive);
        Assert.Equal(4, await db.Set<UserSession>().CountAsync(row => row.UserId == account.UserId));
    }

    [Fact]
    public async Task QueryParametersCannotSelectAnotherOwnersState()
    {
        var caller = await SeedAsync(false);
        var foreign = await SeedAsync(true);
        using var client = await CreateClientAsync(caller, caller.SessionId);
        using var response = await client.GetAsync(Route + "?userId=" + foreign.UserId + "&sessionId=" + foreign.SessionId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<AuthSessionStateDto>();
        Assert.NotNull(state);
        Assert.Equal(caller.UserId, state.UserId);
        Assert.Equal(caller.SessionId, state.SessionId);
        Assert.False(state.MfaEnrolled);
    }

    [Fact]
    public async Task ValidSessionlessBearerReportsNoBoundSession()
    {
        var account = await SeedAsync(false);
        using var client = await CreateClientAsync(account, null);
        using var response = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<AuthSessionStateDto>();
        Assert.NotNull(state);
        Assert.Equal(account.UserId, state.UserId);
        Assert.Null(state.SessionId);
        Assert.False(state.SessionValid);
        Assert.Null(state.SessionExpiresAtUtc);
        Assert.Null(state.SessionLastUsedAtUtc);
        Assert.Null(state.SessionTerminationReason);
        Assert.False(state.TokenRevoked);
        Assert.Equal(2, state.ActiveSessionCount);
    }

    [Fact]
    public async Task AnonymousCallerCannotReadAuthenticationState()
    {
        using var client = fixture.RealAuthenticationFactory.CreateClient();
        using var response = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("terminated")]
    [InlineData("foreign-owner")]
    [InlineData("missing")]
    public async Task InvalidBoundSessionsAreRejectedBeforeStateProjection(string boundary)
    {
        var account = await SeedAsync(false);
        var selectedSession = account.SessionId;
        if (boundary == "foreign-owner")
        {
            selectedSession = (await SeedAsync(true)).SessionId;
        }
        else if (boundary == "missing")
        {
            selectedSession = Guid.NewGuid();
        }
        else
        {
            await using var scope = fixture.RealAuthenticationFactory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.Set<UserSession>().SingleAsync(session => session.Id == selectedSession);
            if (boundary == "expired")
            {
                row.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            }
            else
            {
                row.IsActive = false;
                row.TerminatedAt = DateTime.UtcNow;
                row.TerminationReason = "Synthetic session-state acceptance";
            }
            await db.SaveChangesAsync();
        }
        using var client = await CreateClientAsync(account, selectedSession);
        using var response = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(selectedSession.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionExpiresAtUtc", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokenVersionRevokedAfterIssuanceCannotReadSessionState()
    {
        var account = await SeedAsync(false);
        using var client = await CreateClientAsync(account, account.SessionId);
        await using (var scope = fixture.RealAuthenticationFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = await db.Set<User>().SingleAsync(row => row.Id == account.UserId);
            owner.TokenVersion++;
            await db.SaveChangesAsync();
        }
        using var response = await client.GetAsync(Route);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpClient> CreateClientAsync(Account account, Guid? sessionId)
    {
        await using var scope = fixture.RealAuthenticationFactory.Services.CreateAsyncScope();
        var schemes = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();
        var bearer = await schemes.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme);
        Assert.NotNull(bearer);
        Assert.Equal(typeof(JwtBearerHandler), bearer.HandlerType);
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, (await schemes.GetDefaultAuthenticateSchemeAsync())!.Name);
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var token = sessionId.HasValue
            ? await jwt.GenerateAccessTokenAsync(account.UserId, account.Email, [], account.TenantId, account.TokenVersion,
                DateTimeOffset.UtcNow, sessionId.Value)
            : await jwt.GenerateAccessTokenAsync(account.UserId, account.Email, [], account.TenantId, account.TokenVersion,
                DateTimeOffset.UtcNow);
        var client = fixture.RealAuthenticationFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", account.TenantId.ToString());
        return client;
    }

    private async Task<Account> SeedAsync(bool enrolled)
    {
        await using var scope = fixture.RealAuthenticationFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marker = Guid.NewGuid().ToString("N");
        var owner = User.Create(marker + "@example.test", "Synthetic session-state account");
        owner.Username = marker;
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Session-state tenant " + marker, Slug = "state-" + marker,
            AdminEmail = marker + "@example.test", IsActive = true };
        var now = new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);
        db.AddRange(owner, tenant, new TenantMember { Id = Guid.NewGuid(), UserId = owner.Id, TenantId = tenant.Id,
            Role = "Member", IsActive = true });
        var sessions = Enumerable.Range(0, 4).Select(_ => new UserSession
        {
            Id = Guid.NewGuid(), UserId = owner.Id,
            RefreshToken = new RefreshTokenHasher().HashToken(Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))),
            IpAddress = "127.0.0.1", UserAgent = "Synthetic session-state acceptance",
            IsActive = true, CreatedAt = now.AddHours(-1), UpdatedAt = now.AddMinutes(-5),
            LastUsedAt = now.AddMinutes(-5), ExpiresAt = now.AddHours(6)
        }).ToArray();
        sessions[2].ExpiresAt = now.AddMinutes(-1);
        sessions[3].IsActive = false;
        sessions[3].TerminatedAt = now;
        sessions[3].TerminationReason = "Synthetic historical session";
        db.AddRange(sessions);
        if (enrolled)
        {
            db.Add(new UserMfaConfiguration
            {
                Id = Guid.NewGuid(), UserId = owner.Id, IsEnabled = true, IsSetupComplete = true,
                EnabledAt = now.AddDays(-1), CreatedAt = now.AddDays(-1), UpdatedAt = now.AddDays(-1)
            });
        }
        await db.SaveChangesAsync();
        return new(owner.Id, owner.Email, tenant.Id, owner.TokenVersion, sessions[0].Id,
            sessions[0].ExpiresAt, sessions[0].LastUsedAt, sessions[0].UpdatedAt);
    }

    private sealed record Account(Guid UserId, string Email, Guid TenantId, int TokenVersion, Guid SessionId,
        DateTime ExpiresAt, DateTime LastUsedAt, DateTime UpdatedAt);
}
