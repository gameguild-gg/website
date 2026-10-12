using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Nethereum.Signer;

namespace GameGuild.API.IntegrationTests;

[CollectionDefinition(nameof(Web3PostgreSqlCollection), DisableParallelization = true)]
public sealed class Web3PostgreSqlCollection : ICollectionFixture<Web3PostgreSqlFixture>
{
}

public sealed class Web3PostgreSqlFixture : IAsyncLifetime
{
    private readonly ApiPostgreSqlFixture _inner = new();
    public WebApplicationFactory<Program> Factory => _inner.Factory;
    public WebApplicationFactory<Program> CreateFactory(Action<IWebHostBuilder> configuration) => _inner.CreateFactory(configuration);
    public Guid TenantId { get; } = Guid.NewGuid();
    public async Task InitializeAsync()
    {
        await _inner.InitializeAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marker = TenantId.ToString("N");
        db.Set<Tenant>().Add(new Tenant { Id = TenantId, Name = "Wallet acceptance tenant", Slug = "wallet-" + marker,
            AdminEmail = "admin-" + marker + "@example.test", IsActive = true, IsDefault = true });
        await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => _inner.DisposeAsync();
}

[Collection(nameof(Web3PostgreSqlCollection))]
public sealed class Web3AuthenticationPostgreSqlHttpTests(Web3PostgreSqlFixture fixture)
{
    [Theory]
    [InlineData("invalid-wallet", "1")]
    [InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAeD", "1")]
    [InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", "0")]
    [InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", "-1")]
    [InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", "invalid-chain")]
    [InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed", "31337")]
    public async Task InvalidAddressChecksumOrUnsupportedChainReturnsBadRequestWithoutIssuingCredentials(string walletAddress, string chainId)
    {
        using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = await db.Set<User>().CountAsync();
        var tokens = await db.Set<RefreshToken>().CountAsync();
        var sessions = await db.Set<UserSession>().CountAsync();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/v1/auth/web3/challenge", new { walletAddress, chainId });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("accessToken", body);
        Assert.DoesNotContain("refreshToken", body);
        Assert.Equal(users, await db.Set<User>().CountAsync());
        Assert.Equal(tokens, await db.Set<RefreshToken>().CountAsync());
        Assert.Equal(sessions, await db.Set<UserSession>().CountAsync());
    }

    [Fact]
    public async Task ActualBoundedCacheSiweSignInCreatesStableAccountHashedTokensAndUsableBearerAndRefresh()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        Assert.NotNull(factory.Services.GetRequiredService<IMemoryCache>());
        Assert.True(factory.Services.GetRequiredService<IOptions<MemoryCacheOptions>>().Value.SizeLimit is > 0);
        var key = CreateKey();
        var first = await ChallengeAsync(client, key);
        using var firstResponse = await VerifyAsync(client, key, first, fingerprint: "synthetic-wallet-device");
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        using var firstPayload = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
        var userId = firstPayload.RootElement.GetProperty("userId").GetGuid();
        var sessionId = firstPayload.RootElement.GetProperty("sessionId").GetGuid();
        var raw = firstPayload.RootElement.GetProperty("refreshToken").GetString()!;
        var bearer = firstPayload.RootElement.GetProperty("accessToken").GetString()!;
        Assert.Equal(fixture.TenantId, firstPayload.RootElement.GetProperty("tenantId").GetGuid());
        Assert.True(userId != Guid.Empty && sessionId != Guid.Empty);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var sessions = await client.GetAsync("/v1/auth/sessions");
        Assert.Equal(HttpStatusCode.OK, sessions.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        using var refreshed = await client.PostAsJsonAsync("/v1/auth/tokens:refresh", new { refreshToken = raw, tenantId = fixture.TenantId });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        using var refreshedPayload = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
        var successorRaw = refreshedPayload.RootElement.GetProperty("refreshToken").GetString()!;
        Assert.Equal(sessionId, refreshedPayload.RootElement.GetProperty("sessionId").GetGuid());
        var second = await ChallengeAsync(client, key);
        using var secondResponse = await VerifyAsync(client, key, second);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        using var secondPayload = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        Assert.Equal(userId, secondPayload.RootElement.GetProperty("userId").GetGuid());
        var secondRaw = secondPayload.RootElement.GetProperty("refreshToken").GetString()!;
        var secondSession = secondPayload.RootElement.GetProperty("sessionId").GetGuid();
        Assert.NotEqual(sessionId, secondSession);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Set<User>().AsNoTracking().SingleAsync(value => value.Id == userId);
        Assert.False(user.IsEmailVerified);
        Assert.Null(user.PasswordHash);
        var link = await db.Set<ExternalLogin>().AsNoTracking().SingleAsync(value => value.Provider == "web3" && value.ProviderKey == key.GetPublicAddress().ToLowerInvariant());
        Assert.Equal(userId, link.UserId);
        var tokens = await db.Set<RefreshToken>().AsNoTracking().Where(value => value.UserId == userId).ToListAsync();
        Assert.Equal(3, tokens.Count);
        var hasher = scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>();
        var root = tokens.Single(value => value.Token == hasher.HashToken(raw));
        var child = tokens.Single(value => value.Token == hasher.HashToken(successorRaw));
        var independentRoot = tokens.Single(value => value.Token == hasher.HashToken(secondRaw));
        Assert.True(root.IsRevoked);
        Assert.Null(root.ParentTokenId);
        Assert.Equal(root.Id, child.ParentTokenId);
        Assert.Equal(sessionId, root.SessionId);
        Assert.Equal(sessionId, child.SessionId);
        Assert.Null(independentRoot.ParentTokenId);
        Assert.Equal(secondSession, independentRoot.SessionId);
        Assert.DoesNotContain(raw, tokens.Select(value => value.Token));
        Assert.DoesNotContain(successorRaw, tokens.Select(value => value.Token));
        Assert.DoesNotContain(secondRaw, tokens.Select(value => value.Token));
        var storedSessions = await db.Set<UserSession>().AsNoTracking().Where(value => value.UserId == userId).ToListAsync();
        Assert.Equal(2, storedSessions.Count);
        var actualSession = storedSessions.Single(value => value.Id == sessionId);
        Assert.Equal(child.Token, actualSession.RefreshToken);
        Assert.Equal("synthetic-wallet-device", actualSession.DeviceFingerprint);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(bearer);
        Assert.Equal(user.TokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), jwt.Claims.Single(value => value.Type == "token_version").Value);
    }

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("signature-format")]
    [InlineData("message-format")]
    [InlineData("nonce")]
    [InlineData("address")]
    [InlineData("network")]
    public async Task InvalidCryptographicOrBoundChallengeVectorCannotCreateAccountSessionOrTokens(string vector)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var key = CreateKey();
        var challenge = await ChallengeAsync(client, key);
        var address = vector == "address" ? CreateKey().GetPublicAddress() : key.GetPublicAddress();
        var message = vector == "message-format" ? challenge.Message + "\nUnexpected: injection" : challenge.Message;
        var signature = vector == "signature-format" ? "0xnot-a-signature" : Sign(message, vector == "wrong-key" ? CreateKey() : key);
        using var response = await client.PostAsJsonAsync("/v1/auth/web3:verify", new
        {
            walletAddress = address, signature, challenge = message,
            nonce = vector == "nonce" ? "wrong-nonce" : challenge.Nonce,
            chainId = vector == "network" ? "5" : "1"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoWalletAccountAsync(factory, key.GetPublicAddress());
        using var valid = await VerifyAsync(client, key, challenge);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task ChallengeLifetimeIsFiveMinutesAndExactExpirationRejectsRealSignatureWithoutIssuance()
    {
        var clock = new WalletClock(DateTimeOffset.UtcNow);
        using var factory = CreateFactory(clock);
        using var client = factory.CreateClient();
        var key = CreateKey();
        var challenge = await ChallengeAsync(client, key);
        Assert.Equal(clock.GetUtcNow().AddMinutes(5).UtcDateTime, challenge.ExpiresAt);
        clock.Advance(TimeSpan.FromMinutes(5));
        using var response = await VerifyAsync(client, key, challenge);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoWalletAccountAsync(factory, key.GetPublicAddress());
    }

    [Fact]
    public async Task ValidSignatureNonceIsAcceptedOnceAndConcurrentReplayCreatesOnlyOneCredentialAndSession()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var key = CreateKey();
        var challenge = await ChallengeAsync(client, key);
        var responses = await Task.WhenAll(VerifyAsync(client, key, challenge), VerifyAsync(client, key, challenge));
        try
        {
            Assert.Single(responses, value => value.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, value => value.StatusCode == HttpStatusCode.Unauthorized);
            using var replay = await VerifyAsync(client, key, challenge);
            Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
            using var payload = JsonDocument.Parse(await responses.Single(value => value.StatusCode == HttpStatusCode.OK).Content.ReadAsStringAsync());
            var id = payload.RootElement.GetProperty("userId").GetGuid();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await db.Set<RefreshToken>().CountAsync(value => value.UserId == id));
            Assert.Equal(1, await db.Set<UserSession>().CountAsync(value => value.UserId == id));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task WalletPossessionCannotAutoLinkAnExistingUnverifiedEmailIdentifier()
    {
        using var factory = CreateFactory();
        var key = CreateKey();
        var user = User.CreateOAuthUser(key.GetPublicAddress().ToLowerInvariant() + "@web3.local", "Synthetic collision", emailVerified: false);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Set<User>().Add(user);
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        var challenge = await ChallengeAsync(client, key);
        using var response = await VerifyAsync(client, key, challenge);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var verification = factory.Services.CreateScope();
        var persisted = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await persisted.Set<ExternalLogin>().AnyAsync(value => value.Provider == "web3" && value.ProviderKey == key.GetPublicAddress().ToLowerInvariant()));
        Assert.False(await persisted.Set<RefreshToken>().AnyAsync(value => value.UserId == user.Id));
        Assert.False(await persisted.Set<UserSession>().AnyAsync(value => value.UserId == user.Id));
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("suspended")]
    [InlineData("foreign-tenant")]
    public async Task LinkedWalletCannotBypassStoredAccountStatusOrTenantMembership(string denial)
    {
        using var factory = CreateFactory();
        var key = CreateKey();
        var marker = Guid.NewGuid().ToString("N");
        var user = User.CreateOAuthUser("wallet-guard-" + marker + "@example.test", "Synthetic wallet guard " + marker, emailVerified: true);
        user.IsActive = denial != "inactive";
        user.IsSuspended = denial == "suspended";
        user.TokenVersion = 27;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Set<User>().Add(user);
            db.Set<ExternalLogin>().Add(new ExternalLogin { Id = Guid.NewGuid(), UserId = user.Id,
                Provider = "web3", ProviderKey = key.GetPublicAddress().ToLowerInvariant(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), UserId = user.Id, TenantId = fixture.TenantId, IsActive = true, Role = "Member" });
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        var challenge = await ChallengeAsync(client, key);
        using var response = await client.PostAsJsonAsync("/v1/auth/web3:verify", new
        {
            walletAddress = key.GetPublicAddress(), signature = Sign(challenge.Message, key), challenge = challenge.Message,
            nonce = challenge.Nonce, chainId = "1", tenantId = denial == "foreign-tenant" ? Guid.NewGuid() : fixture.TenantId
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var verification = factory.Services.CreateScope();
        var persisted = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await persisted.Set<RefreshToken>().AnyAsync(value => value.UserId == user.Id));
        Assert.False(await persisted.Set<UserSession>().AnyAsync(value => value.UserId == user.Id));
        var retainedLink = await persisted.Set<ExternalLogin>().AsNoTracking().SingleAsync(value => value.Provider == "web3" && value.ProviderKey == key.GetPublicAddress().ToLowerInvariant());
        Assert.Equal(user.Id, retainedLink.UserId);
    }

    [Fact]
    public async Task ActualPostBindingFailureRollsBackNewWalletAccountIdentitySessionAndHashWithoutReusingNonce()
    {
        var evidence = new BindingEvidence();
        using var factory = CreateFactory(extraServices: services =>
        {
            services.RemoveAll<IRefreshTokenLineageRepository>();
            services.AddScoped<IRefreshTokenLineageRepository>(provider =>
                new PostBindingFailure((IRefreshTokenLineageRepository)provider.GetRequiredService<IRefreshTokenRepository>(), evidence));
        });
        using var client = factory.CreateClient();
        var key = CreateKey();
        var challenge = await ChallengeAsync(client, key);
        using var response = await VerifyAsync(client, key, challenge);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(evidence.Written, "The injected fault must occur after an actual stored token/session binding.");
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("accessToken", body);
        Assert.DoesNotContain("refreshToken", body);
        await AssertNoWalletAccountAsync(factory, key.GetPublicAddress());
        using var verification = factory.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Set<RefreshToken>().AnyAsync(value => value.UserId == evidence.UserId));
        Assert.False(await db.Set<UserSession>().AnyAsync(value => value.UserId == evidence.UserId));
        using var retry = await VerifyAsync(client, key, challenge);
        Assert.Equal(HttpStatusCode.Unauthorized, retry.StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory(WalletClock? clock = null, Action<IServiceCollection>? extraServices = null) => fixture.CreateFactory(builder =>
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Web3:Siwe:Origin"] = "https://wallet.example.test",
            ["Authentication:Web3:Siwe:AllowedChainIds:0"] = "1",
            ["Authentication:Web3:Siwe:AllowedChainIds:1"] = "5"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            });
            if (clock is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            }
            extraServices?.Invoke(services);
        });
    });

    private static async Task<WalletChallenge> ChallengeAsync(HttpClient client, EthECKey key)
    {
        using var response = await client.PostAsJsonAsync("/v1/auth/web3/challenge", new { walletAddress = key.GetPublicAddress(), chainId = "1" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return new WalletChallenge(payload.RootElement.GetProperty("challenge").GetString()!, payload.RootElement.GetProperty("nonce").GetString()!, payload.RootElement.GetProperty("expiresAt").GetDateTime());
    }

    private static Task<HttpResponseMessage> VerifyAsync(HttpClient client, EthECKey key, WalletChallenge challenge, string? fingerprint = null) =>
        client.PostAsJsonAsync("/v1/auth/web3:verify", new { walletAddress = key.GetPublicAddress(), signature = Sign(challenge.Message, key), challenge = challenge.Message, nonce = challenge.Nonce, chainId = "1", deviceFingerprint = fingerprint });

    private static async Task AssertNoWalletAccountAsync(WebApplicationFactory<Program> factory, string address)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var identifier = address.ToLowerInvariant() + "@web3.local";
        Assert.False(await db.Set<User>().AnyAsync(value => value.Email == identifier));
        Assert.False(await db.Set<ExternalLogin>().AnyAsync(value => value.Provider == "web3" && value.ProviderKey == address.ToLowerInvariant()));
    }

    private static EthECKey CreateKey() => new(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    private static string Sign(string message, EthECKey key)
    {
        var signature = new EthereumMessageSigner().EncodeUTF8AndSign(message, key);
        return signature.StartsWith("0x", StringComparison.Ordinal) ? signature : "0x" + signature;
    }
    private sealed record WalletChallenge(string Message, string Nonce, DateTime ExpiresAt);
    private sealed class BindingEvidence
    {
        public bool Written { get; set; }
        public Guid UserId { get; set; }
    }
    private sealed class PostBindingFailure(IRefreshTokenLineageRepository inner, BindingEvidence evidence) : IRefreshTokenLineageRepository
    {
        public Task<Guid?> RevokeFamilyAsync(Guid userId, Guid tokenId, string? revokedByIp, CancellationToken cancellationToken) =>
            inner.RevokeFamilyAsync(userId, tokenId, revokedByIp, cancellationToken);

        public async Task<bool> BindSessionAsync(Guid userId, string tokenHash, Guid sessionId, CancellationToken cancellationToken)
        {
            var bound = await inner.BindSessionAsync(userId, tokenHash, sessionId, cancellationToken);
            evidence.Written = bound;
            evidence.UserId = userId;
            throw new InvalidOperationException("Synthetic post-binding fault");
        }
        public Task RecordRotationAsync(Guid userId, Guid parentTokenId, string replacementHash, Guid sessionId, CancellationToken cancellationToken) =>
            inner.RecordRotationAsync(userId, parentTokenId, replacementHash, sessionId, cancellationToken);
    }
    private sealed class WalletClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan value) => now = now.Add(value);
    }
}
