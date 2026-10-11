using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
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
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class RevokeAllUserTokensPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private const string Endpoint = "/v1/auth/sessions:terminate-all";
    private const string SessionsEndpoint = "/v1/auth/sessions";
    private const string RefreshEndpoint = "/v1/auth/tokens:refresh";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActualSelfLogoutRevokesAllTokensSessionsAndPriorBearerFormatsWithoutChangingAnotherUser(bool sameTenant)
    {
        using var factory = CreateFactory();
        var owner = await SeedAsync(factory);
        var other = await SeedAsync(factory, existingTenant: sameTenant ? owner.TenantId : null);
        var sessionBearer = await AccessAsync(factory, owner, owner.Sessions[0].Id);
        var versionBearer = await AccessAsync(factory, owner);
        var legacyBearer = LegacyBearer(factory, versionBearer);
        var otherBearer = await AccessAsync(factory, other, other.Sessions[0].Id);
        using var client = factory.CreateClient();
        foreach (var bearer in new[] { sessionBearer, versionBearer, legacyBearer, otherBearer })
        {
            await AssertBearerAsync(client, bearer, HttpStatusCode.OK);
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionBearer);
        using var response = await client.PostAsJsonAsync(Endpoint, new { userId = other.User.Id, ipAddress = "203.0.113.44" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SessionTerminationResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result.TerminatedCount);
        Assert.Equal("All sessions terminated successfully", result.Message);
        var ownState = await SnapshotAsync(factory, owner);
        Assert.Equal(owner.User.TokenVersion + 1, ownState.Version);
        Assert.All(ownState.Tokens, token =>
        {
            Assert.True(token.IsRevoked);
            Assert.NotNull(token.RevokedAt);
            Assert.NotEqual("203.0.113.44", token.RevokedByIp);
            Assert.DoesNotContain(token.Token, owner.RawTokens);
        });
        Assert.All(ownState.Sessions, session =>
        {
            Assert.False(session.IsActive);
            Assert.NotNull(session.TerminatedAt);
            Assert.Equal(nameof(SessionTerminationReason.UserLogout), session.TerminationReason);
        });
        await AssertUnchangedAsync(factory, other);
        foreach (var bearer in new[] { sessionBearer, versionBearer, legacyBearer })
        {
            await AssertBearerAsync(client, bearer, HttpStatusCode.Unauthorized);
        }
        await AssertBearerAsync(client, otherBearer, HttpStatusCode.OK);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var operations = await db.Set<OutboxMessage>().AsNoTracking()
                .Where(value => value.ActorId == owner.User.Id && value.EventName == "platform.use-case-operation.occurred.v1").ToListAsync();
            var operation = Assert.Single(operations, value =>
                DurableEventSerializer.Deserialize(value) is UseCaseOperationOccurredV1 { CommandType: nameof(RevokeAllUserTokensCommand) });
            using var operationData = JsonDocument.Parse(operation.Payload);
            Assert.Equal(nameof(RevokeAllUserTokensCommand), operationData.RootElement.GetProperty("commandType").GetString());
            Assert.Equal("identity.authentication.revoke-all-user-tokens", operationData.RootElement.GetProperty("operationCode").GetString());
            Assert.Equal(owner.TenantId, operation.TenantId);
            Assert.DoesNotContain(sessionBearer, operation.Payload, StringComparison.Ordinal);
            Assert.DoesNotContain(legacyBearer, operation.Payload, StringComparison.Ordinal);
            foreach (var token in ownState.Tokens)
            {
                Assert.DoesNotContain(token.Token, operation.Payload, StringComparison.Ordinal);
            }
            foreach (var token in owner.RawTokens)
            {
                Assert.DoesNotContain(token, operation.Payload, StringComparison.Ordinal);
            }
        }
        using var anonymous = factory.CreateClient();
        foreach (var raw in owner.RawTokens)
        {
            using var denied = await anonymous.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = raw, owner.TenantId });
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        await AssertUnchangedAsync(factory, other);
    }

    [Fact]
    public async Task AccountWithoutActiveSessionsStillRevokesRefreshAndVersionOnlyBearer()
    {
        using var factory = CreateFactory();
        var owner = await SeedAsync(factory, createSessions: false);
        using var client = factory.CreateClient();
        var bearer = await AccessAsync(factory, owner);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var response = await client.PostAsJsonAsync(Endpoint, new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await response.Content.ReadFromJsonAsync<SessionTerminationResponse>())!.TerminatedCount);
        var state = await SnapshotAsync(factory, owner);
        Assert.Equal(owner.User.TokenVersion + 1, state.Version);
        Assert.All(state.Tokens, token => Assert.True(token.IsRevoked));
        Assert.Empty(state.Sessions);
        await AssertBearerAsync(client, bearer, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnonymousLogoutCannotRevokeBodySelectedUser()
    {
        using var factory = CreateFactory();
        var owner = await SeedAsync(factory);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Endpoint, new { userId = owner.User.Id });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertUnchangedAsync(factory, owner);
    }

    [Fact]
    public async Task RevocationStoreFailureRollsBackPersistedTokensSessionsAndVersionWithoutReturningSuccess()
    {
        var failure = new FailingRevocationStore();
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<ITokenRevocationService>();
            services.AddSingleton<ITokenRevocationService>(provider =>
            {
                failure.Inner = ActivatorUtilities.CreateInstance<DistributedCacheTokenRevocationService>(provider);
                return failure;
            });
        });
        var owner = await SeedAsync(factory);
        var other = await SeedAsync(factory);
        failure.ExpectedUserId = owner.User.Id;
        var bearer = await AccessAsync(factory, owner, owner.Sessions[0].Id);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var response = await client.PostAsJsonAsync(Endpoint, new { });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, failure.Attempts);
        Assert.DoesNotContain(owner.RawTokens[0], await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await AssertUnchangedAsync(factory, owner);
        await AssertUnchangedAsync(factory, other);
        await AssertBearerAsync(client, bearer, HttpStatusCode.OK);
        await AssertNoOperationAsync(factory, owner);
    }

    [Fact]
    public async Task VersionPersistenceFailureRollsBackEarlierTokenAndSessionWrites()
    {
        var failure = new FailingUserVersionSave();
        using var factory = CreateFactory(services =>
            services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(failure)));
        var owner = await SeedAsync(factory);
        var other = await SeedAsync(factory);
        failure.Owner = owner.User.Id;
        using var client = factory.CreateClient();
        var bearer = await AccessAsync(factory, owner, owner.Sessions[0].Id);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var response = await client.PostAsJsonAsync(Endpoint, new { });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(failure.ObservedVersionWrite);
        await AssertUnchangedAsync(factory, owner);
        await AssertUnchangedAsync(factory, other);
        await AssertBearerAsync(client, bearer, HttpStatusCode.OK);
        await AssertNoOperationAsync(factory, owner);
    }

    [Fact]
    public async Task CancellationAfterRealDatabaseMutationsRollsBackCommandTransaction()
    {
        using var factory = CreateFactory();
        var owner = await SeedAsync(factory);
        var other = await SeedAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var actor = new Mock<IActorContextAccessor>();
        actor.SetupGet(value => value.ActorContext).Returns(ActorContext.Anonymous with
        {
            ActorKind = ActorKind.User, IsAuthenticated = true, SubjectId = owner.User.Id.ToString(), TenantId = owner.TenantId
        });
        using var cancellation = new CancellationTokenSource();
        var store = new Mock<IVersionedUserTokenRevocationService>(MockBehavior.Strict);
        store.Setup(value => value.RevokeAllUserTokensAsync(owner.User.Id, owner.User.TokenVersion + 1, "User initiated logout everywhere", cancellation.Token))
            .Returns(async () =>
            {
                Assert.All(await db.Set<RefreshToken>().AsNoTracking().Where(value => value.UserId == owner.User.Id).ToListAsync(),
                    token => Assert.True(token.IsRevoked));
                Assert.All(await db.Set<UserSession>().AsNoTracking().Where(value => value.UserId == owner.User.Id).ToListAsync(),
                    session => Assert.False(session.IsActive));
                Assert.Equal(owner.User.TokenVersion + 1, (await db.Set<User>().AsNoTracking().SingleAsync(value => value.Id == owner.User.Id)).TokenVersion);
                cancellation.Cancel();
                cancellation.Token.ThrowIfCancellationRequested();
            });
        var handler = new RevokeAllUserTokensHandler(actor.Object,
            scope.ServiceProvider.GetRequiredService<IUserRepository>(), scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>(),
            scope.ServiceProvider.GetRequiredService<ISessionManagementService>(), store.Object,
            scope.ServiceProvider.GetRequiredService<IRefreshTokenLifecycleRecorder>());
        var behavior = new UseCaseOperationBehavior<RevokeAllUserTokensCommand, int>(db, actor.Object,
            scope.ServiceProvider.GetRequiredService<IUseCaseOperationContextAccessor>());
        var command = new RevokeAllUserTokensCommand("127.0.0.1");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => behavior.Handle(command,
            () => handler.Handle(command, cancellation.Token), cancellation.Token));
        store.VerifyAll();
        await AssertUnchangedAsync(factory, owner);
        await AssertUnchangedAsync(factory, other);
        await AssertNoOperationAsync(factory, owner);
    }

    [Fact]
    public async Task TerminateOthersRetainsCurrentSessionAndUserVersion()
    {
        using var factory = CreateFactory();
        var owner = await SeedAsync(factory);
        using var client = factory.CreateClient();
        var currentBearer = await AccessAsync(factory, owner, owner.Sessions[0].Id);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", currentBearer);
        using var response = await client.PostAsJsonAsync("/v1/auth/sessions:terminate-others", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await response.Content.ReadFromJsonAsync<SessionTerminationResponse>())!.TerminatedCount);
        var state = await SnapshotAsync(factory, owner);
        Assert.Equal(owner.User.TokenVersion, state.Version);
        Assert.True(Assert.Single(state.Sessions, value => value.Id == owner.Sessions[0].Id).IsActive);
        Assert.False(Assert.Single(state.Sessions, value => value.Id == owner.Sessions[1].Id).IsActive);
        await AssertBearerAsync(client, currentBearer, HttpStatusCode.OK);
        await AssertBearerAsync(client, await AccessAsync(factory, owner, owner.Sessions[1].Id), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshReadBeforeCompletedLogoutCannotRestoreAnActiveTokenOrSession()
    {
        var pause = new PausedRefreshRead();
        using var factory = CreateFactory(services => services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(pause)));
        var owner = await SeedAsync(factory);
        var other = await SeedAsync(factory);
        using var scope = factory.Services.CreateScope();
        pause.TokenHash = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().HashToken(owner.RawTokens[0]);
        using var anonymous = factory.CreateClient();
        var refreshing = anonymous.PostAsJsonAsync(RefreshEndpoint, new { refreshToken = owner.RawTokens[0], owner.TenantId });
        try
        {
            await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await AccessAsync(factory, owner, owner.Sessions[0].Id));
            using var loggedOut = await client.PostAsJsonAsync(Endpoint, new { });
            Assert.Equal(HttpStatusCode.OK, loggedOut.StatusCode);
        }
        finally
        {
            pause.Release.TrySetResult();
        }
        using var denied = await refreshing;
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        var state = await SnapshotAsync(factory, owner);
        Assert.All(state.Tokens, token => Assert.True(token.IsRevoked));
        Assert.All(state.Sessions, session => Assert.False(session.IsActive));
        Assert.Equal(owner.User.TokenVersion + 2, state.Version);
        await AssertUnchangedAsync(factory, other);
    }

    private WebApplicationFactory<Program> CreateFactory(Action<IServiceCollection>? configure = null) =>
        fixture.CreateFactory(builder => builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            });
            configure?.Invoke(services);
        }));

    private static async Task<Account> SeedAsync(WebApplicationFactory<Program> factory, bool createSessions = true, Guid? existingTenant = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marker = Guid.NewGuid().ToString("N");
        var user = User.Create($"all-token-{marker}@example.test", "Synthetic all-device account");
        user.Username = $"all-token-{marker}";
        var tenant = existingTenant ?? Guid.NewGuid();
        db.Set<User>().Add(user);
        if (!existingTenant.HasValue)
        {
            db.Set<Tenant>().Add(new Tenant { Id = tenant, Name = $"All-device {marker}", Slug = $"all-device-{marker}",
                AdminEmail = $"admin-{marker}@example.test", IsActive = true });
        }
        db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), UserId = user.Id, TenantId = tenant, IsActive = true, Role = "Member" });
        var sessions = new List<UserSession>();
        var rawTokens = new List<string>();
        for (var index = 0; index < 2; index++)
        {
            var now = DateTime.UtcNow;
            var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            rawTokens.Add(raw);
            var hash = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().HashToken(raw);
            db.Set<RefreshToken>().Add(new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, Token = hash,
                CreatedAt = now, UpdatedAt = now, ExpiresAt = now.AddDays(7), CreatedByIp = "127.0.0.1" });
            if (!createSessions)
            {
                continue;
            }
            var session = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, RefreshToken = hash,
                CreatedAt = now, UpdatedAt = now, ExpiresAt = now.AddDays(7), LastUsedAt = now, IsActive = true, IpAddress = "127.0.0.1" };
            sessions.Add(session);
            db.Set<UserSession>().Add(session);
        }
        await db.SaveChangesAsync();
        return new Account(user, tenant, sessions, rawTokens);
    }

    private static async Task<string> AccessAsync(WebApplicationFactory<Program> factory, Account account, Guid? session = null)
    {
        using var scope = factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        return session.HasValue
            ? await jwt.GenerateAccessTokenAsync(account.User.Id, account.User.Email, ["Member"], account.TenantId, account.User.TokenVersion, session.Value)
            : await jwt.GenerateAccessTokenAsync(account.User.Id, account.User.Email, ["Member"], account.TenantId, account.User.TokenVersion);
    }

    private static string LegacyBearer(WebApplicationFactory<Program> factory, string signed)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(signed);
        var options = factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audiences.Single(),
            jwt.Claims.Where(value => value.Type is not "token_version" && value.Type != JwtClaimTypes.SessionId),
            jwt.ValidFrom, jwt.ValidTo, new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task AssertBearerAsync(HttpClient client, string bearer, HttpStatusCode status)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var response = await client.GetAsync(SessionsEndpoint);
        Assert.Equal(status, response.StatusCode);
    }

    private static async Task<StoredState> SnapshotAsync(WebApplicationFactory<Program> factory, Account account)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new StoredState((await db.Set<User>().AsNoTracking().SingleAsync(value => value.Id == account.User.Id)).TokenVersion,
            await db.Set<RefreshToken>().AsNoTracking().Where(value => value.UserId == account.User.Id).ToListAsync(),
            await db.Set<UserSession>().AsNoTracking().Where(value => value.UserId == account.User.Id).ToListAsync());
    }

    private static async Task AssertUnchangedAsync(WebApplicationFactory<Program> factory, Account account)
    {
        var state = await SnapshotAsync(factory, account);
        Assert.Equal(account.User.TokenVersion, state.Version);
        Assert.Equal(2, state.Tokens.Count);
        Assert.All(state.Tokens, token => { Assert.False(token.IsRevoked); Assert.Null(token.RevokedAt); });
        Assert.Equal(account.Sessions.Count, state.Sessions.Count);
        Assert.All(state.Sessions, session => { Assert.True(session.IsActive); Assert.Null(session.TerminatedAt); });
    }

    private static async Task AssertNoOperationAsync(WebApplicationFactory<Program> factory, Account account)
    {
        using var scope = factory.Services.CreateScope();
        var operations = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<OutboxMessage>().AsNoTracking()
            .Where(value => value.ActorId == account.User.Id && value.EventName == "platform.use-case-operation.occurred.v1").ToListAsync();
        Assert.DoesNotContain(operations, value =>
            DurableEventSerializer.Deserialize(value) is UseCaseOperationOccurredV1 { CommandType: nameof(RevokeAllUserTokensCommand) });
    }

    private sealed record Account(User User, Guid TenantId, List<UserSession> Sessions, List<string> RawTokens);
    private sealed record StoredState(int Version, List<RefreshToken> Tokens, List<UserSession> Sessions);

    private sealed class FailingRevocationStore : ITokenRevocationService, IVersionedUserTokenRevocationService
    {
        public ITokenRevocationService Inner { get; set; } = null!;
        public Guid ExpectedUserId { get; set; }
        public int Attempts { get; private set; }
        public Task RevokeAllUserTokensAsync(Guid userId, string? reason = null, CancellationToken cancellationToken = default)
            => Inner.RevokeAllUserTokensAsync(userId, reason, cancellationToken);
        public Task RevokeAllUserTokensAsync(Guid userId, int minimumTokenVersion, string? reason, CancellationToken cancellationToken)
        {
            Assert.Equal(ExpectedUserId, userId);
            Assert.Equal(2, minimumTokenVersion);
            Assert.Equal("User initiated logout everywhere", reason);
            cancellationToken.ThrowIfCancellationRequested();
            Attempts++;
            return Task.FromException(new InvalidOperationException("Synthetic revocation store failure"));
        }
        public Task RevokeTokenAsync(string jti, DateTime expiresAt, string? reason = null, CancellationToken cancellationToken = default) =>
            Inner.RevokeTokenAsync(jti, expiresAt, reason, cancellationToken);
        public Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken = default) => Inner.IsRevokedAsync(jti, cancellationToken);
        public Task<bool> IsUserTokenRevokedAsync(Guid userId, DateTime issuedAt, CancellationToken cancellationToken = default) =>
            Inner.IsUserTokenRevokedAsync(userId, issuedAt, cancellationToken);
        public Task<bool> IsUserTokenRevokedAsync(Guid userId, DateTime issuedAt, int? tokenVersion, CancellationToken cancellationToken = default) =>
            ((IVersionedUserTokenRevocationService)Inner).IsUserTokenRevokedAsync(userId, issuedAt, tokenVersion, cancellationToken);
        public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default) => Inner.CleanupExpiredAsync(cancellationToken);
    }

    private sealed class FailingUserVersionSave : SaveChangesInterceptor
    {
        public Guid Owner { get; set; }
        public bool ObservedVersionWrite { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (eventData.Context!.ChangeTracker.Entries<User>().Any(value =>
                    value.State == EntityState.Modified && value.Entity.Id == Owner && value.Entity.TokenVersion > 1))
            {
                ObservedVersionWrite = true;
                throw new InvalidOperationException("Synthetic version persistence failure");
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class PausedRefreshRead : DbCommandInterceptor
    {
        private int _arrivals;
        public string? TokenHash { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (TokenHash is not null && eventData.CommandSource == CommandSource.LinqQuery &&
                command.CommandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains(".refreshtoken AS ", StringComparison.Ordinal) &&
                command.Parameters.Cast<DbParameter>().Any(value => Equals(value.Value, TokenHash)) &&
                Interlocked.Increment(ref _arrivals) == 1)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
            }
            return result;
        }
    }
}
